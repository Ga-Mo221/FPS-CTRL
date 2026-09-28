#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace GaMo.BlenderEditorTools
{
    [CustomEditor(typeof(DynamicBone))]
    [CanEditMultipleObjects]
    [InitializeOnLoad]
    public class DynamicBoneEditor : Editor
    {
        private int currentTab = 0;
        private bool _showCacheFoldout = false;
        private bool _showCollisionFoldout = false;
        private bool _showLinksFoldout = false;
        private Vector2 _cachedListScroll;

        private readonly string[] tabTitles =
            { "Bones & Colliders", "Physics", "Debug & Gizmos" };

        // ── GUIContent cache (tránh alloc mỗi lần repaint) ──────────────────────
        private GUIContent _gcRootBones;
        private GUIContent _gcEnableColliders;
        private GUIContent _gcParticleRadius;
        private GUIContent _gcColliders;
        private GUIContent _gcCollisionSolveIterations;
        private GUIContent _gcDamping;
        private GUIContent _gcInertia;
        private GUIContent _gcCollisionFalloff;
        private GUIContent _gcSpringStiffness;
        private GUIContent _gcSpringForceMul;
        private GUIContent _gcDepthFalloff;
        private GUIContent _gcGravity;
        private GUIContent _gcUseRestPoseGravity;
        private GUIContent _gcRestPoseBlend;
        private GUIContent _gcShowGizmos;
        private GUIContent _gcNodeGizmoSize;
        private GUIContent _gcArrowLength;
        private GUIContent _gcCullingRenderer;

        // ── Properties ──────────────────────────────────────────────────────────
        private SerializedProperty rootBonesProp;
        private SerializedProperty enableCollidersProp;
        private SerializedProperty particleRadiusProp;
        private SerializedProperty collidersProp;
        private SerializedProperty collisionSolveIterationsProp;
        private SerializedProperty excludedBonesProp;
        private SerializedProperty chainLinkModeProp;
        private SerializedProperty linkStiffnessProp;
        private SerializedProperty linkIterationsProp;

        private SerializedProperty dampingProp;
        private SerializedProperty inertiaProp;
        private SerializedProperty collisionParentFalloffProp;
        private SerializedProperty springStiffnessProp;
        private SerializedProperty springForceMultiplierProp;
        private SerializedProperty depthFalloffProp;
        private SerializedProperty gravityProp;
        private SerializedProperty useRestPoseGravityProp;
        private SerializedProperty restPoseGravityBlendProp;

        private SerializedProperty showGizmosProp;
        private SerializedProperty nodeGizmoSizeProp;
        private SerializedProperty arrowLengthMultiplierProp;
        private SerializedProperty cullingRendererProp;

        static DynamicBoneEditor()
        {
            EditorApplication.delayCall += ApplyIconToAll;
            EditorApplication.hierarchyChanged -= ApplyIconToAll;
            EditorApplication.hierarchyChanged += ApplyIconToAll;
        }

        private static void ApplyIconToAll()
            => ComponentIconApplier.ApplyToAll<DynamicBone>("spring");

        private void OnEnable()
        {
            rootBonesProp       = serializedObject.FindProperty("rootBones");
            enableCollidersProp = serializedObject.FindProperty("enableColliders");
            particleRadiusProp  = serializedObject.FindProperty("particleRadius");
            collidersProp       = serializedObject.FindProperty("colliders");
            collisionSolveIterationsProp = serializedObject.FindProperty("collisionSolveIterations");
            excludedBonesProp  = serializedObject.FindProperty("excludedBones");
            chainLinkModeProp  = serializedObject.FindProperty("chainLinkMode");
            linkStiffnessProp  = serializedObject.FindProperty("linkStiffness");
            linkIterationsProp = serializedObject.FindProperty("linkIterations");

            dampingProp                = serializedObject.FindProperty("damping");
            inertiaProp                = serializedObject.FindProperty("inertia");
            collisionParentFalloffProp = serializedObject.FindProperty("collisionParentFalloff");
            springStiffnessProp        = serializedObject.FindProperty("springStiffness");
            springForceMultiplierProp  = serializedObject.FindProperty("springForceMultiplier");
            depthFalloffProp           = serializedObject.FindProperty("depthFalloff");
            gravityProp                = serializedObject.FindProperty("gravity");
            useRestPoseGravityProp     = serializedObject.FindProperty("useRestPoseGravity");
            restPoseGravityBlendProp   = serializedObject.FindProperty("restPoseGravityBlend");

            showGizmosProp            = serializedObject.FindProperty("showGizmos");
            nodeGizmoSizeProp         = serializedObject.FindProperty("nodeGizmoSize");
            arrowLengthMultiplierProp = serializedObject.FindProperty("arrowLengthMultiplier");
            cullingRendererProp       = serializedObject.FindProperty("cullingRenderer");

            _gcRootBones          = new GUIContent("Root Bones", rootBonesProp.tooltip);
            _gcEnableColliders    = new GUIContent("Enable Colliders", enableCollidersProp.tooltip);
            _gcParticleRadius     = new GUIContent("Particle Radius", particleRadiusProp.tooltip);
            _gcColliders          = new GUIContent("Colliders List", collidersProp.tooltip);
            _gcCollisionSolveIterations = new GUIContent("Solve Iterations", collisionSolveIterationsProp.tooltip);
            _gcDamping            = new GUIContent("Damping", dampingProp.tooltip);
            _gcInertia            = new GUIContent("Inertia", inertiaProp.tooltip);
            _gcCollisionFalloff   = new GUIContent("Collision Parent Falloff", collisionParentFalloffProp.tooltip);
            _gcSpringStiffness    = new GUIContent("Spring Stiffness", springStiffnessProp.tooltip);
            _gcSpringForceMul     = new GUIContent("Spring Force Multiplier", springForceMultiplierProp.tooltip);
            _gcDepthFalloff       = new GUIContent("Depth Falloff", depthFalloffProp.tooltip);
            _gcGravity            = new GUIContent("Gravity", gravityProp.tooltip);
            _gcUseRestPoseGravity = new GUIContent("Use Rest Pose Gravity", useRestPoseGravityProp.tooltip);
            _gcRestPoseBlend      = new GUIContent("Rest Pose Blend", restPoseGravityBlendProp.tooltip);
            _gcShowGizmos         = new GUIContent("Show Gizmos", showGizmosProp.tooltip);
            _gcNodeGizmoSize      = new GUIContent("Node Gizmo Size", nodeGizmoSizeProp.tooltip);
            _gcCullingRenderer    = new GUIContent("Culling Renderer (LOD)", cullingRendererProp.tooltip);

            //SetComponentIcon();
        }

        //private void SetComponentIcon() => ComponentIconApplier.ApplyToObject(target, "spring");

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
                    DrawBonesTab(script);
                    break;
                case 1:
                    DrawPhysicsTab();
                    break;
                case 2:
                    DrawDebugTab(script);
                    break;
            }

            EditorGUILayout.Space(5);
            serializedObject.ApplyModifiedProperties();
        }

        // ══════════════════════════════════════════════════════════
        //  TAB 0 — BONES & COLLIDERS
        // ══════════════════════════════════════════════════════════
        private void DrawBonesTab(DynamicBone script)
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

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // ── CACHE BONES (thủ công) ───────────────────────────────────────────
            DrawCacheBonesSection(script);

            EditorGUILayout.Space(6);

            // ── COLLISION (thu gọn) ──────────────────────────────────────────────
            // 1 hàng header: foldout + số collider + toggle Enable ở mép phải. Mặc định GẬP —
            // chỉ khi mở mới hiện Particle Radius / Solve Iterations / danh sách collider.
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            using (new EditorGUILayout.HorizontalScope())
            {
                string title = enableCollidersProp.boolValue
                    ? $"Collision  ({collidersProp.arraySize})"
                    : "Collision  (off)";
                _showCollisionFoldout = EditorGUILayout.Foldout(_showCollisionFoldout, title, true, EditorStyles.foldout);
                GUILayout.FlexibleSpace();
                EditorGUILayout.PropertyField(enableCollidersProp, GUIContent.none, GUILayout.Width(16));
            }

            if (_showCollisionFoldout)
            {
                EditorGUI.BeginDisabledGroup(!enableCollidersProp.boolValue);
                {
                    EditorGUILayout.PropertyField(particleRadiusProp, _gcParticleRadius);
                    EditorGUILayout.PropertyField(collisionSolveIterationsProp, _gcCollisionSolveIterations);
                    // Mỗi phần tử là 1 component DynamicBoneCollider độc lập (gắn trên bone) — chỉ cần
                    // kéo-thả GameObject vào đây, cấu hình Type/Size/Offset nằm trên Inspector của component đó.
                    EditorGUILayout.PropertyField(collidersProp, _gcColliders, true);
                    if (collidersProp.arraySize == 0)
                        EditorGUILayout.LabelField("Chưa có collider — kéo GameObject có DynamicBoneCollider vào list.", EditorStyles.miniLabel);
                }
                EditorGUI.EndDisabledGroup();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(2);
            DrawCrossLinksSection(script);
        }

        // ══════════════════════════════════════════════════════════
        //  CROSS LINKS — nối các chain kề nhau (thứ tự Root Bones = thứ tự kề nhau thật)
        // ══════════════════════════════════════════════════════════
        private void DrawCrossLinksSection(DynamicBone script)
        {
            var mode = (DynamicBone.ChainLinkMode)chainLinkModeProp.enumValueIndex;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            string title = mode == DynamicBone.ChainLinkMode.Off
                ? "Cross Links  (off)"
                : $"Cross Links  ({mode}, {script.LinkCount} links)";
            _showLinksFoldout = EditorGUILayout.Foldout(_showLinksFoldout, title, true, EditorStyles.foldout);

            if (_showLinksFoldout)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(chainLinkModeProp, new GUIContent("Mode", chainLinkModeProp.tooltip));
                bool modeChanged = EditorGUI.EndChangeCheck();

                EditorGUI.BeginDisabledGroup(mode == DynamicBone.ChainLinkMode.Off);
                EditorGUILayout.PropertyField(linkStiffnessProp, new GUIContent("Stiffness", linkStiffnessProp.tooltip));
                EditorGUILayout.PropertyField(linkIterationsProp, new GUIContent("Iterations", linkIterationsProp.tooltip));
                EditorGUI.EndDisabledGroup();

                if (mode != DynamicBone.ChainLinkMode.Off)
                {
                    if (rootBonesProp.arraySize < 2)
                        EditorGUILayout.HelpBox("Cần ít nhất 2 Root Bone để nối chain.", MessageType.Warning);
                    else
                        EditorGUILayout.LabelField("Root Bones phải xếp theo thứ tự kề nhau (A, B, C...).", EditorStyles.miniLabel);
                }

                // Đổi Mode làm thay đổi CẤU TRÚC (danh sách link + nhóm job) → dựng lại cây ngay, để manager
                // rebuild buffer (lúc Play) và gizmo thấy kết quả tức thì (lúc Edit).
                if (modeChanged)
                {
                    serializedObject.ApplyModifiedProperties();
                    foreach (UnityEngine.Object t in targets)
                        if (t is DynamicBone db) db.InitializeTrees();
                }
            }
            EditorGUILayout.EndVertical();
        }

        // ══════════════════════════════════════════════════════════
        //  CACHE BONES (thủ công) — quét hierarchy từ Root Bones, cho phép loại trừ
        //  GameObject không phải bone thật (phụ kiện, decal...) trước khi vào Play.
        //  Runtime (kể cả build thật) vẫn TỰ ĐỘNG gọi InitializeTrees() lúc OnEnable để mô
        //  phỏng luôn hoạt động, nhưng nó sẽ tôn trọng danh sách excludedBones đã lưu ở đây —
        //  nên chỉ cần chỉnh 1 lần trong Editor, không cần bấm nút mỗi lần chạy game.
        // ══════════════════════════════════════════════════════════
        private void DrawCacheBonesSection(DynamicBone script)
        {
            EditorGUILayout.LabelField("Cache Bones", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.HelpBox(
                "Quét bone từ Root Bones. Mở rộng bên dưới để loại các GameObject không phải bone thật.",
                MessageType.Info);

            EditorGUILayout.Space(4);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("🔄  Cache Bones", GUILayout.Height(24f)))
                {
                    foreach (var obj in targets)
                        (obj as DynamicBone)?.InitializeTrees();
                    _showCacheFoldout = true; // tự mở ra sau khi cache để thấy kết quả ngay
                }

                EditorGUI.BeginDisabledGroup(excludedBonesProp.arraySize == 0);
                if (GUILayout.Button("Reset loại trừ", GUILayout.Height(24f), GUILayout.Width(110f)))
                {
                    foreach (var obj in targets)
                    {
                        var db = obj as DynamicBone;
                        if (db == null) continue;
                        Undo.RecordObject(db, "Reset Excluded Bones");
                        db.ClearExclusions();
                        EditorUtility.SetDirty(db);
                    }
                    serializedObject.Update();
                }
                EditorGUI.EndDisabledGroup();
            }

            // Danh sách chi tiết (xem/loại trừ từng bone) chỉ hỗ trợ khi chọn đúng 1 component —
            // với multi-edit, mỗi object có cây khác nhau nên hiển thị gộp sẽ gây nhầm lẫn.
            if (targets.Length == 1)
            {
                int cachedCount = script.CachedBoneTransforms?.Count ?? 0;
                int excludedCount = script.excludedBones.Count;

                EditorGUILayout.Space(4);
                _showCacheFoldout = EditorGUILayout.Foldout(
                    _showCacheFoldout,
                    $"Chi tiết ({cachedCount} đã cache, {excludedCount} đã loại trừ)",
                    true);

                if (_showCacheFoldout)
                {
                    EditorGUI.indentLevel++;
                    DrawCachedBoneList(script);
                    DrawExcludedBoneList(script);
                    EditorGUI.indentLevel--;
                }
            }
            else
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    "Chọn đúng 1 component để xem/loại trừ từng bone cụ thể.", MessageType.None);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawCachedBoneList(DynamicBone script)
        {
            var cached = script.CachedBoneTransforms;
            if (cached == null || cached.Count == 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox("Chưa cache bone nào — bấm 'Cache Bones' ở trên.", MessageType.None);
                return;
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField($"Đã cache {cached.Count} bone:", EditorStyles.miniBoldLabel);

            // Giới hạn chiều cao bằng ScrollView khi danh sách dài (VD chain tóc/đuôi nhiều đốt) —
            // tránh Inspector bị kéo dài quá mức. Ngưỡng 8 dòng ~ chiều cao 1 hàng button (~20px).
            const int rowHeight = 20;
            const int maxVisibleRows = 8;
            bool needsScroll = cached.Count > maxVisibleRows;

            if (needsScroll)
                _cachedListScroll = EditorGUILayout.BeginScrollView(
                    _cachedListScroll, GUI.skin.box, GUILayout.Height(maxVisibleRows * rowHeight));
            else
                EditorGUILayout.BeginVertical(GUI.skin.box);

            for (int i = 0; i < cached.Count; i++)
            {
                Transform t = cached[i];
                if (t == null) continue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.ObjectField(t, typeof(Transform), true);
                    EditorGUI.EndDisabledGroup();

                    if (GUILayout.Button("X", GUILayout.Width(22f)))
                    {
                        Undo.RecordObject(script, "Exclude Bone");
                        script.ExcludeBone(t);
                        EditorUtility.SetDirty(script);
                        serializedObject.Update();
                        // Mảng vừa đổi kích thước giữa lúc đang vẽ list — thoát GUI ngay để tránh
                        // lỗi "GUILayout: Mismatched LayoutGroup" ở lần repaint kế tiếp.
                        GUIUtility.ExitGUI();
                    }
                }
            }

            if (needsScroll)
                EditorGUILayout.EndScrollView();
            else
                EditorGUILayout.EndVertical();
        }

        private void DrawExcludedBoneList(DynamicBone script)
        {
            if (script.excludedBones.Count == 0) return;

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField($"Đã loại trừ ({script.excludedBones.Count}):", EditorStyles.miniBoldLabel);

            EditorGUILayout.BeginVertical(GUI.skin.box);
            for (int i = script.excludedBones.Count - 1; i >= 0; i--)
            {
                Transform t = script.excludedBones[i];

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.ObjectField(t, typeof(Transform), true);
                    EditorGUI.EndDisabledGroup();

                    if (GUILayout.Button("+", GUILayout.Width(22f)))
                    {
                        Undo.RecordObject(script, "Include Bone");
                        script.IncludeBone(t);
                        EditorUtility.SetDirty(script);
                        serializedObject.Update();
                        GUIUtility.ExitGUI();
                    }
                }
            }
            EditorGUILayout.EndVertical();
        }

        // ══════════════════════════════════════════════════════════
        //  TAB 1 — PHYSICS
        // ══════════════════════════════════════════════════════════
        private void DrawPhysicsTab()
        {
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
            EditorGUILayout.PropertyField(springStiffnessProp, _gcSpringStiffness);
            EditorGUILayout.PropertyField(springForceMultiplierProp, _gcSpringForceMul);
            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(depthFalloffProp, _gcDepthFalloff);
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
        //  TAB 2 — DEBUG & GIZMOS
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
                //EditorGUILayout.PropertyField(arrowLengthMultiplierProp, _gcArrowLength);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // ── LOD / CULLING ────────────────────────────────────────────────────
            // Trước đây field này tồn tại trong DynamicBone.cs (có Tooltip đầy đủ) nhưng
            // KHÔNG được vẽ ở đâu trong Inspector, nên user không có cách nào override tay
            // khi auto-detect (GetComponentInParent/Children) chọn nhầm Renderer — ví dụ nhân
            // vật có nhiều SkinnedMeshRenderer con (tóc/áo/thân tách mesh riêng).
            EditorGUILayout.LabelField("LOD / Culling", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(cullingRendererProp, _gcCullingRenderer);
            if (cullingRendererProp.objectReferenceValue == null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    "Để trống: hệ thống sẽ tự tìm Renderer gần nhất (cha rồi tới con) lúc OnEnable. " +
                    "Chỉ cần gán tay nếu auto-detect chọn sai Renderer (VD nhân vật có nhiều " +
                    "SkinnedMeshRenderer tách mesh riêng cho tóc/áo/thân).",
                    MessageType.None);
            }
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
#endif