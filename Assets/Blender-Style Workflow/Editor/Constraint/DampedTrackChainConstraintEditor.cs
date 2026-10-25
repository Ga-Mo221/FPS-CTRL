#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace GaMo.BlenderEditorTools
{
    [CustomEditor(typeof(DampedTrackChainConstraint))]
    [InitializeOnLoad]
    public class DampedTrackChainConstraintEditor : Editor
    {
        private int currentTab = 0;

        private readonly string[] tabTitles =
            { "Chain & Targets", "Track & Limits", "Gizmo Settings", "API Reference" };

        // Khớp thứ tự enum DampedTrackConstraint.TrackAxis { X, Y, Z, NegX, NegY, NegZ }
        private static readonly string[] AxisRow1 = { "X", "Y", "Z" };
        private static readonly string[] AxisRow2 = { "-X", "-Y", "-Z" };

        // Cache GUIContent
        private GUIContent _gcHasExternalPoseDriver, _gcTargetsList, _gcTrackAxis, _gcLimitUpAxis,
            _gcDistribution, _gcInfluence, _gcFollowSpeed, _gcOverflowMode, _gcLimitMargin,
            _gcReturnDuration, _gcUseHold, _gcHoldDuration, _gcShowGizmos, _gcGizmoChainColor,
            _gcGizmoTargetColor, _gcNormalizeWeights, _gcCheckObstacles, _gcObstacleMask,
            _gcIgnoreOwnHierarchy, _gcOcclusionBlendTime, _gcUseOcclusionHold, _gcOcclusionHoldDuration,
            _gcAutoTracking, _gcAutoSwitchBlendTime, _gcAutoSwitchMargin, _gcAutoMinSwitchInterval,
            _gcGizmoBlockedColor, _gcGizmoSelectedColor;

        private static readonly GUIContent _gcBoneWeight = new GUIContent("Weight",
            "Độ ảnh hưởng của xương này (chỉ dùng khi Distribution = Weighted). 0 = không tham gia.");
        private static readonly GUIContent _gcYawL = new GUIContent("Yaw L", "Góc tối đa xương NÀY đóng góp khi quay sang TRÁI (độ).");
        private static readonly GUIContent _gcYawR = new GUIContent("Yaw R", "Góc tối đa xương NÀY đóng góp khi quay sang PHẢI (độ).");
        private static readonly GUIContent _gcPitchU = new GUIContent("Pitch ↑", "Góc tối đa xương NÀY đóng góp khi ngẩng LÊN (độ).");
        private static readonly GUIContent _gcPitchD = new GUIContent("Pitch ↓", "Góc tối đa xương NÀY đóng góp khi cúi XUỐNG (độ).");

        // Properties
        private SerializedProperty hasExternalPoseDriverProp, targetsProp, bonesProp;
        private SerializedProperty trackAxisProp, limitUpAxisProp, distributionProp, influenceProp,
            followSpeedProp, overflowModeProp, limitMarginProp, returnDurationProp, useHoldProp, holdDurationProp;
        private SerializedProperty showGizmosProp, gizmoChainColorProp, gizmoTargetColorProp,
            gizmoBlockedColorProp, gizmoSelectedColorProp;

        // Multi-Target / Auto Tracking / Obstacle Check
        private SerializedProperty normalizeWeightsProp;
        private SerializedProperty autoTrackingProp, autoSwitchBlendTimeProp, autoSwitchMarginProp, autoMinSwitchIntervalProp;
        private SerializedProperty checkObstaclesProp, obstacleMaskProp, ignoreOwnHierarchyProp,
            occlusionBlendTimeProp, useOcclusionHoldProp, occlusionHoldDurationProp, obstacleCheckIntervalProp;
        private GUIContent _gcObstacleCheckInterval;

        // Bone Chain: mặc định thu gọn
        private bool _boneChainFoldout = false;
        private static GUIStyle _boneChainFoldoutStyle;

        // Công cụ "Fill Chain"
        private Transform _fillHead;
        private Transform _fillLowest;

        // ══════════════════════════════ API REFERENCE TAB ══════════════════════════════

        private struct ApiEntry
        {
            public string category;
            public string signature;
            public string description;
            public string searchText; // chữ thường, tính 1 lần → không ToLowerInvariant mỗi lần repaint

            public ApiEntry(string category, string signature, string description)
            {
                this.category = category;
                this.signature = signature;
                this.description = description;
                searchText = (category + "\n" + signature + "\n" + description).ToLowerInvariant();
            }
        }

        // Cập nhật mảng này mỗi khi thêm/sửa/xoá 1 hàm public trong DampedTrackChainConstraint.cs.
        private static readonly ApiEntry[] apiEntries = new ApiEntry[]
        {
            // ── TARGET ──
            new ApiEntry("Target",
                "bool AddTarget(string id, Transform targetTransform, float initialWeight = 0f)",
                "Thêm 1 Target mới vào danh sách targets. Trả về false nếu id đã tồn tại hoặc transform null."),
            new ApiEntry("Target",
                "bool SetTargetWeight(string id, float weight)",
                "Set Weight (0-1, tự Clamp01) cho Target có id tương ứng. Hướng nhìn = trung bình có trọng số của các Target. Trả về false nếu không tìm thấy id."),

            // ── AUTO TRACKING ──
            new ApiEntry("Auto Tracking",
                "void SetAutoTracking(bool enabled)",
                "Bật/tắt Auto Tracking tại runtime. Chỉ thực sự chạy khi có từ 2 Target hợp lệ (Transform khác null và Weight > 0): chọn Target GẦN NHẤT mà cả chuỗi với tới được và không bị che (nếu bật Check Obstacles)."),
            new ApiEntry("Auto Tracking",
                "bool IsAutoTrackingActive { get; }",
                "true khi Auto Tracking đang thực sự chạy (đã bật và có từ 2 Target hợp lệ)."),
            new ApiEntry("Auto Tracking",
                "string AutoTargetId { get; }",
                "ID của Target đang được Auto Tracking chọn. null nếu Auto Tracking không chạy."),

            // ── SETTINGS ──
            new ApiEntry("Settings",
                "void SetInfluence(float value)",
                "Set ngay lập tức Influence tổng thể (0-1, tự Clamp01) cho cả chuỗi. 1 = bám Target, 0 = trả về Animation/Rest Pose gốc."),
            new ApiEntry("Settings",
                "void SetInfluence(float value, float duration)",
                "Đổi Influence tổng thể MƯỢT MÀ trong duration giây (coroutine). Gọi lại sẽ thay thế lần fade đang chạy; duration <= 0 → set ngay."),

            // ── CHAIN ──
            new ApiEntry("Chain",
                "void RebuildChain()",
                "Sắp xếp lại list bones theo hierarchy (gốc → đầu) và kiểm tra hợp lệ. Gọi lại nếu đổi list bones lúc runtime."),
            new ApiEntry("Chain",
                "IReadOnlyList<Transform> SortedBones { get; }",
                "Các xương theo thứ tự đã tự sắp xếp: [0] = dưới cùng, [cuối] = đầu (xương aim)."),
            new ApiEntry("Chain",
                "bool IsChainValid { get; }",
                "true khi list bones hợp lệ: có xương, cùng 1 nhánh cha-con, không trùng nhau."),
            new ApiEntry("Chain",
                "bool IsOutOfRange { get; }",
                "true khi Target nằm ngoài TỔNG giới hạn của cả chuỗi (đầu + cổ + ngực... đều đã chạm trần)."),

            // ── CORE ──
            new ApiEntry("Core",
                "void CaptureRestPose()",
                "Chụp lại Rest Pose (local rotation hiện tại của MỌI xương trong chuỗi) làm baseline cố định khi Has External Pose Driver = false. Tự gọi ở OnEnable (sau 1 frame)."),

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
        private Dictionary<string, bool> apiCategoryFoldout;
        private static GUIStyle _apiSignatureStyle;
        private static GUIStyle _apiDescriptionStyle;

        static DampedTrackChainConstraintEditor()
        {
            EditorApplication.delayCall += ApplyIconToAll;
            EditorApplication.hierarchyChanged -= ScheduleApplyIcon;
            EditorApplication.hierarchyChanged += ScheduleApplyIcon;
        }

        // hierarchyChanged bắn cực thường xuyên → gộp thành 1 lần/lượt thay vì quét toàn scene mỗi sự kiện.
        private static bool _iconPending;
        private static void ScheduleApplyIcon()
        {
            if (_iconPending) return;
            _iconPending = true;
            EditorApplication.delayCall += () => { _iconPending = false; ApplyIconToAll(); };
        }

        private static void ApplyIconToAll()
            => ComponentIconApplier.ApplyToAll<DampedTrackChainConstraint>("DampeTrackChain");

        private void OnEnable()
        {
            hasExternalPoseDriverProp = serializedObject.FindProperty("hasExternalPoseDriver");
            targetsProp = serializedObject.FindProperty("targets");
            bonesProp = serializedObject.FindProperty("bones");

            trackAxisProp = serializedObject.FindProperty("trackAxis");
            limitUpAxisProp = serializedObject.FindProperty("limitUpAxis");
            distributionProp = serializedObject.FindProperty("distribution");
            influenceProp = serializedObject.FindProperty("influence");
            followSpeedProp = serializedObject.FindProperty("followSpeed");
            overflowModeProp = serializedObject.FindProperty("overflowMode");
            limitMarginProp = serializedObject.FindProperty("limitMargin");
            returnDurationProp = serializedObject.FindProperty("returnDuration");
            useHoldProp = serializedObject.FindProperty("useHold");
            holdDurationProp = serializedObject.FindProperty("holdDuration");

            showGizmosProp = serializedObject.FindProperty("showGizmos");
            gizmoChainColorProp = serializedObject.FindProperty("gizmoChainColor");
            gizmoTargetColorProp = serializedObject.FindProperty("gizmoTargetColor");

            gizmoBlockedColorProp = serializedObject.FindProperty("gizmoBlockedColor");
            gizmoSelectedColorProp = serializedObject.FindProperty("gizmoSelectedColor");

            normalizeWeightsProp = serializedObject.FindProperty("normalizeWeights");
            autoTrackingProp = serializedObject.FindProperty("autoTracking");
            autoSwitchBlendTimeProp = serializedObject.FindProperty("autoSwitchBlendTime");
            autoSwitchMarginProp = serializedObject.FindProperty("autoSwitchMargin");
            autoMinSwitchIntervalProp = serializedObject.FindProperty("autoMinSwitchInterval");
            checkObstaclesProp = serializedObject.FindProperty("checkObstacles");
            obstacleMaskProp = serializedObject.FindProperty("obstacleMask");
            ignoreOwnHierarchyProp = serializedObject.FindProperty("ignoreOwnHierarchy");
            occlusionBlendTimeProp = serializedObject.FindProperty("occlusionBlendTime");
            useOcclusionHoldProp = serializedObject.FindProperty("useOcclusionHold");
            occlusionHoldDurationProp = serializedObject.FindProperty("occlusionHoldDuration");
            obstacleCheckIntervalProp = serializedObject.FindProperty("obstacleCheckInterval");

            _gcHasExternalPoseDriver = new GUIContent("Has External Pose Driver", hasExternalPoseDriverProp.tooltip);
            _gcTargetsList = new GUIContent("Targets List", targetsProp.tooltip);
            _gcTrackAxis = new GUIContent("Track Axis (Head)", trackAxisProp.tooltip);
            _gcLimitUpAxis = new GUIContent("Limit Up Axis (Head)", limitUpAxisProp.tooltip);
            _gcDistribution = new GUIContent("Distribution", distributionProp.tooltip);
            _gcInfluence = new GUIContent("Influence", influenceProp.tooltip);
            _gcFollowSpeed = new GUIContent("Follow Speed", followSpeedProp.tooltip);
            _gcOverflowMode = new GUIContent("Overflow Mode", overflowModeProp.tooltip);
            _gcLimitMargin = new GUIContent("Limit Margin", limitMarginProp.tooltip);
            _gcReturnDuration = new GUIContent("Return Duration", returnDurationProp.tooltip);
            _gcUseHold = new GUIContent("Hold Before Return", useHoldProp.tooltip);
            _gcHoldDuration = new GUIContent("Hold Duration", holdDurationProp.tooltip);
            _gcShowGizmos = new GUIContent("Show Gizmos", showGizmosProp.tooltip);
            _gcGizmoChainColor = new GUIContent("Chain Color", gizmoChainColorProp.tooltip);
            _gcGizmoTargetColor = new GUIContent("Line To Target Color", gizmoTargetColorProp.tooltip);
            _gcGizmoBlockedColor = new GUIContent("Blocked Target Color", gizmoBlockedColorProp.tooltip);
            _gcGizmoSelectedColor = new GUIContent("Auto Tracking Selected Color", gizmoSelectedColorProp.tooltip);

            _gcNormalizeWeights = new GUIContent("Normalize Weights", normalizeWeightsProp.tooltip);
            _gcAutoTracking = new GUIContent("Auto Tracking", autoTrackingProp.tooltip);
            _gcAutoSwitchBlendTime = new GUIContent("Switch Blend Time", autoSwitchBlendTimeProp.tooltip);
            _gcAutoSwitchMargin = new GUIContent("Switch Margin", autoSwitchMarginProp.tooltip);
            _gcAutoMinSwitchInterval = new GUIContent("Min Switch Interval", autoMinSwitchIntervalProp.tooltip);
            _gcCheckObstacles = new GUIContent("Check Obstacles", checkObstaclesProp.tooltip);
            _gcObstacleMask = new GUIContent("Obstacle Mask", obstacleMaskProp.tooltip);
            _gcIgnoreOwnHierarchy = new GUIContent("Ignore Own Hierarchy", ignoreOwnHierarchyProp.tooltip);
            _gcOcclusionBlendTime = new GUIContent("Occlusion Blend Time", occlusionBlendTimeProp.tooltip);
            _gcUseOcclusionHold = new GUIContent("Hold When Occluded", useOcclusionHoldProp.tooltip);
            _gcOcclusionHoldDuration = new GUIContent("Occlusion Hold Duration", occlusionHoldDurationProp.tooltip);
            _gcObstacleCheckInterval = new GUIContent("Raycast Interval", obstacleCheckIntervalProp.tooltip);
        }

        // Cập nhật liên tục khi đang Play: Out Of Range (tab Track & Limits) và Auto Tracking (tab Chain & Targets)
        public override bool RequiresConstantRepaint()
        {
            if (!Application.isPlaying) return false;
            if (currentTab == 1) return true;
            var s = target as DampedTrackChainConstraint;
            return currentTab == 0 && s != null && s.autoTracking;
        }

        // ── Wrapped Toolbar ───────────────────────────────────────────────────────

        private int DrawWrappedToolbar(int selected, string[] tabs)
        {
            float viewWidth = EditorGUIUtility.currentViewWidth - 20f;
            float tabMinWidth = 100f;
            float tabPadding = 4f;

            int tabsPerRow = Mathf.Max(1, Mathf.FloorToInt((viewWidth + tabPadding) / (tabMinWidth + tabPadding)));
            int tabCount = tabs.Length;
            int rows = Mathf.CeilToInt((float)tabCount / tabsPerRow);

            for (int row = 0; row < rows; row++)
            {
                int startIdx = row * tabsPerRow;
                int endIdx = Mathf.Min(startIdx + tabsPerRow, tabCount);
                int count = endIdx - startIdx;

                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int i = startIdx; i < endIdx; i++)
                    {
                        float tabWidth = (viewWidth - (count - 1) * tabPadding) / count;
                        bool isSelected = (selected == i);
                        bool clicked = GUILayout.Toggle(isSelected, tabs[i], EditorStyles.toolbarButton,
                            GUILayout.Width(tabWidth), GUILayout.Height(22f));

                        if (clicked && !isSelected) selected = i;
                        if (i < endIdx - 1) GUILayout.Space(tabPadding);
                    }
                }
            }

            return selected;
        }

        // ── Axis Buttons (2 hàng x 3 nút, chọn 1 trong 6) ────────────────────────

        private void DrawAxisButtons(SerializedProperty prop, GUIContent label, int blockedAxis)
        {
            EditorGUILayout.LabelField(label);
            int current = prop.hasMultipleDifferentValues ? -1 : prop.enumValueIndex;

            int picked = DrawAxisRow(AxisRow1, 0, current, blockedAxis);
            if (picked < 0) picked = DrawAxisRow(AxisRow2, 3, current, blockedAxis);
            else DrawAxisRow(AxisRow2, 3, current, blockedAxis);

            if (picked >= 0 && picked != current)
                prop.enumValueIndex = picked;
        }

        private int DrawAxisRow(string[] labels, int baseIndex, int current, int blockedAxis)
        {
            Rect rowRect = EditorGUILayout.GetControlRect(true, 20);
            float buttonWidth = rowRect.width / 3f;
            int picked = -1;

            for (int i = 0; i < 3; i++)
            {
                int enumIndex = baseIndex + i;
                Rect r = new Rect(rowRect.x + buttonWidth * i, rowRect.y, buttonWidth, rowRect.height);
                GUIStyle style = i == 0 ? EditorStyles.miniButtonLeft
                               : i == 2 ? EditorStyles.miniButtonRight
                               : EditorStyles.miniButtonMid;

                bool blocked = blockedAxis >= 0 && (enumIndex % 3) == blockedAxis;

                EditorGUI.BeginDisabledGroup(blocked);
                bool isOn = GUI.Toggle(r, current == enumIndex, labels[i], style);
                EditorGUI.EndDisabledGroup();

                if (isOn && current != enumIndex) picked = enumIndex;
            }

            return picked;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DampedTrackChainConstraint script = (DampedTrackChainConstraint)target;

            EditorGUILayout.Space(3);
            ConstraintOrderGUI.Draw(serializedObject, script);
            currentTab = DrawWrappedToolbar(currentTab, tabTitles);
            EditorGUILayout.Space(10);

            switch (currentTab)
            {
                case 0: DrawChainTab(script); break;
                case 1: DrawTrackTab(script); break;
                case 2: DrawGizmoTab(); break;
                case 3: DrawApiReferenceTab(); break;
            }

            EditorGUILayout.Space(5);
            serializedObject.ApplyModifiedProperties();
        }

        // ── TAB 1: CHAIN & TARGETS ───────────────────────────────────────────────

        private void DrawChainTab(DampedTrackChainConstraint script)
        {
            // Edit mode không chạy OnEnable → đảm bảo chuỗi đã được sắp xếp để hiển thị đúng
            if (!Application.isPlaying && bonesProp.arraySize > 0 && script.SortedBones.Count == 0)
                script.RebuildChain();

            // ── CORE ─────────────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Core", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(hasExternalPoseDriverProp, _gcHasExternalPoseDriver);

            if (!hasExternalPoseDriverProp.boolValue)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    "Không có driver ngoài điều khiển các xương mỗi frame. Baseline (pose khi Influence = 0) " +
                    "sẽ dùng Rest Pose đã chụp thay vì đọc transform hiện tại, để tránh drift qua từng frame.",
                    MessageType.Info);

                EditorGUI.BeginDisabledGroup(!Application.isPlaying);
                if (GUILayout.Button("Capture Rest Pose Now"))
                    script.CaptureRestPose();
                EditorGUI.EndDisabledGroup();

                if (!Application.isPlaying)
                    EditorGUILayout.HelpBox("Rest Pose sẽ tự động được chụp lại khi vào Play Mode (OnEnable).",
                        MessageType.None);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // ── BONE CHAIN ───────────────────────────────────────────────────────
            if (_boneChainFoldoutStyle == null)
            {
                _boneChainFoldoutStyle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
            }

            _boneChainFoldout = EditorGUILayout.Foldout(_boneChainFoldout,
                string.Concat("Bone Chain (", bonesProp.arraySize.ToString(), ")"), true, _boneChainFoldoutStyle);

            if (_boneChainFoldout)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                DrawFillChainTool(script);
                EditorGUILayout.Space(6);
                DrawChainStatus(script);
                EditorGUILayout.Space(4);
                DrawBonesList(script);
                EditorGUILayout.EndVertical();
            }
            else if (bonesProp.arraySize == 0 || !script.IsChainValid)
            {
                // Thu gọn vẫn phải báo lỗi để không bỏ sót chuỗi chưa có / không hợp lệ
                EditorGUILayout.HelpBox(
                    bonesProp.arraySize == 0
                        ? "Chưa có xương nào. Mở Bone Chain để dùng Quick Fill."
                        : "Chuỗi KHÔNG hợp lệ. Mở Bone Chain để xem chi tiết.",
                    bonesProp.arraySize == 0 ? MessageType.Warning : MessageType.Error);
            }

            EditorGUILayout.Space(6);

            // ── TARGETS ──────────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Targets", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(normalizeWeightsProp, _gcNormalizeWeights);
            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(targetsProp, _gcTargetsList, true);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);
            DrawAutoTrackingBox(script);
        }

        private static int CountEligibleTargets(DampedTrackChainConstraint script)
        {
            int n = 0;
            var list = script.targets;
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                if (t != null && t.target != null && t.weight > 0f) n++;
            }
            return n;
        }

        private void DrawAutoTrackingBox(DampedTrackChainConstraint script)
        {
            EditorGUILayout.LabelField("Auto Tracking", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(autoTrackingProp, _gcAutoTracking);

            if (autoTrackingProp.hasMultipleDifferentValues)
            {
                EditorGUILayout.HelpBox("Các object đang chọn có giá trị Auto Tracking khác nhau.", MessageType.None);
            }
            else if (autoTrackingProp.boolValue) // Tắt → ẩn toàn bộ thông số bên dưới
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.PropertyField(autoSwitchBlendTimeProp, _gcAutoSwitchBlendTime);
                EditorGUILayout.PropertyField(autoSwitchMarginProp, _gcAutoSwitchMargin);
                EditorGUILayout.PropertyField(autoMinSwitchIntervalProp, _gcAutoMinSwitchInterval);

                // Trạng thái chỉ hiện khi chọn 1 object (mỗi object có danh sách target riêng)
                if (targets.Length == 1)
                {
                    EditorGUILayout.Space(4);
                    int eligible = CountEligibleTargets(script);

                    if (eligible < 2)
                    {
                        EditorGUILayout.HelpBox(
                            string.Concat("Cần ít nhất 2 Target hợp lệ (có Transform và Weight > 0), hiện có ",
                                eligible.ToString(), ". Auto Tracking tạm không hoạt động — chuỗi chạy theo Weight như bình thường."),
                            MessageType.Warning);
                    }
                    else if (Application.isPlaying)
                    {
                        string id = script.AutoTargetId;
                        EditorGUILayout.HelpBox(
                            script.IsAutoTrackingActive
                                ? string.Concat("Đang chạy — Target hiện tại: ", string.IsNullOrEmpty(id) ? "(không có ID)" : id)
                                : "Chưa chạy.",
                            MessageType.Info);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox(
                            "Ưu tiên Target gần nhất mà cả chuỗi với tới được (trong tổng giới hạn) " +
                            "và không bị che (nếu bật Check Obstacles ở tab Track & Limits).",
                            MessageType.Info);
                    }

                    // Auto Tracking phụ thuộc Check Obstacles ở tab khác → hiện trạng thái để khỏi phải chuyển tab
                    EditorGUILayout.LabelField(
                        string.Concat("Check Obstacles: ", script.checkObstacles ? "Bật" : "Tắt"),
                        EditorStyles.miniLabel);
                }
            }
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Chọn Head + xương dưới cùng → tự điền toàn bộ xương nằm giữa (đi ngược cha từ Head), kèm giới hạn
        /// và Weight gợi ý giảm dần từ đầu xuống.
        /// </summary>
        private void DrawFillChainTool(DampedTrackChainConstraint script)
        {
            EditorGUILayout.LabelField("Quick Fill", EditorStyles.miniBoldLabel);

            if (_fillHead == null)
            {
                var sorted = script.SortedBones;
                if (sorted.Count > 0) _fillHead = sorted[sorted.Count - 1];
            }

            _fillHead = (Transform)EditorGUILayout.ObjectField(
                new GUIContent("Head Bone", "Xương đầu (xương aim)."), _fillHead, typeof(Transform), true);
            _fillLowest = (Transform)EditorGUILayout.ObjectField(
                new GUIContent("Lowest Bone", "Xương dưới cùng của chuỗi (VD: Hips/Spine). Phải là tổ tiên của Head Bone."),
                _fillLowest, typeof(Transform), true);

            EditorGUI.BeginDisabledGroup(_fillHead == null || _fillLowest == null);
            if (GUILayout.Button("Fill Chain (Head → Lowest)"))
                FillChain(script, _fillHead, _fillLowest);
            EditorGUI.EndDisabledGroup();
        }

        private void FillChain(DampedTrackChainConstraint script, Transform head, Transform lowest)
        {
            var list = new List<Transform>();
            Transform t = head;
            while (t != null)
            {
                list.Add(t);
                if (t == lowest) break;
                t = t.parent;
            }

            if (list[list.Count - 1] != lowest)
            {
                EditorUtility.DisplayDialog("Fill Chain",
                    $"'{lowest.name}' không phải tổ tiên của '{head.name}'.", "OK");
                return;
            }

            if (bonesProp.arraySize > 0 &&
                !EditorUtility.DisplayDialog("Fill Chain",
                    "Thay toàn bộ list bones hiện tại (kể cả giới hạn/weight đã chỉnh)?", "Thay", "Huỷ"))
                return;

            int n = list.Count;
            bonesProp.arraySize = n;
            for (int i = 0; i < n; i++) // i = 0 là đầu
            {
                float k = n > 1 ? (float)i / (n - 1) : 0f; // 0 = đầu, 1 = xương dưới cùng
                SerializedProperty el = bonesProp.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("bone").objectReferenceValue = list[i];
                el.FindPropertyRelative("weight").floatValue = Mathf.Lerp(1f, 0.1f, k);
                el.FindPropertyRelative("maxYawLeft").floatValue = Mathf.Lerp(70f, 15f, k);
                el.FindPropertyRelative("maxYawRight").floatValue = Mathf.Lerp(70f, 15f, k);
                el.FindPropertyRelative("maxPitchUp").floatValue = Mathf.Lerp(40f, 10f, k);
                el.FindPropertyRelative("maxPitchDown").floatValue = Mathf.Lerp(30f, 10f, k);
            }

            serializedObject.ApplyModifiedProperties();
            script.RebuildChain();
            serializedObject.Update();
        }

        private void DrawChainStatus(DampedTrackChainConstraint script)
        {
            var sorted = script.SortedBones;

            if (bonesProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox("Chưa có xương nào. Dùng Quick Fill hoặc bấm \"+ Add Bone\".", MessageType.Warning);
                return;
            }

            if (!script.IsChainValid)
            {
                EditorGUILayout.HelpBox(
                    "Chuỗi KHÔNG hợp lệ: các xương phải nằm trên cùng 1 nhánh cha-con và không trùng nhau " +
                    "(có thể bỏ qua xương trung gian). Tool đang không chạy.", MessageType.Error);
                return;
            }

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < sorted.Count; i++)
            {
                if (i > 0) sb.Append("  →  ");
                sb.Append(sorted[i].name);
            }

            EditorGUILayout.HelpBox(
                "Thứ tự tự động (dưới cùng → đầu):\n" + sb, MessageType.None);
        }

        private void DrawBonesList(DampedTrackChainConstraint script)
        {
            var sorted = script.SortedBones;
            bool weighted = distributionProp.enumValueIndex ==
                            (int)DampedTrackChainConstraint.DistributionMode.Weighted;

            int removeIndex = -1;
            for (int i = 0; i < bonesProp.arraySize; i++)
            {
                SerializedProperty el = bonesProp.GetArrayElementAtIndex(i);
                if (DrawBoneElement(el, sorted, weighted)) removeIndex = i;
            }

            if (removeIndex >= 0)
                bonesProp.DeleteArrayElementAtIndex(removeIndex);

            if (GUILayout.Button("+ Add Bone"))
                bonesProp.arraySize++;
        }

        /// <summary>Vẽ 1 xương trong list. Trả về true nếu người dùng bấm xoá.</summary>
        private bool DrawBoneElement(SerializedProperty el, IReadOnlyList<Transform> sorted, bool weighted)
        {
            SerializedProperty boneP = el.FindPropertyRelative("bone");
            Transform b = boneP.objectReferenceValue as Transform;

            string role = "—";
            if (b != null)
            {
                int rank = -1;
                for (int i = 0; i < sorted.Count; i++)
                    if (sorted[i] == b) { rank = i; break; }

                if (rank >= 0)
                    role = rank == sorted.Count - 1 && sorted.Count > 1 ? "Head (aim)"
                         : rank == 0 && sorted.Count > 1 ? "Lowest"
                         : sorted.Count == 1 ? "Head (aim)"
                         : "Mid #" + rank;
            }

            bool remove = false;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(role, EditorStyles.miniBoldLabel, GUILayout.Width(70));
            EditorGUILayout.PropertyField(boneP, GUIContent.none);
            if (GUILayout.Button("×", GUILayout.Width(22))) remove = true;
            EditorGUILayout.EndHorizontal();

            if (weighted)
                EditorGUILayout.PropertyField(el.FindPropertyRelative("weight"), _gcBoneWeight);

            DrawLimitPair(el.FindPropertyRelative("maxYawLeft"), _gcYawL,
                          el.FindPropertyRelative("maxYawRight"), _gcYawR);
            DrawLimitPair(el.FindPropertyRelative("maxPitchUp"), _gcPitchU,
                          el.FindPropertyRelative("maxPitchDown"), _gcPitchD);

            EditorGUILayout.EndVertical();
            return remove;
        }

        private static void DrawLimitPair(SerializedProperty a, GUIContent la, SerializedProperty b, GUIContent lb)
        {
            float old = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 52f;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(a, la);
            EditorGUILayout.PropertyField(b, lb);
            EditorGUILayout.EndHorizontal();
            EditorGUIUtility.labelWidth = old;
        }

        // ── TAB 2: TRACK & LIMITS ────────────────────────────────────────────────

        private void DrawTrackTab(DampedTrackChainConstraint script)
        {
            // ── TRACK ────────────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Track Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            DrawAxisButtons(trackAxisProp, _gcTrackAxis, -1);
            EditorGUILayout.Space(4);

            int trackIdx = trackAxisProp.hasMultipleDifferentValues ? -1 : trackAxisProp.enumValueIndex;
            int blocked = trackIdx >= 0 ? trackIdx % 3 : -1;
            DrawAxisButtons(limitUpAxisProp, _gcLimitUpAxis, blocked);

            if (!limitUpAxisProp.hasMultipleDifferentValues && !trackAxisProp.hasMultipleDifferentValues &&
                limitUpAxisProp.enumValueIndex % 3 == trackAxisProp.enumValueIndex % 3)
            {
                EditorGUILayout.HelpBox("Limit Up Axis đang trùng Track Axis → giới hạn góc không xác định.",
                    MessageType.Warning);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.PropertyField(influenceProp, _gcInfluence);
            EditorGUILayout.PropertyField(followSpeedProp, _gcFollowSpeed);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // ── DISTRIBUTION ─────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Distribution", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(distributionProp, _gcDistribution);

            bool weighted = distributionProp.enumValueIndex ==
                            (int)DampedTrackChainConstraint.DistributionMode.Weighted;
            EditorGUILayout.HelpBox(
                weighted
                    ? "Chia góc cho mọi xương theo Weight (chỉnh ở tab Chain & Targets). Xương chạm giới hạn thì " +
                      "phần dư chia tiếp cho các xương còn lại."
                    : "Đầu nhận hết tới giới hạn của đầu; phần dư mới chuyển xuống cổ, ngực... tới xương dưới cùng. " +
                      "Weight bị bỏ qua.",
                MessageType.None);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // ── OVERFLOW / LIMITS ────────────────────────────────────────────────
            EditorGUILayout.LabelField("Overflow & Return", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(overflowModeProp, _gcOverflowMode);
            EditorGUILayout.PropertyField(limitMarginProp, _gcLimitMargin);

            bool release = overflowModeProp.enumValueIndex ==
                           (int)DampedTrackChainConstraint.OverflowMode.ReleaseToBaseline;
            if (release)
            {
                EditorGUILayout.PropertyField(returnDurationProp, _gcReturnDuration);
                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(useHoldProp, _gcUseHold);
                if (useHoldProp.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(holdDurationProp, _gcHoldDuration);
                    EditorGUI.indentLevel--;
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Clamp: mỗi xương dừng ở giới hạn của mình, đầu nhìn tới hướng xa nhất có thể.", MessageType.None);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // ── OBSTACLE CHECK ───────────────────────────────────────────────────
            DrawObstacleBox();

            // ── RUNTIME STATUS ───────────────────────────────────────────────────
            if (Application.isPlaying)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Runtime Status", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    script.IsOutOfRange
                        ? "Target đang NGOÀI tổng giới hạn của cả chuỗi."
                        : "Target nằm trong tổng giới hạn của chuỗi.",
                    script.IsOutOfRange ? MessageType.Warning : MessageType.Info);
            }
        }

        private void DrawObstacleBox()
        {
            EditorGUILayout.LabelField("Obstacle Check", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(checkObstaclesProp, _gcCheckObstacles);

            if (checkObstaclesProp.hasMultipleDifferentValues)
            {
                EditorGUILayout.HelpBox("Các object đang chọn có giá trị Check Obstacles khác nhau.", MessageType.None);
            }
            else if (checkObstaclesProp.boolValue) // Tắt → ẩn toàn bộ thông số bên dưới
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.PropertyField(obstacleMaskProp, _gcObstacleMask);

                // Mask rỗng → không có gì để che → tính năng vô tác dụng
                if (!obstacleMaskProp.hasMultipleDifferentValues && obstacleMaskProp.intValue == 0)
                {
                    EditorGUILayout.HelpBox(
                        "Obstacle Mask đang là Nothing → không có vật cản nào được tính, Check Obstacles không có tác dụng.",
                        MessageType.Warning);
                }

                EditorGUILayout.PropertyField(ignoreOwnHierarchyProp, _gcIgnoreOwnHierarchy);
                EditorGUILayout.PropertyField(occlusionBlendTimeProp, _gcOcclusionBlendTime);
                EditorGUILayout.PropertyField(obstacleCheckIntervalProp, _gcObstacleCheckInterval);

                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(useOcclusionHoldProp, _gcUseOcclusionHold);
                if (useOcclusionHoldProp.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(occlusionHoldDurationProp, _gcOcclusionHoldDuration);
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.EndVertical();
        }

        // ── TAB 3: GIZMO ─────────────────────────────────────────────────────────

        private void DrawGizmoTab()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(showGizmosProp, _gcShowGizmos);

            if (showGizmosProp.boolValue)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Colors", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(gizmoChainColorProp, _gcGizmoChainColor);
                EditorGUILayout.PropertyField(gizmoTargetColorProp, _gcGizmoTargetColor);
                EditorGUILayout.PropertyField(gizmoBlockedColorProp, _gcGizmoBlockedColor);
                EditorGUILayout.PropertyField(gizmoSelectedColorProp, _gcGizmoSelectedColor);
                EditorGUILayout.HelpBox("Chuỗi chuyển sang màu đỏ khi Target ngoài tổng giới hạn (chỉ thấy khi Play).",
                    MessageType.None);
            }
            EditorGUILayout.EndVertical();
        }

        // ── TAB 4: API REFERENCE ─────────────────────────────────────────────────

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
                _apiDescriptionStyle = new GUIStyle(EditorStyles.wordWrappedLabel);

            if (apiCategoryFoldout == null)
                apiCategoryFoldout = new Dictionary<string, bool>();

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
                               || entry.searchText.IndexOf(filter, System.StringComparison.Ordinal) >= 0;

                if (!matches) continue;
                totalMatched++;

                if (entry.category != lastCategory)
                {
                    lastCategory = entry.category;

                    bool storedOpen;
                    if (!apiCategoryFoldout.TryGetValue(entry.category, out storedOpen))
                    {
                        storedOpen = false; // Mặc định thu gọn
                        apiCategoryFoldout[entry.category] = false;
                    }

                    bool searching = !string.IsNullOrEmpty(filter);
                    categoryOpen = searching ? true : storedOpen;

                    EditorGUILayout.Space(4);
                    bool newState = EditorGUILayout.Foldout(categoryOpen, entry.category, true, EditorStyles.foldoutHeader);
                    if (!searching && newState != storedOpen)
                        apiCategoryFoldout[entry.category] = newState;
                    categoryOpen = newState;
                }

                if (!categoryOpen) continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.SelectableLabel(entry.signature, _apiSignatureStyle, GUILayout.Height(20));
                if (GUILayout.Button("Copy", GUILayout.Width(50), GUILayout.Height(20)))
                    EditorGUIUtility.systemCopyBuffer = entry.signature;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField(entry.description, _apiDescriptionStyle);

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
            }

            if (totalMatched == 0)
                EditorGUILayout.HelpBox($"Không tìm thấy API nào khớp với \"{apiSearchFilter}\".", MessageType.None);

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                $"Tổng cộng {apiEntries.Length} hàm/thuộc tính public API. Bấm \"Copy\" để sao chép chữ ký, " +
                "dùng để gọi từ script khác (VD: GetComponent<DampedTrackChainConstraint>().SetInfluence(0f, 0.3f)).",
                MessageType.Info);
        }
    }
}
#endif