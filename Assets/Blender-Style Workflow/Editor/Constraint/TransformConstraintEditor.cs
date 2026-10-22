#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace GaMo.BlenderEditorTools
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(TransformConstraint))]
    [InitializeOnLoad]
    public class TransformConstraintEditor : Editor
    {
        private int currentTab = 0;

        private readonly string[] tabTitles =
            { "Targets & Core", "Position & Rotation", "Gizmo Settings", "API Reference" };

        // Biến lưu trữ duration trên UI Editor
        private float debugSwitchDuration = 0.25f;

        // Cache GUIContent để tránh tạo object mới mỗi lần OnInspectorGUI repaint
        private GUIContent _gcConstraintObject;
        private GUIContent _gcHasExternalPoseDriver;
        private GUIContent _gcNormalizeWeights;
        private GUIContent _gcTargetsList;
        private GUIContent _gcEnablePosition;
        private GUIContent _gcPositionSpace;
        private GUIContent _gcPositionMode;
        private GUIContent _gcPositionInfluence;
        private GUIContent _gcEnableRotation;
        private GUIContent _gcRotationSpace;
        private GUIContent _gcRotationMode;
        private GUIContent _gcRotationInfluence;
        private GUIContent _gcShowGizmos;
        private GUIContent _gcGizmoSize;
        private GUIContent _gcGizmoColorX;
        private GUIContent _gcGizmoColorY;
        private GUIContent _gcGizmoColorZ;

        // Properties chung
        private SerializedProperty constraintObjectProp;
        private SerializedProperty hasExternalPoseDriverProp;
        private SerializedProperty normalizeWeightsProp;
        private SerializedProperty targetsProp;

        // Properties - Position
        private SerializedProperty enablePositionProp;
        private SerializedProperty positionSpaceProp;
        private SerializedProperty positionModeProp;
        private SerializedProperty positionInfluenceProp;
        private SerializedProperty posCopyXProp;
        private SerializedProperty posCopyYProp;
        private SerializedProperty posCopyZProp;

        // Properties - Rotation
        private SerializedProperty enableRotationProp;
        private SerializedProperty rotationSpaceProp;
        private SerializedProperty rotationModeProp;
        private SerializedProperty rotationInfluenceProp;
        private SerializedProperty rotCopyXProp;
        private SerializedProperty rotCopyYProp;
        private SerializedProperty rotCopyZProp;

        // Properties - Gizmo
        private SerializedProperty showGizmosProp;
        private SerializedProperty gizmoSizeProp;
        private SerializedProperty gizmoColorXProp;
        private SerializedProperty gizmoColorYProp;
        private SerializedProperty gizmoColorZProp;
        
        static TransformConstraintEditor()
        {
            EditorApplication.delayCall += ApplyIconToAll;
            EditorApplication.hierarchyChanged -= ApplyIconToAll;
            EditorApplication.hierarchyChanged += ApplyIconToAll;
        }

        private static void ApplyIconToAll()
            => ComponentIconApplier.ApplyToAll<TransformConstraint>("CopyTransform");

        // ══════════════════════════════ API REFERENCE TAB ══════════════════════════════

        /// <summary>Mô tả 1 hàm public API để hiển thị trong tab "API Reference".</summary>
        private struct ApiEntry
        {
            public string category;
            public string signature;
            public string description;

            public ApiEntry(string category, string signature, string description)
            {
                this.category = category;
                this.signature = signature;
                this.description = description;
            }
        }

        // Danh sách toàn bộ API public của TransformConstraint, nhóm theo category.
        // Cập nhật mảng này mỗi khi thêm/sửa/xoá 1 hàm public trong TransformConstraint.cs.
        private static readonly ApiEntry[] apiEntries = new ApiEntry[]
        {
            // ── TARGET ──────────────────────────────────────────────────────────────
            new ApiEntry("Target",
                "bool AddTarget(string id, Transform targetTransform, float initialWeight = 0f)",
                "Thêm Target mới tại runtime và ghi lại pose ban đầu của nó (dùng cho chế độ Delta). Trả về false nếu id đã tồn tại hoặc transform null. initialWeight mặc định 0 để có thể blend lên từ từ."),
            new ApiEntry("Target",
                "bool RemoveTarget(string id)",
                "Xoá Target có id tương ứng (huỷ luôn fade weight đang chạy của Target đó). Trả về false nếu không tìm thấy id."),
            new ApiEntry("Target",
                "bool SetTargetWeight(string id, float weight)",
                "Set ngay lập tức Weight (0-1, tự Clamp01) cho Target có id tương ứng. Trả về false nếu không tìm thấy id."),
            new ApiEntry("Target",
                "bool SetTargetWeight(string id, float weight, float duration)",
                "Đổi Weight (0-1, tự Clamp01) của Target có id tương ứng MƯỢT MÀ trong duration giây (coroutine), không đụng các Target khác. Gọi lại cùng id sẽ thay thế lần fade đang chạy; duration <= 0 → set ngay lập tức. Lưu ý: SwitchTarget đang chạy sẽ bị huỷ. Trả về false nếu không tìm thấy id."),
            new ApiEntry("Target",
                "void SwitchTarget(string targetId, float duration)",
                "Chuyển mượt (lerp weight) sang Target có id chỉ định trong duration giây (coroutine); các Target khác giảm dần về 0. Delta reference của Target đích được recapture ngay lúc switch để tránh giật. duration <= 0 → chuyển ngay lập tức."),
            new ApiEntry("Target",
                "bool RecaptureTargetInitialState(string targetId)",
                "Chụp lại pose gốc (initialLocalPos/Rot) của Target làm điểm xuất phát cho chế độ Delta. Gọi sau khi Target bị reparent, hoặc khi muốn reset delta về pose hiện tại mà không switch. Trả về false nếu không tìm thấy id hoặc Transform bị null."),

            // ── SETTINGS ────────────────────────────────────────────────────────────
            new ApiEntry("Settings",
                "void SetPositionInfluence(float influence)",
                "Set ngay lập tức Position Influence tổng thể (0-1, tự Clamp01) và huỷ fade Position Influence đang chạy."),
            new ApiEntry("Settings",
                "void SetPositionInfluence(float influence, float duration)",
                "Đổi Position Influence MƯỢT MÀ trong duration giây (coroutine). Gọi lại sẽ thay thế lần fade đang chạy; duration <= 0 → set ngay lập tức."),
            new ApiEntry("Settings",
                "void SetRotationInfluence(float influence)",
                "Set ngay lập tức Rotation Influence tổng thể (0-1, tự Clamp01) và huỷ fade Rotation Influence đang chạy."),
            new ApiEntry("Settings",
                "void SetRotationInfluence(float influence, float duration)",
                "Đổi Rotation Influence MƯỢT MÀ trong duration giây (coroutine). Gọi lại sẽ thay thế lần fade đang chạy; duration <= 0 → set ngay lập tức."),

            // ── CORE ────────────────────────────────────────────────────────────────
            new ApiEntry("Core",
                "void CaptureRestPose()",
                "Chụp lại Rest Pose (local position/rotation hiện tại của Constraint Object) làm baseline cố định khi Has External Pose Driver = false. Tự gọi ở OnEnable (sau 1 frame)."),
            new ApiEntry("Core",
                "void CaptureInitialStates()",
                "Ghi lại pose ban đầu (localPosition/localRotation/world rotation) của TẤT CẢ Target, làm điểm xuất phát cho chế độ Delta. Tự gọi ở OnEnable."),

            // ── SCHEDULER (ConstraintBehaviour) ─────────────────────────────────────
            new ApiEntry("Scheduler",
                "void Solve()",
                "Tính và áp constraint cho frame hiện tại. ConstraintScheduler tự gọi mỗi LateUpdate theo thứ tự phụ thuộc — không còn LateUpdate riêng."),
            new ApiEntry("Scheduler",
                "int ExecutionOrder { get; set; }",
                "Thứ tự do bạn chỉ định (nhỏ chạy trước). Chỉ quyết định giữa các constraint không phụ thuộc nhau; đổi lúc runtime sẽ tự sắp xếp lại."),
            new ApiEntry("Scheduler",
                "bool ExternallyDriven { get; set; }",
                "true = scheduler bỏ qua constraint này, script khác tự gọi Solve() đúng thời điểm."),
            new ApiEntry("Scheduler",
                "static void MarkSchedulerDirty()",
                "Báo scheduler sắp xếp lại (VD sau khi sửa trực tiếp list targets/bones bằng code hoặc reparent target). AddTarget/RemoveTarget đã tự gọi."),
        };

        private string apiSearchFilter = "";
        private Vector2 apiScrollPos;
        private System.Collections.Generic.Dictionary<string, bool> apiCategoryFoldout;
        private static GUIStyle _apiSignatureStyle;
        private static GUIStyle _apiDescriptionStyle;

        // Chạy duy nhất một lần khi click vào Object có chứa script này
        private void OnEnable()
        {
            constraintObjectProp = serializedObject.FindProperty("constraintObject");
            hasExternalPoseDriverProp = serializedObject.FindProperty("hasExternalPoseDriver");
            normalizeWeightsProp = serializedObject.FindProperty("normalizeWeights");
            targetsProp = serializedObject.FindProperty("targets");

            enablePositionProp = serializedObject.FindProperty("enablePositionConstraint");
            positionSpaceProp = serializedObject.FindProperty("positionSpace");
            positionModeProp = serializedObject.FindProperty("positionMode");
            positionInfluenceProp = serializedObject.FindProperty("positionInfluence");
            posCopyXProp = serializedObject.FindProperty("posCopyX");
            posCopyYProp = serializedObject.FindProperty("posCopyY");
            posCopyZProp = serializedObject.FindProperty("posCopyZ");

            enableRotationProp = serializedObject.FindProperty("enableRotationConstraint");
            rotationSpaceProp = serializedObject.FindProperty("rotationSpace");
            rotationModeProp = serializedObject.FindProperty("rotationMode");
            rotationInfluenceProp = serializedObject.FindProperty("rotationInfluence");
            rotCopyXProp = serializedObject.FindProperty("rotCopyX");
            rotCopyYProp = serializedObject.FindProperty("rotCopyY");
            rotCopyZProp = serializedObject.FindProperty("rotCopyZ");

            showGizmosProp = serializedObject.FindProperty("showGizmos");
            gizmoSizeProp = serializedObject.FindProperty("gizmoSize");
            gizmoColorXProp = serializedObject.FindProperty("gizmoColorX");
            gizmoColorYProp = serializedObject.FindProperty("gizmoColorY");
            gizmoColorZProp = serializedObject.FindProperty("gizmoColorZ");

            // Khởi tạo GUIContent cache một lần duy nhất thay vì tạo mới mỗi frame repaint
            _gcConstraintObject = new GUIContent("Constraint Object", constraintObjectProp.tooltip);
            _gcHasExternalPoseDriver = new GUIContent("Has External Pose Driver", hasExternalPoseDriverProp.tooltip);
            _gcNormalizeWeights = new GUIContent("Normalize Weights", normalizeWeightsProp.tooltip);
            _gcTargetsList = new GUIContent("Targets List", targetsProp.tooltip);
            _gcEnablePosition = new GUIContent("Enable Position Constraint", enablePositionProp.tooltip);
            _gcPositionSpace = new GUIContent("Position Space", positionSpaceProp.tooltip);
            _gcPositionMode = new GUIContent("Position Mode", positionModeProp.tooltip);
            _gcPositionInfluence = new GUIContent("Position Influence", positionInfluenceProp.tooltip);
            _gcEnableRotation = new GUIContent("Enable Rotation Constraint", enableRotationProp.tooltip);
            _gcRotationSpace = new GUIContent("Rotation Space", rotationSpaceProp.tooltip);
            _gcRotationMode = new GUIContent("Rotation Mode", rotationModeProp.tooltip);
            _gcRotationInfluence = new GUIContent("Rotation Influence", rotationInfluenceProp.tooltip);
            _gcShowGizmos = new GUIContent("Show Gizmos", showGizmosProp.tooltip);
            _gcGizmoSize = new GUIContent("Gizmo Size", gizmoSizeProp.tooltip);
            _gcGizmoColorX = new GUIContent("Axis X Color", gizmoColorXProp.tooltip);
            _gcGizmoColorY = new GUIContent("Axis Y Color", gizmoColorYProp.tooltip);
            _gcGizmoColorZ = new GUIContent("Axis Z Color", gizmoColorZProp.tooltip);

            //SetComponentIcon();
        }

        //private void SetComponentIcon() => ComponentIconApplier.ApplyToObject(target, "CopyTransform");

        // ── Wrapped Toolbar ───────────────────────────────────────────────────────

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
            serializedObject.Update();

            TransformConstraint script = (TransformConstraint)target;

            EditorGUILayout.Space(3);
            ConstraintOrderGUI.Draw(serializedObject, script);
            currentTab = DrawWrappedToolbar(currentTab, tabTitles);
            EditorGUILayout.Space(10);

            switch (currentTab)
            {
                case 0:
                    DrawTargetsTab(script);
                    break;
                case 1:
                    DrawTransformTab();
                    break;
                case 2:
                    DrawGizmoTab();
                    break;
                case 3:
                    DrawApiReferenceTab();
                    break;
            }

            EditorGUILayout.Space(5);
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawTargetsTab(TransformConstraint script)
        {
            // ── CORE OBJECTS ─────────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Core Objects", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(constraintObjectProp, _gcConstraintObject);
            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(hasExternalPoseDriverProp, _gcHasExternalPoseDriver);

            // Chỉ cần thiết khi KHÔNG có driver ngoài (Animator...) — lúc đó baseline dựa vào Rest Pose
            // đã chụp sẵn, người dùng có thể chủ động "khóa" lại pose gốc mới bất cứ lúc nào (tương tự
            // nút Activate của Unity built-in Constraint).
            if (!hasExternalPoseDriverProp.boolValue)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    "Không có driver ngoài điều khiển Constraint Object mỗi frame. Baseline (pose khi Influence = 0) " +
                    "sẽ dùng Rest Pose đã chụp thay vì đọc transform hiện tại, để tránh drift qua từng frame.",
                    MessageType.Info);

                EditorGUI.BeginDisabledGroup(!Application.isPlaying);
                if (GUILayout.Button("Capture Rest Pose Now"))
                {
                    // Multi-edit: áp cho tất cả object đang chọn
                    foreach (var tg in targets)
                    {
                        var tc = tg as TransformConstraint;
                        if (tc != null) tc.CaptureRestPose();
                    }
                }

                EditorGUI.EndDisabledGroup();

                if (!Application.isPlaying)
                {
                    EditorGUILayout.HelpBox("Rest Pose sẽ tự động được chụp lại khi vào Play Mode (OnEnable).",
                        MessageType.None);
                }
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // ── MULTI-TARGET SETTINGS ────────────────────────────────────────────────
            EditorGUILayout.LabelField("Multi-Target Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(normalizeWeightsProp, _gcNormalizeWeights);
            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(targetsProp, _gcTargetsList, true);
            EditorGUILayout.EndVertical();

            // ── RUNTIME DEBUG TOOLS ──────────────────────────────────────────────────
            // Chỉ hiện khi chọn 1 object: các nút Switch dựa trên danh sách targets của riêng từng object
            if (Application.isPlaying && targets.Length == 1)
            {
                EditorGUILayout.Space(15);
                EditorGUILayout.LabelField("Runtime Debug Tools", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                debugSwitchDuration = EditorGUILayout.Slider("Debug Switch Duration", debugSwitchDuration, 0f, 5f);
                EditorGUILayout.Space(5);

                // --- XỬ LÝ CHIA HÀNG CHO CÁC BUTTON: TỐI ĐA 3 BUTTON / HÀNG ---
                int buttonCount = 0;
                bool isHorizontalOpen = false;
                int targetCount = script.targets.Count;

                for (int i = 0; i < targetCount; i++)
                {
                    var t = script.targets[i];
                    if (t == null || string.IsNullOrEmpty(t.id)) continue;

                    if (buttonCount % 3 == 0)
                    {
                        if (isHorizontalOpen) EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                        isHorizontalOpen = true;
                    }

                    // string.Concat tránh alloc của string interpolation ($"") mỗi repaint
                    if (GUILayout.Button(string.Concat("Switch To: ", t.id)))
                    {
                        script.SwitchTarget(t.id, debugSwitchDuration);
                    }

                    buttonCount++;
                }

                if (isHorizontalOpen) EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
        }

        // ── TAB 2: POSITION & ROTATION ───────────────────────────────────────────

        private void DrawTransformTab()
        {
            DrawPositionSection();
            EditorGUILayout.Space(8);
            DrawRotationSection();
        }

        // Chỉ ghi vào property khi người dùng thật sự bấm. Ghi vô điều kiện (prop.boolValue = GUI.Toggle(...))
        // sẽ đè giá trị của object đầu tiên lên TẤT CẢ object khi chúng đang khác nhau (multi-edit).
        private static void DrawCopyToggle(Rect r, SerializedProperty prop, string label, GUIStyle style)
        {
            EditorGUI.BeginChangeCheck();
            bool v = GUI.Toggle(r, prop.boolValue, label, style);
            if (EditorGUI.EndChangeCheck()) prop.boolValue = v;
        }

        private void DrawPositionSection()
        {
            EditorGUILayout.LabelField("Position Constraint", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(enablePositionProp, _gcEnablePosition);

            // Tắt Enable Position Constraint → ẩn toàn bộ thông số bên dưới
            if (enablePositionProp.boolValue)
            {
                EditorGUILayout.Space(5);

                EditorGUILayout.PropertyField(positionSpaceProp, _gcPositionSpace);

                if (positionSpaceProp.enumValueIndex == (int)TransformConstraint.SpaceMode.Local)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(positionModeProp, _gcPositionMode);
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.PropertyField(positionInfluenceProp, _gcPositionInfluence);

                EditorGUILayout.Space(8);

                Rect rowRect = EditorGUILayout.GetControlRect(true, 20);
                float buttonWidth = rowRect.width / 3f;

                Rect xRect = new Rect(rowRect.x, rowRect.y, buttonWidth, rowRect.height);
                Rect yRect = new Rect(xRect.xMax, rowRect.y, buttonWidth, rowRect.height);
                Rect zRect = new Rect(yRect.xMax, rowRect.y, buttonWidth, rowRect.height);

                DrawCopyToggle(xRect, posCopyXProp, "X", EditorStyles.miniButtonLeft);
                DrawCopyToggle(yRect, posCopyYProp, "Y", EditorStyles.miniButtonMid);
                DrawCopyToggle(zRect, posCopyZProp, "Z", EditorStyles.miniButtonRight);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawRotationSection()
        {
            EditorGUILayout.LabelField("Rotation Constraint", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(enableRotationProp, _gcEnableRotation);

            // Tắt Enable Rotation Constraint → ẩn toàn bộ thông số bên dưới
            if (enableRotationProp.boolValue)
            {
                EditorGUILayout.Space(5);

                EditorGUILayout.PropertyField(rotationSpaceProp, _gcRotationSpace);

                if (rotationSpaceProp.enumValueIndex == (int)TransformConstraint.SpaceMode.Local)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(rotationModeProp, _gcRotationMode);
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.PropertyField(rotationInfluenceProp, _gcRotationInfluence);

                EditorGUILayout.Space(8);

                Rect rowRect = EditorGUILayout.GetControlRect(true, 20);
                float buttonWidth = rowRect.width / 3f;

                Rect xRect = new Rect(rowRect.x, rowRect.y, buttonWidth, rowRect.height);
                Rect yRect = new Rect(xRect.xMax, rowRect.y, buttonWidth, rowRect.height);
                Rect zRect = new Rect(yRect.xMax, rowRect.y, buttonWidth, rowRect.height);

                DrawCopyToggle(xRect, rotCopyXProp, "X", EditorStyles.miniButtonLeft);
                DrawCopyToggle(yRect, rotCopyYProp, "Y", EditorStyles.miniButtonMid);
                DrawCopyToggle(zRect, rotCopyZProp, "Z", EditorStyles.miniButtonRight);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawGizmoTab()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(showGizmosProp, _gcShowGizmos);

            // Tắt Show Gizmos → ẩn toàn bộ thông số bên dưới
            if (showGizmosProp.boolValue)
            {
                EditorGUILayout.Space(5);

                EditorGUILayout.PropertyField(gizmoSizeProp, _gcGizmoSize);
                if (gizmoSizeProp.floatValue < 0f) gizmoSizeProp.floatValue = 0f;

                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Axis Colors", EditorStyles.boldLabel);

                EditorGUILayout.PropertyField(gizmoColorXProp, _gcGizmoColorX);
                EditorGUILayout.PropertyField(gizmoColorYProp, _gcGizmoColorY);
                EditorGUILayout.PropertyField(gizmoColorZProp, _gcGizmoColorZ);
            }
            EditorGUILayout.EndVertical();
        }

        // ── TAB 4: API REFERENCE ─────────────────────────────────────────────────

        /// <summary>
        /// Tab liệt kê toàn bộ API public của TransformConstraint (Target, Settings, Core),
        /// có thể tìm kiếm và Copy nhanh chữ ký hàm vào Clipboard.
        /// </summary>
        private void DrawApiReferenceTab()
        {
            if (_apiSignatureStyle == null)
            {
                _apiSignatureStyle = new GUIStyle(EditorStyles.textField)
                {
                    wordWrap = true,
                    richText = true,
                    fontStyle = FontStyle.Bold
                };
            }

            if (_apiDescriptionStyle == null)
            {
                _apiDescriptionStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
            }

            if (apiCategoryFoldout == null)
                apiCategoryFoldout = new System.Collections.Generic.Dictionary<string, bool>();

            // ── SEARCH BOX ───────────────────────────────────────────────────────
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Tìm kiếm", GUILayout.Width(EditorGUIUtility.labelWidth - 4));
            apiSearchFilter = EditorGUILayout.TextField(apiSearchFilter);
            if (GUILayout.Button("×", GUILayout.Width(22)))
            {
                apiSearchFilter = "";
                GUI.FocusControl(null);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            string filter = apiSearchFilter?.Trim().ToLowerInvariant() ?? "";
            int totalMatched = 0;

            apiScrollPos = EditorGUILayout.BeginScrollView(apiScrollPos, GUILayout.ExpandHeight(true));

            string lastCategory = null;
            bool categoryOpen = true;

            for (int i = 0; i < apiEntries.Length; i++)
            {
                ApiEntry entry = apiEntries[i];

                bool matches = string.IsNullOrEmpty(filter)
                               || entry.signature.ToLowerInvariant().Contains(filter)
                               || entry.description.ToLowerInvariant().Contains(filter)
                               || entry.category.ToLowerInvariant().Contains(filter);

                if (!matches) continue;
                totalMatched++;

                // Mở header category mới khi entry chuyển sang nhóm khác
                if (entry.category != lastCategory)
                {
                    lastCategory = entry.category;

                    bool storedOpen;
                    if (!apiCategoryFoldout.TryGetValue(entry.category, out storedOpen))
                    {
                        storedOpen = false; // Mặc định thu gọn (collapsed) khi lần đầu hiển thị
                        apiCategoryFoldout[entry.category] = false;
                    }

                    // Đang gõ tìm kiếm -> luôn hiện mở để thấy ngay kết quả khớp,
                    // không ghi đè lựa chọn đã lưu của người dùng khi hết filter.
                    bool searching = !string.IsNullOrEmpty(filter);
                    categoryOpen = searching ? true : storedOpen;

                    EditorGUILayout.Space(4);
                    bool newState =
                        EditorGUILayout.Foldout(categoryOpen, entry.category, true, EditorStyles.foldoutHeader);
                    if (!searching && newState != storedOpen)
                        apiCategoryFoldout[entry.category] = newState;
                    categoryOpen = newState;
                }

                if (!categoryOpen) continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.SelectableLabel(entry.signature, _apiSignatureStyle, GUILayout.Height(20));
                if (GUILayout.Button("Copy", GUILayout.Width(50), GUILayout.Height(20)))
                {
                    EditorGUIUtility.systemCopyBuffer = entry.signature;
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField(entry.description, _apiDescriptionStyle);

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
            }

            if (totalMatched == 0)
            {
                EditorGUILayout.HelpBox($"Không tìm thấy API nào khớp với \"{apiSearchFilter}\".", MessageType.None);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                $"Tổng cộng {apiEntries.Length} hàm public API. Bấm \"Copy\" để sao chép chữ ký hàm, " +
                "dùng để gọi từ script khác (VD: GetComponent<TransformConstraint>().SwitchTarget(\"m4a1\", 0.3f)).",
                MessageType.Info
            );
        }
    }
}
#endif