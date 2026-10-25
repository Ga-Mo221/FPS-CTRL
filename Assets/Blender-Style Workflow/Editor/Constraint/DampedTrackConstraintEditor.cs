#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace GaMo.BlenderEditorTools
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(DampedTrackConstraint))]
    [InitializeOnLoad]
    public class DampedTrackConstraintEditor : Editor
    {
        private int currentTab = 0;

        private readonly string[] tabTitles =
            { "Targets & Core", "Track & Limits", "Gizmo Settings", "API Reference" };

        // Nhãn nút axis: hàng 1 = X Y Z (index 0..2), hàng 2 = -X -Y -Z (index 3..5)
        // Khớp thứ tự enum DampedTrackConstraint.TrackAxis { X, Y, Z, NegX, NegY, NegZ }
        private static readonly string[] AxisRow1 = { "X", "Y", "Z" };
        private static readonly string[] AxisRow2 = { "-X", "-Y", "-Z" };

        // Biến lưu trữ duration trên UI Editor
        private float debugSwitchDuration = 0.25f;

        // Cache GUIContent để tránh tạo object mới mỗi lần OnInspectorGUI repaint
        private GUIContent _gcConstraintObject;
        private GUIContent _gcHasExternalPoseDriver;
        private GUIContent _gcTrackAxis;
        private GUIContent _gcNormalizeWeights;
        private GUIContent _gcTargetsList;
        private GUIContent _gcInfluence;
        private GUIContent _gcFollowSpeed;
        private GUIContent _gcRotationOffset;
        private GUIContent _gcUseLimits;
        private GUIContent _gcLimitUpAxis;
        private GUIContent _gcMaxYawLeft;
        private GUIContent _gcMaxYawRight;
        private GUIContent _gcMaxPitchUp;
        private GUIContent _gcMaxPitchDown;
        private GUIContent _gcLimitMargin;
        private GUIContent _gcReturnDuration;
        private GUIContent _gcShowGizmos;
        private GUIContent _gcGizmoSize;
        private GUIContent _gcGizmoAxisColor;
        private GUIContent _gcGizmoLineColor;
        private GUIContent _gcGizmoBlockedColor;
        private GUIContent _gcCheckObstacles;
        private GUIContent _gcObstacleMask;
        private GUIContent _gcIgnoreOwnHierarchy;
        private GUIContent _gcOcclusionBlendTime;
        private GUIContent _gcUseLimitHold;
        private GUIContent _gcLimitHoldDuration;
        private GUIContent _gcUseOcclusionHold;
        private GUIContent _gcOcclusionHoldDuration;
        private GUIContent _gcAutoTracking;
        private GUIContent _gcAutoSwitchBlendTime;
        private GUIContent _gcAutoSwitchMargin;
        private GUIContent _gcAutoMinSwitchInterval;
        private GUIContent _gcGizmoSelectedColor;

        // Properties - Core / Targets
        private SerializedProperty constraintObjectProp;
        private SerializedProperty hasExternalPoseDriverProp;
        private SerializedProperty normalizeWeightsProp;
        private SerializedProperty targetsProp;

        // Properties - Track & Limits
        private SerializedProperty trackAxisProp;
        private SerializedProperty influenceProp;
        private SerializedProperty followSpeedProp;
        private SerializedProperty rotationOffsetProp;
        private SerializedProperty useLimitsProp;
        private SerializedProperty limitUpAxisProp;
        private SerializedProperty maxYawLeftProp;
        private SerializedProperty maxYawRightProp;
        private SerializedProperty maxPitchUpProp;
        private SerializedProperty maxPitchDownProp;
        private SerializedProperty limitMarginProp;
        private SerializedProperty returnDurationProp;

        // Properties - Gizmo
        private SerializedProperty showGizmosProp;
        private SerializedProperty gizmoSizeProp;
        private SerializedProperty gizmoAxisColorProp;
        private SerializedProperty gizmoLineColorProp;
        private SerializedProperty gizmoBlockedColorProp;
        private SerializedProperty gizmoSelectedColorProp;

        // Properties - Auto Tracking
        private SerializedProperty autoTrackingProp;
        private SerializedProperty autoSwitchBlendTimeProp;
        private SerializedProperty autoSwitchMarginProp;
        private SerializedProperty autoMinSwitchIntervalProp;

        // Properties - Obstacle Check
        private SerializedProperty checkObstaclesProp;
        private SerializedProperty obstacleMaskProp;
        private SerializedProperty ignoreOwnHierarchyProp;
        private SerializedProperty occlusionBlendTimeProp;
        private SerializedProperty useLimitHoldProp;
        private SerializedProperty limitHoldDurationProp;
        private SerializedProperty useOcclusionHoldProp;
        private SerializedProperty occlusionHoldDurationProp;

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

        // Danh sách toàn bộ API public của DampedTrackConstraint, nhóm theo category.
        // Cập nhật mảng này mỗi khi thêm/sửa/xoá 1 hàm public trong DampedTrackConstraint.cs.
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
                "Set Weight (0-1, tự Clamp01) cho Target có id tương ứng. Trả về false nếu không tìm thấy id."),
            new ApiEntry("Target",
                "bool SetTargetWeight(string id, float weight, float duration)",
                "Đổi Weight (0-1, tự Clamp01) của Target có id tương ứng MƯỢT MÀ trong duration giây (coroutine), không đụng các Target khác. Gọi lại cùng id sẽ thay thế lần fade đang chạy; duration <= 0 → set ngay lập tức. Lưu ý: SwitchTarget đang chạy sẽ bị huỷ. Trả về false nếu không tìm thấy id."),
            new ApiEntry("Target",
                "void SwitchTarget(string targetId, float duration)",
                "Chuyển mượt (lerp weight) sang Target có id chỉ định trong duration giây (coroutine); các Target khác giảm dần về 0. Truyền duration <= 0 để chuyển ngay lập tức."),

            // ── SETTINGS ────────────────────────────────────────────────────────────
            new ApiEntry("Settings",
                "void SetInfluence(float value)",
                "Set ngay lập tức Influence tổng thể (0-1, tự Clamp01). 1 = hướng khớp hoàn toàn theo Target, 0 = trả về Animation/Rest Pose gốc."),
            new ApiEntry("Settings",
                "void SetInfluence(float value, float duration)",
                "Đổi Influence tổng thể (0-1, tự Clamp01) MƯỢT MÀ trong duration giây (coroutine). Gọi lại sẽ thay thế lần fade đang chạy; duration <= 0 → set ngay lập tức. Dùng để bật/tắt constraint êm (VD: fade về 0 khi nhân vật chết)."),
            new ApiEntry("Settings",
                "void SetTrackAxis(TrackAxis axis)",
                "Đổi trục Track Axis tại runtime (VD: đổi hướng nòng khi swap model súng). TrackAxis: X, Y, Z, NegX, NegY, NegZ."),

            // ── AUTO TRACKING ───────────────────────────────────────────────────────
            new ApiEntry("Auto Tracking",
                "void SetAutoTracking(bool enabled)",
                "Bật/tắt Auto Tracking tại runtime. Chỉ thực sự chạy khi có từ 2 Target hợp lệ (Transform khác null và Weight > 0): chọn Target gần nhất nằm trong Limits (nếu bật) và không bị che (nếu bật Check Obstacles)."),
            new ApiEntry("Auto Tracking",
                "bool IsAutoTrackingActive { get; }",
                "true khi Auto Tracking đang thực sự chạy (đã bật và có từ 2 Target hợp lệ)."),
            new ApiEntry("Auto Tracking",
                "string AutoTargetId { get; }",
                "ID của Target đang được Auto Tracking chọn. null nếu Auto Tracking không chạy."),

            // ── CORE ────────────────────────────────────────────────────────────────
            new ApiEntry("Core",
                "void CaptureRestPose()",
                "Chụp lại Rest Pose (local rotation hiện tại của Constraint Object) làm baseline cố định khi Has External Pose Driver = false. Tự gọi ở OnEnable (sau 1 frame)."),

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
        
        static DampedTrackConstraintEditor()
        {
            EditorApplication.delayCall += ApplyIconToAll;
            EditorApplication.hierarchyChanged -= ApplyIconToAll;
            EditorApplication.hierarchyChanged += ApplyIconToAll;
        }

        private static void ApplyIconToAll()
            => ComponentIconApplier.ApplyToAll<DampedTrackConstraint>("DampeTrack");

        // Chạy duy nhất một lần khi click vào Object có chứa script này
        private void OnEnable()
        {
            constraintObjectProp = serializedObject.FindProperty("constraintObject");
            hasExternalPoseDriverProp = serializedObject.FindProperty("hasExternalPoseDriver");
            normalizeWeightsProp = serializedObject.FindProperty("normalizeWeights");
            targetsProp = serializedObject.FindProperty("targets");

            trackAxisProp = serializedObject.FindProperty("trackAxis");
            influenceProp = serializedObject.FindProperty("influence");
            followSpeedProp = serializedObject.FindProperty("followSpeed");
            rotationOffsetProp = serializedObject.FindProperty("rotationOffset");
            useLimitsProp = serializedObject.FindProperty("useLimits");
            limitUpAxisProp = serializedObject.FindProperty("limitUpAxis");
            maxYawLeftProp = serializedObject.FindProperty("maxYawLeft");
            maxYawRightProp = serializedObject.FindProperty("maxYawRight");
            maxPitchUpProp = serializedObject.FindProperty("maxPitchUp");
            maxPitchDownProp = serializedObject.FindProperty("maxPitchDown");
            limitMarginProp = serializedObject.FindProperty("limitMargin");
            returnDurationProp = serializedObject.FindProperty("returnDuration");

            showGizmosProp = serializedObject.FindProperty("showGizmos");
            gizmoSizeProp = serializedObject.FindProperty("gizmoSize");
            gizmoAxisColorProp = serializedObject.FindProperty("gizmoAxisColor");
            gizmoLineColorProp = serializedObject.FindProperty("gizmoLineColor");
            gizmoBlockedColorProp = serializedObject.FindProperty("gizmoBlockedColor");
            gizmoSelectedColorProp = serializedObject.FindProperty("gizmoSelectedColor");

            autoTrackingProp = serializedObject.FindProperty("autoTracking");
            autoSwitchBlendTimeProp = serializedObject.FindProperty("autoSwitchBlendTime");
            autoSwitchMarginProp = serializedObject.FindProperty("autoSwitchMargin");
            autoMinSwitchIntervalProp = serializedObject.FindProperty("autoMinSwitchInterval");

            checkObstaclesProp = serializedObject.FindProperty("checkObstacles");
            obstacleMaskProp = serializedObject.FindProperty("obstacleMask");
            ignoreOwnHierarchyProp = serializedObject.FindProperty("ignoreOwnHierarchy");
            occlusionBlendTimeProp = serializedObject.FindProperty("occlusionBlendTime");
            useLimitHoldProp = serializedObject.FindProperty("useLimitHold");
            limitHoldDurationProp = serializedObject.FindProperty("limitHoldDuration");
            useOcclusionHoldProp = serializedObject.FindProperty("useOcclusionHold");
            occlusionHoldDurationProp = serializedObject.FindProperty("occlusionHoldDuration");

            _gcConstraintObject = new GUIContent("Constraint Object", constraintObjectProp.tooltip);
            _gcHasExternalPoseDriver = new GUIContent("Has External Pose Driver", hasExternalPoseDriverProp.tooltip);
            _gcTrackAxis = new GUIContent("Track Axis", trackAxisProp.tooltip);
            _gcNormalizeWeights = new GUIContent("Normalize Weights", normalizeWeightsProp.tooltip);
            _gcTargetsList = new GUIContent("Targets List", targetsProp.tooltip);
            _gcInfluence = new GUIContent("Influence", influenceProp.tooltip);
            _gcFollowSpeed = new GUIContent("Follow Speed", followSpeedProp.tooltip);
            _gcRotationOffset = new GUIContent("Rotation Offset", rotationOffsetProp.tooltip);
            _gcUseLimits = new GUIContent("Use Limits", useLimitsProp.tooltip);
            _gcLimitUpAxis = new GUIContent("Limit Up Axis", limitUpAxisProp.tooltip);
            _gcMaxYawLeft = new GUIContent("Max Yaw Left", maxYawLeftProp.tooltip);
            _gcMaxYawRight = new GUIContent("Max Yaw Right", maxYawRightProp.tooltip);
            _gcMaxPitchUp = new GUIContent("Max Pitch Up", maxPitchUpProp.tooltip);
            _gcMaxPitchDown = new GUIContent("Max Pitch Down", maxPitchDownProp.tooltip);
            _gcLimitMargin = new GUIContent("Limit Margin", limitMarginProp.tooltip);
            _gcReturnDuration = new GUIContent("Return Duration", returnDurationProp.tooltip);
            _gcShowGizmos = new GUIContent("Show Gizmos", showGizmosProp.tooltip);
            _gcGizmoSize = new GUIContent("Gizmo Size", gizmoSizeProp.tooltip);
            _gcGizmoAxisColor = new GUIContent("Axis Color", gizmoAxisColorProp.tooltip);
            _gcGizmoLineColor = new GUIContent("Line To Target Color", gizmoLineColorProp.tooltip);
            _gcGizmoBlockedColor = new GUIContent("Blocked Line Color", gizmoBlockedColorProp.tooltip);
            _gcCheckObstacles = new GUIContent("Check Obstacles", checkObstaclesProp.tooltip);
            _gcObstacleMask = new GUIContent("Obstacle Mask", obstacleMaskProp.tooltip);
            _gcIgnoreOwnHierarchy = new GUIContent("Ignore Own Hierarchy", ignoreOwnHierarchyProp.tooltip);
            _gcOcclusionBlendTime = new GUIContent("Occlusion Blend Time", occlusionBlendTimeProp.tooltip);
            _gcUseLimitHold = new GUIContent("Hold Before Return", useLimitHoldProp.tooltip);
            _gcLimitHoldDuration = new GUIContent("Hold Duration", limitHoldDurationProp.tooltip);
            _gcUseOcclusionHold = new GUIContent("Hold Before Return", useOcclusionHoldProp.tooltip);
            _gcOcclusionHoldDuration = new GUIContent("Hold Duration", occlusionHoldDurationProp.tooltip);
            _gcGizmoSelectedColor = new GUIContent("Auto Selected Line Color", gizmoSelectedColorProp.tooltip);
            _gcAutoTracking = new GUIContent("Auto Tracking", autoTrackingProp.tooltip);
            _gcAutoSwitchBlendTime = new GUIContent("Switch Blend Time", autoSwitchBlendTimeProp.tooltip);
            _gcAutoSwitchMargin = new GUIContent("Switch Margin", autoSwitchMarginProp.tooltip);
            _gcAutoMinSwitchInterval = new GUIContent("Min Switch Interval", autoMinSwitchIntervalProp.tooltip);
        }

        // Cập nhật liên tục trạng thái Auto Tracking khi đang Play ở tab Targets
        public override bool RequiresConstantRepaint()
        {
            DampedTrackConstraint s = target as DampedTrackConstraint;
            return Application.isPlaying && currentTab == 0 && s != null && s.autoTracking;
        }

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

        // ── Axis Buttons (2 hàng x 3 nút, chọn 1 trong 6) ────────────────────────

        /// <summary>
        /// Vẽ lựa chọn trục dạng nút bấm (radio) thay cho dropdown enum:
        /// hàng 1 = X Y Z, hàng 2 = -X -Y -Z. Trục nào có (index % 3) == blockedAxis
        /// sẽ bị khóa (dùng để không cho Limit Up Axis trùng Track Axis). Truyền -1 nếu không khóa gì.
        /// </summary>
        private void DrawAxisButtons(SerializedProperty prop, GUIContent label, int blockedAxis)
        {
            EditorGUILayout.LabelField(label);
            // Multi-edit: các object khác giá trị => không nút nào sáng (-1), bấm nút nào sẽ ghi cho tất cả
            int current = prop.hasMultipleDifferentValues ? -1 : prop.enumValueIndex;

            int picked = DrawAxisRow(AxisRow1, 0, current, blockedAxis);
            if (picked < 0) picked = DrawAxisRow(AxisRow2, 3, current, blockedAxis);
            else DrawAxisRow(AxisRow2, 3, current, blockedAxis);

            if (picked >= 0 && picked != current)
                prop.enumValueIndex = picked;
        }

        /// <summary>
        /// Vẽ 1 hàng 3 nút Toggle. Trả về index enum của nút vừa được bấm chọn, hoặc -1 nếu không có thay đổi.
        /// </summary>
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

                // Chỉ nhận khi bấm nút CHƯA chọn (nút đang chọn bấm lại thì giữ nguyên)
                if (isOn && current != enumIndex) picked = enumIndex;
            }

            return picked;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DampedTrackConstraint script = (DampedTrackConstraint)target;

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
                    DrawTrackTab();
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

        // ── TAB 1: TARGETS & CORE ────────────────────────────────────────────────

        private void DrawTargetsTab(DampedTrackConstraint script)
        {
            // ── CORE OBJECTS ─────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Core Objects", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(constraintObjectProp, _gcConstraintObject);
            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(hasExternalPoseDriverProp, _gcHasExternalPoseDriver);

            // Chỉ cần thiết khi KHÔNG có driver ngoài (Animator...) — lúc đó baseline dựa vào Rest Pose
            // đã chụp sẵn, người dùng có thể chủ động "khóa" lại pose gốc mới bất cứ lúc nào.
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
                        var dtc = tg as DampedTrackConstraint;
                        if (dtc != null) dtc.CaptureRestPose();
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

            // ── MULTI-TARGET SETTINGS ────────────────────────────────────────────
            EditorGUILayout.LabelField("Multi-Target Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(normalizeWeightsProp, _gcNormalizeWeights);
            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(targetsProp, _gcTargetsList, true);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);
            DrawAutoTrackingBox(script);

            // ── RUNTIME DEBUG TOOLS ──────────────────────────────────────────────
            // Chỉ hiện khi chọn 1 object: các nút Switch dựa trên danh sách targets của riêng từng object
            if (Application.isPlaying && targets.Length == 1)
            {
                EditorGUILayout.Space(15);
                EditorGUILayout.LabelField("Runtime Debug Tools", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                debugSwitchDuration = EditorGUILayout.Slider("Debug Switch Duration", debugSwitchDuration, 0f, 5f);
                EditorGUILayout.Space(5);

                // --- CHIA HÀNG CHO CÁC BUTTON: TỐI ĐA 3 BUTTON / HÀNG ---
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

        private static int CountEligibleTargets(DampedTrackConstraint script)
        {
            int n = 0;
            int count = script.targets.Count;
            for (int i = 0; i < count; i++)
            {
                var t = script.targets[i];
                if (t != null && t.target != null && t.weight > 0f) n++;
            }
            return n;
        }

        private void DrawAutoTrackingBox(DampedTrackConstraint script)
        {
            EditorGUILayout.LabelField("Auto Tracking", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(autoTrackingProp, _gcAutoTracking);

            // Tắt Auto Tracking → ẩn toàn bộ thông số bên dưới
            if (autoTrackingProp.boolValue)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.PropertyField(autoSwitchBlendTimeProp, _gcAutoSwitchBlendTime);
                EditorGUILayout.PropertyField(autoSwitchMarginProp, _gcAutoSwitchMargin);
                EditorGUILayout.PropertyField(autoMinSwitchIntervalProp, _gcAutoMinSwitchInterval);

                // Trạng thái chỉ hiện khi chọn 1 object (multi-edit mỗi object có danh sách target riêng)
                if (targets.Length == 1)
                {
                    EditorGUILayout.Space(4);
                    int eligible = CountEligibleTargets(script);

                    if (eligible < 2)
                    {
                        EditorGUILayout.HelpBox(
                            string.Concat("Cần ít nhất 2 Target hợp lệ (có Transform và Weight > 0), hiện có ",
                                eligible.ToString(), ". Auto Tracking tạm không hoạt động — constraint chạy theo Weight như bình thường."),
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
                            "Ưu tiên Target gần nhất, nằm trong Limits (nếu bật Use Limits) và không bị che (nếu bật Check Obstacles).",
                            MessageType.Info);
                    }
                }
            }
            EditorGUILayout.EndVertical();
        }

        // ── TAB 2: TRACK & LIMITS ────────────────────────────────────────────────

        private void DrawTrackTab()
        {
            // ── TRACK ────────────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Track Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawAxisButtons(trackAxisProp, _gcTrackAxis, -1);
            EditorGUILayout.Space(6);
            EditorGUILayout.PropertyField(influenceProp, _gcInfluence);
            EditorGUILayout.PropertyField(followSpeedProp, _gcFollowSpeed); // Range → hiển thị dạng slider
            EditorGUILayout.PropertyField(rotationOffsetProp, _gcRotationOffset);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // ── ROTATION LIMITS ──────────────────────────────────────────────────
            EditorGUILayout.LabelField("Rotation Limits", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(useLimitsProp, _gcUseLimits);

            // Tắt Use Limits → ẩn toàn bộ thông số bên dưới
            if (useLimitsProp.boolValue)
            {
                EditorGUILayout.Space(5);

                int trackAxisIndex = trackAxisProp.enumValueIndex % 3;
                DrawAxisButtons(limitUpAxisProp, _gcLimitUpAxis, trackAxisIndex);

                // Phòng trường hợp giá trị đã lưu từ trước đang trùng Track Axis
                if (limitUpAxisProp.enumValueIndex % 3 == trackAxisIndex)
                {
                    EditorGUILayout.HelpBox(
                        "Limit Up Axis đang trùng Track Axis → giới hạn không hoạt động. Hãy chọn trục khác.",
                        MessageType.Warning);
                }

                EditorGUILayout.Space(8);

                EditorGUILayout.PropertyField(maxYawLeftProp, _gcMaxYawLeft);
                EditorGUILayout.PropertyField(maxYawRightProp, _gcMaxYawRight);
                EditorGUILayout.PropertyField(maxPitchUpProp, _gcMaxPitchUp);
                EditorGUILayout.PropertyField(maxPitchDownProp, _gcMaxPitchDown);

                EditorGUILayout.Space(8);

                EditorGUILayout.PropertyField(limitMarginProp, _gcLimitMargin);
                EditorGUILayout.PropertyField(returnDurationProp, _gcReturnDuration);

                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(useLimitHoldProp, _gcUseLimitHold);
                if (useLimitHoldProp.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(limitHoldDurationProp, _gcLimitHoldDuration);
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // ── OBSTACLE CHECK ───────────────────────────────────────────────────
            EditorGUILayout.LabelField("Obstacle Check", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(checkObstaclesProp, _gcCheckObstacles);

            // Tắt Check Obstacles → ẩn toàn bộ thông số bên dưới
            if (checkObstaclesProp.boolValue)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.PropertyField(obstacleMaskProp, _gcObstacleMask);
                EditorGUILayout.PropertyField(ignoreOwnHierarchyProp, _gcIgnoreOwnHierarchy);
                EditorGUILayout.PropertyField(occlusionBlendTimeProp, _gcOcclusionBlendTime);

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

            // Tắt Show Gizmos → ẩn toàn bộ thông số bên dưới
            if (showGizmosProp.boolValue)
            {
                EditorGUILayout.Space(5);

                EditorGUILayout.PropertyField(gizmoSizeProp, _gcGizmoSize);
                if (gizmoSizeProp.floatValue < 0f) gizmoSizeProp.floatValue = 0f;

                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Colors", EditorStyles.boldLabel);

                EditorGUILayout.PropertyField(gizmoAxisColorProp, _gcGizmoAxisColor);
                EditorGUILayout.PropertyField(gizmoLineColorProp, _gcGizmoLineColor);
                EditorGUILayout.PropertyField(gizmoBlockedColorProp, _gcGizmoBlockedColor);
                EditorGUILayout.PropertyField(gizmoSelectedColorProp, _gcGizmoSelectedColor);
            }
            EditorGUILayout.EndVertical();
        }

        // ── TAB 4: API REFERENCE ─────────────────────────────────────────────────

        /// <summary>
        /// Tab liệt kê toàn bộ API public của DampedTrackConstraint (Target, Settings, Core),
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
                "dùng để gọi từ script khác (VD: GetComponent<DampedTrackConstraint>().SwitchTarget(\"enemy\", 0.3f)).",
                MessageType.Info
            );
        }
    }
}
#endif