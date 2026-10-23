#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace GaMo.BlenderEditorTools
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(DynamicBoneManager))]
    [InitializeOnLoad]
    public class DynamicBoneManagerEditor : Editor
    {
        private int currentTab = 0;

        private readonly string[] tabTitles =
            { "Performance", "LOD & Culling", "Runtime Info" };

        // ── GUIContent cache (tránh alloc mỗi lần repaint) ──────────────────────
        private GUIContent _gcCompleteImmediately;
        private GUIContent _gcSimulationBatchSize;
        private GUIContent _gcDebugLogging;

        private GUIContent _gcEnableLodAndCulling;
        private GUIContent _gcLodReferenceOverride;
        private GUIContent _gcCullMinDistance;
        private GUIContent _gcLod1Distance;
        private GUIContent _gcLod2Distance;
        private GUIContent _gcLod3Distance;
        private GUIContent _gcLod2FrameInterval;
        private GUIContent _gcCullWhenNotVisible;
        private GUIContent _gcMaxCatchupDt;

        // ── Properties ──────────────────────────────────────────────────────────
        private SerializedProperty completeImmediateProp;
        private SerializedProperty simulationBatchSizeProp;
        private SerializedProperty debugLoggingProp;

        private SerializedProperty enableLodAndCullingProp;
        private SerializedProperty lodReferenceOverrideProp;
        private SerializedProperty cullMinDistanceProp;
        private SerializedProperty lod1DistanceProp;
        private SerializedProperty lod2DistanceProp;
        private SerializedProperty lod3DistanceProp;
        private SerializedProperty lod2FrameIntervalProp;
        private SerializedProperty cullWhenNotVisibleProp;
        private SerializedProperty maxCatchupDtProp;

        static DynamicBoneManagerEditor()
        {
            EditorApplication.delayCall += ApplyIconToAll;
            EditorApplication.hierarchyChanged -= ApplyIconToAll;
            EditorApplication.hierarchyChanged += ApplyIconToAll;
        }

        private static void ApplyIconToAll()
            => ComponentIconApplier.ApplyToAll<DynamicBoneManager>("dynamicmanager");

        private void OnEnable()
        {
            completeImmediateProp   = serializedObject.FindProperty("completeImmediately");
            simulationBatchSizeProp = serializedObject.FindProperty("simulationBatchSize");
            debugLoggingProp        = serializedObject.FindProperty("debugLogging");

            enableLodAndCullingProp = serializedObject.FindProperty("enableLodAndCulling");
            lodReferenceOverrideProp = serializedObject.FindProperty("lodReferenceOverride");
            cullMinDistanceProp    = serializedObject.FindProperty("cullMinDistance");
            lod1DistanceProp       = serializedObject.FindProperty("lod1Distance");
            lod2DistanceProp       = serializedObject.FindProperty("lod2Distance");
            lod3DistanceProp       = serializedObject.FindProperty("lod3Distance");
            lod2FrameIntervalProp  = serializedObject.FindProperty("lod2FrameInterval");
            cullWhenNotVisibleProp = serializedObject.FindProperty("cullWhenNotVisible");
            maxCatchupDtProp       = serializedObject.FindProperty("maxCatchupDt");

            _gcCompleteImmediately  = new GUIContent("Complete Immediately", completeImmediateProp.tooltip);
            _gcSimulationBatchSize  = new GUIContent("Simulation Batch Size", simulationBatchSizeProp.tooltip);
            _gcDebugLogging         = new GUIContent("Debug Logging", debugLoggingProp.tooltip);

            _gcEnableLodAndCulling  = new GUIContent("Enable LOD & Culling", enableLodAndCullingProp.tooltip);
            _gcLodReferenceOverride = new GUIContent("Reference Override", lodReferenceOverrideProp.tooltip);
            _gcCullMinDistance      = new GUIContent("Cull Min Distance", cullMinDistanceProp.tooltip);
            _gcLod1Distance         = new GUIContent("LOD1 Distance", lod1DistanceProp.tooltip);
            _gcLod2Distance         = new GUIContent("LOD2 Distance", lod2DistanceProp.tooltip);
            _gcLod3Distance         = new GUIContent("LOD3 Distance", lod3DistanceProp.tooltip);
            _gcLod2FrameInterval    = new GUIContent("LOD2 Frame Interval", lod2FrameIntervalProp.tooltip);
            _gcCullWhenNotVisible   = new GUIContent("Cull When Not Visible", cullWhenNotVisibleProp.tooltip);
            _gcMaxCatchupDt         = new GUIContent("Max Catchup Dt", maxCatchupDtProp.tooltip);

            //SetComponentIcon();
        }

        //private void SetComponentIcon() => ComponentIconApplier.ApplyToObject(target, "dynamicmanager");

        // ── Wrapped Toolbar (giống hệt DynamicBoneEditor) ───────────────────────
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

            DynamicBoneManager script = (DynamicBoneManager)target;

            DrawDuplicateManagerWarning(script);

            EditorGUILayout.Space(3);
            currentTab = DrawWrappedToolbar(currentTab, tabTitles);
            EditorGUILayout.Space(10);

            switch (currentTab)
            {
                case 0:
                    DrawPerformanceTab();
                    break;
                case 1:
                    DrawLodTab();
                    break;
                case 2:
                    DrawRuntimeInfoTab(script);
                    break;
            }

            EditorGUILayout.Space(5);
            serializedObject.ApplyModifiedProperties();
        }

        // ══════════════════════════════════════════════════════════
        //  Cảnh báo nếu scene có nhiều hơn 1 DynamicBoneManager — nguồn lỗi phổ biến nhất
        //  (1 cái tự huỷ lúc Awake() do phát hiện trùng, nhưng người dùng vẫn đang chỉnh tay
        //  vào bản sẽ-bị-huỷ nếu Editor mở sẵn từ trước khi Play).
        // ══════════════════════════════════════════════════════════
        private void DrawDuplicateManagerWarning(DynamicBoneManager script)
        {
#if UNITY_2023_1_OR_NEWER
            var all = Object.FindObjectsByType<DynamicBoneManager>(FindObjectsSortMode.None);
#else
            var all = Object.FindObjectsOfType<DynamicBoneManager>();
#endif
            if (all.Length <= 1) return;

            EditorGUILayout.Space(3);
            EditorGUILayout.HelpBox(
                $"Scene đang có {all.Length} DynamicBoneManager cùng lúc. Lúc vào Play, chỉ 1 bản " +
                "sống sót (Awake() tự huỷ bản dư thừa) — nếu bạn đang chỉnh Inspector trên bản sẽ bị " +
                "huỷ, thay đổi sẽ không có tác dụng lúc chạy game. Chỉ nên giữ 1 DynamicBoneManager " +
                "trong scene (thường không cần tự đặt tay — nó tự sinh khi cần).",
                MessageType.Warning);
        }

        // ══════════════════════════════════════════════════════════
        //  TAB 0 — PERFORMANCE
        // ══════════════════════════════════════════════════════════
        private void DrawPerformanceTab()
        {
            // ── SCHEDULING ───────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Job Scheduling", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(completeImmediateProp, _gcCompleteImmediately);

            if (completeImmediateProp.boolValue)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    "Bật: Schedule + Complete đều nằm trong LateUpdate() — job simulate/ghi rotation " +
                    "chỉ bắt đầu SAU khi Animator đã evaluate xong frame này, đảm bảo DynamicBone luôn " +
                    "ghi đè đúng thứ tự lên bone animation. Đổi lại mất khả năng chạy song song job " +
                    "với engine ở nửa đầu frame (tốn CPU hơn 1 chút).",
                    MessageType.None);
            }
            else
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    "Tắt (mặc định): Schedule ở Update(), Complete() ở LateUpdate() — job chạy song " +
                    "song với engine nên rẻ hơn. LƯU Ý: nếu bone đang bị Animator/IK/Rig khác điều " +
                    "khiển, thứ tự ghi Transform thực tế có thể KHÔNG như bạn nghĩ (Unity có thể ép " +
                    "hoàn tất job này trước khi Animator chạy) — nếu thấy DynamicBone bị Animator ghi " +
                    "đè mất hiệu ứng, thử bật Complete Immediately trước tiên.",
                    MessageType.Info);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // ── BATCHING ─────────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Job Batching", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(simulationBatchSizeProp, _gcSimulationBatchSize);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            // ── DEBUG ────────────────────────────────────────────────────────────
            EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(debugLoggingProp, _gcDebugLogging);
            EditorGUILayout.EndVertical();
        }

        // ══════════════════════════════════════════════════════════
        //  TAB 1 — LOD & CULLING
        // ══════════════════════════════════════════════════════════
        private void DrawLodTab()
        {
            EditorGUILayout.LabelField("LOD & Culling", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(enableLodAndCullingProp, _gcEnableLodAndCulling);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            EditorGUI.BeginDisabledGroup(!enableLodAndCullingProp.boolValue);
            {
                // ── REFERENCE ────────────────────────────────────────────────────
                EditorGUILayout.LabelField("Reference Point", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(lodReferenceOverrideProp, _gcLodReferenceOverride);
                if (lodReferenceOverrideProp.objectReferenceValue == null)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.HelpBox("Để trống: dùng Camera.main mỗi frame.", MessageType.None);
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(6);

                // ── DISTANCE THRESHOLDS ──────────────────────────────────────────
                EditorGUILayout.LabelField("Distance Thresholds", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(cullMinDistanceProp, _gcCullMinDistance);
                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(lod1DistanceProp, _gcLod1Distance);
                EditorGUILayout.PropertyField(lod2DistanceProp, _gcLod2Distance);
                EditorGUILayout.PropertyField(lod3DistanceProp, _gcLod3Distance);

                if (lod1DistanceProp.floatValue > lod2DistanceProp.floatValue ||
                    lod2DistanceProp.floatValue > lod3DistanceProp.floatValue)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.HelpBox(
                        "Thứ tự khoảng cách nên tăng dần: LOD1 < LOD2 < LOD3. Thứ tự hiện tại có thể " +
                        "gây kết quả LOD không như mong đợi.",
                        MessageType.Warning);
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(6);

                // ── LOD2 / CULLING BEHAVIOUR ─────────────────────────────────────
                EditorGUILayout.LabelField("Behaviour", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.PropertyField(lod2FrameIntervalProp, _gcLod2FrameInterval);
                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(cullWhenNotVisibleProp, _gcCullWhenNotVisible);
                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(maxCatchupDtProp, _gcMaxCatchupDt);
                EditorGUILayout.EndVertical();
            }
            EditorGUI.EndDisabledGroup();
        }

        // ══════════════════════════════════════════════════════════
        //  TAB 2 — RUNTIME INFO
        // ══════════════════════════════════════════════════════════
        private void DrawRuntimeInfoTab(DynamicBoneManager script)
        {
            EditorGUILayout.LabelField("Runtime Info", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Chỉ có dữ liệu khi đang ở Play Mode — buffer toàn cục được dựng ở lần " +
                    "RebuildNativeArrays() đầu tiên sau khi các DynamicBone đăng ký.",
                    MessageType.None);
            }
            else if (targets.Length > 1)
            {
                // Multi-edit: thông số runtime là của riêng từng instance => không gộp được
                EditorGUILayout.HelpBox("Đang chọn nhiều DynamicBoneManager: chọn 1 object để xem thông số Runtime.",
                    MessageType.None);
            }
            else
            {
                EditorGUILayout.LabelField("Trạng thái Instance",
                    script.IsActiveInstance ? "Đang hoạt động (singleton)" : "BẢN DƯ THỪA — sẽ bị Destroy");
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("DynamicBone đã đăng ký", script.RegisteredCount.ToString());
                EditorGUILayout.LabelField("Tổng số node", script.TotalNodeCount.ToString());
                EditorGUILayout.LabelField("Tổng số chain", script.TotalChainCount.ToString());
                EditorGUILayout.LabelField("Tổng số collider", script.TotalColliderCount.ToString());
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.BeginDisabledGroup(!Application.isPlaying);
            if (GUILayout.Button("🔄  Force Rebuild Buffers", GUILayout.Height(24f)))
            {
                // Multi-edit: áp cho tất cả object đang chọn
                foreach (var tg in targets)
                {
                    var mgr = tg as DynamicBoneManager;
                    if (mgr != null) mgr.MarkDirty();
                }
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();
        }
    }
}
#endif