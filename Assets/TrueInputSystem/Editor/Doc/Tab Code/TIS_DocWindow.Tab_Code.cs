// ============================================================
//  TrueInputSystem — TIS_DocWindow_Tab_Code.cs
//  Tab: Code — Host / Coordinator
// ============================================================
//
//  File này CHỈ chứa:
//    • DrawTab_Code()     — khung scroll + sidebar + content router
//    • DrawCodeSidebar()  — render danh sách module từ _codeModules
//    • InitCodeModules()  — đăng ký module theo thứ tự hiển thị
//
//  ĐỂ THÊM SUB-TAB MỚI:
//    1. Tạo file TIS_DocWindow_Code_<TenModule>.cs (implement ICodeSubTabModule)
//    2. Thêm  _codeModules.Add(new TISCode_<TenModule>());  vào InitCodeModules()
//    Không cần sửa gì thêm ở file này.
// ============================================================

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TrueInputSystem.Editor
{
    public partial class TISDocWindow : EditorWindow
    {
        // ── Module registry ──────────────────────────────────
        private List<ICodeSubTabModule> _codeModules;

        /// <summary>
        /// Gọi một lần trong OnEnable() của class chính.
        /// Thêm module mới vào đây theo thứ tự muốn hiển thị trong sidebar.
        /// </summary>
        private void InitCodeModules()
        {
            _codeModules = new List<ICodeSubTabModule>
            {
                // index 0
                new TISCode_Reference(),
                // index 1
                new TISCode_Movement(),
                // index 2
                new TISCode_Camera(),
                // index 3
                new TISCode_Shortcuts(),
                // index 4
                new TISCode_SkillAction(),
                // index 5
                new TISCode_Vector2Axis(),
                // index 6
                new TISCode_MobileButton(),
                // index 7
                new TISCode_ActionMap(),
                // index 8
                new TISCode_ProfileSwap(),
                // index 9
                new TISCode_Rumble(),
                // index 10
                new TISCode_Rebinding(),
                // index 11
                new TISCode_DirectionalButton(),
                // index 12
                new TISCode_TargetScanner(),
                // index 13
                new TISCode_MobileInputSetup(),
                // index 14
                new TISCode_AimAssistDeadzone(),
                // index 15
                new TISCode_GamepadCursor(),
                // index 16
                new TISCode_FullExample(),
                // index 17
                new TISCode_GestureDetector(),
                // index 18
                new TISCode_ObjectTracker(),
                // index 19
                new TISCode_GamepadUIButton(),
                // index 20
                new TISCode_PointerPosition(),

                // ── THÊM MODULE MỚI Ở ĐÂY ──────────────────
                // new TISCode_MyNewFeature(),
            };
        }

        // ══════════════════════════════════════════════════════
        //  TAB 5 — DÙNG TRONG CODE
        // ══════════════════════════════════════════════════════

        private void DrawTab_Code()
        {
            // Khởi tạo lần đầu (OnEnable chưa gọi hoặc sau domain reload)
            if (_codeModules == null) InitCodeModules();

            // ── Single scroll bao quanh sidebar + content ───
            var scroll    = Scroll("code_main");
            var newScroll = EditorGUILayout.BeginScrollView(scroll);
            SetScroll("code_main", newScroll);

            GUILayout.BeginHorizontal();

            // ── Sidebar trái ────────────────────────────────
            DrawCodeSidebar();

            // ── Đường kẻ dọc ────────────────────────────────
            Rect divRect = GUILayoutUtility.GetRect(1, 1,
                GUILayout.Width(1), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(divRect, BorderSub);

            // ── Vùng nội dung phải ──────────────────────────
            GUILayout.BeginVertical();
            GUILayout.Space(24);
            GUILayout.BeginHorizontal();
            GUILayout.Space(24);
            GUILayout.BeginVertical();

            try
            {
                if (_codeSubTab >= 0 && _codeSubTab < _codeModules.Count)
                    _codeModules[_codeSubTab].Draw(this); // truyền instance để module gọi được helper
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            GUILayout.Space(40);
            GUILayout.EndVertical();
            GUILayout.Space(24);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        // ── Sidebar ──────────────────────────────────────────

        private const float CodeSidebarW = 130f;

        private void DrawCodeSidebar()
        {
            GUILayout.BeginVertical(GUILayout.Width(CodeSidebarW));

            var headerSt = new GUIStyle(EditorStyles.label)
            {
                fontSize  = 9,
                fontStyle = FontStyle.Bold,
                normal    = { textColor = TxtMuted },
                padding   = new RectOffset(14, 14, 14, 6),
                margin    = new RectOffset(0, 0, 0, 0),
            };
            GUILayout.Label("VÍ DỤ CODE", headerSt);

            for (int i = 0; i < _codeModules.Count; i++)
            {
                bool isActive = (_codeSubTab == i);

                Rect rowRect = GUILayoutUtility.GetRect(CodeSidebarW, 30,
                    GUILayout.Width(CodeSidebarW), GUILayout.Height(30));

                // Nền
                if (isActive)
                {
                    EditorGUI.DrawRect(rowRect, SidebarActiveBg);
                    EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.y, 3, rowRect.height), AccentBlue);
                }
                else if (rowRect.Contains(Event.current.mousePosition))
                {
                    EditorGUI.DrawRect(rowRect, SidebarHoverBg);
                }

                // Kẻ đáy
                EditorGUI.DrawRect(
                    new Rect(rowRect.x, rowRect.yMax - 1, rowRect.width, 1), SidebarRowSep);

                // Label
                var labelSt = new GUIStyle(EditorStyles.label)
                {
                    fontSize  = 12,
                    fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal,
                    wordWrap  = false,
                    alignment = TextAnchor.MiddleLeft,
                    normal    = { textColor = isActive ? AccentBlue : TxtSecond },
                    padding   = new RectOffset(isActive ? 14 : 12, 8, 0, 0),
                };
                GUI.Label(rowRect, _codeModules[i].Label, labelSt);

                // Click
                if (Event.current.type == EventType.MouseDown
                    && rowRect.Contains(Event.current.mousePosition))
                {
                    _codeSubTab = i;
                    GUI.FocusControl(null);
                    Event.current.Use();
                    Repaint();
                }
            }

            GUILayout.Space(8);
            GUILayout.EndVertical();
        }

    }  // partial class TISDocWindow
}
#endif