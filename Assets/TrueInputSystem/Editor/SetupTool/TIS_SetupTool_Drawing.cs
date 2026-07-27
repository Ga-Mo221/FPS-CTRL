// ============================================================
//  TrueInputSystem — InputBridgeSetupTool_Drawing.cs  [Partial]
//  Phần 2/5: Banner, Thanh công cụ, Thanh trạng thái,
//  Màn hình chào, và các hàm vẽ nguyên thủy dùng chung.
// ============================================================

#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TrueInputSystem.Editor
{
    public partial class TISSetupTool
    {
        // ══════════════════════════════════════════════════════
        //  MÀN HÌNH CHÀO
        // ══════════════════════════════════════════════════════

        private void DrawLandingScreen()
        {
            // Scan lười lần đầu
            if (_allProfiles == null) ScanAllProfiles();

            float w = position.width;
            float h = position.height;

            // Thanh nhấn trên cùng
            EditorGUI.DrawRect(new Rect(0, 0, w, 3f), C_Accent);

            // ── Card trung tâm ────────────────────────────────
            float cardW = Mathf.Min(540f, w - 80f);
            float cardH = 460f;
            float cardX = (w - cardW) * 0.5f;
            float cardY = (h - cardH) * 0.5f;

            EditorGUI.DrawRect(new Rect(cardX, cardY, cardW, cardH), C_BgPanel);
            DrawBorder(new Rect(cardX, cardY, cardW, cardH), C_Divider, 1f);
            EditorGUI.DrawRect(new Rect(cardX, cardY, 3f, cardH), C_Accent);

            float innerX = cardX + 28f;
            float innerW = cardW - 56f;

            // ── Tiêu đề ───────────────────────────────────────
            GUI.Label(new Rect(innerX, cardY + 22f, innerW, 28f), "True Input System",
                new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize  = 20,
                    normal    = { textColor = C_TextPrimary },
                    alignment = TextAnchor.MiddleLeft,
                });

            GUI.Label(new Rect(innerX, cardY + 52f, innerW, 18f),
                "Chọn profile để bắt đầu hoặc tạo profile mới.",
                new GUIStyle(EditorStyles.miniLabel)
                {
                    normal  = { textColor = C_TextSecond },
                    fontSize = 11,
                });

            EditorGUI.DrawRect(new Rect(cardX, cardY + 80f, cardW, 1f), C_Divider);

            // ══════════════════════════════════════════════════
            //  HEADER ROW: "MỞ PROFILE" + đếm + Refresh
            // ══════════════════════════════════════════════════

            int profileCount = _allProfiles?.Length ?? 0;

            GUI.Label(new Rect(innerX, cardY + 90f, 100f, 18f), "MỞ PROFILE",
                new GUIStyle(EditorStyles.miniLabel)
                {
                    normal    = { textColor = C_TextDim },
                    fontStyle = FontStyle.Bold,
                    fontSize  = 10,
                });

            GUI.Label(new Rect(innerX + 90f, cardY + 90f, 100f, 18f),
                profileCount == 0 ? "(không tìm thấy)" : $"({profileCount} profiles)",
                new GUIStyle(EditorStyles.miniLabel)
                {
                    normal  = { textColor = profileCount == 0 ? C_Orange : C_TextDim },
                    fontSize = 9,
                });

            var refreshRect = new Rect(innerX + innerW - 58f, cardY + 88f, 58f, 18f);
            EditorGUI.DrawRect(refreshRect, C_BgCard);
            DrawBorder(refreshRect, C_Divider, 1f);
            if (GUI.Button(refreshRect, "↻ Quét lại",
                new GUIStyle(EditorStyles.miniLabel)
                {
                    normal    = { textColor = C_TextSecond },
                    alignment = TextAnchor.MiddleCenter,
                }))
            {
                ScanAllProfiles();
                Repaint();
            }
            RegisterTooltip(refreshRect, "Quét lại toàn bộ project để tìm TISInputProfileConfig.asset.");

            // ══════════════════════════════════════════════════
            //  GRID PROFILE — giống Project Window Unity
            // ══════════════════════════════════════════════════

            float gridY    = cardY + 112f;
            float gridH    = 170f;
            var gridBgRect = new Rect(innerX, gridY, innerW, gridH);
            EditorGUI.DrawRect(gridBgRect, C_BgDeep);
            DrawBorder(gridBgRect, C_Divider, 1f);

            if (profileCount == 0)
            {
                // Placeholder
                GUI.Label(
                    new Rect(gridBgRect.x, gridBgRect.y, gridBgRect.width, gridBgRect.height),
                    "Không tìm thấy TISInputProfileConfig.asset trong project.\n" +
                    "Tạo profile mới bên dưới hoặc dùng ô Fallback để chọn thủ công.",
                    new GUIStyle(EditorStyles.miniLabel)
                    {
                        wordWrap  = true,
                        normal    = { textColor = C_TextDim },
                        alignment = TextAnchor.MiddleCenter,
                        fontSize  = 10,
                    });
            }
            else
            {
                // ── Kích thước ô ─────────────────────────────
                const float CELL_W    = 80f;   // chiều rộng ô (icon + label)
                const float ICON_SIZE = 52f;   // kích thước icon vuông
                const float LABEL_H   = 28f;   // chiều cao tên (2 dòng)
                const float CELL_H    = ICON_SIZE + LABEL_H + 8f;  // tổng chiều cao ô
                const float PAD_X     = 10f;
                const float PAD_Y     = 8f;

                // Số cột tự động theo chiều rộng vùng grid
                float availW    = innerW - PAD_X * 2f;
                int   cols      = Mathf.Max(1, Mathf.FloorToInt(availW / (CELL_W + PAD_X)));
                int   rows      = Mathf.CeilToInt((float)profileCount / cols);
                float contentH  = rows * (CELL_H + PAD_Y) + PAD_Y;

                var scrollRect    = new Rect(gridBgRect.x + 1f, gridBgRect.y + 1f,
                                             gridBgRect.width - 2f, gridBgRect.height - 2f);
                var contentRect   = new Rect(0, 0, scrollRect.width - 14f, contentH);

                _profileGridScroll = GUI.BeginScrollView(scrollRect, _profileGridScroll, contentRect);

                var ev = Event.current;

                for (int i = 0; i < profileCount; i++)
                {
                    var profile = _allProfiles[i];
                    if (profile == null) continue;

                    int col = i % cols;
                    int row = i / cols;

                    float cx = PAD_X + col * (CELL_W + PAD_X);
                    float cy = PAD_Y + row * (CELL_H + PAD_Y);

                    var cellRect  = new Rect(cx, cy, CELL_W, CELL_H);
                    var iconRect  = new Rect(cx + (CELL_W - ICON_SIZE) * 0.5f, cy + 2f, ICON_SIZE, ICON_SIZE);
                    var labelRect = new Rect(cx, cy + ICON_SIZE + 4f, CELL_W, LABEL_H);

                    bool hovered = cellRect.Contains(ev.mousePosition);

                    // ── Nền ô ────────────────────────────────
                    if (hovered)
                    {
                        EditorGUI.DrawRect(cellRect, C_BgMapSel * 0.55f);
                        DrawBorder(cellRect, C_Accent * 0.6f, 1f);
                    }

                    // ── Icon ─────────────────────────────────
                    Texture2D icon = GetProfileIconOrFallback(
                        i < _allProfileIcons.Length ? _allProfileIcons[i] : null);

                    if (icon != null)
                    {
                        // Vẽ nền icon (ô vuông nhỏ hơn một chút, như Unity)
                        var iconBgRect = new Rect(iconRect.x - 1f, iconRect.y - 1f,
                                                  iconRect.width + 2f, iconRect.height + 2f);
                        EditorGUI.DrawRect(iconBgRect, C_BgCard);
                        GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
                    }
                    else
                    {
                        // Placeholder nếu không có icon
                        EditorGUI.DrawRect(iconRect, C_BgCard);
                        GUI.Label(iconRect, "?",
                            new GUIStyle(EditorStyles.boldLabel)
                            {
                                fontSize  = 22,
                                normal    = { textColor = C_TextDim },
                                alignment = TextAnchor.MiddleCenter,
                            });
                    }

                    // ── Tên profile ──────────────────────────
                    string displayName = profile.profileName ?? "(không tên)";
                    GUI.Label(labelRect, displayName,
                        new GUIStyle(EditorStyles.miniLabel)
                        {
                            wordWrap  = true,
                            clipping  = TextClipping.Clip,
                            alignment = TextAnchor.UpperCenter,
                            fontSize  = 10,
                            normal    = { textColor = hovered ? C_TextPrimary : C_TextSecond },
                            fontStyle = hovered ? FontStyle.Bold : FontStyle.Normal,
                        });

                    // ── Click để mở ──────────────────────────
                    if (ev.type == EventType.MouseDown && ev.button == 0
                        && cellRect.Contains(ev.mousePosition))
                    {
                        LoadFromAsset(profile);
                        _landingDropTarget = null;
                        _showLanding       = false;
                        ev.Use();
                    }

                    // Tooltip = đường dẫn asset
                    RegisterTooltip(cellRect, AssetDatabase.GetAssetPath(profile));
                }

                GUI.EndScrollView();
            }

            // ══════════════════════════════════════════════════
            //  FALLBACK — ObjectField + nút "…"  (giữ nguyên)
            // ══════════════════════════════════════════════════

            float fallbackY = gridY + gridH + 10f;

            GUI.Label(new Rect(innerX, fallbackY, innerW, 16f),
                "Không thấy trong danh sách? Chọn thủ công:",
                new GUIStyle(EditorStyles.miniLabel)
                {
                    normal   = { textColor = C_TextDim },
                    fontSize = 9,
                });
            fallbackY += 18f;

            float fallbackFieldW  = innerW - 36f;
            var   fallbackFldRect = new Rect(innerX, fallbackY, fallbackFieldW, 22f);
            EditorGUI.DrawRect(fallbackFldRect, C_BgCard);
            DrawBorder(fallbackFldRect,
                _landingDropTarget != null ? C_Accent * 0.5f : C_Divider, 1f);

            var newDrop = (TISInputProfileConfig)EditorGUI.ObjectField(
                new Rect(innerX + 4f, fallbackY + 2f, fallbackFieldW - 8f, 18f),
                _landingDropTarget, typeof(TISInputProfileConfig), false);
            if (newDrop != _landingDropTarget) { _landingDropTarget = newDrop; Repaint(); }

            var browseRect = new Rect(innerX + fallbackFieldW + 4f, fallbackY, 28f, 22f);
            EditorGUI.DrawRect(browseRect, C_BgCard);
            DrawBorder(browseRect, C_Divider, 1f);
            if (GUI.Button(browseRect, GUIContent.none, GUIStyle.none))
            {
                LoadFromAssetDialog();
                if (_workingProfile != null) { _showLanding = false; _landingDropTarget = null; }
            }
            GUI.Label(browseRect, "…",
                new GUIStyle(EditorStyles.boldLabel)
                {
                    normal    = { textColor = C_TextSecond },
                    alignment = TextAnchor.MiddleCenter,
                    fontSize  = 14,
                });
            RegisterTooltip(browseRect, "Duyệt để chọn file .asset từ thư mục Project.");

            fallbackY += 28f;
            bool canOpen  = _landingDropTarget != null;
            var  openRect = new Rect(innerX, fallbackY, innerW, 28f);
            EditorGUI.DrawRect(openRect, canOpen ? C_BgMapSel : C_BgCard);
            DrawBorder(openRect, canOpen ? C_Accent * 0.5f : C_Divider, 1f);
            if (GUI.Button(openRect, GUIContent.none, GUIStyle.none) && canOpen)
            {
                LoadFromAsset(_landingDropTarget);
                _landingDropTarget = null;
                _showLanding       = false;
            }
            GUI.Label(openRect, "▶  Mở",
                new GUIStyle(EditorStyles.boldLabel)
                {
                    normal    = { textColor = canOpen ? C_Accent : C_TextDim },
                    alignment = TextAnchor.MiddleCenter,
                    fontSize  = 13,
                });
            RegisterTooltip(openRect, "Mở profile đang chọn trong ô trên.");

            // ══════════════════════════════════════════════════
            //  TẠO MỚI
            // ══════════════════════════════════════════════════

            EditorGUI.DrawRect(new Rect(cardX, cardY + cardH - 78f, cardW, 1f), C_DividerLight);
            GUI.Label(new Rect(innerX, cardY + cardH - 68f, innerW, 18f), "TẠO MỚI",
                new GUIStyle(EditorStyles.miniLabel)
                {
                    normal    = { textColor = C_TextDim },
                    fontStyle = FontStyle.Bold,
                    fontSize  = 10,
                });

            float newBtnY = cardY + cardH - 48f;
            var   newRect  = new Rect(innerX, newBtnY, innerW, 34f);
            bool  hoverNew = newRect.Contains(Event.current.mousePosition);
            EditorGUI.DrawRect(newRect, hoverNew ? C_BgCard * 1.3f : C_BgCard);
            DrawBorder(newRect, hoverNew ? C_Green * 0.5f : C_Divider, 1f);
            if (GUI.Button(newRect, GUIContent.none, GUIStyle.none))
            {
                NewProfile();
                _showLanding = false;
            }
            GUI.Label(newRect, "⊕  Profile Mới",
                new GUIStyle(EditorStyles.boldLabel)
                {
                    normal    = { textColor = hoverNew ? C_Green : C_TextPrimary },
                    alignment = TextAnchor.MiddleCenter,
                    fontSize  = 13,
                });
            RegisterTooltip(newRect,
                "Tạo profile mới với cấu hình mặc định (Move, Jump, Look, Attack, Skill1, Skill2).");

            // Nhãn phiên bản
            GUI.Label(new Rect(cardX, cardY + cardH - 14f, cardW - 10f, 14f),
                "True Input System  v2",
                new GUIStyle(EditorStyles.miniLabel)
                {
                    normal    = { textColor = C_TextDim },
                    alignment = TextAnchor.MiddleRight,
                    fontSize  = 10,
                });

            if (Event.current.type == EventType.MouseMove)
                Repaint();
        }

        // ══════════════════════════════════════════════════════
        //  BANNER
        // ══════════════════════════════════════════════════════

        private void DrawBanner()
        {
            var bannerRect = EditorGUILayout.GetControlRect(false, 44f);
            EditorGUI.DrawRect(bannerRect, C_BgBanner);
            EditorGUI.DrawRect(new Rect(bannerRect.x, bannerRect.y, 3f, bannerRect.height), C_Accent);

            // Tiêu đề
            GUI.Label(new Rect(bannerRect.x, bannerRect.y, 240f, bannerRect.height),
                "True Input System",
                new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize  = 16,
                    normal    = { textColor = C_TextPrimary },
                    padding   = new RectOffset(16, 0, 0, 0),
                    alignment = TextAnchor.MiddleLeft,
                });

            // Chip "SETUP"
            var chipRect = new Rect(bannerRect.x + 192f, bannerRect.y + 13f, 55f, 18f);
            DrawRoundedRect(chipRect, C_AccentDim, 3f);
            GUI.Label(chipRect, "SETUP",
                new GUIStyle(EditorStyles.miniLabel)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize  = 9,
                    normal    = { textColor = C_Accent },
                    alignment = TextAnchor.MiddleCenter,
                });

            // Nhãn đường dẫn asset
            if (_workingProfile == null) return;

            string assetLabel = _sourceAsset != null
                ? AssetDatabase.GetAssetPath(_sourceAsset)
                : "— chưa lưu —";
            if (_isDirty) assetLabel += "  ●";

            GUI.Label(
                new Rect(bannerRect.x + 258f, bannerRect.y, bannerRect.width - 450f, bannerRect.height),
                assetLabel,
                new GUIStyle(EditorStyles.miniLabel)
                {
                    normal    = { textColor = _isDirty ? C_Orange : C_TextDim },
                    alignment = TextAnchor.MiddleLeft,
                    clipping  = TextClipping.Clip,
                });

            // Nhãn "Profile:"
            var profileLabelRect = new Rect(bannerRect.xMax - 310f, bannerRect.y + 9f, 60f, 22f);
            GUI.Label(profileLabelRect, "Profile:",
                new GUIStyle(EditorStyles.miniLabel)
                {
                    normal    = { textColor = C_TextSecond },
                    alignment = TextAnchor.MiddleRight,
                });

            // Ô nhập tên profile
            var profileFieldRect = new Rect(bannerRect.xMax - 244f, bannerRect.y + 10f, 180f, 22f);
            EditorGUI.DrawRect(profileFieldRect, C_BgCard);
            DrawBorder(profileFieldRect, C_Divider, 1f);

            string newProfileName = GUI.TextField(profileFieldRect,
                _workingProfile.profileName, _styleProfileNameField);
            if (newProfileName != _workingProfile.profileName)
            {
                SafeRecord("Đổi tên Profile");
                _workingProfile.profileName = newProfileName;
                MarkDirty();
            }
            RegisterTooltip(profileFieldRect, "Tên hiển thị của profile. Dùng khi log và debug.");

            // Nút Trợ giúp
            var helpRect = new Rect(bannerRect.xMax - 54f, bannerRect.y + 10f, 44f, 22f);
            EditorGUI.DrawRect(helpRect, C_BgCard);
            DrawBorder(helpRect, C_Divider, 1f);
            if (GUI.Button(helpRect, "? Trợ giúp",
                new GUIStyle(EditorStyles.miniLabel)
                {
                    normal    = { textColor = C_TextAccent },
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    fontSize  = 9,
                }))
            {
                TISDocWindow.OpenWindow();
            }
            RegisterTooltip(helpRect, "Mở cửa sổ hướng dẫn sử dụng đầy đủ.");
        }

        // ══════════════════════════════════════════════════════
        //  THANH CÔNG CỤ
        // ══════════════════════════════════════════════════════

        //[Obsolete("Obsolete")]
        private void DrawToolbar()
        {
            var toolbarRect = EditorGUILayout.GetControlRect(false, 34f);
            EditorGUI.DrawRect(toolbarRect, C_BgToolbar);
            EditorGUI.DrawRect(
                new Rect(toolbarRect.x, toolbarRect.yMax - 1f, toolbarRect.width, 1f), C_Divider);

            float x    = toolbarRect.x + 10f;
            float btnH = 24f;
            float btnY = toolbarRect.y + (toolbarRect.height - btnH) * 0.5f;

            // Phím tắt Ctrl+S
            var ev = Event.current;
            if (ev.type == EventType.KeyDown &&
                ev.keyCode == KeyCode.S &&
                (ev.control || ev.command))
            {
                SaveAsset();
                ev.Use();
            }

            // Nhóm File
            x = DrawToolbarButton(x, btnY, btnH, "⊕ Mới", C_TextSecond, C_BgCard,
                () => { if (ConfirmDiscardChanges()) NewProfile(); },
                "Tạo profile mới với cấu hình mặc định.");
            x += 4f;
            x = DrawToolbarButton(x, btnY, btnH, "⊙ Mở", C_TextSecond, C_BgCard,
                () => { if (ConfirmDiscardChanges()) LoadFromAssetDialog(); },
                "Mở file .asset profile có sẵn.");
            x += 8f;
            EditorGUI.DrawRect(new Rect(x, toolbarRect.y + 6f, 1f, toolbarRect.height - 12f), C_Divider);
            x += 10f;

            // Nhóm Lưu
            bool hasDirty = _isDirty;
            Color saveTextCol = hasDirty ? C_Orange : C_TextSecond;
            Color saveBgCol   = hasDirty ? new Color(0.22f, 0.15f, 0.04f) : C_BgCard;

            float saveBtnW = CalcButtonWidth("Lưu") + 16f;
            x = DrawToolbarButton(x, btnY, btnH, "Lưu", saveTextCol, saveBgCol,
                () => SaveAsset(),
                "Lưu nhanh vào file .asset hiện tại (Ctrl+S).\nNếu chưa có file sẽ hiện hộp thoại chọn nơi lưu.");
            if (hasDirty)
                DrawBorder(new Rect(x - saveBtnW, btnY, saveBtnW, btnH), C_Orange * 0.5f, 1f);
            x += 4f;
            x = DrawToolbarButton(x, btnY, btnH, "Lưu Thành…", C_TextSecond, C_BgCard,
                () => SaveAssetAs(),
                "Lưu ra file .asset mới tại vị trí bạn chọn.");
            x += 8f;
            EditorGUI.DrawRect(new Rect(x, toolbarRect.y + 6f, 1f, toolbarRect.height - 12f), C_Divider);
            x += 10f;

            // Hành động
            x = DrawToolbarButton(x, btnY, btnH, "Kiểm tra", C_Orange, C_BgCard,
                () => RunValidation(),
                "Kiểm tra lỗi cấu hình: tên trùng, binding xung đột, map trống...");
            x += 4f;
            x = DrawToolbarButton(x, btnY, btnH, "▶ Gán vào Scene", C_Accent, C_BgCard,
                () => AssignToScene(),
                "Tự động tìm TISInputBridge trong scene và gán profile hiện tại vào.");

            // ── Platform toggles (PC / Gamepad / Mobile) ─────────
            x += 10f;
            EditorGUI.DrawRect(new Rect(x, toolbarRect.y + 6f, 1f, toolbarRect.height - 12f), C_Divider);
            x += 10f;

            void PlatformToggle(ref float px, string label, ref bool flag, Color onCol, string tip)
            {
                float tw = CalcButtonWidth(label) + 14f;
                var tr = new Rect(px, btnY, tw, btnH);
                bool hover = tr.Contains(Event.current.mousePosition);

                Color bgCol2 = flag  ? new Color(onCol.r * 0.22f, onCol.g * 0.22f, onCol.b * 0.22f)
                                     : C_BgCard;
                Color bdCol2 = flag  ? onCol * 0.75f
                             : hover ? C_DividerLight
                                     : C_Divider;
                EditorGUI.DrawRect(tr, bgCol2);
                DrawBorder(tr, bdCol2, 1f);
                if (flag)
                    EditorGUI.DrawRect(new Rect(tr.x, tr.yMax - 2f, tr.width, 2f), onCol * 0.9f);

                GUI.Label(tr, label,
                    new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal    = { textColor = flag ? onCol : (hover ? C_TextSecond : C_TextDim) },
                        fontStyle = flag ? FontStyle.Bold : FontStyle.Normal,
                        alignment = TextAnchor.MiddleCenter,
                        fontSize  = 10,
                    });

                if (GUI.Button(tr, GUIContent.none, GUIStyle.none))
                {
                    SafeRecord("Đổi Platform");
                    flag = !flag;
                    MarkDirty();
                }
                RegisterTooltip(tr, tip);
                px += tw + 3f;
            }

            bool pc      = _workingProfile.enablePC;
            bool gamepad = _workingProfile.enableGamepad;
            bool mobile  = _workingProfile.enableMobile;

            PlatformToggle(ref x, "PC",      ref pc,      C_Accent,  "Bật/tắt Keyboard & Mouse binding cho toàn profile.");
            PlatformToggle(ref x, "Gamepad", ref gamepad, C_Green,   "Bật/tắt Gamepad binding cho toàn profile.");
            PlatformToggle(ref x, "Mobile",  ref mobile,  C_Orange,  "Bật/tắt Mobile (Virtual Joystick / On-Screen Button) binding cho toàn profile.");

            _workingProfile.enablePC      = pc;
            _workingProfile.enableGamepad = gamepad;
            _workingProfile.enableMobile  = mobile;
            // ─────────────────────────────────────────────────────

            // Huy hiệu kết quả kiểm tra
            if (_validationRan)
            {
                string badgeText = _validationErrors.Count == 0 ? "✔  Hợp lệ" : $"✘  {_validationErrors.Count} lỗi";
                Color  badgeCol  = _validationErrors.Count == 0 ? C_Green : C_Red;

                var badgeRect = new Rect(toolbarRect.xMax - 240f, btnY, 110f, btnH);
                EditorGUI.DrawRect(badgeRect, C_BgCard);
                DrawBorder(badgeRect, badgeCol * 0.7f, 1f);
                GUI.Label(badgeRect, badgeText,
                    new GUIStyle(EditorStyles.boldLabel)
                    {
                        normal    = { textColor = badgeCol },
                        alignment = TextAnchor.MiddleCenter,
                        fontSize  = 11,
                    });
                RegisterTooltip(badgeRect,
                    _validationErrors.Count == 0
                        ? "Profile hợp lệ, sẵn sàng sử dụng."
                        : $"Tìm thấy {_validationErrors.Count} lỗi cấu hình. Xem chi tiết ở phần danh sách action.");
            }

            // Nút Về trang chủ
            var backRect = new Rect(toolbarRect.xMax - 120f, btnY, 110f, btnH);
            EditorGUI.DrawRect(backRect, C_BgCard);
            DrawBorder(backRect, C_Divider, 1f);
            if (GUI.Button(backRect, GUIContent.none, GUIStyle.none))
            {
                if (ConfirmDiscardChanges())
                {
                    _workingProfile    = null;
                    _sourceAsset       = null;
                    _landingDropTarget = null;
                    _isDirty           = false;
                    _validationRan     = false;
                    _showLanding       = true;
                    Repaint();
                }
            }
            GUI.Label(backRect, "← Trang Chủ",
                new GUIStyle(EditorStyles.miniLabel)
                {
                    normal    = { textColor = C_TextSecond },
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    fontSize  = 11,
                });
            RegisterTooltip(backRect, "Quay về màn hình chào để mở hoặc tạo profile khác.");
        }

        private float DrawToolbarButton(float x, float y, float h, string label,
            Color textCol, Color bgCol, Action onClick, string tooltip = "")
        {
            float w = CalcButtonWidth(label) + 16f;
            var r = new Rect(x, y, w, h);

            bool hover = r.Contains(Event.current.mousePosition);
            EditorGUI.DrawRect(r, hover ? C_BgCard * 1.3f : bgCol);
            DrawBorder(r, hover ? C_DividerLight : C_Divider, 1f);

            GUI.Label(r, label,
                new GUIStyle(EditorStyles.miniLabel)
                {
                    normal    = { textColor = hover ? textCol * 1.2f : textCol },
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    fontSize  = 11,
                });

            if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                onClick?.Invoke();

            if (!string.IsNullOrEmpty(tooltip))
                RegisterTooltip(r, tooltip);

            return x + w;
        }

        // ══════════════════════════════════════════════════════
        //  THANH TRẠNG THÁI
        // ══════════════════════════════════════════════════════

        private void DrawStatusBar()
        {
            if (_workingProfile == null) return;

            var rect = EditorGUILayout.GetControlRect(false, 22f);
            EditorGUI.DrawRect(rect, C_BgStatus);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), C_Divider);

            int totalMaps    = _workingProfile.actionMaps?.Count ?? 0;
            int totalActions = _workingProfile.actionMaps?.Sum(m => m.actions?.Count ?? 0) ?? 0;

            GUI.Label(new Rect(rect.x, rect.y, 220f, rect.height),
                $"  {totalMaps} nhóm  ·  {totalActions} hành động",
                new GUIStyle(EditorStyles.miniLabel)
                {
                    normal    = { textColor = C_TextDim },
                    alignment = TextAnchor.MiddleLeft,
                });

            // Trạng thái lưu
            if (_isDirty)
            {
                var dirtyRect = new Rect(rect.x + 210f, rect.y + 5f, 70f, 12f);
                EditorGUI.DrawRect(dirtyRect, new Color(0.35f, 0.22f, 0.06f));
                GUI.Label(dirtyRect, "● Chưa lưu",
                    new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal    = { textColor = C_Orange },
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter,
                        fontSize  = 9,
                    });
                RegisterTooltip(dirtyRect, "Có thay đổi chưa được lưu vào file .asset. Nhấn Ctrl+S để lưu nhanh.");
            }
            else if (_validationRan && _validationErrors.Count == 0)
            {
                var okRect = new Rect(rect.x + 210f, rect.y + 5f, 60f, 12f);
                EditorGUI.DrawRect(okRect, new Color(0.08f, 0.22f, 0.12f));
                GUI.Label(okRect, "✔ Đã lưu",
                    new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal    = { textColor = C_Green },
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter,
                        fontSize  = 9,
                    });
            }

            // Gợi ý thao tác
            GUI.Label(
                new Rect(rect.xMax - 460f, rect.y, 455f, rect.height),
                "Click chọn nhóm  ·  Double-click đổi tên  ·  Chuột phải → tuỳ chọn  ·  Chuột phải vào binding để gõ tay",
                new GUIStyle(EditorStyles.miniLabel)
                {
                    normal    = { textColor = C_TextDim },
                    alignment = TextAnchor.MiddleRight,
                });
        }

        // ══════════════════════════════════════════════════════
        //  HÀM VẼ NGUYÊN THỦY DÙNG CHUNG
        // ══════════════════════════════════════════════════════

        /// <summary>Vẽ viền 4 cạnh với độ dày và màu tuỳ chỉnh.</summary>
        internal static void DrawBorder(Rect rect, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        /// <summary>Giả lập hình chữ nhật bo góc bằng cách fill + cắt góc.</summary>
        internal static void DrawRoundedRect(Rect rect, Color color, float radius)
        {
            EditorGUI.DrawRect(new Rect(rect.x + radius, rect.y, rect.width - radius * 2f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + radius, rect.width, rect.height - radius * 2f), color);
        }

        /// <summary>Vẽ header panel với sọc nhấn trái.</summary>
        internal static void DrawPanelHeader(string title, float width)
        {
            var rect = EditorGUILayout.GetControlRect(false, 28f);
            EditorGUI.DrawRect(rect, C_BgPanel);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), C_Divider);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + 4f, 3f, rect.height - 8f), C_Accent);

            GUI.Label(rect, title,
                new GUIStyle(EditorStyles.boldLabel)
                {
                    normal    = { textColor = C_TextPrimary },
                    fontSize  = 11,
                    padding   = new RectOffset(12, 0, 0, 0),
                    alignment = TextAnchor.MiddleLeft,
                });
        }

        /// <summary>Hiển thị trạng thái trống ở giữa panel.</summary>
        internal static void DrawEmptyState(string message)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();

            var rect = EditorGUILayout.GetControlRect(false, 60f, GUILayout.Width(300f));
            EditorGUI.DrawRect(rect, C_BgCard);
            DrawBorder(rect, C_Divider, 1f);
            GUI.Label(rect, message,
                new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    wordWrap  = true,
                    fontSize  = 11,
                    normal    = { textColor = C_TextDim },
                    alignment = TextAnchor.MiddleCenter,
                });

            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
        
        // ══════════════════════════════════════════════════════
        //  SCAN + ICON CACHE
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Quét toàn bộ AssetDatabase tìm TISInputProfileConfig,
        /// đồng thời cache icon đã gán qua EditorGUIUtility.GetIconForObject.
        /// </summary>
        private void ScanAllProfiles()
        {
            var guids = AssetDatabase.FindAssets("t:TISInputProfileConfig");

            var profiles = guids
                .Select(g => AssetDatabase.LoadAssetAtPath<TISInputProfileConfig>(
                    AssetDatabase.GUIDToAssetPath(g)))
                .Where(a => a != null)
                .OrderBy(a => a.profileName)
                .ToArray();

            _allProfiles     = profiles;
            _allProfileIcons = profiles
                .Select(p => EditorGUIUtility.GetIconForObject(p) as Texture2D)
                .ToArray();
        }

        /// <summary>
        /// Lấy icon fallback khi profile chưa có icon tùy chỉnh:
        /// dùng icon ScriptableObject mặc định của Unity.
        /// </summary>
        private static Texture2D GetProfileIconOrFallback(Texture2D icon)
        {
            if (icon != null) return icon;
            // "ScriptableObject Icon" là tên nội bộ Unity cho icon SO mặc định
            return EditorGUIUtility.IconContent("ScriptableObject Icon").image as Texture2D;
        }
    }
}
#endif