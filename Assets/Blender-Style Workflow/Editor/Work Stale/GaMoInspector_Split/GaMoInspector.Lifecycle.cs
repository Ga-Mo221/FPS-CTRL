// GaMoInspector.Lifecycle.cs  -  part of CustomsEditor (GaMo Inspector)
// Mo cua so, vong doi OnEnable/OnDisable, PreviewPump, OnInspectorUpdate
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
        [MenuItem("Tools/GaMo ToolKit/Blender Style Workflow/GM-Inspector", priority = 1)]
        [MenuItem("Window/General/GM-Inspector", priority = 3010)]
        static void Open()
        {
            // Mỗi lần bấm menu mở thêm MỘT cửa sổ mới (GetWindow chỉ trả về cửa sổ có sẵn nên không mở được 2 cái).
            // Các cửa sổ độc lập: mỗi cái có Lock, tab, lịch sử riêng.
            var w = NewWindow();
            if (!w._lock) w.BindSelection();
        }

        static CustomsEditor NewWindow()
        {
            var w = CreateInstance<CustomsEditor>();
            w.titleContent = new GUIContent("GM-Inspector");
            w.minSize = new Vector2(280, 200);
            w.Show();
            return w;
        }

        // Menu chuột phải / nút ⋮ trên TAB của cửa sổ: thêm "Duplicate Window".
        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Duplicate Window"), false, DuplicateWindow);
        }

        void DuplicateWindow()
        {
            var w = NewWindow();
            var p = position;
            w.position = new Rect(p.x + 30, p.y + 30, p.width, p.height); // cửa sổ nổi, lệch chút để thấy là cửa sổ mới
            w.BindSelection();
        }

        // Các cửa sổ đang mở: dùng để không vẽ trùng scene handle khi 2 cửa sổ cùng hiện một object.
        static readonly List<CustomsEditor> _instances = new List<CustomsEditor>();

        bool HasEditorFor(UObject target)
        {
            if (target == null) return false;
            if (_asset != null)
                return (_objectEditor != null && _objectEditor.target == target) || (_importerEditor != null && _importerEditor.target == target);
            foreach (var kv in _editors)
                if (kv.Value != null && kv.Value.target == target) return true;
            return false;
        }

        bool HandledByEarlierWindow(UObject target)
        {
            foreach (var w in _instances)
            {
                if (w == this) return false;      // chỉ nhường cho cửa sổ đăng ký trước
                if (w != null && w.HasEditorFor(target)) return true;
            }
            return false;
        }

        // Layout (.dwlt) lưu cửa sổ theo m_Script. Cửa sổ tạo lúc Unity chưa tìm được MonoScript (vd file chưa trùng tên class)
        // giữ m_Script = null mãi => lưu layout ra "m_Script: {fileID: 0}" => mở lại Unity cửa sổ bị thay bằng
        // FallbackEditorWindow rồi bị gỡ (biến mất). Gắn lại script cho instance cũ để lần lưu layout sau đúng.
        void EnsureScriptReference()
        {
            var so = new SerializedObject(this);
            var p = so.FindProperty("m_Script");
            if (p == null || p.objectReferenceValue != null) return;
            var ms = MonoScript.FromScriptableObject(this);
            if (ms == null) return;
            p.objectReferenceValue = ms;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        void OnEnable()
        {
            EnsureScriptReference();
            _reloading = false;    // cờ này chỉ đúng trong khoảng chờ reload domain; instance mới luôn bắt đầu ở trạng thái bình thường
            wantsMouseMove = true; // để box tab cập nhật hover
            if (!_instances.Contains(this)) _instances.Add(this);
            Selection.selectionChanged += OnSel;
            Undo.undoRedoPerformed += OnStructureChanged;
            ObjectChangeEvents.changesPublished += OnObjChanged; // Edit mode: repaint theo sự kiện thay vì 10Hz
            EditorApplication.hierarchyChanged += OnStructureChanged;
            SceneView.duringSceneGui += OnSceneHook;             // OnSceneGUI của editor tự dựng
            PrefabUtility.prefabInstanceUpdated += OnPrefabUpdated; // Apply/Revert xong => quét lại override
            EditorApplication.projectChanged += OnProjectChanged;   // đổi tên/xóa/di chuyển asset => làm mới header + kiểm tra Lock
            EditorApplication.update += PreviewPump;                // nhịp vẽ lại riêng cho khung Preview (clip đang play)
            AssemblyReloadEvents.beforeAssemblyReload += SaveGids;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;
            EditorApplication.playModeStateChanged += OnPlayModeState;

            _tabScale = Mathf.Clamp(EditorPrefs.GetFloat(TabScalePrefKey, _tabScale), MinTabScale, MaxTabScale);
            _previewHeight = Mathf.Clamp(EditorPrefs.GetFloat(PreviewHPrefKey, 220f), 80f, 1000f);

            _lastTypes.Clear();
            foreach (var tn in _lastTypeNames)
            {
                var t = Type.GetType(tn);
                if (t != null) _lastTypes.Add(t);
            }

            RestoreUiState(); // phải chạy TRƯỚC Bind: Bind đọc _perObject để chọn tab

            // Lock được giữ qua recompile / Play: nếu object bị khóa vẫn còn thì bind lại đúng nó
            if (_lock && _lockedObj != null) BindToObject(_lockedObj);
            else { _lock = false; _lockedObj = null; BindSelection(); }
        }

        void OnDisable()
        {
            _instances.Remove(this);
            Selection.selectionChanged -= OnSel;
            Undo.undoRedoPerformed -= OnStructureChanged;
            ObjectChangeEvents.changesPublished -= OnObjChanged;
            EditorApplication.hierarchyChanged -= OnStructureChanged;
            SceneView.duringSceneGui -= OnSceneHook;
            PrefabUtility.prefabInstanceUpdated -= OnPrefabUpdated;
            EditorApplication.projectChanged -= OnProjectChanged;
            EditorApplication.update -= PreviewPump;
            AssemblyReloadEvents.beforeAssemblyReload -= SaveGids;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeReload;
            EditorApplication.playModeStateChanged -= OnPlayModeState;
            DisposeDebugSOs();
            _cullList.Clear();
            if (_scalePrefDirty) { _scalePrefDirty = false; EditorPrefs.SetFloat(TabScalePrefKey, _tabScale); }
            ClearEditors();
            DestroyAssetEditors();
            _bodyChunks.Clear();
            _uiSig.Clear();
            _uiRoot = null; _scrollV = null; _topIm = null; _previewIm = null; _previewSplit = null;
        }

        // Học theo FBXAnimationPreviewWindow: vòng vẽ lại do EditorApplication.update điều khiển (chạy mỗi nhịp Editor, kể cả khi đứng yên
        // không có chuột / phím), mỗi nhịp gọi Repaint(). KHÔNG dùng EditorWindow.Update() hay chuỗi "đang vẽ thì xin vẽ tiếp" trong OnGUI.
        // Khác file mẫu ở chỗ cửa sổ này là UI Toolkit: ngoài Repaint() còn phải MarkDirtyRepaint() cho _previewIm để OnGUI của nó chạy lại;
        // các panel khác KHÔNG đánh dấu dirty nên vẫn đứng yên (rẻ).
        void PreviewPump()
        {
            if (DebugPreviewFps)
            {
                _dbgTick++;
                double dn = EditorApplication.timeSinceStartup;
                if (dn - _dbgT >= 1.0)
                {
                    _dbgText = "  tick=" + _dbgTick + " pass=" + _dbgPass + " live=" + _dbgLive + " play=" + CustomsNativePreview.LastPlayState
                             + " why=A" + _dbgWhy[0] + " B" + _dbgWhy[1] + " C" + _dbgWhy[2] + " D" + _dbgWhy[3] + " E" + _dbgWhy[4] + " F" + _dbgWhy[5];
                    _dbgTick = _dbgPass = _dbgLive = 0; System.Array.Clear(_dbgWhy, 0, _dbgWhy.Length); _dbgT = dn;
                }
            }
            if (_previewIm == null || _previewIm.panel == null) { _dbgWhy[0]++; return; }
            if (!_hasPreviewEd)  { _dbgWhy[1]++; return; }
            if (!_showPreview)   { _dbgWhy[2]++; return; }
            if (CustomsNativePreview.HasFor(this)) { _dbgWhy[3]++; return; } // đã tách Preview gốc: cửa sổ đó tự lo
            var pe = _previewEdCache;
            if (pe == null || pe.target == null) { _dbgWhy[4]++; return; }
            if (DebugPreviewFps && pe.RequiresConstantRepaint()) _dbgLive++;
            // RequiresConstantRepaint() của importer editor (Model) luôn false dù clip đang Play => hỏi thẳng AvatarPreview.timeControl.playing.
            // Throttle TRƯỚC khi hỏi trạng thái Play (WantsConstantRepaint dùng reflection): Editor có thể update vài trăm lần/giây (tick=493).
            // Đang Play: trần ~60 fps. Không Play: chỉ hỏi 10 lần/giây (bấm Play sẽ tự vẽ lại nên trễ tối đa 0.1s).
            double np = EditorApplication.timeSinceStartup;
            if (np - _pumpAt < (_previewConstant ? PreviewFrameTime : 0.1)) return;
            _pumpAt = np;
            bool want = CustomsNativePreview.WantsConstantRepaint(pe);
            _previewConstant = want;
            if (!want) { _dbgWhy[5]++; return; }
            if (DebugPreviewFps) _dbgPass++;
            RepaintForPreview(pe);
        }

        void OnInspectorUpdate()
        {
            if (_scalePrefDirty) { _scalePrefDirty = false; EditorPrefs.SetFloat(TabScalePrefKey, _tabScale); }
            CheckLockAlive();
            // Vào/thoát Play: object cũ bị hủy (hoặc Selection chưa kịp trỏ sang bản clone lúc OnEnable chạy) nên cửa sổ trống,
            // mà selectionChanged có thể không bắn lại. Đang trống mà Selection có object => bind lại.
            // (Chỉ khi trống hẳn, để không đè lên việc điều hướng lịch sử.)
            if (!_lock && _go == null && _asset == null && Selection.activeObject != null) BindSelection();
            SyncUI(); // đổi cấu trúc (thêm/xóa component, đổi material...) => dựng lại cây UI; không đổi thì gần như miễn phí
            if (_ovrDirty && _go != null) Repaint(); // có thay đổi chờ quét override (bị gộp đợt 0.25s)
            if (_go == null && _asset == null) return;
            // Asset đang play Preview (clip...): Update() đã lo vẽ lại Preview; Repaint() đầy đủ ở đây sẽ vẽ lại MỌI panel (Import Settings nặng)
            // mỗi 0.1s (Play) / 0.5s (Edit) => khựng định kỳ ngay giữa lúc animation chạy. Asset không có giá trị đổi ngầm nên bỏ qua.
            if (_asset != null && _previewEdCache != null && _previewEdCache.target != null && _previewConstant) return;
            // Play mode: giá trị đổi liên tục => ~10Hz. Edit mode: ~2Hz dự phòng, còn lại repaint theo sự kiện.
            if (EditorApplication.isPlaying) Repaint();
            else
            {
                // Edit mode: ~2Hz khi đang hover / focus cửa sổ, ~0.5Hz khi không (đứng yên mà vẽ lại editor nặng là phí).
                bool active = mouseOverWindow == this || focusedWindow == this;
                if (++_tick % (active ? 5 : 20) == 0) Repaint();
            }
        }

    }
}
#endif