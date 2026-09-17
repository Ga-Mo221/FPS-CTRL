using UnityEngine;
using UnityEditor;

namespace GaMo.BlenderEditorTools
{
    [CustomEditor(typeof(DynamicBone))]
    [CanEditMultipleObjects]
    public class DynamicBoneManagerEditor : Editor
    {
        private int currentTab = 0;

        private readonly string[] tabTitles =
            { "Preset", "Bones & Colliders", "Physics", "Debug & Gizmos" };

        // Label cho dãy nút chọn preset (thay cho EnumToggleButtons của Odin)
        private string[] presetLabels;
        private ClothPreset[] presetValues;

        // ── GUIContent cache (tránh alloc mỗi lần repaint) ──────────────────────
        private GUIContent _gcRootBones;
        private GUIContent _gcEnableColliders;
        private GUIContent _gcParticleRadius;
        private GUIContent _gcColliders;
        private GUIContent _gcEnableClothLinks;
        private GUIContent _gcClothClosedLoop;
        private GUIContent _gcClothStiffness;
        private GUIContent _gcClothIterations;
        private GUIContent _gcDamping;
        private GUIContent _gcInertia;
        private GUIContent _gcCollisionFalloff;
        private GUIContent _gcElasticity;
        private GUIContent _gcSpringStiffness;
        private GUIContent _gcSpringForceMul;
        private GUIContent _gcGravity;
        private GUIContent _gcUseRestPoseGravity;
        private GUIContent _gcRestPoseBlend;
        private GUIContent _gcShowGizmos;
        private GUIContent _gcNodeGizmoSize;
        private GUIContent _gcArrowLength;

        // ── Properties ──────────────────────────────────────────────────────────
        private SerializedProperty clothPresetProp;

        private SerializedProperty rootBonesProp;
        private SerializedProperty enableCollidersProp;
        private SerializedProperty particleRadiusProp;
        private SerializedProperty collidersProp;

        private SerializedProperty enableClothLinksProp;
        private SerializedProperty clothLinkClosedLoopProp;
        private SerializedProperty clothLinkStiffnessProp;
        private SerializedProperty clothLinkIterationsProp;

        private SerializedProperty dampingProp;
        private SerializedProperty inertiaProp;
        private SerializedProperty collisionParentFalloffProp;
        private SerializedProperty elasticityProp;
        private SerializedProperty springStiffnessProp;
        private SerializedProperty springForceMultiplierProp;
        private SerializedProperty gravityProp;
        private SerializedProperty useRestPoseGravityProp;
        private SerializedProperty restPoseGravityBlendProp;

        private SerializedProperty showGizmosProp;
        private SerializedProperty nodeGizmoSizeProp;
        private SerializedProperty arrowLengthMultiplierProp;

        private void OnEnable()
        {
            clothPresetProp = serializedObject.FindProperty("clothPreset");

            rootBonesProp       = serializedObject.FindProperty("rootBones");
            enableCollidersProp = serializedObject.FindProperty("enableColliders");
            particleRadiusProp  = serializedObject.FindProperty("particleRadius");
            collidersProp       = serializedObject.FindProperty("colliders");

            enableClothLinksProp    = serializedObject.FindProperty("enableClothLinks");
            clothLinkClosedLoopProp = serializedObject.FindProperty("clothLinkClosedLoop");
            clothLinkStiffnessProp  = serializedObject.FindProperty("clothLinkStiffness");
            clothLinkIterationsProp = serializedObject.FindProperty("clothLinkIterations");

            dampingProp                = serializedObject.FindProperty("damping");
            inertiaProp                = serializedObject.FindProperty("inertia");
            collisionParentFalloffProp = serializedObject.FindProperty("collisionParentFalloff");
            elasticityProp             = serializedObject.FindProperty("elasticity");
            springStiffnessProp        = serializedObject.FindProperty("springStiffness");
            springForceMultiplierProp  = serializedObject.FindProperty("springForceMultiplier");
            gravityProp                = serializedObject.FindProperty("gravity");
            useRestPoseGravityProp     = serializedObject.FindProperty("useRestPoseGravity");
            restPoseGravityBlendProp   = serializedObject.FindProperty("restPoseGravityBlend");

            showGizmosProp            = serializedObject.FindProperty("showGizmos");
            nodeGizmoSizeProp         = serializedObject.FindProperty("nodeGizmoSize");
            arrowLengthMultiplierProp = serializedObject.FindProperty("arrowLengthMultiplier");

            _gcRootBones          = new GUIContent("Root Bones", rootBonesProp.tooltip);
            _gcEnableColliders    = new GUIContent("Enable Colliders", enableCollidersProp.tooltip);
            _gcParticleRadius     = new GUIContent("Particle Radius", particleRadiusProp.tooltip);
            _gcColliders          = new GUIContent("Colliders List", collidersProp.tooltip);
            _gcEnableClothLinks   = new GUIContent("Enable Cloth Links", enableClothLinksProp.tooltip);
            _gcClothClosedLoop    = new GUIContent("Closed Loop", clothLinkClosedLoopProp.tooltip);
            _gcClothStiffness     = new GUIContent("Link Stiffness", clothLinkStiffnessProp.tooltip);
            _gcClothIterations    = new GUIContent("Solver Iterations", clothLinkIterationsProp.tooltip);
            _gcDamping            = new GUIContent("Damping", dampingProp.tooltip);
            _gcInertia            = new GUIContent("Inertia", inertiaProp.tooltip);
            _gcCollisionFalloff   = new GUIContent("Collision Parent Falloff", collisionParentFalloffProp.tooltip);
            _gcElasticity         = new GUIContent("Elasticity", elasticityProp.tooltip);
            _gcSpringStiffness    = new GUIContent("Spring Stiffness", springStiffnessProp.tooltip);
            _gcSpringForceMul     = new GUIContent("Spring Force Multiplier", springForceMultiplierProp.tooltip);
            _gcGravity            = new GUIContent("Gravity", gravityProp.tooltip);
            _gcUseRestPoseGravity = new GUIContent("Use Rest Pose Gravity", useRestPoseGravityProp.tooltip);
            _gcRestPoseBlend      = new GUIContent("Rest Pose Blend", restPoseGravityBlendProp.tooltip);
            _gcShowGizmos         = new GUIContent("Show Gizmos", showGizmosProp.tooltip);
            _gcNodeGizmoSize      = new GUIContent("Node Gizmo Size", nodeGizmoSizeProp.tooltip);
            _gcArrowLength        = new GUIContent("Arrow Length Multiplier", arrowLengthMultiplierProp.tooltip);

            presetValues = (ClothPreset[])System.Enum.GetValues(typeof(ClothPreset));
            presetLabels = new string[presetValues.Length];
            for (int i = 0; i < presetValues.Length; i++)
                presetLabels[i] = DynamicBone.GetPresetLabel(presetValues[i]);

            SetComponentIcon();
        }
        
        private void SetComponentIcon()
        {
            // Chỉ tìm đúng file có tên là "CopyTransform" (icon của bạn) để tối ưu tốc độ quét
            string[] guids = AssetDatabase.FindAssets("spring t:Texture2D");

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

        // ── Wrapped Toolbar ───────────────────────────────────────────────────────

        /// <summary>
        /// Vẽ toolbar giống GUILayout.Toolbar nhưng tự xuống hàng khi
        /// inspector quá hẹp để chứa tất cả tab trên một hàng.
        /// </summary>
        private int DrawWrappedToolbar(int selected, string[] tabs, float tabMinWidth = 100f)
        {
            float viewWidth  = EditorGUIUtility.currentViewWidth - 20f;
            float tabPadding = 4f;

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

            DynamicBone script = (DynamicBone)target;

            EditorGUILayout.Space(3);
            currentTab = DrawWrappedToolbar(currentTab, tabTitles);
            EditorGUILayout.Space(10);

            switch (currentTab)
            {
                case 0:
                    DrawPresetTab(script);
                    break;
                case 1:
                    DrawBonesTab();
                    break;
                case 2:
                    DrawPhysicsTab();
                    break;
                case 3:
                    DrawDebugTab(script);
                    break;
            }

            EditorGUILayout.Space(5);
            serializedObject.ApplyModifiedProperties();
        }

        // ══════════════════════════════════════════════════════════
        //  TAB 0 — PRESET
        // ══════════════════════════════════════════════════════════
        private void DrawPresetTab(DynamicBone script)
        {
            EditorGUILayout.LabelField("Cloth Preset", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Dãy nút chọn preset — thay cho [EnumToggleButtons] của Odin
            EditorGUI.showMixedValue = clothPresetProp.hasMultipleDifferentValues;
            int newIndex = DrawWrappedToolbar(clothPresetProp.enumValueIndex, presetLabels, 90f);
            EditorGUI.showMixedValue = false;

            if (newIndex != clothPresetProp.enumValueIndex)
            {
                clothPresetProp.enumValueIndex = newIndex;

                // Áp preset ngay khi đổi — tương đương [OnValueChanged("OnPresetChanged")]
                serializedObject.ApplyModifiedProperties();
                ApplyPresetToAllTargets();
                serializedObject.Update();
            }

            EditorGUILayout.Space(6);

            ClothPreset current = (ClothPreset)clothPresetProp.enumValueIndex;
            EditorGUILayout.HelpBox(DynamicBone.GetPresetDescription(current), MessageType.Info);

            if (current != ClothPreset.Custom)
            {
                EditorGUILayout.Space(4);

                GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
                if (GUILayout.Button("▶  Áp Dụng Preset Ngay", GUILayout.Height(30f)))
                {
                    ApplyPresetToAllTargets();
                }

                GUI.backgroundColor = Color.white;

                EditorGUILayout.HelpBox(
                    "Preset sẽ ghi đè toàn bộ thông số ở tab Physics. Muốn giữ chỉnh tay, hãy chuyển sang chế độ Tùy Chỉnh.",
                    MessageType.None);
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Gọi ApplyPreset trên mọi target đang được chọn, có Undo đầy đủ.
        /// </summary>
        private void ApplyPresetToAllTargets()
        {
            foreach (var obj in targets)
            {
                var manager = obj as DynamicBone;
                if (manager == null) continue;

                Undo.RecordObject(manager, "Apply Cloth Preset");
                manager.ApplyCurrentPreset();
                EditorUtility.SetDirty(manager);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  TAB 1 — BONES & COLLIDERS
        // ══════════════════════════════════════════════════════════
        private void DrawBonesTab()
        {
            // ── ROOT BONES ───────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Root Bones", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(rootBonesProp, _gcRootBones, true);

            if (rootBonesProp.arraySize == 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    "Chưa có Root Bone nào. Thêm xương gốc (tóc, đuôi, tà áo...) để hệ thống dựng chuỗi vật lý.",
                    MessageType.Warning);
            }
            else if (enableClothLinksProp.boolValue)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    "Cloth Links đang bật: thứ tự trong danh sách này chính là thứ tự nối vải từ trái sang phải. " +
                    "Kéo thả để sắp lại nếu mặt vải bị xoắn.",
                    MessageType.None);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // ── CLOTH LINKS ──────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Cloth Links", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(enableClothLinksProp, _gcEnableClothLinks);

            EditorGUI.BeginDisabledGroup(!enableClothLinksProp.boolValue);
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.PropertyField(clothLinkClosedLoopProp, _gcClothClosedLoop);
                EditorGUILayout.PropertyField(clothLinkStiffnessProp, _gcClothStiffness);
                EditorGUILayout.PropertyField(clothLinkIterationsProp, _gcClothIterations);
            }
            EditorGUI.EndDisabledGroup();

            if (enableClothLinksProp.boolValue && rootBonesProp.arraySize < 2)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    "Cloth Links cần ít nhất 2 Root Bone mới có gì để nối. Hiện tại tính năng sẽ không chạy.",
                    MessageType.Warning);
            }

            if (clothLinkClosedLoopProp.boolValue && rootBonesProp.arraySize < 3)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    "Closed Loop cần ít nhất 3 Root Bone, nếu không cặp nối vòng sẽ trùng với cặp đã có.",
                    MessageType.Warning);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // ── COLLISION ────────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Collision", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(enableCollidersProp, _gcEnableColliders);

            EditorGUI.BeginDisabledGroup(!enableCollidersProp.boolValue);
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.PropertyField(particleRadiusProp, _gcParticleRadius);
                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(collidersProp, _gcColliders, true);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();
        }

        // ══════════════════════════════════════════════════════════
        //  TAB 2 — PHYSICS
        // ══════════════════════════════════════════════════════════
        private void DrawPhysicsTab()
        {
            bool presetLocked = !clothPresetProp.hasMultipleDifferentValues
                                && (ClothPreset)clothPresetProp.enumValueIndex != ClothPreset.Custom;

            if (presetLocked)
            {
                EditorGUILayout.HelpBox(
                    "Đang dùng preset dựng sẵn. Giá trị chỉnh tay ở đây sẽ bị ghi đè mỗi lần preset được áp dụng.",
                    MessageType.Warning);
                EditorGUILayout.Space(4);
            }

            // ── DAMPING & INERTIA ────────────────────────────────────────────────
            EditorGUILayout.LabelField("Damping & Inertia", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(dampingProp, _gcDamping);
            EditorGUILayout.PropertyField(inertiaProp, _gcInertia);
            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(collisionParentFalloffProp, _gcCollisionFalloff);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // ── SPRING ───────────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Spring", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(elasticityProp, _gcElasticity);
            EditorGUILayout.PropertyField(springStiffnessProp, _gcSpringStiffness);
            EditorGUILayout.PropertyField(springForceMultiplierProp, _gcSpringForceMul);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // ── GRAVITY ──────────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Gravity", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(gravityProp, _gcGravity);
            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(useRestPoseGravityProp, _gcUseRestPoseGravity);

            EditorGUI.BeginDisabledGroup(!useRestPoseGravityProp.boolValue);
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(restPoseGravityBlendProp, _gcRestPoseBlend);
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();
        }

        // ══════════════════════════════════════════════════════════
        //  TAB 3 — DEBUG & GIZMOS
        // ══════════════════════════════════════════════════════════
        private void DrawDebugTab(DynamicBone script)
        {
            // ── GIZMOS ───────────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Gizmos", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(showGizmosProp, _gcShowGizmos);

            EditorGUI.BeginDisabledGroup(!showGizmosProp.boolValue);
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.PropertyField(nodeGizmoSizeProp, _gcNodeGizmoSize);
                EditorGUILayout.PropertyField(arrowLengthMultiplierProp, _gcArrowLength);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // ── RUNTIME INFO ─────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Runtime Info", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUI.BeginDisabledGroup(!Application.isPlaying);
            if (GUILayout.Button("🔄  Re-Init Bone Trees", GUILayout.Height(24f)))
            {
                foreach (var obj in targets)
                {
                    var manager = obj as DynamicBone;
                    if (manager != null) manager.InitializeTrees();
                }
            }

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(4);

            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField("Active Chains", script.ActiveChainCount.ToString());
                EditorGUILayout.LabelField("Cloth Links", script.ClothLinkCount.ToString());
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Bone tree chỉ được dựng khi vào Play Mode (Start). Ngoài Play Mode, Gizmo vẽ trực tiếp từ hierarchy của Root Bones.",
                    MessageType.None);
            }

            EditorGUILayout.EndVertical();
        }
    }
}