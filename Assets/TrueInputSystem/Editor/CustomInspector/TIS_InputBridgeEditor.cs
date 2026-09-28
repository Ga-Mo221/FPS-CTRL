#if UNITY_EDITOR
namespace GaMo.TrueInputSystem
{
    using UnityEditor;
    using UnityEngine;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine.InputSystem;

    [CustomEditor(typeof(TISInputBridge))]
    [InitializeOnLoad]
    public class TISInputBridgeEditor : UnityEditor.Editor
    {
        private int           _tab = 0;
        private readonly string[] _tabs = { "Settings", "Debug" };
        private Vector2       _debugScroll;
        private double        _nextRepaint;

        // Định nghĩa Struct Debug ngay tại file Editor để tự quản lý dữ liệu vẽ bảng
        public struct ActionDebugRow
        {
            public string Map;
            public string Action;
            public string Type;
            public string Active;
            public string Value;
            public string Source;
            public bool   ActiveBool;
        }
        
        static TISInputBridgeEditor()
        {
            EditorApplication.delayCall += ApplyIconToAll;
            EditorApplication.hierarchyChanged -= ApplyIconToAll;
            EditorApplication.hierarchyChanged += ApplyIconToAll;
        }
        private static void ApplyIconToAll()
            => ComponentIconApplier.ApplyToAll<TISInputBridge>("input");


        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            //SetComponentIcon();
        }

        //private void SetComponentIcon() => ComponentIconApplier.ApplyToObject(target, "input");
        
        private void OnDisable() => EditorApplication.update -= OnEditorUpdate;

        private void OnEditorUpdate()
        {
            if (!EditorApplication.isPlaying) return;
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextRepaint) return;
            _nextRepaint = now + 0.05;
            Repaint();
        }

        // ── Wrapped Toolbar ───────────────────────────────────────────────────────

        /// <summary>
        /// Vẽ toolbar giống GUILayout.Toolbar nhưng tự xuống hàng khi
        /// inspector quá hẹp để chứa tất cả tab trên một hàng.
        /// </summary>
        private int DrawWrappedToolbar(int selected, string[] tabs)
        {
            float viewWidth   = EditorGUIUtility.currentViewWidth - 20f;
            float tabMinWidth = 60f;
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
            var bridge = (TISInputBridge)target;
            _tab = DrawWrappedToolbar(_tab, _tabs);
            EditorGUILayout.Space(4);

            if (_tab == 0) DrawSettingsTab();
            else           DrawDebugTab(bridge);
        }

        private void DrawSettingsTab()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("_profile"), new GUIContent("Profile"));
            EditorGUILayout.Space(4);

            var propVerbose = serializedObject.FindProperty("_verboseLog");
            EditorGUILayout.PropertyField(propVerbose, new GUIContent("Verbose Log"));

            var propDDOL = serializedObject.FindProperty("_dontDestroyOnLoad");
            EditorGUILayout.PropertyField(propDDOL, new GUIContent("Don't Destroy On Load"));
            if (propDDOL.boolValue)
                EditorGUILayout.HelpBox("DontDestroyOnLoad đang BẬT — TISInputBridge sẽ tồn tại xuyên scene. Đảm bảo chỉ có một instance trong toàn bộ game.", MessageType.Info);

            var propSimMobile = serializedObject.FindProperty("_simulateMobileInEditor");
            EditorGUILayout.PropertyField(propSimMobile, new GUIContent("Simulate Mobile In Editor"));
            if (propSimMobile.boolValue)
                EditorGUILayout.HelpBox("Simulate Mobile đang BẬT — keyboard/mouse sẽ không trigger input.", MessageType.Warning);

            var propDebugSkill = serializedObject.FindProperty("_debugSkillAction");
            EditorGUILayout.PropertyField(propDebugSkill, new GUIContent("Debug Skill Action Log"));
            if (propDebugSkill.boolValue)
                EditorGUILayout.HelpBox("Debug Skill Log đang BẬT — log nguồn action mỗi lần trigger.", MessageType.Info);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Object Tracker", EditorStyles.boldLabel);

            var propEnableTracker = serializedObject.FindProperty("_enableObjectTracker");
            EditorGUILayout.PropertyField(propEnableTracker, new GUIContent("Enable Object Tracker"));

            if (propEnableTracker.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_trackerCamera"), new GUIContent("Raycast Camera"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_trackerDoubleTapInterval"), new GUIContent("Double-Tap Interval"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_trackerDoubleTapDistancePx"), new GUIContent("Double-Tap Distance (px)"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_trackerLayerMask"), new GUIContent("Layer Mask"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_trackerRaycastMaxDistance"), new GUIContent("Raycast Max Distance"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_trackerUse2DPhysics"), new GUIContent("Use 2D Physics"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_trackerDebugLog"), new GUIContent("Debug Log"));
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawDebugTab(TISInputBridge bridge)
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Tab Debug chỉ hiển thị dữ liệu khi đang Play Mode.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Profile Info", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            // Lấy dữ liệu gián tiếp thông qua Reflection hoặc Property hệ thống nếu chuyển dịch hoàn toàn
            EditorGUILayout.LabelField(GetDbProfileInfo(bridge),  EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField(GetDbPlatformInfo(bridge), EditorStyles.wordWrappedMiniLabel);
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Actions (live)", EditorStyles.boldLabel);

            // Gọi hàm Build nội bộ của Editor
            var rows = BuildDebugRowsInternal(bridge);
            _debugScroll = EditorGUILayout.BeginScrollView(_debugScroll, GUILayout.MaxHeight(300));

            DrawDebugTableRow("MAP", "ACTION", "TYPE", "ACTIVE", "VALUE", "SOURCE", isHeader: true);
            EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), new Color(0.3f, 0.3f, 0.3f));

            foreach (var row in rows)
            {
                Color bg = row.ActiveBool
                    ? new Color(0.15f, 0.35f, 0.15f, 0.4f)
                    : row.Active == "---" ? new Color(0.2f, 0.2f, 0.2f, 0.2f) : Color.clear;

                Rect rowRect = EditorGUILayout.GetControlRect(false, 18);
                if (bg != Color.clear) EditorGUI.DrawRect(rowRect, bg);

                DrawDebugTableRowFixed(rowRect, row.Map, row.Action, row.Type,
                    row.Active, row.Value, row.Source, row.ActiveBool);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField("Mobile Registry", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(GetDbMobileRegistry(bridge), EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Object Tracker", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(GetDbObjectTrackerInfo(bridge), EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Log Profile", GUILayout.Height(28)))
                bridge.LogActiveProfile();

            GUI.backgroundColor = new Color(0.4f, 1f, 0.6f);
            if (GUILayout.Button("Validate", GUILayout.Height(28)))
            {
                // Gọi hàm Validate private bằng reflection từ Bridge
                var method = typeof(TISInputBridge).GetMethod("ValidateProfileMenu",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(bridge, null);
            }
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = new Color(1f, 0.5f, 0.4f);
            if (GUILayout.Button("Clear Mobile Registry", GUILayout.Height(28)))
                TISMobileButtonRegistry.ClearAll();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(1f, 0.75f, 0.4f);
            if (GUILayout.Button("Log Toucher Objects", GUILayout.Height(24))) bridge.ToucherLogObjects();
            if (GUILayout.Button("Clear Toucher",       GUILayout.Height(24))) bridge.ToucherClearAll();
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        // ── LOGIC CHUYỂN ĐỔI TỪ RUNTIME SANG EDITOR ──────────────────────────────

        private string GetDbProfileInfo(TISInputBridge bridge)
        {
            // Sử dụng Reflection để đọc trường private _activeProfile từ Runtime DLL
            var field = typeof(TISInputBridge).GetField("_activeProfile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var activeProfile = field?.GetValue(bridge) as TISInputProfileConfig;

            if (activeProfile == null) return "-- Chưa load profile --";
            return $"Profile: {activeProfile.profileName}   |   " +
                   $"{activeProfile.actionMaps?.Count ?? 0} maps   " +
                   $"{activeProfile.actionMaps?.Sum(m => m.actions?.Count ?? 0) ?? 0} actions";
        }

        private string GetDbPlatformInfo(TISInputBridge bridge)
        {
            var fieldSim = typeof(TISInputBridge).GetField("_simulateMobileInEditor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            bool simMobile = (bool)(fieldSim?.GetValue(bridge) ?? false);

            var fieldOverride = typeof(TISInputBridge).GetField("_override", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var ovr = fieldOverride?.GetValue(bridge);
            
            bool useMove = false, useLook = false, useZoom = false;
            if (ovr != null)
            {
                useMove = (bool)(ovr.GetType().GetProperty("UseMoveOverride")?.GetValue(ovr) ?? false);
                useLook = (bool)(ovr.GetType().GetProperty("UseLookOverride")?.GetValue(ovr) ?? false);
                useZoom = (bool)(ovr.GetType().GetProperty("UseZoomOverride")?.GetValue(ovr) ?? false);
            }

            return $"Platform: {(Application.isMobilePlatform ? "Mobile" : Application.isEditor ? "Editor" : "PC/Gamepad")}   " +
                   $"| SimulateMobile: {simMobile}   " +
                   $"| MoveOverride: {useMove}   " +
                   $"LookOverride: {useLook}   " +
                   $"ZoomOverride: {useZoom}";
        }

        private string GetDbObjectTrackerInfo(TISInputBridge bridge)
        {
            var fieldEnable = typeof(TISInputBridge).GetField("_enableObjectTracker", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            bool enableTracker = (bool)(fieldEnable?.GetValue(bridge) ?? false);
            if (!enableTracker) return "Object Tracker: OFF";

            var fieldTracker = typeof(TISInputBridge).GetField("_objectTracker", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var tracker = fieldTracker?.GetValue(bridge);
            if (tracker == null) return "Object Tracker: not initialized";

            int count = (int)(tracker.GetType().GetProperty("TouchedCount")?.GetValue(tracker) ?? 0);
            string lastName = (string)(tracker.GetType().GetMethod("GetLastTouchedName")?.Invoke(tracker, null) ?? "");
            return $"Object Tracker: ON  |  Touched: {count}" + (string.IsNullOrEmpty(lastName) ? "" : $"  |  Last: \"{lastName}\"");
        }

        private List<ActionDebugRow> BuildDebugRowsInternal(TISInputBridge bridge)
        {
            var rows = new List<ActionDebugRow>();
            
            var fieldProfile = typeof(TISInputBridge).GetField("_activeProfile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var activeProfile = fieldProfile?.GetValue(bridge) as TISInputProfileConfig;

            var fieldAsset = typeof(TISInputBridge).GetField("_asset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var asset = fieldAsset?.GetValue(bridge) as InputActionAsset;

            if (activeProfile == null || asset == null) return rows;

            var fieldSim = typeof(TISInputBridge).GetField("_simulateMobileInEditor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            bool simMobile = (bool)(fieldSim?.GetValue(bridge) ?? false);

            var fieldOverride = typeof(TISInputBridge).GetField("_override", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var ovr = fieldOverride?.GetValue(bridge);

            bool isMobileCtx = Application.isMobilePlatform || (Application.isEditor && simMobile);

            foreach (var mapData in activeProfile.actionMaps)
            {
                var uisMap = asset.FindActionMap(mapData.mapName, throwIfNotFound: false);
                bool mapEnabled = uisMap != null && uisMap.enabled;

                foreach (var def in mapData.actions)
                {
                    var row = new ActionDebugRow
                    {
                        Map    = mapData.mapName,
                        Action = def.actionName,
                        Type   = def.actionType.ToString(),
                    };

                    bool hasMobileBinding = isMobileCtx && !string.IsNullOrEmpty(def.bindingMobile);

                    if (!mapEnabled)
                    {
                        row.Active = "---"; row.Value = "(map disabled)"; row.Source = "—";
                        rows.Add(row);
                        continue;
                    }

                    if (hasMobileBinding)
                    {
                        bool pressed = TISMobileButtonRegistry.GetState(def.actionName);
                        row.Source = "Mobile";

                        // TISInputActionType cần khớp enum từ Runtime DLL
                        int actionTypeInt = (int)def.actionType; 
                        if (actionTypeInt == 0) // Button
                        {
                            row.ActiveBool = pressed;
                            row.Active     = pressed ? "YES" : "no";
                            row.Value      = pressed ? "true" : "false";
                        }
                        else if (actionTypeInt == 1) // Vector2
                        {
                            bool useMoveOverride = false, useLookOverride = false;
                            Vector2 moveDirection = Vector2.zero, lookDelta = Vector2.zero;
                            if (ovr != null)
                            {
                                useMoveOverride = (bool)(ovr.GetType().GetProperty("UseMoveOverride")?.GetValue(ovr) ?? false);
                                useLookOverride = (bool)(ovr.GetType().GetProperty("UseLookOverride")?.GetValue(ovr) ?? false);
                                moveDirection = (Vector2)(ovr.GetType().GetProperty("MoveDirection")?.GetValue(ovr) ?? Vector2.zero);
                                lookDelta = (Vector2)(ovr.GetType().GetProperty("LookDelta")?.GetValue(ovr) ?? Vector2.zero);
                            }

                            Vector2 mv = useMoveOverride ? moveDirection : Vector2.zero;
                            if (def.bindingMobile == "CameraZone") // Thay bằng chuỗi id trực tiếp nếu VirtualControlID là internal
                                mv = useLookOverride ? lookDelta : Vector2.zero;
                            
                            bool v2Active = mv.sqrMagnitude > 0.001f;
                            row.ActiveBool = v2Active;
                            row.Active     = v2Active ? "YES" : "no";
                            row.Value      = mv.ToString("F2");
                        }
                        else
                        {
                            row.Active = "no"; row.Value = "—";
                        }
                    }
                    else
                    {
                        var action = asset.FindAction(def.actionName, throwIfNotFound: false);
                        row.Source = "InputSystem";

                        if (action == null)
                        {
                            row.Active = "---"; row.Value = "(not found)";
                        }
                        else
                        {
                            int actionTypeInt = (int)def.actionType;
                            if (actionTypeInt == 0) // Button
                            {
                                bool bp = action.IsPressed();
                                row.ActiveBool = bp;
                                row.Active = bp ? "YES" : "no";
                                row.Value  = bp ? "true" : "false";
                            }
                            else if (actionTypeInt == 1) // Vector2
                            {
                                Vector2 v2 = action.ReadValue<Vector2>();
                                bool v2a = v2.sqrMagnitude > 0.001f;
                                row.ActiveBool = v2a;
                                row.Active = v2a ? "YES" : "no";
                                row.Value  = v2.ToString("F2");
                            }
                            else if (actionTypeInt == 2) // Axis
                            {
                                float ax = action.ReadValue<float>();
                                bool axa = Mathf.Abs(ax) > 0.01f;
                                row.ActiveBool = axa;
                                row.Active = axa ? "YES" : "no";
                                row.Value  = ax.ToString("F3");
                            }
                        }
                    }
                    rows.Add(row);
                }
            }
            return rows;
        }

        private string GetDbMobileRegistry(TISInputBridge bridge)
        {
            var fieldProfile = typeof(TISInputBridge).GetField("_activeProfile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var activeProfile = fieldProfile?.GetValue(bridge) as TISInputProfileConfig;
            if (activeProfile == null) return "";

            var sb = new System.Text.StringBuilder("MobileRegistry pressed: ");
            bool any = false;
            foreach (var map in activeProfile.actionMaps)
                foreach (var def in map.actions)
                    if (!string.IsNullOrEmpty(def.bindingMobile) && TISMobileButtonRegistry.GetState(def.actionName))
                    {
                        sb.Append(def.actionName).Append("  ");
                        any = true;
                    }
            return any ? sb.ToString() : "MobileRegistry: (none pressed)";
        }

        // ── Table helpers ─────────────────────────────────────

        private static readonly float[] ColWidths = { 80, 100, 60, 48, 120, 72 };

        private void DrawDebugTableRow(string c0, string c1, string c2, string c3, string c4, string c5, bool isHeader)
        {
            var rect = EditorGUILayout.GetControlRect(false, isHeader ? 20 : 18);
            DrawDebugTableRowFixed(rect, c0, c1, c2, c3, c4, c5, false, isHeader);
        }

        private void DrawDebugTableRowFixed(Rect rect, string c0, string c1, string c2, string c3, string c4, string c5, bool activeFlag, bool isHeader = false)
        {
            var style = isHeader ? EditorStyles.boldLabel : EditorStyles.miniLabel;
            Color activeColor = activeFlag
                ? new Color(0.4f, 1f, 0.5f)
                : c3 == "---" ? new Color(0.5f, 0.5f, 0.5f) : GUI.skin.label.normal.textColor;

            float x = rect.x;
            GUI.Label(new Rect(x, rect.y, ColWidths[0], rect.height), c0, style); x += ColWidths[0];
            GUI.Label(new Rect(x, rect.y, ColWidths[1], rect.height), c1, style); x += ColWidths[1];
            GUI.Label(new Rect(x, rect.y, ColWidths[2], rect.height), c2, style); x += ColWidths[2];

            var prevColor = GUI.contentColor;
            GUI.contentColor = activeColor;
            GUI.Label(new Rect(x, rect.y, ColWidths[3], rect.height), c3, style);
            GUI.contentColor = prevColor;
            x += ColWidths[3];

            GUI.Label(new Rect(x, rect.y, ColWidths[4], rect.height), c4, style); x += ColWidths[4];
            GUI.Label(new Rect(x, rect.y, ColWidths[5], rect.height), c5, style);
        }
    }
}

#endif