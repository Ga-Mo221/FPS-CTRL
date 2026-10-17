// GaMoInspector.SelectionEvents.cs  -  part of CustomsEditor (GaMo Inspector)
// Theo doi Selection/ObjectChangeEvents
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UEditor = UnityEditor.Editor;
using UIE = UnityEngine.UIElements;
using UEUI = UnityEditor.UIElements;
using UObject = UnityEngine.Object;

namespace GaMo.BlenderEditorTools
{
    public partial class CustomsEditor
    {
        void OnSel()
        {
            CheckLockAlive();
            if (!_lock) BindSelection();
        }

        // Chỉ các sự kiện làm đổi CẤU TRÚC mới làm cache component/target mất hiệu lực; đổi giá trị property thì không.
        void OnObjChanged(ref ObjectChangeEventStream stream)
        {
            bool structural = false;
            for (int i = 0; i < stream.length && !structural; i++)
            {
                switch (stream.GetEventType(i))
                {
                    case ObjectChangeKind.ChangeScene:
                    case ObjectChangeKind.CreateGameObjectHierarchy:
                    case ObjectChangeKind.ChangeGameObjectStructureHierarchy:
                    case ObjectChangeKind.ChangeGameObjectStructure:
                    case ObjectChangeKind.ChangeGameObjectParent:
                    case ObjectChangeKind.DestroyGameObjectHierarchy:
                    case ObjectChangeKind.UpdatePrefabInstances:
                    case ObjectChangeKind.ChangeChildrenOrder:
                        structural = true;
                        break;
                }
            }
            if (structural)
            {
                _structVer++;
                CheckLockAlive();
            }
            else if (!IsViewedChange(ref stream)) return; // thay đổi ở object khác (kéo object khác ở Scene View...): không vẽ lại cửa sổ này

            _ovrDirty = true;
            _dataVer++;
            Repaint();
            RequestSync();
        }

        // ---------- Lọc ObjectChangeEvents: chỉ phản ứng với thay đổi dính tới thứ đang xem ----------
        // Trước đây MỌI thay đổi trong scene đều làm cửa sổ vẽ lại toàn bộ (kể cả editor nặng) + quét lại prefab override.
        // Id object trong event là int (instanceId) ở Unity cũ, EntityId ở Unity 6 mới => đọc / so khớp bằng reflection (có cache),
        // đọc không được thì coi như "có liên quan" (giữ hành vi cũ). Sót event chỉ chậm tối đa ~0.5s vì còn repaint dự phòng ở OnInspectorUpdate.
        readonly HashSet<object> _viewedIds = new HashSet<object>();
        readonly List<Component> _tmpViewComps = new List<Component>();
        readonly List<Material> _tmpViewMats = new List<Material>();
        int _viewedVer = -1, _viewedKind = -1;
        bool _viewedAll;                                   // chọn quá nhiều object: khỏi lọc
        static readonly Dictionary<Type, MemberInfo> _evIdMember = new Dictionary<Type, MemberInfo>();
        static MethodInfo _idByInstance, _idByEntity;
        static bool _idMethodsInit;

        static MemberInfo FindIdMember(Type t, string name)
        {
            return (MemberInfo)t.GetField(name, AnyInst) ?? t.GetProperty(name, AnyInst);
        }

        // kind: 1 = int (instanceId), 2 = kiểu khác (EntityId), 0 = không đọc được
        static object ReadEventId(object ev, out int kind)
        {
            kind = 0;
            var t = ev.GetType();
            if (!_evIdMember.TryGetValue(t, out var mi))
            {
                mi = FindIdMember(t, "entityId") ?? FindIdMember(t, "instanceId");
                _evIdMember[t] = mi;
            }
            if (mi == null) return null;
            try
            {
                object v = mi is FieldInfo fi ? fi.GetValue(ev) : ((PropertyInfo)mi).GetValue(ev, null);
                if (v == null) return null;
                kind = v is int ? 1 : 2;
                return v;
            }
            catch (Exception) { kind = 0; return null; }
        }

        static object ObjectIdOf(UObject o, int kind)
        {
            if (!_idMethodsInit)
            {
                _idMethodsInit = true;
                _idByInstance = typeof(UObject).GetMethod("GetInstanceID", AnyInst, null, Type.EmptyTypes, null);
                _idByEntity = typeof(UObject).GetMethod("GetEntityId", AnyInst, null, Type.EmptyTypes, null);
            }
            var m = kind == 1 ? _idByInstance : _idByEntity;
            if (m == null) return null;
            try { return m.Invoke(o, null); }
            catch (Exception) { return null; }
        }

        void AddViewedId(UObject o, int kind)
        {
            var id = ObjectIdOf(o, kind);
            if (id != null) _viewedIds.Add(id);
        }

        void RebuildViewedIds(int kind)
        {
            _viewedIds.Clear();
            _viewedVer = _structVer;
            _viewedKind = kind;
            _viewedAll = _gos.Count > 128;
            if (_viewedAll) return;
            foreach (var g in _gos)
            {
                if (g == null) continue;
                AddViewedId(g, kind);
                g.GetComponents(_tmpViewComps);
                foreach (var c in _tmpViewComps)
                {
                    if (c == null) continue;
                    AddViewedId(c, kind);
                    if (c is Renderer r)
                    {
                        r.GetSharedMaterials(_tmpViewMats);
                        foreach (var mat in _tmpViewMats)
                            if (mat != null) AddViewedId(mat, kind);
                    }
                }
            }
            _tmpViewComps.Clear();
            _tmpViewMats.Clear();
        }

        // true nếu có event property đổi trên object / component / material đang hiển thị (hoặc loại event mà ta không lọc).
        bool IsViewedChange(ref ObjectChangeEventStream stream)
        {
            if (_go == null && _asset == null) return false;
            for (int i = 0; i < stream.length; i++)
            {
                var kind = stream.GetEventType(i);
                object ev;
                if (kind == ObjectChangeKind.ChangeGameObjectOrComponentProperties)
                {
                    if (_asset != null) continue;           // đang xem asset: thay đổi trong scene không liên quan
                    stream.GetChangeGameObjectOrComponentPropertiesEvent(i, out var e1);
                    ev = e1;
                }
                else if (kind == ObjectChangeKind.ChangeAssetObjectProperties)
                {
                    if (_asset != null) return true;        // asset hiếm khi đổi: cứ cập nhật
                    stream.GetChangeAssetObjectPropertiesEvent(i, out var e2);
                    ev = e2;
                }
                else return true;                           // loại event khác (tạo/xóa asset...): giữ hành vi cũ

                int idKind;
                object id = ReadEventId(ev, out idKind);
                if (id == null) return true;
                if (_viewedVer != _structVer || _viewedKind != idKind) RebuildViewedIds(idKind);
                if (_viewedAll || _viewedIds.Contains(id)) return true;
            }
            return false;
        }

        void OnStructureChanged()
        {
            _ovrDirty = true;
            _structVer++;
            _dataVer++;
            CheckLockAlive();
            Repaint();
            RequestSync();
        }

        void OnProjectChanged()
        {
            _hdrDirty = true;
            CustomsTabColors.Invalidate(); // file JSON màu ghim có thể bị sửa ngoài / git
            _iconCache.Clear(); _iconMissing.Clear(); _tabContentCache.Clear(); // icon script / asset có thể đã đổi
            CheckLockAlive();
            Repaint();
        }

        // Cache (danh sách component, target multi-edit) chỉ bị xóa khi cấu trúc đổi (_structVer).
        // Mỗi chunk IMGUI là một IMGUIContainer riêng có Layout riêng, nên KHÔNG được xóa vô điều kiện (trước đây xóa ở mọi Layout
        // của mọi chunk => GetComponents + ToArray lặp lại hàng chục lần mỗi frame). Chọn 1 object / Play mode: ObjectChangeEvents
        // không đáng tin tuyệt đối nên thêm lưới an toàn theo thời gian (Play 0.1s, Edit 0.5s) thay vì xóa mỗi lần.
        bool CachesStale(ref int seen, ref double at)
        {
            double now = EditorApplication.timeSinceStartup;
            bool playing = EditorApplication.isPlaying;
            bool stale = seen != _structVer
                         || ((_gos.Count <= 1 || playing) && now - at > (playing ? 0.1 : 0.5));
            if (stale) { seen = _structVer; at = now; }
            return stale;
        }

        void InvalidateStruct() { _structVer++; _compsCache = null; }

        // Object/asset đang khóa bị xóa => tự nhả khóa, trở về theo dõi Selection (trước đây cửa sổ trống và Lock vẫn bật).
        void CheckLockAlive()
        {
            if (!_lock || _lockedObj != null) return;
            _lock = false;
            _lockedObj = null;
            BindSelection();
            ShowNotification(new GUIContent("Object đang khóa đã bị xóa, đã tự mở khóa"));
        }

    }
}
#endif