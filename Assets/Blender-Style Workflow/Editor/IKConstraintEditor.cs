using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(IKConstraint))]
public class IKConstraintEditor : Editor
{
    private int currentTab = 0;
    private readonly string[] tabTitles = { "Core & Targets", "Solver Parameters", "Gizmo Settings" };
    private float debugSwitchDuration     = 0.5f;
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

    private void OnEnable()
    {
        previewProp      = serializedObject.FindProperty("preview");
        bonesProp        = serializedObject.FindProperty("bones");
        poleTargetsProp  = serializedObject.FindProperty("poleTargets");
        targetsProp      = serializedObject.FindProperty("targets");
        ikInfluenceProp  = serializedObject.FindProperty("ikInfluence");
        iterationsProp   = serializedObject.FindProperty("iterations");
        deltaProp        = serializedObject.FindProperty("delta");

        drawGizmosProp        = serializedObject.FindProperty("drawGizmos");
        targetGizmoSizeProp   = serializedObject.FindProperty("targetGizmoSize");
        targetGizmoHeightProp = serializedObject.FindProperty("targetGizmoHeight");
        targetGizmoColorProp  = serializedObject.FindProperty("targetGizmoColor");
        elbowGizmoRadiusProp  = serializedObject.FindProperty("elbowGizmoRadius");
        elbowGizmoColorProp   = serializedObject.FindProperty("elbowGizmoColor");

        // Khởi tạo GUIContent cache một lần duy nhất thay vì tạo mới mỗi frame repaint
        _gcBones             = new GUIContent("Bones Chain",            bonesProp.tooltip);
        _gcPoleTargets       = new GUIContent("Pole Targets List",      poleTargetsProp.tooltip);
        _gcTargets           = new GUIContent("Targets List",           targetsProp.tooltip);
        _gcIKInfluence       = new GUIContent("IK Influence",           ikInfluenceProp.tooltip);
        _gcIterations        = new GUIContent("Max Iterations",         iterationsProp.tooltip);
        _gcDelta             = new GUIContent("Tolerance Delta (m)",    deltaProp.tooltip);
        _gcDrawGizmos        = new GUIContent("Draw Gizmos",            drawGizmosProp.tooltip);
        _gcTargetGizmoSize   = new GUIContent("Kích Thước Đáy",        targetGizmoSizeProp.tooltip);
        _gcTargetGizmoHeight = new GUIContent("Chiều Dài (Chiều Cao)", targetGizmoHeightProp.tooltip);
        _gcTargetGizmoColor  = new GUIContent("Màu Sắc",               targetGizmoColorProp.tooltip);
        _gcElbowGizmoRadius  = new GUIContent("Bán Kính",              elbowGizmoRadiusProp.tooltip);
        _gcElbowGizmoColor   = new GUIContent("Màu Sắc",               elbowGizmoColorProp.tooltip);

        invisibleCapFunction = InvisibleButtonCap;

        SetComponentIcon();
    }

    /// <summary>
    /// Cho phép CLICK trực tiếp vào Gizmo trong Scene View để chọn GameObject tương ứng.
    /// </summary>
    private void OnSceneGUI()
    {
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

                if (Handles.Button(t.target.position, t.target.rotation, targetPickSize, targetPickSize, invisibleCapFunction))
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

                if (Handles.Button(p.poleTarget.position, p.poleTarget.rotation, polePickSize, polePickSize, invisibleCapFunction))
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
    private void InvisibleButtonCap(int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType)
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
        serializedObject.Update();
        IKConstraint script = (IKConstraint)target;

        EditorGUILayout.Space(3);
        currentTab = GUILayout.Toolbar(currentTab, tabTitles, EditorStyles.toolbarButton, GUILayout.Height(20));
        EditorGUILayout.Space(10);

        switch (currentTab)
        {
            case 0: DrawCoreAndTargetsTab(script); break;
            case 1: DrawSolverParametersTab();     break;
            case 2: DrawGizmoSettingsTab();        break;
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
                id => { script.SwitchTarget(id, debugSwitchDuration); if (!Application.isPlaying) EditorUtility.SetDirty(script); },
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
                id => { script.SwitchPoleTarget(id, debugPoleSwitchDuration); if (!Application.isPlaying) EditorUtility.SetDirty(script); },
                "Pole → "
            );

            EditorGUILayout.EndVertical();
        }
    }

    /// <summary>
    /// Vẽ các nút Switch theo hàng ngang, tối đa 3 nút mỗi hàng.
    /// </summary>
    private void DrawSwitchButtons(int count, System.Func<int, string> getId, System.Action<string> onButton, string prefix)
    {
        int  buttonCount       = 0;
        bool isHorizontalOpen  = false;

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
        EditorGUILayout.PropertyField(deltaProp,      _gcDelta);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        EditorGUILayout.HelpBox(
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
        EditorGUILayout.PropertyField(targetGizmoSizeProp,   _gcTargetGizmoSize);
        EditorGUILayout.PropertyField(targetGizmoHeightProp, _gcTargetGizmoHeight);
        EditorGUILayout.PropertyField(targetGizmoColorProp,  _gcTargetGizmoColor);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        // ── HÌNH CẦU (POLE TARGET) ────────────────────────────────────────────────
        EditorGUILayout.LabelField("Pole Target - Hình Cầu", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.PropertyField(elbowGizmoRadiusProp, _gcElbowGizmoRadius);
        EditorGUILayout.PropertyField(elbowGizmoColorProp,  _gcElbowGizmoColor);
        EditorGUILayout.EndVertical();
    }
}