using UnityEngine;
using UnityEditor;


namespace GaMo.BlenderEditorTools
{
    [CustomEditor(typeof(TransformConstraint))]
    public class TransformConstraintEditor : Editor
    {
        private int currentTab = 0;

        private readonly string[] tabTitles =
            { "Targets & Core", "Position Settings", "Rotation Settings", "Gizmo Settings" };

        // Biến lưu trữ duration trên UI Editor
        private float debugSwitchDuration = 0.25f;

        // Cache GUIContent để tránh tạo object mới mỗi lần OnInspectorGUI repaint
        private GUIContent _gcConstraintObject;
        private GUIContent _gcHasExternalPoseDriver;
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

        // Chạy duy nhất một lần khi click vào Object có chứa script này
        private void OnEnable()
        {
            constraintObjectProp = serializedObject.FindProperty("constraintObject");
            hasExternalPoseDriverProp = serializedObject.FindProperty("hasExternalPoseDriver");
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

            SetComponentIcon();
        }

        private void SetComponentIcon()
        {
            // Chỉ tìm đúng file có tên là "CopyTransform" (icon của bạn) để tối ưu tốc độ quét
            string[] guids = AssetDatabase.FindAssets("CopyTransform t:Texture2D");

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

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            TransformConstraint script = (TransformConstraint)target;

            EditorGUILayout.Space(3);
            currentTab = GUILayout.Toolbar(currentTab, tabTitles, EditorStyles.toolbarButton, GUILayout.Height(20));
            EditorGUILayout.Space(10);

            switch (currentTab)
            {
                case 0:
                    DrawTargetsTab(script);
                    break;
                case 1:
                    DrawPositionTab();
                    break;
                case 2:
                    DrawRotationTab();
                    break;
                case 3:
                    DrawGizmoTab();
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
                    ((TransformConstraint)target).CaptureRestPose();
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
            EditorGUILayout.PropertyField(targetsProp, _gcTargetsList, true);
            EditorGUILayout.EndVertical();

            // ── RUNTIME DEBUG TOOLS ──────────────────────────────────────────────────
            if (Application.isPlaying)
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

        private void DrawPositionTab()
        {
            //EditorGUILayout.LabelField("Position Constraint", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(enablePositionProp, _gcEnablePosition);

            EditorGUI.BeginDisabledGroup(!enablePositionProp.boolValue);
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

                posCopyXProp.boolValue = GUI.Toggle(xRect, posCopyXProp.boolValue, "X", EditorStyles.miniButtonLeft);
                posCopyYProp.boolValue = GUI.Toggle(yRect, posCopyYProp.boolValue, "Y", EditorStyles.miniButtonMid);
                posCopyZProp.boolValue = GUI.Toggle(zRect, posCopyZProp.boolValue, "Z", EditorStyles.miniButtonRight);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();
        }

        private void DrawRotationTab()
        {
            //EditorGUILayout.LabelField("Rotation Constraint", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(enableRotationProp, _gcEnableRotation);

            EditorGUI.BeginDisabledGroup(!enableRotationProp.boolValue);
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

                rotCopyXProp.boolValue = GUI.Toggle(xRect, rotCopyXProp.boolValue, "X", EditorStyles.miniButtonLeft);
                rotCopyYProp.boolValue = GUI.Toggle(yRect, rotCopyYProp.boolValue, "Y", EditorStyles.miniButtonMid);
                rotCopyZProp.boolValue = GUI.Toggle(zRect, rotCopyZProp.boolValue, "Z", EditorStyles.miniButtonRight);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();
        }

        private void DrawGizmoTab()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(showGizmosProp, _gcShowGizmos);

            EditorGUI.BeginDisabledGroup(!showGizmosProp.boolValue);
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
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();
        }
    }
}