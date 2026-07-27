#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TrueInputSystem.Editor
{
    public partial class TISDocWindow : EditorWindow
    {
        // ══════════════════════════════════════════════════════
        //  MENU ITEM
        // ══════════════════════════════════════════════════════

        //[MenuItem("Tools/True Input System/Documentation", priority = 15)]
        public static void OpenWindow()
        {
            var window = GetWindow<TISDocWindow>();
            window.titleContent = new GUIContent("TIS — Documentation", EditorGUIUtility.IconContent("_Help").image);
            window.minSize      = new Vector2(860f, 640f);
            window.Show();
        }

        // ══════════════════════════════════════════════════════
        //  PALETTE
        //  Tất cả màu được định nghĩa tập trung tại:
        //  → TIS_DocWindow_Palette.cs
        //  Không định nghĩa màu ở file này hoặc các partial khác.
        // ══════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════
        //  STYLE CACHE
        // ══════════════════════════════════════════════════════

        private static GUIStyle _sTitle, _sH2, _sH3, _sBody, _sSmall;
        private static GUIStyle _sCode, _sCodeKw, _sCodeType, _sCodeStr,
                                _sCodeCm, _sCodeFn, _sCodeNum, _sCodeVar,
                                _sCodeOp, _sCodePlain;
        private static GUIStyle _sCalloutNote, _sCalloutTip, _sCalloutWarn;
        private static GUIStyle _sNavBtn, _sNavBtnActive;
        private static GUIStyle _sTabBtn, _sTabBtnActive;
        private static GUIStyle _sBadgeBlue, _sBadgeGreen, _sBadgeAmber, _sBadgeRed, _sBadgePurple;
        private static GUIStyle _sSearchBox, _sSearchResult, _sSearchResultHover;
        private static GUIStyle _sSectionHeader, _sStepNum, _sStepTitle, _sStepDesc;
        private static GUIStyle _sFaqQuestion, _sFaqAnswer;
        private static GUIStyle _sApiMethod, _sApiReturn, _sApiDesc;
        private static bool _stylesReady;
        // Track which skin styles were built for so we rebuild on theme switch
        private static bool _styleBuiltForProSkin;

        private static Texture2D _texBgDeep, _texBgBase, _texBgSurface, _texBgElevated;
        private static Texture2D _texAccentBlue, _texAccentGreen, _texAccentAmber, _texAccentRed;
        private static Texture2D _texAccentPurple, _texBorderSub, _texBorderNorm;
        private static Texture2D _texCodeBg, _texCalloutNote, _texCalloutTip, _texCalloutWarn;
        private static Texture2D _texSidebar, _texSearchBg, _texSearchResult, _texSearchResultHover;
        private static Texture2D _texNavActive, _texNavHover;

        private static Texture2D Tex(Color c)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        private void EnsureStyles()
        {
            // Rebuild styles whenever the Editor skin changes (user switches Light ↔ Dark)
            if (_stylesReady && _sTitle != null && _styleBuiltForProSkin == EditorGUIUtility.isProSkin) return;
            _styleBuiltForProSkin = EditorGUIUtility.isProSkin;

            _texBgDeep    = Tex(BgDeep);
            _texBgBase    = Tex(BgBase);
            _texBgSurface = Tex(BgSurface);
            _texBgElevated = Tex(BgElevated);
            _texAccentBlue  = Tex(AccentBlue);
            _texAccentGreen = Tex(AccentGreen);
            _texAccentAmber = Tex(AccentAmber);
            _texAccentRed   = Tex(AccentRed);
            _texAccentPurple = Tex(AccentPurple);
            _texBorderSub  = Tex(BorderSub);
            _texBorderNorm = Tex(BorderNorm);
            _texCodeBg     = Tex(CodeBg);
            _texCalloutNote = Tex(CalloutNoteBg);
            _texCalloutTip  = Tex(CalloutTipBg);
            _texCalloutWarn = Tex(CalloutWarnBg);
            _texSidebar    = Tex(BgDeep);
            _texSearchBg   = Tex(SearchBg);
            _texSearchResult = Tex(BgSurface);
            _texSearchResultHover = Tex(BgElevated);
            _texNavActive  = Tex(NavActiveBg);
            _texNavHover   = Tex(NavHoverBg);

            // ── Page title
            _sTitle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 22, fontStyle = FontStyle.Bold, richText = true, wordWrap = true,
                normal = { textColor = TxtPrimary },
                margin = new RectOffset(0, 0, 4, 6),
            };

            // ── H2
            _sH2 = new GUIStyle(EditorStyles.label)
            {
                fontSize = 14, fontStyle = FontStyle.Bold, richText = true, wordWrap = true,
                normal = { textColor = TxtPrimary },
                margin = new RectOffset(0, 0, 16, 6),
            };

            // ── H3
            _sH3 = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11, fontStyle = FontStyle.Bold, richText = true, wordWrap = true,
                normal = { textColor = TxtMuted },
                margin = new RectOffset(0, 0, 12, 4),
            };

            // ── Body text
            _sBody = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12, richText = true, wordWrap = true,
                normal = { textColor = TxtSecond },
                margin = new RectOffset(0, 0, 2, 4),
            };

            // ── Small / Muted
            _sSmall = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11, richText = true, wordWrap = true,
                normal = { textColor = TxtMuted },
                margin = new RectOffset(0, 0, 1, 2),
            };

            // ── Code styles (inline mono tokens)
            GUIStyle MakeMono(Color col, bool italic = false) => new GUIStyle(EditorStyles.label)
            {
                fontSize = 12, richText = false, wordWrap = true,
                font = (Font)EditorGUIUtility.Load("Fonts/RobotoMono/RobotoMono-Regular"),
                fontStyle = italic ? FontStyle.Italic : FontStyle.Normal,
                normal = { textColor = col, background = _texCodeBg },
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
            };

            _sCode      = MakeMono(SynPlain);
            _sCodeKw    = MakeMono(SynKeyword);
            _sCodeType  = MakeMono(SynType);
            _sCodeStr   = MakeMono(SynString);
            _sCodeCm    = MakeMono(SynComment, italic: true);
            _sCodeFn    = MakeMono(SynFunction);
            _sCodeNum   = MakeMono(SynNumber);
            _sCodeVar   = MakeMono(SynVar);
            _sCodeOp    = MakeMono(SynOperator);
            _sCodePlain = MakeMono(SynPlain);

            // ── Callout backgrounds
            _sCalloutNote = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12, richText = true, wordWrap = true,
                normal = { textColor = CalloutNoteTxt, background = _texCalloutNote },
                padding = new RectOffset(14, 14, 10, 10),
                margin = new RectOffset(0, 0, 6, 6),
            };
            _sCalloutTip = new GUIStyle(_sCalloutNote)
            {
                normal = { textColor = CalloutTipTxt, background = _texCalloutTip },
            };
            _sCalloutWarn = new GUIStyle(_sCalloutNote)
            {
                normal = { textColor = CalloutWarnTxt, background = _texCalloutWarn },
            };

            // ── Nav buttons (sidebar)
            _sNavBtn = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12, richText = true, wordWrap = false,
                normal = { textColor = TxtSecond },
                hover  = { textColor = TxtPrimary, background = _texNavHover },
                padding = new RectOffset(14, 14, 7, 7),
                margin = new RectOffset(0, 0, 0, 0),
            };
            _sNavBtnActive = new GUIStyle(_sNavBtn)
            {
                normal = { textColor = AccentBlue, background = _texNavActive },
                fontStyle = FontStyle.Bold,
            };

            // ── Section header label
            _sSectionHeader = new GUIStyle(EditorStyles.label)
            {
                fontSize = 10, fontStyle = FontStyle.Bold,
                normal = { textColor = TxtMuted },
                padding = new RectOffset(14, 14, 10, 4),
                margin = new RectOffset(0, 0, 0, 0),
            };

            // ── Step components
            _sStepNum = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = StepNumTxt, background = _texAccentBlue },
                fixedWidth = 26, fixedHeight = 26,
                margin = new RectOffset(0, 10, 0, 0),
                padding = new RectOffset(0, 0, 0, 0),
            };
            _sStepTitle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 13, fontStyle = FontStyle.Bold,
                normal = { textColor = TxtPrimary },
                margin = new RectOffset(0, 0, 0, 2),
            };
            _sStepDesc = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12, richText = true, wordWrap = true,
                normal = { textColor = TxtSecond },
                margin = new RectOffset(0, 0, 0, 0),
            };

            // ── Badges
            GUIStyle MakeBadge(Color bg, Color txt) => new GUIStyle(EditorStyles.label)
            {
                fontSize = 11, fontStyle = FontStyle.Bold, richText = false,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = txt, background = Tex(bg) },
                padding = new RectOffset(8, 8, 3, 3),
                margin = new RectOffset(0, 4, 0, 0),
                fixedHeight = 18,
            };
            _sBadgeBlue   = MakeBadge(BadgeBlueBg,   AccentBlue);
            _sBadgeGreen  = MakeBadge(BadgeGreenBg,  AccentGreen);
            _sBadgeAmber  = MakeBadge(BadgeAmberBg,  AccentAmber);
            _sBadgeRed    = MakeBadge(BadgeRedBg,    AccentRed);
            _sBadgePurple = MakeBadge(BadgePurpleBg, AccentPurple);

            // ── Search box
            _sSearchBox = new GUIStyle(EditorStyles.textField)
            {
                fontSize = 13,
                normal   = { textColor = TxtPrimary, background = Tex(SearchBg) },
                focused  = { textColor = TxtPrimary, background = Tex(SearchBg) },
                padding = new RectOffset(32, 10, 8, 8),
                margin = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(1, 1, 1, 1),
            };

            // ── FAQ
            _sFaqQuestion = new GUIStyle(EditorStyles.label)
            {
                fontSize = 13, fontStyle = FontStyle.Bold, richText = true, wordWrap = true,
                normal = { textColor = TxtPrimary, background = _texBgSurface },
                padding = new RectOffset(14, 14, 10, 10),
                margin = new RectOffset(0, 0, 0, 0),
            };
            _sFaqAnswer = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12, richText = true, wordWrap = true,
                normal = { textColor = TxtSecond, background = _texBgBase },
                padding = new RectOffset(18, 14, 8, 12),
                margin = new RectOffset(0, 0, 0, 0),
            };

            // ── API table styles
            _sApiMethod = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12, richText = false,
                font = (Font)EditorGUIUtility.Load("Fonts/RobotoMono/RobotoMono-Regular"),
                normal = { textColor = AccentCyan },
                padding = new RectOffset(8, 8, 5, 5),
            };
            _sApiReturn = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11, fontStyle = FontStyle.Bold,
                normal = { textColor = TxtMuted },
                padding = new RectOffset(4, 4, 5, 5),
                fixedWidth = 70,
            };
            _sApiDesc = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12, richText = true, wordWrap = true,
                normal = { textColor = TxtSecond },
                padding = new RectOffset(4, 4, 5, 5),
            };

            _stylesReady = true;
        }

        // ══════════════════════════════════════════════════════
        //  NAVIGATION STATE
        // ══════════════════════════════════════════════════════

        private int _activeTab = 0;

        private readonly string[] _tabIds = {
            "favorites",
            "overview", "setup", "mobile", "gamepadui",
            "code", "api", "setuptool"
        };

        private readonly string[] _tabLabels = {
            "★  Favorites",
            "◈  Tổng Quan",
            "◈  Setup Scene",
            "◈  Mobile UI",
            "◈  Gamepad UI",
            "<>  Code",
            "≡  API Reference",
            "⚙  Setup Tool",
        };

        private readonly string[] _tabGroups = {
            "HAY DÙNG",
            "BẮT ĐẦU", "BẮT ĐẦU", "BẮT ĐẦU", "BẮT ĐẦU",
            "PHÁT TRIỂN",
            "THAM KHẢO", "THAM KHẢO",
        };

        // ══════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════
        //  SEARCH — moved to TIS_DocWindow_Search.cs
        //  State fields, SearchEntry struct, SearchDB, DrawSidebarSearch,
        //  DrawSearchDropdown, and _searchBoxBottomY are all defined there.
        //  To update the search database, only edit that file.
        // ══════════════════════════════════════════════════════


        // ══════════════════════════════════════════════════════
        //  SCROLL STATES
        // ══════════════════════════════════════════════════════

        private readonly Dictionary<string, Vector2> _scrolls = new();
        private Vector2 Scroll(string k) => _scrolls.TryGetValue(k, out var v) ? v : Vector2.zero;
        private void SetScroll(string k, Vector2 v) => _scrolls[k] = v;

        // ══════════════════════════════════════════════════════
        //  FAQ STATE
        // ══════════════════════════════════════════════════════
        private readonly HashSet<int> _faqOpen = new();

        // Cached content width for ApiRow — updated each Repaint, used next Layout pass
        private float _lastApiRowWidth = 600f;

        // ══════════════════════════════════════════════════════
        //  CODE SUB-TAB STATE
        // ══════════════════════════════════════════════════════
        private int _codeSubTab = 0;

        private readonly string[] _codeSubLabels = {
            "Reference",       // 0
            "Di Chuyển",       // 1
            "Camera Input",    // 2
            "Button Shortcut", // 3
            "GetSkillAction",  // 4
            "Vector2 / Axis",  // 5
            "Mobile Button",   // 6
            "Action Map",      // 7
            "Profile & Events",// 8
            "Gamepad Rumble",  // 9
            "Rebinding",       // 10
            "Directional Btn", // 11
            "TargetScanner",   // 12
            "MobileSetup",     // 13
            "AimAssist & DZ",  // 14
            "GamepadCursor",   // 15
            "Full Example",    // 16
            "Gesture Detector",// 17
            "Object Tracker",  // 18
            "Gamepad UI Btn",  // 19
            "Pointer Pos",     // 20
        };

        // ══════════════════════════════════════════════════════
        //  ODIN OVERRIDE
        // ══════════════════════════════════════════════════════

        private void OnGUI()
        {
            _codeBlockCounter = 0;
            EnsureStyles();
            DrawLayout();
        }

        // ══════════════════════════════════════════════════════
        //  MAIN LAYOUT
        // ══════════════════════════════════════════════════════

        private const float SidebarW = 170f;
        private const float TopbarH  = 40f;

        private void DrawLayout()
        {
            // Full-window dark background
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), BgBase);

            // ── Sidebar — guarded so its BeginArea always has a matching EndArea
            try
            {
                DrawSidebar();
            }
            catch (Exception ex)
            {
                // End the area that DrawSidebar opened before the exception
                try { GUILayout.EndArea(); } catch { }
                Debug.LogException(ex);
            }

            // ── Main area
            float mainX = SidebarW;
            float mainW = position.width - SidebarW;

            // Topbar
            DrawTopbar(mainX, mainW);

            // Content — guarded so BeginArea always has a matching EndArea
            float contentY = TopbarH;
            float contentH = position.height - TopbarH;
            Rect contentRect = new Rect(mainX, contentY, mainW, contentH);
            GUILayout.BeginArea(contentRect);
            try
            {
                DrawContent();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                GUILayout.EndArea();
            }

            // ── Search dropdown — drawn LAST at window level (no BeginArea clip)
            if (_showSearchResults && !string.IsNullOrEmpty(_searchQuery))
                DrawSearchDropdown();
        }

        // ── SIDEBAR ────────────────────────────────────────────

        private void DrawSidebar()
        {
            Rect sideRect = new Rect(0, 0, SidebarW, position.height);
            EditorGUI.DrawRect(sideRect, BgDeep);

            // Right border
            EditorGUI.DrawRect(new Rect(SidebarW - 1, 0, 1, position.height), BorderSub);

            GUILayout.BeginArea(sideRect);
            try
            {
                // Logo area
                EditorGUI.DrawRect(new Rect(0, 0, SidebarW, 56), SidebarHeaderBg);
                EditorGUI.DrawRect(new Rect(0, 55, SidebarW, 1), BorderSub);

                GUILayout.Space(12);
                GUILayout.BeginHorizontal();
                GUILayout.Space(14);

                // Accent dot
                Rect dotRect = GUILayoutUtility.GetRect(8, 8, GUILayout.Width(8), GUILayout.Height(8));
                dotRect.y += 6;
                EditorGUI.DrawRect(dotRect, AccentBlue);

                GUILayout.Space(8);
                var logoStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 13, fontStyle = FontStyle.Bold,
                    normal = { textColor = TxtPrimary },
                };
                GUILayout.Label("TIS DOCS", logoStyle);
                GUILayout.EndHorizontal();

                var versionStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 10,
                    normal = { textColor = TxtMuted },
                    padding = new RectOffset(30, 0, 0, 0),
                };
                GUILayout.Label("TrueInputSystem — Unity Editor", versionStyle);
                GUILayout.Space(10);

                // Search bar
                DrawSidebarSearch();

                GUILayout.Space(4);
                EditorGUI.DrawRect(GUILayoutUtility.GetRect(SidebarW, 1), BorderSub);
                GUILayout.Space(4);

                // Nav items
                DrawNavItems();
            }
            finally
            {
                GUILayout.EndArea();
            }
        }





        // _searchBoxBottomY → TIS_DocWindow_Search.cs





        private void DrawNavItems()
        {
            string prevGroup = "";
            for (int i = 0; i < _tabIds.Length; i++)
            {
                // Group label
                if (_tabGroups[i] != prevGroup)
                {
                    GUILayout.Space(4);
                    GUILayout.Label(_tabGroups[i], _sSectionHeader);
                    prevGroup = _tabGroups[i];
                }

                bool isActive = _activeTab == i;

                // Active background
                Rect btnRect = GUILayoutUtility.GetRect(SidebarW, 30);
                if (isActive)
                {
                    EditorGUI.DrawRect(btnRect, NavActiveBg);
                    EditorGUI.DrawRect(new Rect(btnRect.x, btnRect.y, 3, btnRect.height), AccentBlue);
                }
                else if (btnRect.Contains(Event.current.mousePosition))
                {
                    EditorGUI.DrawRect(btnRect, NavHoverBg);
                }

                var style = isActive ? _sNavBtnActive : _sNavBtn;
                GUI.Label(btnRect, _tabLabels[i], style);

                if (Event.current.type == EventType.MouseDown && btnRect.Contains(Event.current.mousePosition))
                {
                    _activeTab = i;
                    GUI.FocusControl(null);
                    _searchQuery = "";
                    _showSearchResults = false;
                    Event.current.Use();
                    Repaint();
                }
            }
        }

        // ── TOPBAR ─────────────────────────────────────────────

        private void DrawTopbar(float x, float w)
        {
            Rect topRect = new Rect(x, 0, w, TopbarH);
            EditorGUI.DrawRect(topRect, BgDeep);
            EditorGUI.DrawRect(new Rect(x, TopbarH - 1, w, 1), BorderSub);

            // Breadcrumb
            var mutedSt = new GUIStyle(EditorStyles.label) { fontSize = 11, normal = { textColor = TxtMuted } };
            var sepSt   = new GUIStyle(EditorStyles.label) { fontSize = 11, normal = { textColor = TxtMuted } };
            var curSt   = new GUIStyle(EditorStyles.label) { fontSize = 11, fontStyle = FontStyle.Bold, normal = { textColor = TxtSecond } };

            float bx = x + 16;
            float by = (TopbarH - 16) / 2;
            GUI.Label(new Rect(bx, by, 60, 16), "TIS Docs", mutedSt);
            bx += 58;
            GUI.Label(new Rect(bx, by, 12, 16), "›", sepSt);
            bx += 12;
            string tabName = _tabLabels[_activeTab].Trim();
            // Strip icon prefix
            if (tabName.Length > 2 && tabName[1] == ' ')
                tabName = tabName.Substring(2);
            GUI.Label(new Rect(bx, by, 200, 16), tabName, curSt);

            // Version badge (right)
            var vSt = new GUIStyle(EditorStyles.label)
            {
                fontSize = 10, fontStyle = FontStyle.Bold,
                normal = { textColor = AccentGreen, background = Tex(BadgeGreenBg) },
                padding = new RectOffset(6, 6, 3, 3),
                alignment = TextAnchor.MiddleCenter,
            };
            float vw = 100;
            GUI.Label(new Rect(x + w - vw - 10, (TopbarH - 20) / 2, vw, 20), "GaMo Tools v1.0.7", vSt);
        }

        // ══════════════════════════════════════════════════════
        //  CONTENT ROUTER
        // ══════════════════════════════════════════════════════

        private void DrawContent()
        {
            switch (_activeTab)
            {
                case 0: DrawTab_Favorites(); break;
                case 1: DrawTab_Overview();   break;
                case 2: DrawTab_Setup();      break;
                case 3: DrawTab_Mobile();     break;
                case 4: DrawTab_GamepadUI();   break;
                case 5: DrawTab_Code();       break;
                case 6: DrawTab_API();        break;
                case 7: DrawTab_SetupTool();  break;
            }
        }

        // ══════════════════════════════════════════════════════
        //  SHARED DRAW HELPERS
        // ══════════════════════════════════════════════════════

        private void BeginContentScroll(string key)
        {
            var s = Scroll(key);
            var ns = EditorGUILayout.BeginScrollView(s);
            SetScroll(key, ns);
            GUILayout.Space(24);
            GUILayout.BeginHorizontal();
            GUILayout.Space(24);
            GUILayout.BeginVertical();
        }

        private void EndContentScroll()
        {
            GUILayout.Space(40);
            GUILayout.EndVertical();
            GUILayout.Space(24);
            GUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        internal void PageTitle(string t, string subtitle = "")
        {
            GUILayout.Label(t, _sTitle);
            if (!string.IsNullOrEmpty(subtitle))
                GUILayout.Label(subtitle, _sBody);
            GUILayout.Space(8);
            DrawSep(AccentBlue, 2);
            GUILayout.Space(16);
        }

        internal void H2(string t) { GUILayout.Label(t, _sH2); }
        internal void H3(string t) { GUILayout.Label(t.ToUpper(), _sH3); }
        internal void Body(string t) { GUILayout.Label(t, _sBody); }

        private void DrawSep(Color col, float height = 1f)
        {
            var r = EditorGUILayout.GetControlRect(false, height);
            EditorGUI.DrawRect(r, col);
        }

        internal void Sep() { GUILayout.Space(8); DrawSep(BorderSub); GUILayout.Space(8); }

        internal void Note(string t) { GUILayout.Label("ℹ  " + t, _sCalloutNote); }
        internal void Tip(string t)  { GUILayout.Label("💡  " + t, _sCalloutTip); }
        internal void Warn(string t) { GUILayout.Label("⚠  " + t, _sCalloutWarn); }

        // ── CODE BLOCK — selectable + copy ────────────────────

        // Tracks which block just had "Copied!" shown  (blockId → expiry EditorApplication.timeSinceStartup)
        private readonly Dictionary<int, double> _copyFeedback = new();
        private int _codeBlockCounter; // reset each OnGUI frame

        internal void CodeBlock(string content, string lang = "C#")
        {
            GUILayout.Space(4);

            int blockId = _codeBlockCounter++;

            // ── Header bar ──────────────────────────────────────
            Rect headerRect = EditorGUILayout.GetControlRect(false, 28);
            EditorGUI.DrawRect(headerRect, SidebarHeaderBg);
            EditorGUI.DrawRect(new Rect(headerRect.x, headerRect.yMax - 1, headerRect.width, 1), BorderSub);

            // Traffic-light dots (macOS-style — keep vivid in both themes)
            float dx = headerRect.x + 12, dy = headerRect.y + 10;
            EditorGUI.DrawRect(new Rect(dx,      dy, 8, 8), DotRed);
            EditorGUI.DrawRect(new Rect(dx + 14, dy, 8, 8), DotYellow);
            EditorGUI.DrawRect(new Rect(dx + 28, dy, 8, 8), DotGreen);

            // Language label (center)
            var langSt = new GUIStyle(EditorStyles.label)
            {
                fontSize = 10, fontStyle = FontStyle.Bold,
                normal = { textColor = TxtMuted },
                alignment = TextAnchor.MiddleCenter,
            };
            GUI.Label(headerRect, lang, langSt);

            // Copy button (right side)
            bool copied = _copyFeedback.TryGetValue(blockId, out double exp)
                          && EditorApplication.timeSinceStartup < exp;
            string copyLabel = copied ? "✓ Copied!" : "Copy";
            Color  copyColor = copied ? AccentGreen : TxtMuted;

            var copySt = new GUIStyle(EditorStyles.label)
            {
                fontSize  = 10, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal    = { textColor = copyColor },
                padding   = new RectOffset(0, 10, 0, 0),
            };
            Rect copyRect = new Rect(headerRect.xMax - 72, headerRect.y, 72, headerRect.height);
            GUI.Label(copyRect, copyLabel, copySt);

            if (Event.current.type == EventType.MouseDown && copyRect.Contains(Event.current.mousePosition))
            {
                EditorGUIUtility.systemCopyBuffer = content;
                _copyFeedback[blockId] = EditorApplication.timeSinceStartup + 1.8;
                Event.current.Use();
                Repaint();
            }
            // Repaint until feedback expires
            if (copied) Repaint();

            // ── Code body — selectable TextArea ─────────────────
            var codeBg = CodeBg;
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(0, 0), codeBg); // zero-height bg seed

            var areaSt = new GUIStyle(EditorStyles.textArea)
            {
                font      = (Font)EditorGUIUtility.Load("Fonts/RobotoMono/RobotoMono-Regular"),
                fontSize  = 12,
                wordWrap  = false,
                richText  = false,
                normal    = { textColor = SynPlain,  background = Tex(codeBg) },
                focused   = { textColor = SynPlain,  background = Tex(codeBg) },
                hover     = { textColor = SynPlain,  background = Tex(codeBg) },
                active    = { textColor = SynPlain,  background = Tex(codeBg) },
                padding   = new RectOffset(14, 14, 10, 10),
                margin    = new RectOffset(0, 0, 0, 0),
                border    = new RectOffset(0, 0, 0, 0),
                stretchWidth = true,
            };

            // Calculate height from line count so block doesn't scroll internally
            int lineCount = content.Split('\n').Length;
            float lineH   = areaSt.lineHeight > 0 ? areaSt.lineHeight : 18f;
            float areaH   = lineH * lineCount + areaSt.padding.top + areaSt.padding.bottom;

            EditorGUI.BeginDisabledGroup(false); // keep text selectable
            EditorGUILayout.SelectableLabel(content, areaSt,
                GUILayout.Height(areaH), GUILayout.ExpandWidth(true));
            EditorGUI.EndDisabledGroup();

            GUILayout.Space(4);
        }

        // ── STEP ───────────────────────────────────────────────

        internal void Step(int n, string title, string desc)
        {
            GUILayout.Space(2);
            GUILayout.BeginHorizontal();

            // Number circle
            var numStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11, fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = StepNumTxt, background = _texAccentBlue },
                fixedWidth = 24, fixedHeight = 24,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 12, 2, 0),
            };
            GUILayout.Label(n.ToString(), numStyle, GUILayout.Width(24), GUILayout.Height(24));

            GUILayout.BeginVertical();
            GUILayout.Label(title, _sStepTitle);
            if (!string.IsNullOrEmpty(desc))
                GUILayout.Label(desc, _sStepDesc);
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.Space(8);
        }

        // ── BADGE ──────────────────────────────────────────────

        internal void Badge(string label, string color = "blue")
        {
            var s = color == "green"  ? _sBadgeGreen
                  : color == "amber"  ? _sBadgeAmber
                  : color == "red"    ? _sBadgeRed
                  : color == "purple" ? _sBadgePurple
                  : _sBadgeBlue;
            GUILayout.Label(label, s, GUILayout.ExpandWidth(false));
        }

        // ── API TABLE ROW ──────────────────────────────────────

        internal void ApiRow(string method, string returnType, string desc, string retColor = "blue")
        {
            // ── Styles ────────────────────────────────────────────────
            var mSt = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12, wordWrap = true,
                normal = { textColor = AccentCyan },
                padding = new RectOffset(8, 4, 5, 5),
            };

            Color retCol = retColor == "green"  ? AccentGreen
                         : retColor == "amber"  ? AccentAmber
                         : retColor == "red"    ? AccentRed
                         : retColor == "purple" ? AccentPurple
                         : AccentBlue;
            var retBg = retColor == "green"  ? BadgeGreenBg
                      : retColor == "amber"  ? BadgeAmberBg
                      : retColor == "red"    ? BadgeRedBg
                      : retColor == "purple" ? BadgePurpleBg
                      : BadgeBlueBg;

            var rSt = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11, fontStyle = FontStyle.Bold,
                normal = { textColor = retCol, background = Tex(retBg) },
                padding = new RectOffset(6, 6, 5, 5),
                fixedWidth = 72,
                alignment = TextAnchor.MiddleCenter,
            };

            var dSt = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12, richText = true, wordWrap = true,
                normal = { textColor = TxtSecond },
                padding = new RectOffset(8, 8, 5, 5),
            };

            // ── Column widths ─────────────────────────────────────────
            // Use _lastApiRowWidth (captured from previous Repaint frame) so that
            // CalcHeight() has the real width available during the Layout pass.
            const float kBadgeW = 72f;
            const float kGap    = 8f;
            const float kMinH   = 28f;

            float totalW = _lastApiRowWidth;
            float mW     = totalW * 0.38f;
            float dW     = totalW - mW - kBadgeW - kGap;

            float hMethod = mSt.CalcHeight(new GUIContent(method), mW);
            float hDesc   = dSt.CalcHeight(new GUIContent(desc),   Mathf.Max(dW, 80f));
            float rowH    = Mathf.Max(kMinH, hMethod, hDesc);

            // ── Reserve layout rect ───────────────────────────────────
            Rect rowRect = GUILayoutUtility.GetRect(0f, rowH, GUILayout.ExpandWidth(true));

            // Capture the real width for the next frame
            if (Event.current.type == EventType.Repaint && rowRect.width > 1f)
                _lastApiRowWidth = rowRect.width;

            // ── Draw ──────────────────────────────────────────────────
            EditorGUI.DrawRect(rowRect, BgSurface);
            EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.yMax - 1f, rowRect.width, 1f), BorderSub);

            // Recalculate with the actual rect width for pixel-perfect positioning
            float rW  = rowRect.width;
            float rmW = rW * 0.38f;
            float rdW = rW - rmW - kBadgeW - kGap;

            float badgeCenterY = rowRect.y + (rowH - kMinH) * 0.5f;

            GUI.Label(new Rect(rowRect.x,                     rowRect.y, rmW,    rowH), method,     mSt);
            GUI.Label(new Rect(rowRect.x + rmW,               badgeCenterY, kBadgeW, kMinH),  returnType, rSt);
            GUI.Label(new Rect(rowRect.x + rmW + kBadgeW + kGap, rowRect.y, rdW,    rowH), desc,       dSt);
        }

        internal void ApiHeader(string col1 = "Method / Property", string col2 = "Return", string col3 = "Mô tả")
        {
            var hSt = new GUIStyle(EditorStyles.label)
            {
                fontSize = 10, fontStyle = FontStyle.Bold,
                normal = { textColor = TxtMuted, background = _texBgElevated },
                padding = new RectOffset(8, 8, 6, 6),
            };
            GUILayout.BeginHorizontal();
            Rect hr = GUILayoutUtility.GetRect(0, 24);
            EditorGUI.DrawRect(hr, BgElevated);
            GUI.Label(new Rect(hr.x, hr.y, hr.width * 0.38f, 24), col1.ToUpper(), hSt);
            GUI.Label(new Rect(hr.x + hr.width * 0.38f, hr.y, 72, 24), col2.ToUpper(), hSt);
            GUI.Label(new Rect(hr.x + hr.width * 0.38f + 76, hr.y, hr.width - hr.width * 0.38f - 80, 24), col3.ToUpper(), hSt);
            GUILayout.EndHorizontal();
        }


    }  // partial class TISDocWindow
}
#endif