using UnityEngine;
using UnityEditor;

namespace GaMo.BlenderEditorTools
{
    [CustomEditor(typeof(IKConstraint))]
    public class IKConstraintEditor : Editor
    {
        private int currentTab = 0;

        private readonly string[] tabTitles =
            { "Core & Targets", "Solver Parameters", "Gizmo Settings", "API Reference" };

        private float debugSwitchDuration = 0.5f;
        private float debugPoleSwitchDuration = 0.5f;

        // Cache icon theo static để chỉ tìm bằng AssetDatabase.FindAssets 1 LẦN DUY NHẤT
        // trong suốt phiên làm việc Editor, thay vì quét lại mỗi khi OnEnable được gọi.
        private static Texture2D cachedIcon;
        private static bool iconLookupDone = false;

        // Properties chính của IKConstraint
        private SerializedProperty previewProp;
        private SerializedProperty bonesProp;
        private SerializedProperty poleTargetsProp;
        private SerializedProperty targetsProp;
        private SerializedProperty ikInfluenceProp;
        private SerializedProperty iterationsProp;
        private SerializedProperty deltaProp;
        private SerializedProperty poleAngleOffsetsProp;

        // Properties cho tab Gizmo Settings
        private SerializedProperty drawGizmosProp;
        private SerializedProperty targetGizmoSizeProp;
        private SerializedProperty targetGizmoHeightProp;
        private SerializedProperty targetGizmoColorProp;
        private SerializedProperty elbowGizmoRadiusProp;
        private SerializedProperty elbowGizmoColorProp;

        // Cache GUIContent để tránh tạo object mới mỗi lần OnInspectorGUI repaint
        private GUIContent _gcBones;
        private GUIContent _gcPoleTargets;
        private GUIContent _gcTargets;
        private GUIContent _gcIKInfluence;
        private GUIContent _gcIterations;
        private GUIContent _gcDelta;
        private GUIContent _gcDrawGizmos;
        private GUIContent _gcTargetGizmoSize;
        private GUIContent _gcTargetGizmoHeight;
        private GUIContent _gcTargetGizmoColor;
        private GUIContent _gcElbowGizmoRadius;
        private GUIContent _gcElbowGizmoColor;

        // Cache sẵn delegate CapFunction 1 LẦN DUY NHẤT tránh allocate mỗi frame OnSceneGUI
        private Handles.CapFunction invisibleCapFunction;

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

        // Danh sách toàn bộ API public của IKConstraint, nhóm theo category.
        // Cập nhật mảng này mỗi khi thêm/sửa/xoá 1 hàm public trong IKConstraint.cs.
        private static readonly ApiEntry[] apiEntries = new ApiEntry[]
        {
            // ── TARGET ──────────────────────────────────────────────────────────────
            new ApiEntry("Target",
                "bool AddTarget(string id, Transform targetTransform, float initialWeight = 0f)",
                "Thêm 1 Target mới vào danh sách targets. Trả về false nếu id đã tồn tại hoặc transform null."),
            new ApiEntry("Target",
                "bool RemoveTarget(string id)",
                "Xoá Target có id tương ứng. Trả về false nếu không tìm thấy id."),
            new ApiEntry("Target",
                "bool SetTargetWeight(string id, float weight)",
                "Set Weight (0-1, tự Clamp01) cho Target có id tương ứng."),
            new ApiEntry("Target",
                "void SwitchTarget(string targetId, float duration = 0.25f)",
                "Chuyển mượt (lerp weight) sang Target có id chỉ định trong duration giây; các Target khác giảm dần về 0. Ở Edit Mode set ngay lập tức."),

            // ── POLE TARGET ─────────────────────────────────────────────────────────
            new ApiEntry("Pole Target",
                "bool AddPoleTarget(string id, Transform poleTransform, float initialWeight = 0f)",
                "Thêm 1 Pole Target mới vào danh sách poleTargets. Trả về false nếu id đã tồn tại hoặc transform null."),
            new ApiEntry("Pole Target",
                "bool RemovePoleTarget(string id)",
                "Xoá Pole Target có id tương ứng. Trả về false nếu không tìm thấy id."),
            new ApiEntry("Pole Target",
                "bool SetPoleTargetWeight(string id, float weight)",
                "Set Weight (0-1, tự Clamp01) cho Pole Target có id tương ứng."),
            new ApiEntry("Pole Target",
                "void SwitchPoleTarget(string poleTargetId, float duration = 0.25f)",
                "Chuyển mượt (lerp weight) sang Pole Target có id chỉ định trong duration giây. Ở Edit Mode set ngay lập tức."),

            // ── PAIRED (TARGET + POLE TARGET CÙNG ID) ──────────────────────────────
            new ApiEntry("Paired",
                "bool AddPair(string id, Transform targetTransform, Transform poleTransform, float initialWeight = 0f)",
                "Thêm đồng thời 1 Target và 1 Pole Target dùng chung id. Trả về false nếu 1 trong 2 add thất bại."),
            new ApiEntry("Paired",
                "bool RemovePair(string id)",
                "Xoá đồng thời cả Target và Pole Target có cùng id."),
            new ApiEntry("Paired",
                "bool SetPairWeight(string id, float weight)",
                "Set Weight đồng thời cho cả Target và Pole Target có cùng id."),
            new ApiEntry("Paired",
                "void SwitchPair(string id, float duration = 0.25f)",
                "Gọi đồng thời SwitchTarget + SwitchPoleTarget cho cùng id — tiện khi Target và Pole Target luôn đi theo cặp (VD tay trái = Target+Elbow trái)."),

            // ── INFLUENCE (BẬT / TẮT TOÀN BỘ IK) ────────────────────────────────────
            new ApiEntry("Influence",
                "void SetIKInfluence(float influence)",
                "Set ngay lập tức ikInfluence (0-1, tự Clamp01). 1 = hoàn toàn theo IK, 0 = trả về Animation gốc."),
            new ApiEntry("Influence",
                "void TurnOn(float duration = 0.25f)",
                "Tăng dần ikInfluence từ giá trị hiện tại lên 1 trong duration giây (coroutine). Truyền 0 để set ngay lập tức."),
            new ApiEntry("Influence",
                "void TurnOff(float duration = 0.25f)",
                "Giảm dần ikInfluence từ giá trị hiện tại xuống 0 trong duration giây (coroutine). Truyền 0 để set ngay lập tức."),

            // ── CORE / LIFECYCLE ─────────────────────────────────────────────────────
            new ApiEntry("Core",
                "void InitIKChain()",
                "Khởi tạo lại và đo đạc thông số chuỗi xương (độ dài từng bone, resize poleAngleOffsets...). Tự gọi ở OnEnable/OnValidate."),
            new ApiEntry("Core",
                "void ApplyIK()",
                "Thực thi toàn bộ logic giải IK trong 1 frame: blend target theo weight, FABRIK, Pole Target, rồi Apply rotation/position lên bones. Tự gọi ở LateUpdate."),
        };

        private string apiSearchFilter = "";
        private Vector2 apiScrollPos;
        private System.Collections.Generic.Dictionary<string, bool> apiCategoryFoldout;
        private static GUIStyle _apiSignatureStyle;
        private static GUIStyle _apiDescriptionStyle;

        private void OnEnable()
        {
            if (target == null) return;

            previewProp = serializedObject.FindProperty("preview");
            bonesProp = serializedObject.FindProperty("bones");
            poleTargetsProp = serializedObject.FindProperty("poleTargets");
            targetsProp = serializedObject.FindProperty("targets");
            ikInfluenceProp = serializedObject.FindProperty("ikInfluence");
            iterationsProp = serializedObject.FindProperty("iterations");
            deltaProp = serializedObject.FindProperty("delta");
            poleAngleOffsetsProp = serializedObject.FindProperty("poleAngleOffsets");

            drawGizmosProp = serializedObject.FindProperty("drawGizmos");
            targetGizmoSizeProp = serializedObject.FindProperty("targetGizmoSize");
            targetGizmoHeightProp = serializedObject.FindProperty("targetGizmoHeight");
            targetGizmoColorProp = serializedObject.FindProperty("targetGizmoColor");
            elbowGizmoRadiusProp = serializedObject.FindProperty("elbowGizmoRadius");
            elbowGizmoColorProp = serializedObject.FindProperty("elbowGizmoColor");

            // Khởi tạo GUIContent cache một lần duy nhất thay vì tạo mới mỗi frame repaint
            _gcBones = new GUIContent("Bones Chain", bonesProp.tooltip);
            _gcPoleTargets = new GUIContent("Pole Targets List", poleTargetsProp.tooltip);
            _gcTargets = new GUIContent("Targets List", targetsProp.tooltip);
            _gcIKInfluence = new GUIContent("IK Influence", ikInfluenceProp.tooltip);
            _gcIterations = new GUIContent("Max Iterations", iterationsProp.tooltip);
            _gcDelta = new GUIContent("Tolerance Delta (m)", deltaProp.tooltip);
            _gcDrawGizmos = new GUIContent("Draw Gizmos", drawGizmosProp.tooltip);
            _gcTargetGizmoSize = new GUIContent("Kích Thước Đáy", targetGizmoSizeProp.tooltip);
            _gcTargetGizmoHeight = new GUIContent("Chiều Dài (Chiều Cao)", targetGizmoHeightProp.tooltip);
            _gcTargetGizmoColor = new GUIContent("Màu Sắc", targetGizmoColorProp.tooltip);
            _gcElbowGizmoRadius = new GUIContent("Bán Kính", elbowGizmoRadiusProp.tooltip);
            _gcElbowGizmoColor = new GUIContent("Màu Sắc", elbowGizmoColorProp.tooltip);

            invisibleCapFunction = InvisibleButtonCap;

            SetComponentIcon();
        }

        /// <summary>
        /// Cho phép CLICK trực tiếp vào Gizmo trong Scene View để chọn GameObject tương ứng.
        /// </summary>
        private void OnSceneGUI()
        {
            if (target == null) return;

            IKConstraint script = (IKConstraint)target;
            if (!script.drawGizmos) return;

            if (invisibleCapFunction == null) invisibleCapFunction = InvisibleButtonCap;

            // Vùng click cho từng Target đang có Weight > 0 (hình chóp vuông)
            if (script.targets != null)
            {
                float targetPickSize = Mathf.Max(script.targetGizmoSize, script.targetGizmoHeight);

                foreach (var t in script.targets)
                {
                    if (t == null || t.target == null || t.weight <= 0f) continue;

                    if (Handles.Button(t.target.position, t.target.rotation, targetPickSize, targetPickSize,
                            invisibleCapFunction))
                    {
                        Selection.activeGameObject = t.target.gameObject;
                    }
                }
            }

            // Vùng click cho từng Pole Target đang có Weight > 0 (hình cầu)
            if (script.poleTargets != null)
            {
                float polePickSize = script.elbowGizmoRadius;
                foreach (var p in script.poleTargets)
                {
                    if (p == null || p.poleTarget == null || p.weight <= 0f) continue;

                    if (Handles.Button(p.poleTarget.position, p.poleTarget.rotation, polePickSize, polePickSize,
                            invisibleCapFunction))
                    {
                        Selection.activeGameObject = p.poleTarget.gameObject;
                    }
                }
            }
        }

        /// <summary>
        /// Cap Function "vô hình" cho Handles.Button: chỉ đăng ký vùng nhận Click (Layout),
        /// KHÔNG vẽ thêm gì ở Repaint vì hình dạng thực tế đã do OnDrawGizmos() vẽ sẵn.
        /// </summary>
        private void InvisibleButtonCap(int controlID, Vector3 position, Quaternion rotation, float size,
            EventType eventType)
        {
            switch (eventType)
            {
                case EventType.Layout:
                    HandleUtility.AddControl(controlID, HandleUtility.DistanceToCircle(position, size));
                    break;
                case EventType.Repaint:
                    // Cố ý để trống — hình dạng gizmo do IKConstraint.OnDrawGizmos() đảm nhiệm
                    break;
            }
        }

        private void SetComponentIcon()
        {
            // Chỉ chạy AssetDatabase.FindAssets đúng 1 lần cho cả phiên Editor.
            if (!iconLookupDone)
            {
                iconLookupDone = true;
                string[] guids = AssetDatabase.FindAssets("IKConstraint t:Texture2D");
                foreach (var guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.Contains("Editor/Icon") || path.Contains("Icon"))
                    {
                        Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                        if (icon != null)
                        {
                            cachedIcon = icon;
                            break;
                        }
                    }
                }
            }

            if (cachedIcon != null)
                EditorGUIUtility.SetIconForObject(target, cachedIcon);
        }

        public override void OnInspectorGUI()
        {
            if (target == null || bonesProp == null) return;

            serializedObject.Update();
            IKConstraint script = (IKConstraint)target;

            EditorGUILayout.Space(3);
            currentTab = GUILayout.Toolbar(currentTab, tabTitles, EditorStyles.toolbarButton, GUILayout.Height(20));
            EditorGUILayout.Space(10);

            switch (currentTab)
            {
                case 0: DrawCoreAndTargetsTab(script); break;
                case 1: DrawSolverParametersTab(); break;
                case 2: DrawGizmoSettingsTab(); break;
                case 3: DrawApiReferenceTab(); break;
            }

            EditorGUILayout.Space(5);
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawCoreAndTargetsTab(IKConstraint script)
        {
            // ── PREVIEW TOGGLE ──────────────────────────────────────────────────────
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Preview In Editor", GUILayout.Width(EditorGUIUtility.labelWidth));
            previewProp.boolValue = GUILayout.Toggle(
                previewProp.boolValue,
                previewProp.boolValue ? "ACTIVE" : "INACTIVE",
                "Button",
                GUILayout.ExpandWidth(true),
                GUILayout.Height(20)
            );
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // ── IK CHAIN SETUP ──────────────────────────────────────────────────────
            EditorGUILayout.LabelField("IK Chain Setup", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(bonesProp, _gcBones, true);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // ── MULTI-TARGET SETUP ──────────────────────────────────────────────────
            EditorGUILayout.LabelField("Multi-Target Setup", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(targetsProp, _gcTargets, true);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // ── MULTI-POLE TARGET SETUP ─────────────────────────────────────────────
            EditorGUILayout.LabelField("Multi-Pole Target Setup", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(poleTargetsProp, _gcPoleTargets, true);
            EditorGUILayout.EndVertical();

            // ── RUNTIME DEBUG TOOLS ──────────────────────────────────────────────────
            if (Application.isPlaying || script.preview)
            {
                EditorGUILayout.Space(15);
                EditorGUILayout.LabelField("Runtime Debug Tools", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // --- DEBUG: SWITCH TARGET ---
                EditorGUILayout.LabelField("Switch Target", EditorStyles.miniBoldLabel);
                debugSwitchDuration = EditorGUILayout.Slider("Duration (s)", debugSwitchDuration, 0f, 5f);
                EditorGUILayout.Space(4);

                DrawSwitchButtons(
                    script.targets.Count,
                    i => script.targets[i]?.id,
                    id =>
                    {
                        script.SwitchTarget(id, debugSwitchDuration);
                        if (!Application.isPlaying) EditorUtility.SetDirty(script);
                    },
                    "Switch To: "
                );

                EditorGUILayout.Space(10);

                // --- DEBUG: SWITCH POLE TARGET ---
                EditorGUILayout.LabelField("Switch Pole Target", EditorStyles.miniBoldLabel);
                debugPoleSwitchDuration = EditorGUILayout.Slider("Duration (s)", debugPoleSwitchDuration, 0f, 5f);
                EditorGUILayout.Space(4);

                DrawSwitchButtons(
                    script.poleTargets.Count,
                    i => script.poleTargets[i]?.id,
                    id =>
                    {
                        script.SwitchPoleTarget(id, debugPoleSwitchDuration);
                        if (!Application.isPlaying) EditorUtility.SetDirty(script);
                    },
                    "Pole → "
                );

                EditorGUILayout.EndVertical();
            }
        }

        /// <summary>
        /// Vẽ các nút Switch theo hàng ngang, tối đa 3 nút mỗi hàng.
        /// </summary>
        private void DrawSwitchButtons(int count, System.Func<int, string> getId, System.Action<string> onButton,
            string prefix)
        {
            int buttonCount = 0;
            bool isHorizontalOpen = false;

            for (int i = 0; i < count; i++)
            {
                string id = getId(i);
                if (string.IsNullOrEmpty(id)) continue;

                if (buttonCount % 3 == 0)
                {
                    if (isHorizontalOpen) EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    isHorizontalOpen = true;
                }

                // string.Concat tránh alloc của string interpolation ($"") mỗi repaint
                if (GUILayout.Button(string.Concat(prefix, id)))
                    onButton(id);

                buttonCount++;
            }

            if (isHorizontalOpen) EditorGUILayout.EndHorizontal();
        }

        private void DrawSolverParametersTab()
        {
            // ── WEIGHT & INFLUENCE ───────────────────────────────────────────────────
            EditorGUILayout.LabelField("Weight & Influence", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(ikInfluenceProp, _gcIKInfluence);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // ── FABRIK OPTIMIZATION ──────────────────────────────────────────────────
            EditorGUILayout.LabelField("FABRIK Optimization", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(iterationsProp, _gcIterations);
            EditorGUILayout.PropertyField(deltaProp, _gcDelta);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // ── POLE ANGLE OFFSET (giống "Pole Angle" của Blender IK) ────────────────
            EditorGUILayout.LabelField("Pole Angle Offset", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            int jointCount = poleAngleOffsetsProp.arraySize;
            if (jointCount == 0)
            {
                EditorGUILayout.HelpBox(
                    "Chưa có khớp giữa nào để chỉnh offset (cần chuỗi Bones từ 3 xương trở lên, ví dụ UpperArm-LowerArm-Hand).",
                    MessageType.None
                );
            }
            else
            {
                for (int i = 0; i < jointCount; i++)
                {
                    SerializedProperty element = poleAngleOffsetsProp.GetArrayElementAtIndex(i);

                    // Nếu chuỗi chỉ có đúng 1 khớp giữa (trường hợp phổ biến nhất: tay/chân 3 xương)
                    // thì đặt tên trực quan "Elbow / Knee" thay vì "Element 0" cho dễ hiểu.
                    string label = jointCount == 1
                        ? "Elbow / Knee Offset"
                        : $"Joint {i + 1} Offset (từ Gốc)";

                    EditorGUILayout.PropertyField(element, new GUIContent(label));
                }
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            EditorGUILayout.HelpBox(
                "Pole Angle Offset: cộng thêm góc (độ) vào hướng bẻ cong tại khớp giữa, tương tự tham số " +
                "'Pole Angle' trong Blender IK. Dùng khi hướng bẻ cong mặc định (tính từ Pole Target) hơi " +
                "lệch so với ý muốn — chỉnh số này thay vì phải di chuyển lại Pole Target.\n\n" +
                "Gợi ý tối ưu hiệu năng:\n" +
                "• Mobile/NPC số lượng lớn: Nên để Iterations từ 5 - 10, Delta từ 0.005 - 0.01.\n" +
                "• PC/Cutscene góc cận cảnh: Nên để Iterations từ 15 - 25, Delta từ 0.001.",
                MessageType.Info
            );
        }

        private void DrawGizmoSettingsTab()
        {
            // ── BẬT / TẮT GIZMO ───────────────────────────────────────────────────────
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(drawGizmosProp, _gcDrawGizmos);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // ── HÌNH CHÓP VUÔNG (TARGET) ──────────────────────────────────────────────
            EditorGUILayout.LabelField("Target - Hình Chóp Vuông", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(targetGizmoSizeProp, _gcTargetGizmoSize);
            EditorGUILayout.PropertyField(targetGizmoHeightProp, _gcTargetGizmoHeight);
            EditorGUILayout.PropertyField(targetGizmoColorProp, _gcTargetGizmoColor);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // ── HÌNH CẦU (POLE TARGET) ────────────────────────────────────────────────
            EditorGUILayout.LabelField("Pole Target - Hình Cầu", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(elbowGizmoRadiusProp, _gcElbowGizmoRadius);
            EditorGUILayout.PropertyField(elbowGizmoColorProp, _gcElbowGizmoColor);
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Tab liệt kê toàn bộ API public của IKConstraint (Target, Pole Target, Paired,
        /// Influence, Core), có thể tìm kiếm và Copy nhanh chữ ký hàm vào Clipboard.
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

            // ── SEARCH BOX ───────────────────────────────────────────────────────────
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
                "dùng để gọi từ script khác (VD: GetComponent<IKConstraint>().SwitchTarget(\"Left_Hand\", 0.3f)).",
                MessageType.Info
            );
        }
    }
}