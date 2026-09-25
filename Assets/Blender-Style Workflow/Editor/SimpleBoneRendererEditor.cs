using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;


namespace GaMo.BlenderEditorTools
{
    [CustomEditor(typeof(SimpleBoneRenderer))]
    [InitializeOnLoad]
    public class SimpleBoneRendererEditor : Editor
    {
        // --- Cache danh sách renderer trong scene ---
        // Trước kia FindObjectsByType() bị gọi lại ở MỌI event (MouseMove, Layout,
        // Repaint...) tức hàng chục lần/giây. Giờ chỉ quét lại khi hierarchy thực sự
        // thay đổi (thêm/xóa object, đổi scene, undo/redo...).
        private static SimpleBoneRenderer[] _cachedRenderers;
        private static bool _renderersDirty = true;

        // Stage (Main Stage hoặc Prefab Stage) mà cache trên được quét ra.
        // Khi mở/đóng/chuyển Prefab Mode thì stage đổi -> phải quét lại.
        private static StageHandle _cachedStage;

        static SimpleBoneRendererEditor()
        {
            SceneView.duringSceneGui -= OnGlobalSceneGUI;
            SceneView.duringSceneGui += OnGlobalSceneGUI;

            EditorApplication.hierarchyChanged -= MarkRenderersDirty;
            EditorApplication.hierarchyChanged += MarkRenderersDirty;

            Undo.undoRedoPerformed -= MarkRenderersDirty;
            Undo.undoRedoPerformed += MarkRenderersDirty;
        }

        private static void MarkRenderersDirty()
        {
            _renderersDirty = true;

            // Đổi tên / xóa / reparent xương đều làm hierarchy đổi -> bỏ cache label để
            // tên hiển thị luôn đúng, đồng thời dọn entry của xương đã bị xóa (không rò rỉ).
            _jointLabelCache.Clear();
        }

        private static SimpleBoneRenderer[] GetRenderers()
        {
            // Object.FindObjectsByType không thấy các object nằm trong Prefab Mode (prefab stage
            // là 1 preview scene riêng), nên phải quét theo stage đang mở thay vì quét toàn cục.
            // - Đang ở Scene thường: stage hiện tại = Main Stage (gồm mọi scene đang mở).
            // - Đang ở Prefab Mode: stage hiện tại = prefab đang chỉnh sửa.
            StageHandle stage = StageUtility.GetCurrentStageHandle();

            if (_renderersDirty || _cachedRenderers == null || stage != _cachedStage)
            {
                if (stage != _cachedStage)
                    _jointLabelCache.Clear(); // Transform của stage cũ không còn dùng nữa

                _cachedRenderers = stage.FindComponentsOfType<SimpleBoneRenderer>();
                _cachedStage = stage;
                _renderersDirty = false;
            }

            return _cachedRenderers;
        }

        // ────────────────────────────────────────────────────────────────────────
        // UI Inspector theo phong cách tab (giống TransformConstraintEditor)
        // ────────────────────────────────────────────────────────────────────────
        private int currentTab = 0;
        private readonly string[] tabTitles = { "Setup", "Bone & Joint Settings" };

        // Properties - Setup
        private SerializedProperty rootBoneProp;
        private SerializedProperty bonesProp;
        private SerializedProperty showProp;
        private SerializedProperty boneAxisProp;

        // Properties - Bone Settings
        private SerializedProperty boneColorProp;
        private SerializedProperty boneLengthProp;
        private SerializedProperty boneRadiusProp;
        private SerializedProperty pingInHierarchyProp;

        // Properties - Joint Settings
        private SerializedProperty drawJointsProp;
        private SerializedProperty jointColorProp;
        private SerializedProperty jointFontSizeProp;

        private void OnEnable()
        {
            // Guard: nếu target null (object vừa bị xóa/Undo, hoặc Editor được tái tạo
            // ngay lúc recompile/prefab mode...), việc chạm vào serializedObject sẽ
            // ném SerializedObjectNotCreatableException. Thoát sớm để tránh crash Inspector.
            if (target == null)
                return;

            rootBoneProp = serializedObject.FindProperty("rootBone");
            bonesProp = serializedObject.FindProperty("bones");
            showProp = serializedObject.FindProperty("show");
            boneAxisProp = serializedObject.FindProperty("boneAxis");

            boneColorProp = serializedObject.FindProperty("boneColor");
            boneLengthProp = serializedObject.FindProperty("boneLength");
            boneRadiusProp = serializedObject.FindProperty("boneRadius");
            pingInHierarchyProp = serializedObject.FindProperty("pingInHierarchy");

            drawJointsProp = serializedObject.FindProperty("drawJoints");
            jointColorProp = serializedObject.FindProperty("jointColor");
            jointFontSizeProp = serializedObject.FindProperty("jointFontSize");

            SetComponentIcon();
        }

        private void SetComponentIcon()
        {
            // Chỉ tìm đúng file có tên là "BoneRenderer" (icon của bạn) để tối ưu tốc độ quét.
            // Nếu bạn có icon riêng, đặt tên file trùng với chuỗi tìm kiếm bên dưới,
            // hoặc đổi lại chuỗi "BoneRenderer" cho đúng tên file icon bạn đang dùng.
            string[] guids = AssetDatabase.FindAssets("BoneRenderer t:Texture2D");

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (path.Contains("Editor/Icon") || path.Contains("Blender-Style Workflow"))
                {
                    Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (icon != null)
                    {
                        EditorGUIUtility.SetIconForObject(target, icon);
                        break;
                    }
                }
            }
        }

        // ── Wrapped Toolbar (giống TransformConstraintEditor) ───────────────────────
        /// <summary>
        /// Vẽ toolbar giống GUILayout.Toolbar nhưng tự xuống hàng khi
        /// inspector quá hẹp để chứa tất cả tab trên một hàng.
        /// </summary>
        private int DrawWrappedToolbar(int selected, string[] tabs)
        {
            float viewWidth   = EditorGUIUtility.currentViewWidth - 20f;
            float tabMinWidth = 100f;
            float tabPadding  = 4f;

            int tabsPerRow = Mathf.Max(1, Mathf.FloorToInt(
                (viewWidth + tabPadding) / (tabMinWidth + tabPadding)));

            int tabCount = tabs.Length;
            int rows     = Mathf.CeilToInt((float)tabCount / tabsPerRow);

            for (int row = 0; row < rows; row++)
            {
                int startIdx = row * tabsPerRow;
                int endIdx   = Mathf.Min(startIdx + tabsPerRow, tabCount);
                int count    = endIdx - startIdx;

                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int i = startIdx; i < endIdx; i++)
                    {
                        float tabWidth = (viewWidth - (count - 1) * tabPadding) / count;

                        bool isSelected = (selected == i);
                        bool clicked    = GUILayout.Toggle(
                            isSelected, tabs[i],
                            EditorStyles.toolbarButton,
                            GUILayout.Width(tabWidth), GUILayout.Height(22f));

                        if (clicked && !isSelected)
                            selected = i;

                        if (i < endIdx - 1)
                            GUILayout.Space(tabPadding);
                    }
                }
            }

            return selected;
        }

        public override void OnInspectorGUI()
        {
            // Guard tương tự OnEnable: target có thể trở thành null giữa chừng
            // (ví dụ object bị xóa trong khi Inspector đang mở).
            if (target == null)
                return;

            // Nếu OnEnable từng bail sớm vì target null, properties sẽ chưa được gán.
            // Thử khởi tạo lại một lần cho chắc trước khi dùng.
            if (rootBoneProp == null)
            {
                OnEnable();
                if (rootBoneProp == null) return;
            }

            serializedObject.Update();

            SimpleBoneRenderer boneRenderer = (SimpleBoneRenderer)target;

            EditorGUILayout.Space(3);

            // Nút Show / Hide luôn nằm trên cùng (ngoài các tab) để bật/tắt tức thì.
            // Nhấn = Show (đang hiển thị), nhả = Hide (đang ẩn).
            EditorGUI.BeginChangeCheck();
            bool newShow = GUILayout.Toggle(showProp.boolValue,
                showProp.boolValue ? Content.Show : Content.Hide, "Button", GUILayout.Height(22));
            if (EditorGUI.EndChangeCheck())
            {
                showProp.boolValue = newShow;
                serializedObject.ApplyModifiedProperties();
                SceneView.RepaintAll(); // cập nhật Scene view ngay, không chờ event kế tiếp
            }

            // 3 nút X / Y / Z nằm thẳng hàng ngay dưới nút Show: chọn trục cục bộ để vẽ xương.
            EditorGUILayout.Space(3);
            DrawAxisButtons();

            EditorGUILayout.Space(3);
            currentTab = DrawWrappedToolbar(currentTab, tabTitles);
            EditorGUILayout.Space(10);

            switch (currentTab)
            {
                case 0:
                    DrawSetupTab(boneRenderer);
                    break;
                case 1:
                    DrawBoneAndJointSettingsTab();
                    break;
            }

            EditorGUILayout.Space(5);
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// 3 nút X / Y / Z cùng một hàng, hoạt động như radio: đúng 1 nút được nhấn tại một thời điểm.
        /// Dùng cùng style "Button" với nút Show để nhìn đồng bộ.
        /// </summary>
        private void DrawAxisButtons()
        {
            int current = boneAxisProp.enumValueIndex;
            int selected = current;

            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < Content.AxisLabels.Length; i++)
                {
                    bool isSelected = (current == i);
                    bool clicked = GUILayout.Toggle(isSelected, Content.AxisLabels[i],
                        "Button", GUILayout.Height(22), GUILayout.ExpandWidth(true));

                    // Bấm lại nút đang chọn thì giữ nguyên (không cho bỏ chọn hết).
                    if (clicked && !isSelected)
                        selected = i;
                }
            }

            if (selected != current)
            {
                boneAxisProp.enumValueIndex = selected;
                serializedObject.ApplyModifiedProperties();
                SceneView.RepaintAll();
            }
        }

        private void DrawSetupTab(SimpleBoneRenderer boneRenderer)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(rootBoneProp, Content.RootBone);

            EditorGUILayout.Space(6);

            if (GUILayout.Button(Content.LoadBones, "Button", GUILayout.Height(20)))
            {
                boneRenderer.PopulateChildren();
                MarkRenderersDirty();
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(bonesProp, Content.Bones, true);
            EditorGUILayout.EndVertical();
        }

        private void DrawBoneAndJointSettingsTab()
        {
            // ── BONE SETTINGS ────────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Bone Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(boneColorProp, Content.BoneColor);
            EditorGUILayout.PropertyField(boneLengthProp, Content.BoneLength);
            EditorGUILayout.PropertyField(boneRadiusProp, Content.BoneRadius);
            EditorGUILayout.PropertyField(pingInHierarchyProp, Content.PingInHierarchy);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // ── JOINT SETTINGS ───────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Joint Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(drawJointsProp, Content.DrawJoints);

            // Không disable theo Draw Joints: màu/cỡ chữ này còn áp dụng cho tên của bone đang
            // được chọn khi Draw Joints tắt.
            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(jointColorProp, Content.JointColor);
            EditorGUILayout.PropertyField(jointFontSizeProp, Content.FontSize);
            EditorGUILayout.EndVertical();
        }

        private void OnSceneGUI()
        {
            // Để trống vì sự kiện đã được quản lý tập trung ở OnGlobalSceneGUI static
        }

        // --- Cache GUIContent để tránh new GUIContent(...) mỗi lần repaint ---
        // Inspector repaint hàng chục lần/giây; mỗi lần tạo mới GUIContent là 1 lần GC alloc.
        private static class Content
        {
            public static readonly GUIContent RootBone = new GUIContent("Root Bone");
            public static readonly GUIContent LoadBones = new GUIContent("Load All Child Bones");
            public static readonly GUIContent Show = new GUIContent("Show",
                "Đang hiển thị khung xương trong Scene view. Nhấn để ẩn.");
            public static readonly GUIContent Hide = new GUIContent("Hide",
                "Đang ẩn khung xương trong Scene view. Nhấn để hiện.");
            // Thứ tự phải khớp với enum BoneAxis (X = 0, Y = 1, Z = 2).
            public static readonly GUIContent[] AxisLabels =
            {
                new GUIContent("X", "Vẽ xương dọc theo trục X cục bộ của bone."),
                new GUIContent("Y", "Vẽ xương dọc theo trục Y cục bộ của bone (mặc định, kiểu Blender)."),
                new GUIContent("Z", "Vẽ xương dọc theo trục Z cục bộ của bone.")
            };
            public static readonly GUIContent Bones = new GUIContent("Bones");
            public static readonly GUIContent BoneColor = new GUIContent("Bone Color");
            public static readonly GUIContent BoneLength = new GUIContent("Bone Length",
                "Chiều dài mặc định của xương không có xương con nối tiếp (connected). " +
                "Xương có con connected thì chiều dài lấy theo vị trí xương con.");
            public static readonly GUIContent BoneRadius = new GUIContent("Bone Radius",
                "Bán kính phần đế của xương (world unit), độc lập với chiều dài.");
            public static readonly GUIContent PingInHierarchy = new GUIContent("Ping In Hierarchy",
                "Khi click chọn bone trong Scene view, đồng thời hiển thị (ping) bone đó trong cửa sổ Hierarchy.");
            public static readonly GUIContent DrawJoints = new GUIContent("Draw Joints");
            public static readonly GUIContent JointColor = new GUIContent("Joint Color");
            public static readonly GUIContent FontSize = new GUIContent("Font Size",
                "Cỡ chữ của tên xương hiển thị trong Scene view.");
        }

        // --- Dung sai xác định xương con có "connect" với xương cha hay không ---
        // Xương con được coi là nối (connected) khi gốc của nó nằm trên trục đã chọn (X/Y/Z)
        // của xương cha (phía trước, theo chiều dương của trục). Lệch trục quá dung sai -> xương
        // con chỉ là "offset", không nối với cha.
        // Dung sai = max(tuyệt đối, tỉ lệ * chiều dài đoạn dọc trục đã chọn) để
        // chịu được sai số float khi import từ Blender/FBX, mà vẫn đúng với xương nhỏ hay lớn.
        private const float ConnectMinAlong = 0.0001f;      // dưới mức này coi như trùng gốc
        private const float ConnectAbsTolerance = 0.0001f;  // sai số lệch trục tối thiểu (world unit)
        private const float ConnectRelTolerance = 0.01f;    // 1% chiều dài (~0.6°)

        // --- Danh sách label tên xương (chỉ để HIỂN THỊ) ---
        // Việc click chọn bone do HandleBonePicking (GetControlID + AddControl) đảm nhận, không
        // phụ thuộc label. Label được gom ở đây rồi vẽ bằng GUI.Label ở pass GUI riêng
        // (Handles.BeginGUI/EndGUI, không gian màn hình) và không bắt chuột.
        private struct JointGuiInfo
        {
            public Vector3 worldPos;
            public Transform bone;
            public Color jointColor;
            public int fontSize;
        }

        private static readonly List<JointGuiInfo> _reusableJointInfos = new List<JointGuiInfo>(32);

        // Cache theo xương: GUIContent + kích thước chữ đã đo (GUIStyle.CalcSize khá tốn),
        // chỉ đo lại khi font size đổi. Không gọi bone.name mỗi frame vì mỗi lần gọi Unity
        // cấp phát 1 string mới; thay vào đó cache bị xóa trong MarkRenderersDirty khi đổi tên.
        private sealed class LabelEntry
        {
            public GUIContent content;
            public Vector2 size;
            public int fontSize = -1; // -1 = chưa đo
        }

        private static readonly Dictionary<Transform, LabelEntry> _jointLabelCache =
            new Dictionary<Transform, LabelEntry>(32);

        private static LabelEntry GetJointLabel(Transform bone)
        {
            if (!_jointLabelCache.TryGetValue(bone, out LabelEntry entry))
            {
                entry = new LabelEntry { content = new GUIContent(bone.name) };
                _jointLabelCache[bone] = entry;
            }

            return entry;
        }

        // Handles.Draw* và GUI.Label chỉ có tác dụng ở event Repaint. Các event khác (Layout,
        // MouseMove, MouseDown...) chỉ cần chạy picking (HandleBonePicking) nên bỏ qua việc
        // vẽ thân xương và gom label ở các event đó.
        private static bool _isRepaint;

        // --- Cache hình học xương giữa Layout và Repaint ---
        // IMGUI luôn gửi Layout rồi mới tới Repaint (hoặc event khác) trong cùng 1 lượt OnGUI.
        // Hình học (start/end) chỉ tính ở Layout, Repaint đọc lại theo thứ tự "slot" (mỗi
        // GetControlID = 1 slot) thay vì tính lần 2. Các event khác (MouseMove/Down/Up/Drag...)
        // không cần hình học nên bỏ qua hoàn toàn.
        private struct BoneSegment
        {
            public Transform bone; // dùng để kiểm chứng slot còn khớp bone hay không
            public Vector3 start;
            public Vector3 end;
        }

        private static readonly List<BoneSegment> _segments = new List<BoneSegment>(64);
        private static int _segCursor;

        // nearestControl / hotControl chỉ đọc 1 lần mỗi lần callback thay vì mỗi bone.
        private static int _nearestControl;
        private static int _hotControl;

        // --- Picking bằng HandleUtility (cơ chế chọn của Scene view) ---
        // Mỗi bone đăng ký 1 control (AddControl) với khoảng cách từ chuột tới đoạn xương.
        // Unity chọn control có khoảng cách nhỏ nhất -> nearestControl. Cùng hệ thống với gizmo
        // Move/Rotate nên không còn "tranh click" như GUI.Button.
        private static readonly int BonePickHint = "GaMo.SimpleBoneRenderer.Pick".GetHashCode();

        // Cộng thêm vài pixel vào khoảng cách của bone: khi đường xương trùng với tay gizmo
        // (cùng khoảng cách 0) thì gizmo luôn thắng, bone chỉ thắng khi chuột ở ngoài gizmo.
        private const float PickBias = 1f;

        // Control của bone đang được rê chuột tới (0 = không có) — để chỉ Repaint khi đổi hover.
        private static int _hoverControl;
        private static readonly Color HoverColor = new Color(1f, 0.85f, 0.2f);

        private static void OnGlobalSceneGUI(SceneView sceneView)
        {
            SimpleBoneRenderer[] renderers = GetRenderers();
            if (renderers == null || renderers.Length == 0) return;

            Matrix4x4 originalMatrix = Handles.matrix;

            EventType evType = Event.current.type;
            _isRepaint = evType == EventType.Repaint;
            bool isLayout = evType == EventType.Layout;

            // Chỉ Layout (AddControl cần khoảng cách) và Repaint (vẽ) mới cần start/end.
            bool needGeometry = isLayout || _isRepaint;

            if (isLayout) _segments.Clear();
            _segCursor = 0;

            _nearestControl = HandleUtility.nearestControl;
            _hotControl = GUIUtility.hotControl;

            // Cache một lần bên ngoài vòng lặp kép; truy cập Selection.activeTransform
            // mỗi xương mỗi frame tốn không đáng.
            Transform selectedTransform = Selection.activeTransform;

            // Gom danh sách label tên xương để vẽ ở pass GUI riêng — xem _reusableJointInfos.
            _reusableJointInfos.Clear();

            // true nếu đối tượng đang chọn là 1 bone thuộc bất kỳ SimpleBoneRenderer nào.
            bool selectionIsBone = false;

            foreach (var boneRenderer in renderers)
            {
                if (boneRenderer == null || !boneRenderer.enabled || !boneRenderer.gameObject.activeInHierarchy ||
                    boneRenderer.bones == null)
                    continue;

                // Nút Show/Hide trong Inspector: tắt là bỏ qua hoàn toàn (cả Edit lẫn Play),
                // gồm cả thân xương lẫn label tên.
                if (!boneRenderer.show)
                    continue;

                HashSet<Transform> boneSet = boneRenderer.BoneSet;
                List<Transform> bones = boneRenderer.bones;

                if (selectedTransform != null && boneSet.Contains(selectedTransform))
                    selectionIsBone = true;

                for (int i = 0; i < bones.Count; i++)
                {
                    // GetControlID PHẢI được gọi cho mọi phần tử ở mọi event, theo cùng thứ tự
                    // (kể cả bone null bị bỏ qua bên dưới), để ID của từng bone ổn định giữa
                    // Layout / MouseDown / Repaint. Vì vậy đặt nó TRƯỚC mọi lệnh continue.
                    int controlID = GUIUtility.GetControlID(BonePickHint, FocusType.Passive);

                    Transform bone = bones[i];
                    int slot = _segCursor++;

                    if (bone == null)
                    {
                        // Giữ slot đồng bộ giữa Layout và Repaint dù bone null.
                        if (isLayout) _segments.Add(default);
                        continue;
                    }

                    // MouseMove/MouseDown/MouseUp/MouseDrag...: picking chỉ cần controlID và
                    // nearestControl, không cần start/end -> bỏ qua toàn bộ phần tính hình học.
                    if (!needGeometry)
                    {
                        HandleBonePicking(bone, default, default, controlID, boneRenderer.pingInHierarchy);
                        continue;
                    }

                    Vector3 start, end;
                    if (_isRepaint && slot < _segments.Count && ReferenceEquals(_segments[slot].bone, bone))
                    {
                        // Dùng lại kết quả đã tính ở Layout cùng lượt OnGUI.
                        BoneSegment seg = _segments[slot];
                        start = seg.start;
                        end = seg.end;
                    }
                    else
                    {
                        // Layout, hoặc Repaint không khớp cache (fallback an toàn).
                        ComputeBoneSegment(boneRenderer, bone, boneSet, out start, out end);
                    }

                    if (isLayout)
                        _segments.Add(new BoneSegment { bone = bone, start = start, end = end });

                    ProcessBone(boneRenderer, bone, start, end, selectedTransform, controlID);
                }
            }

            Handles.matrix = originalMatrix;

            // Khi đang chọn 1 bone: chỉ vẽ label của CHÍNH bone đó, ẩn tất cả label còn lại.
            // Bỏ chọn bone (hoặc chọn đối tượng không phải bone) thì onlyBone = null -> hiện lại tất cả.
            Transform onlyBone = selectionIsBone ? selectedTransform : null;

            // Pass GUI riêng cho label tên xương (chỉ hiển thị, không bắt click).
            DrawJointLabels(sceneView, onlyBone);
        }

        /// <summary>
        /// Tính đoạn start-end (world space) của 1 bone.
        /// Mỗi xương là MỘT đoạn đi dọc trục cục bộ do người dùng chọn (X/Y/Z, mặc định Y
        /// kiểu Blender) của chính nó. Tail = gốc của xương con gần nhất nằm đúng trên trục đó
        /// (connected). Xương con lệch trục chỉ là offset: KHÔNG kéo xương cha tới nó; xương
        /// cha dùng chiều dài mặc định, còn xương con tự vẽ đoạn của riêng nó.
        /// </summary>
        private static void ComputeBoneSegment(SimpleBoneRenderer boneRenderer, Transform bone,
            HashSet<Transform> boneSet, out Vector3 start, out Vector3 end)
        {
            start = bone.position;
            Vector3 axis = GetBoneAxisDirection(bone, boneRenderer.boneAxis);

            bool hasConnectedChild = false;
            float nearestAlong = float.MaxValue;
            Vector3 connectedTail = start;

            // Không dùng foreach trên Transform: nó cấp phát 1 enumerator mỗi lần gọi.
            int childCount = bone.childCount;
            for (int c = 0; c < childCount; c++)
            {
                Transform child = bone.GetChild(c);
                if (!boneSet.Contains(child)) continue;

                Vector3 toChild = child.position - start;
                float along = Vector3.Dot(toChild, axis);
                if (along <= ConnectMinAlong) continue; // nằm sau/trùng gốc -> không nối

                Vector3 perpendicular = toChild - axis * along;
                float tolerance = Mathf.Max(ConnectAbsTolerance, along * ConnectRelTolerance);
                if (perpendicular.sqrMagnitude > tolerance * tolerance) continue; // lệch trục -> offset

                if (along < nearestAlong)
                {
                    nearestAlong = along;
                    connectedTail = child.position;
                    hasConnectedChild = true;
                }
            }

            end = hasConnectedChild
                ? connectedTail
                : start + axis * boneRenderer.boneLength;
        }

        /// <summary>
        /// Trả về hướng (world space) của trục cục bộ được chọn trên bone.
        /// </summary>
        private static Vector3 GetBoneAxisDirection(Transform bone, BoneAxis axis)
        {
            switch (axis)
            {
                case BoneAxis.X: return bone.right;
                case BoneAxis.Z: return bone.forward;
                default:         return bone.up; // BoneAxis.Y (mặc định)
            }
        }

        private static void ProcessBone(SimpleBoneRenderer renderer, Transform bone,
            Vector3 start, Vector3 end, Transform selectedTransform, int controlID)
        {
            // Picking chạy ở MỌI event (Layout: đăng ký control; MouseDown/Up: xử lý click).
            HandleBonePicking(bone, start, end, controlID, renderer.pingInHierarchy);

            if (_isRepaint)
            {
                // Hover chỉ hiện khi không có control nào đang bị kéo (hotControl == 0),
                // tránh sáng nhầm bone lúc người dùng đang kéo gizmo Move/Rotate.
                bool hovered = _hotControl == 0 && _nearestControl == controlID;

                if (selectedTransform == bone) Handles.color = Color.green;
                else if (hovered) Handles.color = HoverColor;
                else Handles.color = renderer.boneColor;
            }

            DrawBoneGraphics(start, end, renderer, bone, selectedTransform == bone);
        }

        /// <summary>
        /// Scene View picking cho 1 bone:
        ///  - Layout   : AddControl(id, khoảng cách pixel từ chuột tới đoạn start-end).
        ///  - MouseDown: nếu bone này là nearestControl -> chọn nó (và ping trong Hierarchy nếu bật).
        ///  - MouseUp  : nhả hotControl.
        ///  - MouseMove: chỉ Repaint khi trạng thái hover của bone này đổi.
        /// Handles.DrawLine chỉ là hình ảnh; AddControl mới là thứ cho Unity biết "có control ở đây".
        /// </summary>
        private static void HandleBonePicking(Transform bone, Vector3 start, Vector3 end, int controlID,
            bool pingInHierarchy)
        {
            Event e = Event.current;

            switch (e.GetTypeForControl(controlID))
            {
                case EventType.Layout:
                    HandleUtility.AddControl(controlID,
                        HandleUtility.DistanceToLine(start, end) + PickBias);
                    break;

                case EventType.MouseDown:
                    // Bỏ qua Alt+click / chuột giữa / chuột phải (đang orbit, pan, fly...).
                    if (e.button == 0 && !e.alt && !Tools.viewToolActive
                        && GUIUtility.hotControl == 0
                        && _nearestControl == controlID)
                    {
                        GUIUtility.hotControl = controlID;
                        e.Use(); // chặn picking mặc định của Scene view (chọn mesh phía sau)
                        Selection.activeTransform = bone;

                        // Tùy chọn: cuộn/mở tới bone này trong cửa sổ Hierarchy và nháy sáng nó.
                        if (pingInHierarchy)
                            EditorGUIUtility.PingObject(bone);
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlID)
                    {
                        GUIUtility.hotControl = 0;
                        e.Use();
                    }
                    break;

                case EventType.MouseMove:
                    bool isNearest = _nearestControl == controlID;
                    if (isNearest && _hoverControl != controlID)
                    {
                        _hoverControl = controlID;
                        HandleUtility.Repaint();
                    }
                    else if (!isNearest && _hoverControl == controlID)
                    {
                        _hoverControl = 0;
                        HandleUtility.Repaint();
                    }
                    break;
            }
        }

        private static void DrawBoneGraphics(Vector3 start, Vector3 end, SimpleBoneRenderer renderer,
            Transform bone, bool isSelected)
        {
            Vector3 direction = end - start;
            float distance = direction.magnitude;
            if (distance < 0.0001f) return;

            Color originalColor = default;

            // --- 1. VẼ THÂN XƯƠNG (chỉ ở Repaint) ---
            if (_isRepaint)
            {
                float radius = renderer.boneRadius;
                originalColor = Handles.color;

                Handles.DrawLine(start, end);

                Quaternion lookRotation = Quaternion.LookRotation(direction);
                Handles.DrawWireDisc(start, direction, radius);

                Vector3 right = lookRotation * Vector3.right * radius;
                Vector3 up = lookRotation * Vector3.up * radius;

                Handles.DrawLine(start + right, end);
                Handles.DrawLine(start - right, end);
                Handles.DrawLine(start + up, end);
                Handles.DrawLine(start - up, end);
            }

            // --- 2. GOM LABEL TÊN XƯƠNG ĐỂ VẼ Ở PASS GUI RIÊNG (chỉ ở Repaint) ---
            // Chỉ enqueue vị trí + xương; việc vẽ nằm ở DrawJointLabels. Click chọn bone
            // không đi qua label mà đi qua HandleBonePicking.
            // Draw Joints tắt vẫn hiện tên của bone đang được chọn (vừa click vào).
            if (_isRepaint && (renderer.drawJoints || isSelected))
            {
                // Label nằm ở điểm GIỮA thân xương (mid), không phải đầu mút (tip).
                Vector3 mid = (start + end) * 0.5f;
                _reusableJointInfos.Add(new JointGuiInfo
                {
                    worldPos = mid,
                    bone = bone,
                    jointColor = renderer.jointColor,
                    fontSize = renderer.jointFontSize
                });
            }

            if (_isRepaint)
                Handles.color = originalColor;
        }

        // Khởi tạo trễ (không phải static field initializer): EditorStyles chỉ an toàn
        // để truy cập bên trong 1 lệnh gọi kiểu OnGUI, không phải lúc static constructor
        // của class chạy (có thể xảy ra sớm hơn do [InitializeOnLoad]).
        // Style này KHÔNG có background/border — chỉ chữ, để khớp không chiếm diện tích
        // hình khối trên màn hình như button thường.
        private static GUIStyle _jointLabelStyle;

        private static void DrawJointLabels(SceneView sceneView, Transform onlyBone)
        {
            // GUI.Label chỉ cần vẽ ở Repaint; các event khác không có gì để làm.
            if (!_isRepaint || _reusableJointInfos.Count == 0) return;

            Handles.BeginGUI();

            if (_jointLabelStyle == null)
            {
                _jointLabelStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(4, 4, 1, 1)
                };
                _jointLabelStyle.normal.background = null;
                _jointLabelStyle.hover.background = null;
                _jointLabelStyle.active.background = null;
                _jointLabelStyle.focused.background = null;
                _jointLabelStyle.border = new RectOffset(0, 0, 0, 0);
            }

            // Lấy camera 1 lần cho cả vòng lặp thay vì mỗi label.
            Camera cam = sceneView.camera;

            for (int i = 0; i < _reusableJointInfos.Count; i++)
            {
                JointGuiInfo info = _reusableJointInfos[i];
                if (info.bone == null) continue;

                // Có bone đang được chọn -> chỉ giữ lại label của bone đó.
                if (onlyBone != null && info.bone != onlyBone) continue;

                Vector2 guiPoint = HandleUtility.WorldToGUIPoint(info.worldPos);

                // Khớp ở phía sau camera thì bỏ qua, tránh vẽ chữ "ma" ở góc màn hình.
                if (cam != null)
                {
                    Vector3 viewPos = cam.WorldToViewportPoint(info.worldPos);
                    if (viewPos.z < 0f) continue;
                }

                LabelEntry label = GetJointLabel(info.bone);

                // Màu chữ theo Joint Color; mutate trực tiếp style dùng chung vì vẽ tuần
                // tự trong cùng 1 thread, không có tranh chấp.
                _jointLabelStyle.normal.textColor = info.jointColor;
                _jointLabelStyle.hover.textColor = info.jointColor;
                _jointLabelStyle.fontSize = info.fontSize; // set trước CalcSize để rect ôm đúng chữ

                // Chỉ đo lại kích thước chữ khi lần đầu hoặc font size đổi.
                if (label.fontSize != info.fontSize)
                {
                    label.size = _jointLabelStyle.CalcSize(label.content);
                    label.fontSize = info.fontSize;
                }

                Vector2 size = label.size;
                Rect rect = new Rect(guiPoint.x - size.x * 0.5f, guiPoint.y - size.y * 0.5f, size.x, size.y);

                // GUI.Label không bắt chuột -> click xuyên qua chữ, xuống control picking của bone
                // (label nằm ngay giữa thân xương nên click vào chữ vẫn chọn được đúng bone).
                GUI.Label(rect, label.content, _jointLabelStyle);
            }

            Handles.EndGUI();
        }
    }
}