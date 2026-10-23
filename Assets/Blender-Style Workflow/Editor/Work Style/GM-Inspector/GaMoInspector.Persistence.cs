// GaMoInspector.Persistence.cs  -  part of CustomsEditor (GaMo Inspector)
// Luu/khoi phuc trang thai qua recompile, GlobalObjectId
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
        // ---------- Lưu / khôi phục trạng thái ----------
        // Chuỗi nối tên type của một tập tab: cache theo khóa băm không phụ thuộc thứ tự để OnBeforeSerialize
        // (chạy khá thường xuyên) không phải ghép chuỗi lại cho tối đa 256 object mỗi lần.
        static readonly Dictionary<long, string> _typeSetStr = new Dictionary<long, string>();

        static string TypeSetString(HashSet<Type> set)
        {
            long key = (long)set.Count << 56;
            ulong acc = 0;
            foreach (var ty in set)
            {
                ulong h = (uint)ty.GetHashCode();   // trộn bit trước khi cộng: tổng hash thô của 2 tập khác nhau rất dễ trùng
                h *= 0x9E3779B97F4A7C15UL;
                h ^= h >> 29;
                acc += h;
            }
            key ^= (long)acc;
            if (_typeSetStr.TryGetValue(key, out var s)) return s;
            var sb = new StringBuilder();
            foreach (var ty in set)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(TypeName(ty));
            }
            s = sb.ToString();
            if (_typeSetStr.Count > 2048) _typeSetStr.Clear();
            _typeSetStr[key] = s;
            return s;
        }

        static string TypeName(Type t)
        {
            if (!_aqnCache.TryGetValue(t, out var n)) { n = t.AssemblyQualifiedName; _aqnCache[t] = n; }
            return n;
        }

        public void OnBeforeSerialize()
        {
            SaveStateOfCurrent(); // gồm cả tab của object đang hiện, để qua recompile cũng giữ đúng tab đang chọn
            _collapsedSer.Clear();
            foreach (var c in _collapsed)
                if (c != null && _collapsedSer.Count < MaxPersistedObjects) _collapsedSer.Add(c);

            _perObjKeys.Clear();
            _perObjTypes.Clear();
            foreach (var kv in _perObject)
            {
                if (kv.Key == null || kv.Value == null || kv.Value.Count == 0) continue;
                if (_perObjKeys.Count >= MaxPersistedObjects) break;
                _perObjKeys.Add(kv.Key);
                _perObjTypes.Add(TypeSetString(kv.Value));
            }
        }

        // Không đụng API Unity ở đây: tham chiếu chưa chắc đã sẵn sàng. OnEnable (RestoreUiState) mới đọc.
        public void OnAfterDeserialize() { }

        void OnPlayModeState(PlayModeStateChange s)
        {
            if (s == PlayModeStateChange.EnteredPlayMode || s == PlayModeStateChange.EnteredEditMode)
            {
                _reloading = false; // nếu cờ lỡ kẹt true thì SyncUI / EnsureEditor bị chặn mãi => thân cửa sổ trống
                RequestSync();
            }
            // Bản chụp = {đường dẫn object trong scene -> tab đang mở}. Đường dẫn của clone ở Play GIỐNG đường dẫn object gốc ở Edit,
            // nên: (1) chụp lúc RỜI Edit mode để clone ở Play lấy lại tab; (2) chụp lại lúc SẮP THOÁT Play (clone còn sống)
            // để tab / panel thu gọn mà người dùng đổi trong Play được áp lên object tương ứng ở Edit mode.
            // SaveGidsCore GỘP vào bản chụp cũ (object không tính được id thì giữ mục cũ), nên không làm rơi mục nào.
            if (s == PlayModeStateChange.ExitingEditMode)
            {
                SaveGidsCore();                    // chụp trạng thái Edit mode
                CustomsPlaySession.Active = true;  // từ giờ tới EnteredEditMode: cấm mọi lần ghi đè bản chụp (xem SaveGids)
                LoadPendingFromSnapshot();         // Reload Domain tắt: clone ở Play lấy lại tab từ bản chụp
            }
            else if (s == PlayModeStateChange.ExitingPlayMode)
            {
                SaveGidsCore();                    // chụp tab đã đổi trong Play (gồm tab của object đang xem)
                LoadPendingFromSnapshot();         // object Edit mode sau khi thoát lấy lại tab từ bản chụp (cả khi tắt Reload Domain)
            }

            // Sau khi đã vào Play / về lại Edit, Selection đã trỏ sang object mới: bind lại (trừ khi đang Lock)
            if (s == PlayModeStateChange.EnteredPlayMode || s == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += () => { if (this != null && !_lock) BindSelection(); };
        }

        // Khóa định danh theo ĐƯỜNG DẪN trong scene: "đường dẫn scene|chỉ số sibling từ gốc đến object[#chỉ số component]".
        // Thay cho GlobalObjectId vì GlobalObjectId trả về rỗng với scene chưa lưu / object mới tạo (chưa có localId),
        // và không khớp với bản clone lúc Play. Đường dẫn thì giống nhau giữa scene Edit, bản clone trong Play và scene sau khi thoát Play.
        // Đổi thứ tự/đổi cha object giữa lúc lưu và lúc khôi phục thì sẽ không khớp (chấp nhận được: chỉ mất trạng thái giao diện).
        static readonly List<int> _pathTmp = new List<int>();

        static string SceneKey(UnityEngine.SceneManagement.Scene s)
        {
            return string.IsNullOrEmpty(s.path) ? "untitled:" + s.name : s.path;
        }

        static string GidOf(UObject o)
        {
            if (o == null) return null;
            GameObject go = o as GameObject;
            int ci = -1;
            if (go == null && o is Component comp)
            {
                go = comp.gameObject;
                var cs = go.GetComponents<Component>();
                ci = Array.IndexOf(cs, comp);
                if (ci < 0) return null;
            }
            if (go == null || EditorUtility.IsPersistent(go) || !go.scene.IsValid()) return null;

            _pathTmp.Clear();
            for (var tr = go.transform; tr != null; tr = tr.parent) _pathTmp.Add(tr.GetSiblingIndex());
            var sb = new StringBuilder(SceneKey(go.scene)).Append('|');
            for (int i = _pathTmp.Count - 1; i >= 0; i--)
            {
                sb.Append(_pathTmp[i]);
                if (i > 0) sb.Append('/');
            }
            if (ci >= 0) sb.Append('#').Append(ci);
            return sb.ToString();
        }

        // Khóa màu ghim của TỪNG component (lưu lâu dài trong TabColors.json):
        // ưu tiên GlobalObjectId (không đổi khi đổi thứ tự / đổi cha, dùng được cho prefab asset); scene chưa lưu / object mới tạo
        // chưa có GlobalObjectId thì dùng đường dẫn trong scene (GidOf). Gọi ít (CustomsTabColors cache theo component).
        internal static string PinKeyOf(Component c)
        {
            if (c == null) return null;
            var id = GlobalObjectId.GetGlobalObjectIdSlow(c);
            if (id.identifierType != 0 && id.targetObjectId != 0 && !id.assetGUID.Empty()) return id.ToString();
            return GidOf(c);
        }

        static UObject FromGid(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            int bar = key.LastIndexOf('|');
            if (bar <= 0) return null;
            string sceneKey = key.Substring(0, bar);
            string rest = key.Substring(bar + 1);
            int ci = -1;
            int hash = rest.IndexOf('#');
            if (hash >= 0)
            {
                if (!int.TryParse(rest.Substring(hash + 1), out ci)) return null;
                rest = rest.Substring(0, hash);
            }

            for (int s = 0; s < UnityEngine.SceneManagement.SceneManager.sceneCount; s++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(s);
                if (!scene.IsValid() || !scene.isLoaded || SceneKey(scene) != sceneKey) continue;

                var parts = rest.Split('/');
                var roots = scene.GetRootGameObjects();
                if (!int.TryParse(parts[0], out int ri) || ri < 0 || ri >= roots.Length) return null;
                var tr = roots[ri].transform;
                for (int i = 1; i < parts.Length; i++)
                {
                    if (!int.TryParse(parts[i], out int idx) || idx < 0 || idx >= tr.childCount) return null;
                    tr = tr.GetChild(idx);
                }
                if (ci < 0) return tr.gameObject;
                var cs = tr.GetComponents<Component>();
                return ci < cs.Length ? cs[ci] : null;
            }
            return null;
        }

        // Hủy MỌI editor TRƯỚC khi Unity reload domain. Nếu để tới OnDisable (chạy SAU khi Unity đã Dispose các SerializedObject lúc reload)
        // thì OnDisable của GameObjectInspector / ModelImporterEditor đọc property đã Dispose => NullReferenceException
        // "SerializedObject of SerializedProperty has been Disposed" + "ModelImporterEditor.OnEnable must call base.OnEnable".
        // Inspector gốc cũng dọn editor ở bước này. Cờ _reloading chặn mọi chỗ dựng lại editor trong khoảng chờ reload.
        bool _reloading;

        void OnBeforeReload()
        {
            _reloading = true;
            DisposeDebugSOs();
            ClearEditors();
            DestroyAssetEditors();
        }

        // beforeAssemblyReload: đang vào/ở/THOÁT Play thì bỏ qua, recompile ở Edit mode thì lưu.
        // NGUYÊN NHÂN CHÍNH của lỗi "mất tab khi thoát Play": lúc THOÁT Play, Unity reload domain khi object vẫn là bản clone
        // sắp bị hủy, và isPlayingOrWillChangePlaymode lúc này có thể đã là false (trạng thái đích = Edit) => bản cũ KHÔNG bỏ qua,
        // chạy SaveGidsCore trên clone: key bị hủy => rơi mất => bản chụp Edit mode bị xóa sạch. Nên dùng cờ riêng
        // (SessionState, sống qua domain reload) đặt ở ExitingEditMode và chỉ hạ ở EnteredEditMode.
        void SaveGids()
        {
            if (CustomsPlaySession.Active || EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            SaveGidsCore();
        }

        void SaveGidsCore()
        {
            // Tab đang mở của object hiện tại chưa nằm trong _perObject (chỉ được ghi khi rời object) => ghi ngay,
            // nếu không thoát Play xong sẽ mất tab đang chọn và rơi về tab mặc định.
            SaveStateOfCurrent();
            _collapsedGid.Clear();
            foreach (var c in _collapsed)
            {
                if (_collapsedGid.Count >= MaxGidObjects) break;
                var g = GidOf(c);
                if (g != null) _collapsedGid.Add(g);
            }

            // Tab theo object: GỘP vào bản chụp cũ thay vì xóa rồi dựng lại. Object nào không tính được id lúc này
            // (đã bị hủy, scene đang nạp dở...) thì giữ nguyên mục cũ của nó, không bị rơi mất.
            var gidTypes = new Dictionary<string, string>();
            var gidOrder = new List<string>();
            int oldN = Mathf.Min(_perObjGid.Count, _perObjGidTypes.Count);
            for (int i = 0; i < oldN; i++)
            {
                var og = _perObjGid[i];
                if (string.IsNullOrEmpty(og) || string.IsNullOrEmpty(_perObjGidTypes[i])) continue;
                if (!gidTypes.ContainsKey(og)) gidOrder.Add(og);
                gidTypes[og] = _perObjGidTypes[i];
            }
            foreach (var kv in _perObject)
            {
                if (kv.Key == null || kv.Value == null || kv.Value.Count == 0) continue;
                var g = GidOf(kv.Key);
                if (g == null) continue;
                if (gidTypes.ContainsKey(g)) gidOrder.Remove(g); // đưa lên cuối = mới nhất
                gidOrder.Add(g);
                gidTypes[g] = TypeSetString(kv.Value);
            }
            while (gidOrder.Count > MaxGidObjects) { gidTypes.Remove(gidOrder[0]); gidOrder.RemoveAt(0); } // bỏ mục cũ nhất

            _perObjGid.Clear();
            _perObjGidTypes.Clear();
            foreach (var g in gidOrder) { _perObjGid.Add(g); _perObjGidTypes.Add(gidTypes[g]); }

            // Panel thu gọn chưa khớp được: đừng làm rơi mất ở lần lưu kế tiếp
            foreach (var g in _pendingCollapsed)
                if (_collapsedGid.Count < MaxGidObjects && !_collapsedGid.Contains(g)) _collapsedGid.Add(g);
        }

        static HashSet<Type> ParseTypes(string joined)
        {
            var set = new HashSet<Type>();
            if (string.IsNullOrEmpty(joined)) return set;
            foreach (var name in joined.Split('\n'))
            {
                if (string.IsNullOrEmpty(name)) continue;
                Type ty = null;
                try { ty = Type.GetType(name); } catch (Exception) { }
                if (ty != null) set.Add(ty);
            }
            return set;
        }

        void RestoreUiState()
        {
            _collapsed.Clear();
            foreach (var c in _collapsedSer)
                if (c != null) _collapsed.Add(c);

            _perObject.Clear();
            int n = Mathf.Min(_perObjKeys.Count, _perObjTypes.Count);
            for (int i = 0; i < n; i++)
            {
                var g = _perObjKeys[i];
                if (g == null || string.IsNullOrEmpty(_perObjTypes[i])) continue;
                var set = ParseTypes(_perObjTypes[i]);
                if (set.Count > 0) _perObject[g] = set;
            }

            // Dự phòng: tham chiếu trực tiếp đã mất (vd sau Play mode) thì tìm lại bằng GlobalObjectId
            foreach (var gid in _collapsedGid)
                if (FromGid(gid) is Component rc) _collapsed.Add(rc);
            int m = Mathf.Min(_perObjGid.Count, _perObjGidTypes.Count);
            for (int i = 0; i < m; i++)
            {
                if (!(FromGid(_perObjGid[i]) is GameObject rg) || _perObject.ContainsKey(rg)) continue;
                var set = ParseTypes(_perObjGidTypes[i]);
                if (set.Count > 0) _perObject[rg] = set;
            }

            // Phần chưa khớp được (scene chưa nạp): để Bind khớp sau
            LoadPendingFromSnapshot();
        }

        // Nạp lại hàng chờ từ bản chụp Edit mode (_perObjGid / _collapsedGid).
        // Gọi ở OnEnable (có domain reload) VÀ ở ExitingEditMode / ExitingPlayMode (khi tắt Reload Domain thì OnEnable
        // không chạy lại, hàng chờ đã bị tiêu thụ hết lúc vào Play => thoát Play không còn gì để khôi phục).
        void LoadPendingFromSnapshot()
        {
            _pendingTabs.Clear();
            _pendingCollapsed.Clear();
            _pendingTries = 0;
            int m = Mathf.Min(_perObjGid.Count, _perObjGidTypes.Count);
            for (int i = 0; i < m; i++)
                if (!string.IsNullOrEmpty(_perObjGid[i]) && !string.IsNullOrEmpty(_perObjGidTypes[i]))
                    _pendingTabs[_perObjGid[i]] = _perObjGidTypes[i];
            foreach (var gid in _collapsedGid)
                if (!string.IsNullOrEmpty(gid)) _pendingCollapsed.Add(gid);
        }

        // EditorWindow.Repaint() chỉ yêu cầu vẽ lại cửa sổ; các IMGUIContainer cần được đánh dấu dirty thì OnGUI của chúng mới chạy lại.
        public new void Repaint()
        {
            base.Repaint();
            if (_topIm != null) _topIm.MarkDirtyRepaint();
            if (_previewIm != null) { ApplyPreviewHeight(); _previewIm.MarkDirtyRepaint(); }
            for (int i = 0; i < _bodyChunks.Count; i++) _bodyChunks[i].MarkDirtyRepaint();
        }

    }
}
#endif
