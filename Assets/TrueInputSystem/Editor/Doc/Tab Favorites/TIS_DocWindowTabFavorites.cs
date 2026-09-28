// ============================================================
//  TrueInputSystem — TIS_DocWindow_Tab_Favorites.cs
//  Tab: Favorites — Host / Coordinator
// ============================================================
//
//  File này CHỈ chứa:
//    • DrawTab_Favorites()      — khung scroll + sidebar + content router
//    • DrawFavSubTabSidebar()   — render danh sách module từ _favModules
//    • InitFavModules()         — đăng ký module theo thứ tự hiển thị
//    • FavApiBox()              — helper vẽ khung signature API (public để module gọi)
//
//  ĐỂ THÊM API MỚI:
//    1. Tạo file TISFav_<TenAPI>.cs (implement IFavSubTabModule)
//    2. Thêm  _favModules.Add(new TISFav_<TenAPI>());  vào InitFavModules()
//    Không cần sửa gì thêm ở file này.
// ============================================================

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GaMo.TrueInputSystem.Editor
{
    public partial class TISDocWindow : EditorWindow
    {
        // ── Module registry ──────────────────────────────────
        private List<IFavSubTabModule> _favModules;
        private int _favSubTab = 0;

        /// <summary>
        /// Gọi một lần trong OnEnable() của class chính.
        /// Thêm module mới vào đây theo thứ tự muốn hiển thị trong sidebar.
        /// </summary>
        private void InitFavModules()
        {
            _favModules = new List<IFavSubTabModule>
            {
                // index 0
                new TISFav_MoveDirection(),
                // index 1
                new TISFav_LookDelta(),
                // index 2
                new TISFav_ZoomDelta(),
                // index 3
                new TISFav_IsJumping(),
                // index 4
                new TISFav_IsRunning(),
                // index 5
                new TISFav_IsAttacking(),
                // index 6
                new TISFav_GetSkillAction(),
                // index 7
                new TISFav_GetVector2Action(),
                // index 8
                new TISFav_GetAxisAction(),
                // index 9
                new TISFav_TryRumble(),
                // index 10
                new TISFav_GesturePanDelta(),
                // index 11
                new TISFav_ToucherCount(),
                // index 12
                new TISFav_ToucherGetLastID(),
                // index 13
                new TISFav_DoubleTapOnObject(),
                // index 14
                new TISFav_DoubleTapOnScreen(),
                // index 15
                new TISFav_GetDoubleTapped(),
                // index 16
                new TISFav_ToucherClear(),
                // index 17
                new TISFav_SetObjTracker(),
                // index 18
                new TISFav_SetTrackerCamera(),
                // index 19
                new TISFav_GetSingleObject(),
                // index 20
                new TISFav_SetCooldownFill(),
                // index 21
                new TISFav_SetDisabled(),

                // ── THÊM MODULE MỚI Ở ĐÂY ──────────────────
                // new TISFav_MyNewApi(),
            };
        }

        // ══════════════════════════════════════════════════════
        //  TAB 10 — FAVORITES
        // ══════════════════════════════════════════════════════

        private void DrawTab_Favorites()
        {
            // Khởi tạo lần đầu (OnEnable chưa gọi hoặc sau domain reload)
            if (_favModules == null) InitFavModules();

            // ── Single scroll bao quanh sidebar + content ───
            var scroll    = Scroll("fav_main");
            var newScroll = EditorGUILayout.BeginScrollView(scroll);
            SetScroll("fav_main", newScroll);

            GUILayout.BeginHorizontal();

            // ── Sidebar trái ────────────────────────────────
            DrawFavSubTabSidebar();

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
                if (_favSubTab >= 0 && _favSubTab < _favModules.Count)
                    _favModules[_favSubTab].Draw(this); // truyền instance để module gọi được helper
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

        private const float FavSidebarW = 130f;

        private void DrawFavSubTabSidebar()
        {
            GUILayout.BeginVertical(GUILayout.Width(FavSidebarW));

            var headerSt = new GUIStyle(EditorStyles.label)
            {
                fontSize  = 9,
                fontStyle = FontStyle.Bold,
                normal    = { textColor = TxtMuted },
                padding   = new RectOffset(14, 14, 14, 6),
                margin    = new RectOffset(0, 0, 0, 0),
            };
            GUILayout.Label("API HAY DÙNG", headerSt);

            for (int i = 0; i < _favModules.Count; i++)
            {
                bool isActive = (_favSubTab == i);

                Rect rowRect = GUILayoutUtility.GetRect(FavSidebarW, 30,
                    GUILayout.Width(FavSidebarW), GUILayout.Height(30));

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
                GUI.Label(rowRect, _favModules[i].Label, labelSt);

                // Click
                if (Event.current.type == EventType.MouseDown
                    && rowRect.Contains(Event.current.mousePosition))
                {
                    _favSubTab = i;
                    GUI.FocusControl(null);
                    Event.current.Use();
                    Repaint();
                }
            }

            GUILayout.Space(8);
            GUILayout.EndVertical();
        }

        // ══════════════════════════════════════════════════════
        //  HELPER — FavApiBox: khung signature màu accent
        //  Internal method — các module gọi qua win.FavApiBox(...)
        // ══════════════════════════════════════════════════════

        internal void FavApiBox(string className, string memberName, string returnType, string description, Color accent)
        {
            Rect boxRect = GUILayoutUtility.GetRect(0, 64, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(boxRect, BgSurface);
            EditorGUI.DrawRect(new Rect(boxRect.x, boxRect.y, 4, boxRect.height), accent);
            EditorGUI.DrawRect(new Rect(boxRect.x, boxRect.yMax - 1, boxRect.width, 1), BorderSub);

            // Return type badge
            var retBg = new Color(accent.r * 0.25f, accent.g * 0.25f, accent.b * 0.25f, 1f);
            var retSt = new GUIStyle(EditorStyles.label)
            {
                fontSize  = 10, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = accent, background = Tex(retBg) },
                padding   = new RectOffset(6, 6, 3, 3),
            };
            float retW = 70;
            GUI.Label(new Rect(boxRect.xMax - retW - 10, boxRect.y + 10, retW, 18), returnType, retSt);

            // Class · Member · Description
            var classSt  = new GUIStyle(EditorStyles.label) { fontSize = 10, normal = { textColor = TxtMuted  }, padding = new RectOffset(14, 0, 8, 0) };
            var memberSt = new GUIStyle(EditorStyles.label) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = accent    }, padding = new RectOffset(14, 0, 0, 0) };
            var descSt   = new GUIStyle(EditorStyles.label) { fontSize = 11, wordWrap = true, normal = { textColor = TxtSecond }, padding = new RectOffset(14, 84, 0, 0) };

            GUI.Label(new Rect(boxRect.x, boxRect.y,      boxRect.width, 20), className,   classSt);
            GUI.Label(new Rect(boxRect.x, boxRect.y + 18, boxRect.width, 22), memberName,  memberSt);
            GUI.Label(new Rect(boxRect.x, boxRect.y + 40, boxRect.width, 20), description, descSt);

            GUILayout.Space(6);
        }

    }  // partial class TISDocWindow
}
#endif