// ============================================================
//  TrueInputSystem — InputBridgeSetupTool_Panels.cs  [Partial]
//  Phần 3/5: Bố cục khu vực chính
//
//  BỐ CỤC (3 cột — tỉ lệ theo chiều rộng cửa sổ):
//  ┌─ Panel Trái (~18%) ─┬── Panel Giữa (~54%) ──┬─ Panel Phải (~28%) ──┐
//  │  Nhóm Action        │  Danh sách hành động  │ [Cài đặt][Nền tảng] │
//  └─────────────────────┴───────────────────────┴──────────────────────┘
// ============================================================

#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TrueInputSystem.Editor
{
    public struct BindingOption
    {
        public string Text;
        public string Value;
        public BindingOption(string text, string value) { Text = text; Value = value; }
    }
    
    public partial class TISSetupTool
    {
        // ══════════════════════════════════════════════════════
        //  TRẠNG THÁI — action được chọn
        // ══════════════════════════════════════════════════════

        private int _selectedActionIndex = -1;
        private int _rightPanelTab       = 0;  // 0 = Cài Đặt Action, 1 = Cài Đặt Nền Tảng
        private readonly string[] _rightPanelTabs = { "Cài Đặt Action", "Cài Đặt Nền Tảng" };

        // ══════════════════════════════════════════════════════
        //  KHU VỰC CHÍNH
        // ══════════════════════════════════════════════════════

        private void DrawMainArea()
        {
            // Guard: nút Back Home set _workingProfile = null trong cùng frame
            // trước khi DrawMainArea kết thúc → vẽ placeholder để layout không bị lệch.
            if (_workingProfile == null)
            {
                GUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                return;
            }

            GUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));

            DrawLeftPanel();

            // Đường kẻ dọc
            var dv1 = EditorGUILayout.GetControlRect(false, 0f,
                GUILayout.Width(1f), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(dv1, C_Divider);

            DrawCenterPanel();

            // Đường kẻ dọc
            var dv2 = EditorGUILayout.GetControlRect(false, 0f,
                GUILayout.Width(1f), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(dv2, C_Divider);

            DrawRightPanel();

            GUILayout.EndHorizontal();
        }

        // ══════════════════════════════════════════════════════
        //  PANEL TRÁI — DANH SÁCH NHÓM ACTION
        // ══════════════════════════════════════════════════════

        private void DrawLeftPanel()
        {
            float panelW = LeftPanelW;
            GUILayout.BeginVertical(GUILayout.Width(panelW));
            EditorGUI.DrawRect(
                EditorGUILayout.GetControlRect(false, 0f, GUILayout.ExpandHeight(false)),
                C_BgPanel);

            DrawPanelHeader("  Nhóm Action", panelW);

            // Thanh thống kê
            int totalMaps = _workingProfile.actionMaps?.Count ?? 0;
            var subRect   = EditorGUILayout.GetControlRect(false, 18f);
            EditorGUI.DrawRect(subRect, C_BgDeep);
            GUI.Label(subRect, $"  {totalMaps} nhóm",
                new GUIStyle(EditorStyles.miniLabel)
                {
                    normal    = { textColor = C_TextDim },
                    alignment = TextAnchor.MiddleLeft,
                    padding   = new RectOffset(6, 0, 0, 0),
                });

            // Danh sách nhóm
            _mapListScroll = GUILayout.BeginScrollView(_mapListScroll,
                GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            var maps = _workingProfile.actionMaps;
            for (int i = 0; i < maps.Count; i++)
                DrawMapItem(maps[i], i, i == _selectedMapIndex);
            GUILayout.FlexibleSpace();
            GUILayout.EndScrollView();

            // Thanh dưới: Thêm / Xoá
            var footerRect = EditorGUILayout.GetControlRect(false, 34f);
            EditorGUI.DrawRect(footerRect, C_BgDeep);

            var addRect = new Rect(footerRect.x + 6f, footerRect.y + 5f,
                footerRect.width - 36f, 24f);
            EditorGUI.DrawRect(addRect, C_BgCard);
            DrawBorder(addRect, C_Green * 0.5f, 1f);
            if (GUI.Button(addRect, "＋  Thêm Nhóm",
                new GUIStyle(EditorStyles.miniLabel)
                {
                    normal    = { textColor = C_Green },
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    fontSize  = 11,
                }))
                AddNewMap();
            RegisterTooltip(addRect, "Thêm Action Map mới.\nVí dụ: Nhóm xe cộ, nhóm UI, nhóm chiến đấu...");

            var removeRect = new Rect(footerRect.xMax - 28f, footerRect.y + 5f, 22f, 24f);
            EditorGUI.DrawRect(removeRect, C_BgCard);
            DrawBorder(removeRect, C_Red * 0.4f, 1f);
            if (GUI.Button(removeRect, "－",
                new GUIStyle(EditorStyles.miniLabel)
                {
                    normal    = { textColor = C_Red },
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    fontSize  = 13,
                }))
            {
                if (_selectedMapIndex < maps.Count)
                    RemoveMap(_selectedMapIndex);
            }
            RegisterTooltip(removeRect, "Xoá nhóm đang chọn (yêu cầu xác nhận nếu có hành động bên trong).");

            GUILayout.EndVertical();
        }

        // ── Mục nhóm ─────────────────────────────────────────

        private void DrawMapItem(TISActionMapData map, int index, bool selected)
        {
            // Chế độ đổi tên
            if (_isRenaming && _renamingIndex == index)
            {
                var renameRect = EditorGUILayout.GetControlRect(false, 30f);
                EditorGUI.DrawRect(renameRect, C_BgCard);
                DrawBorder(renameRect, C_Accent * 0.6f, 1f);

                GUILayout.BeginHorizontal();
                GUI.SetNextControlName("RenameField");
                _renameBuffer = GUI.TextField(
                    new Rect(renameRect.x + 4f, renameRect.y + 5f, renameRect.width - 50f, 20f),
                    _renameBuffer,
                    new GUIStyle(EditorStyles.textField)
                    { normal = { textColor = C_TextPrimary } });

                if (GUI.Button(new Rect(renameRect.xMax - 44f, renameRect.y + 5f, 20f, 20f), "✔",
                    new GUIStyle(EditorStyles.miniButton) { normal = { textColor = C_Green } }))
                    CommitRename();

                if (GUI.Button(new Rect(renameRect.xMax - 22f, renameRect.y + 5f, 20f, 20f), "✕",
                    new GUIStyle(EditorStyles.miniButton) { normal = { textColor = C_Red } }))
                    CancelRename();

                GUILayout.EndHorizontal();

                if (Event.current.type == EventType.Layout)
                    EditorGUI.FocusTextInControl("RenameField");
                if (Event.current.type == EventType.KeyDown)
                {
                    if (Event.current.keyCode == KeyCode.Return)  { CommitRename(); Event.current.Use(); }
                    if (Event.current.keyCode == KeyCode.Escape)  { CancelRename(); Event.current.Use(); }
                }
                return;
            }

            var rect  = EditorGUILayout.GetControlRect(false, 36f);
            bool hover = rect.Contains(Event.current.mousePosition);

            Color bg = selected ? C_BgMapSel
                     : hover    ? C_BgMapHov
                                : (index % 2 == 0 ? C_BgPanel : C_BgCard);
            EditorGUI.DrawRect(rect, bg);

            if (selected)
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), C_Accent);

            // Chấm trạng thái
            Color dotColor = map.enabledByDefault ? C_Green : C_TextDim;
            EditorGUI.DrawRect(
                new Rect(rect.x + 10f, rect.y + (rect.height - 8f) * 0.5f, 8f, 8f), dotColor);

            // Tên nhóm
            GUI.Label(
                new Rect(rect.x + 24f, rect.y, rect.width - 58f, rect.height),
                map.mapName,
                new GUIStyle(EditorStyles.label)
                {
                    normal    = { textColor = selected ? Color.white : C_TextPrimary },
                    fontStyle = selected ? FontStyle.Bold : FontStyle.Normal,
                    fontSize  = 12,
                    alignment = TextAnchor.MiddleLeft,
                    clipping  = TextClipping.Clip,
                });

            // Huy hiệu số lượng action
            int count  = map.actions?.Count ?? 0;
            float badgeW = count > 9 ? 26f : 20f;
            var badgeRect = new Rect(rect.xMax - badgeW - 6f,
                rect.y + (rect.height - 18f) * 0.5f, badgeW, 18f);
            EditorGUI.DrawRect(badgeRect, selected ? C_AccentDim : C_BgDeep);
            GUI.Label(badgeRect, count.ToString(),
                new GUIStyle(EditorStyles.miniLabel)
                {
                    normal    = { textColor = selected ? C_Accent : C_TextSecond },
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                });

            EditorGUI.DrawRect(
                new Rect(rect.x, rect.yMax - 1f, rect.width, 1f),
                C_Divider * (selected ? 0.5f : 1f));

            // Tooltip nhóm
            string mapTooltip = map.enabledByDefault
                ? $"Nhóm \"{map.mapName}\" — bật mặc định khi load profile.\n{count} hành động."
                : $"Nhóm \"{map.mapName}\" — tắt mặc định, phải bật thủ công bằng SetActionMapActive().\n{count} hành động.";
            RegisterTooltip(rect, mapTooltip);

            // Sự kiện click / double-click / chuột phải
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                if (_selectedMapIndex != index) _selectedActionIndex = -1;
                _selectedMapIndex = index;
                _isRenaming       = false;
                GUI.FocusControl(null);
                Event.current.Use();
                Repaint();
            }
            if (Event.current.type == EventType.MouseDown &&
                Event.current.clickCount == 2 &&
                rect.Contains(Event.current.mousePosition))
            {
                BeginRename(index);
                Event.current.Use();
            }
            if (Event.current.type == EventType.ContextClick &&
                rect.Contains(Event.current.mousePosition))
            {
                var menu = new UnityEditor.GenericMenu();
                int ci = index;
                menu.AddItem(new GUIContent("Đổi tên"),  false, () => BeginRename(ci));
                menu.AddItem(new GUIContent("Nhân đôi"), false, () => DuplicateMap(ci));
                menu.AddSeparator("");
                menu.AddItem(new GUIContent(map.enabledByDefault ? "Tắt mặc định" : "Bật mặc định"),
                    false, () => ToggleMapEnabled(ci));
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Xoá"), false, () => RemoveMap(ci));
                menu.ShowAsContext();
                Event.current.Use();
            }
        }

        // ══════════════════════════════════════════════════════
        //  PANEL GIỮA — DANH SÁCH HÀNH ĐỘNG
        // ══════════════════════════════════════════════════════

        private void DrawCenterPanel()
        {
            // Chiều rộng còn lại sau hai panel cố định
            float centerW = position.width - LeftPanelW - RightPanelW - 2f; // 2 đường kẻ
            centerW = Mathf.Max(180f, centerW);

            GUILayout.BeginVertical(GUILayout.Width(centerW));
            var maps = _workingProfile.actionMaps;

            if (maps == null || maps.Count == 0)
            {
                DrawPanelHeader("  Hành Động", centerW);
                GUILayout.FlexibleSpace();
                DrawEmptyState("Chưa có nhóm nào.\nThêm nhóm trước.");
                GUILayout.FlexibleSpace();
                GUILayout.EndVertical();
                return;
            }

            _selectedMapIndex = Mathf.Clamp(_selectedMapIndex, 0, maps.Count - 1);
            var map = maps[_selectedMapIndex];

            // Header breadcrumb
            var headerRect = EditorGUILayout.GetControlRect(false, 32f);
            EditorGUI.DrawRect(headerRect, C_BgPanel);
            EditorGUI.DrawRect(new Rect(headerRect.x, headerRect.yMax - 1f, headerRect.width, 1f), C_Divider);

            GUI.Label(new Rect(headerRect.x + 8f, headerRect.y, 60f, headerRect.height),
                "Hành Động",
                new GUIStyle(EditorStyles.miniLabel)
                { normal = { textColor = C_TextDim }, alignment = TextAnchor.MiddleLeft });
            GUI.Label(new Rect(headerRect.x + 66f, headerRect.y, 10f, headerRect.height), "›",
                new GUIStyle(EditorStyles.boldLabel)
                { normal = { textColor = C_TextDim }, alignment = TextAnchor.MiddleLeft });
            GUI.Label(new Rect(headerRect.x + 76f, headerRect.y, headerRect.width - 76f, headerRect.height),
                map.mapName,
                new GUIStyle(EditorStyles.boldLabel)
                {
                    normal    = { textColor = C_TextPrimary },
                    fontSize  = 12,
                    alignment = TextAnchor.MiddleLeft,
                    clipping  = TextClipping.Clip,
                });

            // Sub-header: số lượng + toggle kích hoạt
            var subRect = EditorGUILayout.GetControlRect(false, 20f);
            EditorGUI.DrawRect(subRect, C_BgDeep);

            int cnt = map.actions?.Count ?? 0;
            GUI.Label(new Rect(subRect.x + 8f, subRect.y, 100f, subRect.height),
                $"{cnt} hành động",
                new GUIStyle(EditorStyles.miniLabel)
                { normal = { textColor = C_TextDim }, alignment = TextAnchor.MiddleLeft });

            // Toggle enabledByDefault
            bool newEnabled = GUI.Toggle(
                new Rect(subRect.xMax - 75f, subRect.y + 3f, 70f, 14f),
                map.enabledByDefault, " kích hoạt",
                new GUIStyle(EditorStyles.toggle)
                {
                    normal   = { textColor = map.enabledByDefault ? C_Green : C_TextDim },
                    fontSize = 9,
                });
            if (newEnabled != map.enabledByDefault)
            {
                SafeRecord("Đổi trạng thái nhóm");
                map.enabledByDefault = newEnabled;
                MarkDirty();
            }
            RegisterTooltip(new Rect(subRect.xMax - 75f, subRect.y, 70f, subRect.height),
                "Bật → nhóm này tự động kích hoạt khi load profile.\nTắt → phải gọi SetActionMapActive() từ code.");

            // Cuộn danh sách action
            _bindingScroll = GUILayout.BeginScrollView(_bindingScroll, GUILayout.ExpandHeight(true));
            var actions = map.actions;

            if (actions == null || actions.Count == 0)
                DrawEmptyState("Chưa có hành động.\nBấm ＋ bên dưới.");
            else
            {
                for (int i = 0; i < actions.Count; i++)
                    DrawActionListItem(actions[i], i, map);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndScrollView();

            // Thanh dưới: Thêm
            var bottomRect = EditorGUILayout.GetControlRect(false, 34f);
            EditorGUI.DrawRect(bottomRect, C_BgDeep);
            EditorGUI.DrawRect(new Rect(bottomRect.x, bottomRect.y, bottomRect.width, 1f), C_Divider);

            var addBtnRect = new Rect(bottomRect.x + 6f, bottomRect.y + 5f, bottomRect.width - 12f, 24f);
            EditorGUI.DrawRect(addBtnRect, C_BgCard);
            DrawBorder(addBtnRect, C_Green * 0.5f, 1f);
            if (GUI.Button(addBtnRect, "＋  Thêm Hành Động",
                new GUIStyle(EditorStyles.miniLabel)
                {
                    normal    = { textColor = C_Green },
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    fontSize  = 11,
                }))
            {
                AddNewAction(map);
                _selectedActionIndex = (map.actions?.Count ?? 1) - 1;
                _rightPanelTab = 0;
            }
            RegisterTooltip(addBtnRect, "Thêm một hành động mới vào nhóm này.\nSau đó cấu hình binding PC, Gamepad và Mobile ở panel phải.");

            // Lỗi validation
            if (_validationRan && _validationErrors.Count > 0)
            {
                GUILayout.Space(2);
                foreach (var err in _validationErrors)
                {
                    var errRect = EditorGUILayout.GetControlRect(false, 18f);
                    EditorGUI.DrawRect(errRect, new Color(0.3f, 0.08f, 0.08f));
                    DrawBorder(errRect, C_Red * 0.5f, 1f);
                    GUI.Label(errRect, $"  ✘  {err}",
                        new GUIStyle(EditorStyles.miniLabel)
                        {
                            richText  = true,
                            normal    = { textColor = C_Red },
                            alignment = TextAnchor.MiddleLeft,
                        });
                }
            }

            GUILayout.Space(2);
            GUILayout.EndVertical();
        }

        // ── Mục hành động (gọn — chỉ badge + tên) ───────────

        private void DrawActionListItem(TISInputActionDefinition def, int index, TISActionMapData ownerMap)
        {
            bool selected = index == _selectedActionIndex;
            var rect = EditorGUILayout.GetControlRect(false, 32f);
            bool hover = rect.Contains(Event.current.mousePosition);

            Color bg = selected ? C_BgMapSel
                     : hover    ? C_BgRowHover
                                : (index % 2 == 0 ? C_BgRowEven : C_BgRowOdd);
            EditorGUI.DrawRect(rect, bg);

            if (selected)
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), C_Accent);

            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), C_Divider * 0.4f);

            // Badge loại
            DrawTypeBadge(new Rect(rect.x + 5f, rect.y + 7f, 22f, 18f), def.actionType);

            // Tên hành động
            GUI.Label(
                new Rect(rect.x + 32f, rect.y, rect.width - 56f, rect.height),
                def.actionName,
                new GUIStyle(EditorStyles.label)
                {
                    normal    = { textColor = selected ? Color.white : C_TextPrimary },
                    fontStyle = selected ? FontStyle.Bold : FontStyle.Normal,
                    fontSize  = 11,
                    alignment = TextAnchor.MiddleLeft,
                    clipping  = TextClipping.Clip,
                });

            // Chỉ báo read mode
            if (def.actionType == TISInputActionType.Button)
            {
                string tag = def.readMode switch
                {
                    TISButtonReadMode.PressDown => "↓",
                    TISButtonReadMode.Hold      => "⬤",
                    TISButtonReadMode.PressUp   => "↑",
                    _                        => "↓",
                };
                Color tagCol = def.readMode switch
                {
                    TISButtonReadMode.PressDown => C_Accent,
                    TISButtonReadMode.Hold      => C_Orange,
                    TISButtonReadMode.PressUp   => C_Purple,
                    _                        => C_Accent,
                };
                GUI.Label(
                    new Rect(rect.xMax - 20f, rect.y, 16f, rect.height), tag,
                    new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal    = { textColor = tagCol },
                        alignment = TextAnchor.MiddleCenter,
                        fontSize  = 11,
                    });
            }

            // Tooltip cho hành động
            string actionTooltip = BuildActionTooltip(def);
            RegisterTooltip(rect, actionTooltip);

            // Click để chọn
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                _selectedActionIndex = index;
                _rightPanelTab = 0;
                GUI.FocusControl(null);
                Event.current.Use();
                Repaint();
            }

            // Chuột phải
            if (Event.current.type == EventType.ContextClick && rect.Contains(Event.current.mousePosition))
            {
                _selectedActionIndex = index;
                ShowActionOptions(def, index, ownerMap);
                Event.current.Use();
            }
        }

        private string BuildActionTooltip(TISInputActionDefinition def)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"<b>{def.actionName}</b>  [{def.actionType}]");
            if (!string.IsNullOrEmpty(def.bindingPC))
                sb.AppendLine($"PC: {def.bindingPC}");
            if (!string.IsNullOrEmpty(def.bindingGamepad))
                sb.AppendLine($"Gamepad: {def.bindingGamepad}");
            if (!string.IsNullOrEmpty(def.bindingMobile))
                sb.AppendLine($"Mobile: {def.bindingMobile}");
            if (def.actionType == TISInputActionType.Button)
            {
                string modeDesc = def.readMode switch
                {
                    TISButtonReadMode.PressDown => "Chế độ: SINGLE (GetKeyDown) — kích hoạt 1 frame khi nhấn.",
                    TISButtonReadMode.Hold      => "Chế độ: HOLD (GetKey) — kích hoạt liên tục khi giữ.",
                    TISButtonReadMode.PressUp   => "Chế độ: NHẢ (GetKeyUp) — kích hoạt 1 frame khi nhả ra.",
                    _                        => "",
                };
                sb.AppendLine(modeDesc);
            }
            return sb.ToString().TrimEnd();
        }

        // ══════════════════════════════════════════════════════
        //  PANEL PHẢI — TAB KÉP
        // ══════════════════════════════════════════════════════

        private void DrawRightPanel()
        {
            float panelW = RightPanelW;
            GUILayout.BeginVertical(GUILayout.Width(panelW));

            DrawPanelHeader("  Trình Kiểm Tra", panelW);

            // Tab bar
            var tabRect = EditorGUILayout.GetControlRect(false, 28f);
            EditorGUI.DrawRect(tabRect, C_BgDeep);

            float tabW = tabRect.width / _rightPanelTabs.Length;
            for (int i = 0; i < _rightPanelTabs.Length; i++)
            {
                var tr  = new Rect(tabRect.x + i * tabW, tabRect.y, tabW, tabRect.height);
                bool sel = i == _rightPanelTab;
                EditorGUI.DrawRect(tr, sel ? C_BgCard : C_BgDeep);
                if (sel)
                    EditorGUI.DrawRect(new Rect(tr.x, tr.yMax - 2f, tr.width, 2f), C_Accent);

                if (GUI.Button(tr, _rightPanelTabs[i],
                    new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal    = { textColor = sel ? C_TextPrimary : C_TextSecond },
                        fontStyle = sel ? FontStyle.Bold : FontStyle.Normal,
                        alignment = TextAnchor.MiddleCenter,
                        fontSize  = 10,
                    }))
                    _rightPanelTab = i;

                string tabTip = i == 0
                    ? "Cấu hình chi tiết hành động đang chọn: loại, chế độ nhấn, binding cho 3 nền tảng."
                    : "Cài đặt cảm ứng Mobile, Camera và Gamepad cho toàn bộ profile.";
                RegisterTooltip(tr, tabTip);
            }
            EditorGUI.DrawRect(
                new Rect(tabRect.x, tabRect.yMax, tabRect.width, 1f), C_Divider);

            // Nội dung tab
            _settingsScroll = GUILayout.BeginScrollView(_settingsScroll, GUILayout.ExpandHeight(true));
            GUILayout.Space(6);

            switch (_rightPanelTab)
            {
                case 0: DrawActionSettingContent(); break;
                case 1: DrawPlatformSettingsContent(); break;
            }

            GUILayout.Space(10);
            GUILayout.EndScrollView();

            GUILayout.EndVertical();
        }

        // ── Nội dung tab Cài Đặt Action ──────────────────────

        private void DrawActionSettingContent()
        {
            var maps = _workingProfile?.actionMaps;
            if (maps == null || maps.Count == 0)
            {
                DrawEmptyState("Chưa có nhóm action nào.");
                return;
            }

            _selectedMapIndex = Mathf.Clamp(_selectedMapIndex, 0, maps.Count - 1);
            var map = maps[_selectedMapIndex];
            var actions = map?.actions;

            if (actions == null || actions.Count == 0)
            {
                DrawEmptyState("Chưa có hành động.\nBấn ＋ Thêm Hành Động.");
                return;
            }

            _selectedActionIndex = Mathf.Clamp(_selectedActionIndex, 0, actions.Count - 1);
            if (_selectedActionIndex < 0) _selectedActionIndex = 0;
            var def = actions[_selectedActionIndex];

            // ── Header hành động ───────────────────────────────
            var hRect = EditorGUILayout.GetControlRect(false, 36f);
            EditorGUI.DrawRect(hRect, C_BgDeep);
            DrawBorder(hRect, C_Divider, 1f);
            EditorGUI.DrawRect(new Rect(hRect.x, hRect.y, 3f, hRect.height), C_Accent);

            DrawTypeBadge(new Rect(hRect.x + 8f, hRect.y + 9f, 24f, 18f), def.actionType);

            // Ô nhập tên hành động
            string newName = GUI.TextField(
                new Rect(hRect.x + 38f, hRect.y + 8f, hRect.width - 48f, 20f),
                def.actionName,
                new GUIStyle(EditorStyles.textField)
                {
                    normal    = { textColor = C_TextPrimary },
                    fontSize  = 12,
                    fontStyle = FontStyle.Bold,
                });
            if (newName != def.actionName)
            { SafeRecord("Đổi tên hành động"); def.actionName = newName; MarkDirty(); }
            RegisterTooltip(new Rect(hRect.x + 38f, hRect.y + 8f, hRect.width - 48f, 20f),
                "Tên duy nhất của hành động.\nDùng để gọi TISInputBridge.GetSkillAction(\"tên này\") từ code.");

            GUILayout.Space(8);

            // ── Loại hành động ────────────────────────────────
            A_Group("Loại Hành Động", "Loại dữ liệu mà hành động này trả về khi đọc.\n" +
                "BTN (Button) = bool  ·  V2 (Vector2) = Vector2  ·  AX (Axis) = float");

            string typeLabel = def.actionType switch
            {
                TISInputActionType.Vector2 => "Vector2  (trả về Vector2 — dùng cho di chuyển, xoay camera)",
                TISInputActionType.Axis    => "Axis  (trả về float — dùng cho ga, phanh)",
                _                       => "Button  (trả về bool — dùng cho nhảy, tấn công, kỹ năng)",
            };
            A_InfoRow("Loại (tự suy)", typeLabel, C_TextAccent);

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUILayout.Space(8);

            void TypeBtn(string label, TISInputActionType t, Color col, string tip)
            {
                bool active = def.actionType == t;
                var r = GUILayoutUtility.GetRect(
                    new GUIContent(label),
                    new GUIStyle(EditorStyles.miniButton),
                    GUILayout.Height(22f), GUILayout.ExpandWidth(true));

                EditorGUI.DrawRect(r, active ? col * 0.35f : C_BgCard);
                DrawBorder(r, active ? col * 0.8f : C_Divider, 1f);
                if (active)
                    EditorGUI.DrawRect(new Rect(r.x, r.y, 3f, r.height), col);

                GUI.Label(r, label,
                    new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal    = { textColor = active ? col : C_TextSecond },
                        fontStyle = active ? FontStyle.Bold : FontStyle.Normal,
                        alignment = TextAnchor.MiddleCenter,
                        fontSize  = 10,
                    });

                if (GUI.Button(r, GUIContent.none, GUIStyle.none) && !active)
                {
                    SafeRecord("Ghi đè loại");
                    def.actionType = t;
                    MarkDirty();
                }
                RegisterTooltip(r, tip);
            }

            TypeBtn("BTN", TISInputActionType.Button,  C_BadgeBtn.gamma,
                "Button — trả về bool.\nDùng cho: nhảy, tấn công, kỹ năng, tương tác.");
            GUILayout.Space(4);
            TypeBtn("V2",  TISInputActionType.Vector2, C_BadgeV2.gamma,
                "Vector2 — trả về Vector2.\nDùng cho: di chuyển (WASD), xoay camera (right stick), joystick ảo.");
            GUILayout.Space(4);
            TypeBtn("AX",  TISInputActionType.Axis,    C_BadgeAx.gamma,
                "Axis — trả về float.\nDùng cho: ga/phanh (trigger), zoom, thanh trượt.");
            GUILayout.Space(8);
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // ── Chế độ nút (chỉ Button) ───────────────────────
            if (def.actionType == TISInputActionType.Button)
            {
                A_Group("Chế Độ Nút", "Xác định khi nào hành động được kích hoạt.\n" +
                    "SINGLE (PressDown): chỉ 1 frame khi nhấn xuống (GetKeyDown).\n" +
                    "GIỮ   (Hold):      liên tục trong khi giữ (GetKey).\n" +
                    "NHẢ   (PressUp):   chỉ 1 frame khi nhả ra (GetKeyUp).");

                GUILayout.BeginHorizontal();
                GUILayout.Space(8);

                void ModeBtn(string label, string desc, TISButtonReadMode targetMode, string tip)
                {
                    bool isThis = def.readMode == targetMode;
                    var r = GUILayoutUtility.GetRect(
                        new GUIContent(label),
                        new GUIStyle(EditorStyles.miniButton),
                        GUILayout.Height(32f), GUILayout.ExpandWidth(true));

                    Color mCol = isThis ? C_Accent : C_TextDim;
                    EditorGUI.DrawRect(r, isThis ? new Color(0.14f, 0.24f, 0.38f) : C_BgCard);
                    DrawBorder(r, isThis ? C_Accent * 0.6f : C_Divider, 1f);

                    GUI.Label(new Rect(r.x, r.y + 2f, r.width, 14f), label,
                        new GUIStyle(EditorStyles.miniLabel)
                        {
                            normal    = { textColor = mCol },
                            fontStyle = FontStyle.Bold,
                            alignment = TextAnchor.MiddleCenter,
                            fontSize  = 10,
                        });
                    GUI.Label(new Rect(r.x, r.y + 16f, r.width, 14f), desc,
                        new GUIStyle(EditorStyles.miniLabel)
                        {
                            normal    = { textColor = C_TextDim },
                            alignment = TextAnchor.MiddleCenter,
                            fontSize  = 8,
                        });

                    if (GUI.Button(r, GUIContent.none, GUIStyle.none) && !isThis)
                    {
                        SafeRecord("Đổi chế độ nhấn");
                        def.readMode      = targetMode;
                        def.isSinglePress = (targetMode == TISButtonReadMode.PressDown);
                        def._readModeMigrated = true;
                        MarkDirty();
                    }
                    RegisterTooltip(r, tip);
                }

                ModeBtn("SINGLE", "GetKeyDown", TISButtonReadMode.PressDown,
                    "true đúng 1 frame khi nhấn xuống.\nDùng cho: nhảy, tấn công, tương tác.");
                GUILayout.Space(4);
                ModeBtn("GIỮ",    "GetKey",     TISButtonReadMode.Hold,
                    "true suốt khi đang giữ phím.\nDùng cho: chạy, ngồi xuống, ngắm bắn.");
                GUILayout.Space(4);
                ModeBtn("NHẢ",    "GetKeyUp",   TISButtonReadMode.PressUp,
                    "true đúng 1 frame khi nhả ra.\nDùng cho: release charge, kết thúc aim.");
                GUILayout.Space(8);
                GUILayout.EndHorizontal();

                GUILayout.Space(8);
            }

            // ── Deadzone ghi đè (không phải Button) ──────────
            if (def.actionType != TISInputActionType.Button)
            {
                A_Group("Deadzone", "Vùng chết riêng cho hành động này.\n" +
                    "0 = dùng deadzone mặc định từ profile.\n" +
                    "Chỉ ảnh hưởng đến hành động loại Vector2 và Axis.");
                GUILayout.BeginHorizontal();
                GUILayout.Space(8);
                GUILayout.Label("Ghi đè",
                    new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = C_TextSecond } },
                    GUILayout.Width(60f));
                EditorGUI.BeginChangeCheck();
                float newDz = GUILayout.HorizontalSlider(def.overrideDeadzone, 0f, 0.99f);
                if (EditorGUI.EndChangeCheck()) { def.overrideDeadzone = newDz; MarkDirty(); }
                GUILayout.Label(def.overrideDeadzone > 0f
                        ? def.overrideDeadzone.ToString("F2")
                        : "mặc định",
                    new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal    = { textColor = def.overrideDeadzone > 0f ? C_TextAccent : C_TextDim },
                        alignment = TextAnchor.MiddleRight,
                        fontStyle = FontStyle.Bold,
                    },
                    GUILayout.Width(50f));
                GUILayout.Space(8);
                GUILayout.EndHorizontal();
                GUILayout.Space(8);
            }

            // ── Binding ───────────────────────────────────────
            bool pcEnabled      = _workingProfile.enablePC;
            bool gamepadEnabled = _workingProfile.enableGamepad;
            bool mobileEnabled  = _workingProfile.enableMobile;

            // ── PC ────────────────────────────────────────────
            A_PlatformGroupHeader("Binding PC", pcEnabled, C_Accent,
                "Phím / chuột kích hoạt hành động này trên PC.\n" +
                "Click trái để chọn từ danh sách  ·  Chuột phải để gõ path thủ công.",
                "⌨ PC bị tắt trong toolbar — bật lại để chỉnh binding này.");

            if (pcEnabled)
            {
                A_BindingRow("Path",     def.bindingPC,        TISBindingPathOptions.PC().ToArray(),        C_Accent,
                    v => { SafeRecord("Đổi PC"); def.bindingPC = v;
                        var inf = TISBindingPathOptions.InferType(v);
                        if (inf != TISInputActionType.Button || string.IsNullOrEmpty(def.bindingGamepad)) def.actionType = inf;
                        MarkDirty(); }, "Binding PC…",
                    "Phím hoặc nút chuột cho nền tảng PC.\nVí dụ: <Keyboard>/space, <Mouse>/leftButton");

                A_BindingRow("Modifier", def.bindingPCModifier, TISBindingPathOptions.PCModifier().ToArray(), C_Purple,
                    v => { SafeRecord("Đổi Modifier"); def.bindingPCModifier = v; MarkDirty(); }, "Modifier…",
                    "Phím bổ trợ kết hợp với binding chính.\nVí dụ: chọn LeftShift → nhấn Shift + <binding> mới kích hoạt.");
            }
            else
            {
                A_DisabledBindingRow("Path",     def.bindingPC,        "Binding PC…");
                A_DisabledBindingRow("Modifier", def.bindingPCModifier, "Modifier…");
            }

            // ── Gamepad ───────────────────────────────────────
            GUILayout.Space(6);
            A_PlatformGroupHeader("Binding Gamepad", gamepadEnabled, C_Green,
                "Nút trên tay cầm chơi game kích hoạt hành động này.\nHỗ trợ Xbox, PlayStation, Switch Pro Controller.",
                "🎮 Gamepad bị tắt trong toolbar — bật lại để chỉnh binding này.");

            if (gamepadEnabled)
            {
                A_BindingRow("Path", def.bindingGamepad, TISBindingPathOptions.Gamepad().ToArray(), C_Green,
                    v => { SafeRecord("Đổi Gamepad"); def.bindingGamepad = v;
                        if (!string.IsNullOrEmpty(v)) def.actionType = TISBindingPathOptions.InferType(v);
                        MarkDirty(); }, "Binding Gamepad…",
                    "Nút trên tay cầm.\nVí dụ: <Gamepad>/buttonSouth (A/Cross), <Gamepad>/leftStick");

                // ← THÊM DÒNG NÀY
                A_BindingRow("Modifier", def.bindingGamepadModifier, TISBindingPathOptions.GamepadModifier().ToArray(), C_Purple,
                    v => { SafeRecord("Đổi Gamepad Modifier"); def.bindingGamepadModifier = v; MarkDirty(); }, "Modifier…",
                    "Nút bổ trợ kết hợp với Gamepad binding.\nVí dụ: Left Shoulder → LB + <binding> mới kích hoạt.");
            }
            else
            {
                A_DisabledBindingRow("Path",     def.bindingGamepad,         "Binding Gamepad…");
                A_DisabledBindingRow("Modifier", def.bindingGamepadModifier, "Modifier…"); // ← THÊM
            }

            // ── Mobile ────────────────────────────────────────
            GUILayout.Space(6);
            A_PlatformGroupHeader("Binding Mobile", mobileEnabled, C_Orange,
                "Điều khiển ảo trên màn hình cảm ứng.\nMobileSkillButton sẽ inject trạng thái vào TISMobileButtonRegistry.",
                "📱 Mobile bị tắt trong toolbar — bật lại để chỉnh binding này.");

            if (mobileEnabled)
            {
                A_BindingRow("Điều khiển", def.bindingMobile,  TISBindingPathOptions.Mobile().ToArray(),    C_Orange,
                    v => { SafeRecord("Đổi Mobile"); def.bindingMobile = v;
                        if (!string.IsNullOrEmpty(v)) def.actionType = TISBindingPathOptions.InferType(v);
                        MarkDirty(); }, "Điều khiển Mobile…",
                    "Virtual control trên mobile.\nVirtualJoystick = joystick ảo, Button_N = nút trên màn hình.");
            }
            else
            {
                A_DisabledBindingRow("Điều khiển", def.bindingMobile, "Điều khiển Mobile…");
            }

            GUILayout.Space(8);

            // ── Thao tác nhanh ────────────────────────────────
            A_Group("Thao Tác", "Sắp xếp, nhân đôi hoặc xoá hành động này.");
            GUILayout.Space(4);

            GUILayout.BeginHorizontal();
            GUILayout.Space(8);

            void QuickBtn(string label, Color col, Action onClick, string tip)
            {
                var r = GUILayoutUtility.GetRect(
                    new GUIContent(label), GUIStyle.none,
                    GUILayout.Height(24f), GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(r, C_BgCard);
                DrawBorder(r, col * 0.4f, 1f);
                GUI.Label(r, label,
                    new GUIStyle(EditorStyles.miniLabel)
                    {
                        normal    = { textColor = col },
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = FontStyle.Bold,
                        fontSize  = 10,
                    });
                if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                    onClick?.Invoke();
                RegisterTooltip(r, tip);
            }

            var currentMap = _workingProfile.actionMaps[_selectedMapIndex];
            QuickBtn("↑ Lên",     C_TextSecond, () => MoveAction(currentMap, _selectedActionIndex, -1),
                "Dịch hành động này lên trên trong danh sách.");
            GUILayout.Space(3);
            QuickBtn("↓ Xuống",   C_TextSecond, () => MoveAction(currentMap, _selectedActionIndex, +1),
                "Dịch hành động này xuống dưới trong danh sách.");
            GUILayout.Space(3);
            QuickBtn("Nhân đôi", C_Accent,   () => DuplicateAction(currentMap, _selectedActionIndex),
                "Tạo bản sao của hành động này.");
            GUILayout.Space(3);
            QuickBtn("✕ Xoá",    C_Red,        () => { RemoveAction(currentMap, _selectedActionIndex); _selectedActionIndex = Mathf.Max(0, _selectedActionIndex - 1); },
                "Xoá hành động này khỏi nhóm.");

            GUILayout.Space(8);
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
        }

        // ── Widget A_ (panel cài đặt action) ─────────────────

        private void A_Group(string title, string tooltip = "")
        {
            GUILayout.Space(4);
            var rect = EditorGUILayout.GetControlRect(false, 18f);
            EditorGUI.DrawRect(rect, C_BgDeep);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), C_Accent * 0.6f);
            GUI.Label(rect, "  " + title,
                new GUIStyle(EditorStyles.boldLabel)
                {
                    normal    = { textColor = C_TextAccent },
                    fontSize  = 9,
                    alignment = TextAnchor.MiddleLeft,
                });
            if (!string.IsNullOrEmpty(tooltip))
                RegisterTooltip(rect, tooltip);
            GUILayout.Space(3);
        }

        /// <summary>
        /// Group header cho binding section, hiển thị trạng thái ON/OFF của platform.
        /// Khi platform bị tắt: nền xám, sọc nhấn mờ, chip "OFF" màu cam.
        /// </summary>
        private void A_PlatformGroupHeader(string title, bool platformEnabled,
            Color accentCol, string tooltipOn, string tooltipOff)
        {
            GUILayout.Space(4);
            var rect = EditorGUILayout.GetControlRect(false, 18f);

            Color bgCol    = platformEnabled ? C_BgDeep : new Color(0.11f, 0.11f, 0.13f);
            Color barCol   = platformEnabled ? accentCol * 0.6f : C_TextDim * 0.4f;
            Color textCol  = platformEnabled ? C_TextAccent : C_TextDim;

            EditorGUI.DrawRect(rect, bgCol);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), barCol);

            GUI.Label(new Rect(rect.x + 6f, rect.y, rect.width - 50f, rect.height),
                title,
                new GUIStyle(EditorStyles.boldLabel)
                {
                    normal    = { textColor = textCol },
                    fontSize  = 9,
                    alignment = TextAnchor.MiddleLeft,
                });

            // Chip trạng thái bên phải
            string chipText = platformEnabled ? "ON" : "OFF";
            Color  chipCol  = platformEnabled ? accentCol : C_Orange;
            Color  chipBg   = platformEnabled
                ? new Color(accentCol.r * 0.15f, accentCol.g * 0.15f, accentCol.b * 0.15f)
                : new Color(0.20f, 0.12f, 0.03f);

            var chipRect = new Rect(rect.xMax - 32f, rect.y + 3f, 28f, 12f);
            EditorGUI.DrawRect(chipRect, chipBg);
            DrawBorder(chipRect, chipCol * 0.6f, 1f);
            GUI.Label(chipRect, chipText,
                new GUIStyle(EditorStyles.miniLabel)
                {
                    normal    = { textColor = chipCol },
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    fontSize  = 8,
                });

            RegisterTooltip(rect, platformEnabled ? tooltipOn : tooltipOff);
            GUILayout.Space(3);
        }

        /// <summary>
        /// Binding row dạng disabled (read-only, mờ) khi platform bị tắt.
        /// Không có dropdown, không cho click, chỉ hiển thị giá trị hiện tại.
        /// </summary>
        private void A_DisabledBindingRow(string label, string currentValue, string placeholder)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(8);

            GUILayout.Label(label,
                new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = C_TextDim * 0.7f } },
                GUILayout.Width(65f));

            bool hasValue = !string.IsNullOrEmpty(currentValue);
            string display = hasValue ? currentValue : placeholder;
            if (display.Length > 28) display = "…" + display.Substring(display.Length - 26);

            var fieldRect = GUILayoutUtility.GetRect(
                new GUIContent(display), GUIStyle.none,
                GUILayout.Height(22f), GUILayout.ExpandWidth(true));

            // Nền xám rất tối, viền hầu như vô hình
            EditorGUI.DrawRect(fieldRect, new Color(0.11f, 0.11f, 0.13f));
            DrawBorder(fieldRect, C_Divider * 0.4f, 1f);

            // Gạch chéo overlay để rõ ràng là "bị khoá"
            EditorGUI.DrawRect(new Rect(fieldRect.x, fieldRect.y + fieldRect.height * 0.5f - 0.5f,
                fieldRect.width, 1f), C_Divider * 0.5f);

            GUI.Label(new Rect(fieldRect.x + 6f, fieldRect.y, fieldRect.width - 22f, fieldRect.height),
                hasValue ? display : $"({placeholder})",
                new GUIStyle(EditorStyles.miniLabel)
                {
                    normal    = { textColor = hasValue ? C_TextDim * 0.5f : C_TextDim * 0.3f },
                    fontStyle = hasValue ? FontStyle.Italic : FontStyle.Normal,
                    clipping  = TextClipping.Clip,
                    alignment = TextAnchor.MiddleLeft,
                    fontSize  = 10,
                });

            // Icon khoá
            GUI.Label(new Rect(fieldRect.xMax - 16f, fieldRect.y, 16f, fieldRect.height), "🔒",
                new GUIStyle(EditorStyles.miniLabel)
                { normal = { textColor = C_TextDim * 0.4f }, alignment = TextAnchor.MiddleCenter, fontSize = 9 });

            RegisterTooltip(fieldRect, "Platform này đang bị tắt trong toolbar.\nBật lại để chỉnh binding.");

            GUILayout.Space(8);
            GUILayout.EndHorizontal();
            GUILayout.Space(3);
        }

        private void A_InfoRow(string label, string value, Color valueColor)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(8);
            GUILayout.Label(label,
                new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = C_TextSecond } },
                GUILayout.Width(80f));
            GUILayout.Label(value,
                new GUIStyle(EditorStyles.miniLabel)
                {
                    normal    = { textColor = valueColor },
                    fontStyle = FontStyle.Bold,
                    clipping  = TextClipping.Clip,
                });
            GUILayout.EndHorizontal();
        }

        private void A_BindingRow(
            string label,
            string currentValue,
            TISBindingOption[] options,
            Color accentCol,
            Action<string> onChange,
            string placeholder,
            string tooltip = "")
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(8);

            GUILayout.Label(label,
                new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = C_TextSecond } },
                GUILayout.Width(65f));

            bool hasValue = !string.IsNullOrEmpty(currentValue);
            string display = GetShortLabel(currentValue, options, placeholder);

            var fieldRect = GUILayoutUtility.GetRect(
                new GUIContent(display), GUIStyle.none,
                GUILayout.Height(22f), GUILayout.ExpandWidth(true));

            EditorGUI.DrawRect(fieldRect, hasValue ? C_BgBinding : C_BgBindingEmp);
            if (hasValue)
                EditorGUI.DrawRect(new Rect(fieldRect.x, fieldRect.y + 2f, 2f, fieldRect.height - 4f), accentCol * 0.7f);

            var labelRect = new Rect(fieldRect.x + 6f, fieldRect.y, fieldRect.width - 22f, fieldRect.height);
            GUI.Label(labelRect, display,
                new GUIStyle(EditorStyles.miniLabel)
                {
                    normal   = { textColor = hasValue ? C_TextPrimary : C_TextDim },
                    clipping = TextClipping.Clip,
                    alignment = TextAnchor.MiddleLeft,
                    fontSize  = 10,
                });

            GUI.Label(new Rect(fieldRect.xMax - 16f, fieldRect.y, 16f, fieldRect.height), "▾",
                new GUIStyle(EditorStyles.miniLabel)
                { normal = { textColor = C_TextDim }, alignment = TextAnchor.MiddleCenter, fontSize = 11 });

            if (fieldRect.Contains(Event.current.mousePosition))
                DrawBorder(fieldRect, accentCol * 0.5f, 1f);

            if (!string.IsNullOrEmpty(tooltip))
                RegisterTooltip(fieldRect, tooltip);

            if (Event.current.type == EventType.MouseDown && fieldRect.Contains(Event.current.mousePosition))
            {
                if (Event.current.button == 1)
                {
                    Event.current.Use();
                    TISBindingInputPopup.Show(currentValue, onChange);
                }
                else
                {
                    var capturedCurrent = currentValue;
                    var capturedOptions = options;
                    var capturedChange  = onChange;
                    var menu = new UnityEditor.GenericMenu();
                    menu.AddItem(new GUIContent("(xoá)"), false, () => capturedChange(""));
                    menu.AddSeparator("");
                    string currentGroup = "";
                    foreach (var item in capturedOptions)
                    {
                        if (string.IsNullOrEmpty(item.Path))
                        {
                            string h = item.Label.Replace("──", "").Trim();
                            if (!string.IsNullOrEmpty(h)) currentGroup = h + "/";
                            continue;
                        }
                        string lbl = currentGroup + item.Label.Trim();
                        string cap = item.Path;
                        bool isOn  = cap == capturedCurrent;
                        menu.AddItem(new GUIContent(lbl), isOn, () => capturedChange(cap));
                    }
                    menu.ShowAsContext();
                    Event.current.Use();
                }
            }

            GUILayout.Space(8);
            GUILayout.EndHorizontal();
            GUILayout.Space(3);
        }

        // ══════════════════════════════════════════════════════
        //  TIỆN ÍCH DÙNG CHUNG
        // ══════════════════════════════════════════════════════

        private string GetShortLabel(
            string path,
            TISBindingOption[] options,
            string placeholder) 
        {
            if (string.IsNullOrEmpty(path)) return placeholder;
            foreach (var item in options)
            {
                if (item.Path == path)
                {
                    string t  = item.Label.Trim();
                    int    si = t.IndexOf("  ", StringComparison.Ordinal);
                    return si > 0 ? t.Substring(0, si).Trim() : t;
                }
            }
            return path.Length > 28 ? "…" + path.Substring(path.Length - 26) : path;
        }

        private void DrawTypeBadge(Rect rect, TISInputActionType type)
        {
            Color bg;
            string label;
            switch (type)
            {
                case TISInputActionType.Vector2: bg = C_BadgeV2;  label = "V2";  break;
                case TISInputActionType.Axis:    bg = C_BadgeAx;  label = "AX";  break;
                default:                      bg = C_BadgeBtn; label = "BTN"; break;
            }
            EditorGUI.DrawRect(rect, bg);
            GUI.Label(rect, label,
                new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    normal    = { textColor = Color.white },
                    fontSize  = 8,
                });
            EditorGUI.DrawRect(new Rect(rect.xMax - 4f, rect.y + 1f, 3f, 3f),
                new Color(1f, 1f, 1f, 0.5f));
        }

        // ── Menu tuỳ chọn action (chuột phải trong danh sách) ─

        private void ShowActionOptions(TISInputActionDefinition def, int rowIndex, TISActionMapData ownerMap)
        {
            var menu = new UnityEditor.GenericMenu();
            var cd   = def;
            var cm   = ownerMap;
            int ci   = rowIndex;

            string inferredLabel = def.actionType switch
            {
                TISInputActionType.Vector2 => "⬡  Loại: Vector2  (tự suy)",
                TISInputActionType.Axis    => "⬡  Loại: Axis  (tự suy)",
                _                       => "⬡  Loại: Button  (tự suy)",
            };
            menu.AddDisabledItem(new GUIContent(inferredLabel));
            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Ghi đè Loại/Button"),
                def.actionType == TISInputActionType.Button,
                () => { SafeRecord("Ghi đè loại"); cd.actionType = TISInputActionType.Button;  MarkDirty(); });
            menu.AddItem(new GUIContent("Ghi đè Loại/Vector2"),
                def.actionType == TISInputActionType.Vector2,
                () => { SafeRecord("Ghi đè loại"); cd.actionType = TISInputActionType.Vector2; MarkDirty(); });
            menu.AddItem(new GUIContent("Ghi đè Loại/Axis"),
                def.actionType == TISInputActionType.Axis,
                () => { SafeRecord("Ghi đè loại"); cd.actionType = TISInputActionType.Axis;    MarkDirty(); });
            menu.AddSeparator("");

            if (def.actionType == TISInputActionType.Button)
            {
                menu.AddItem(new GUIContent("Chế độ/SINGLE  (GetKeyDown)"), cd.readMode == TISButtonReadMode.PressDown,
                    () =>
                    {
                        SafeRecord("Đổi chế độ nhấn");
                        cd.readMode           = TISButtonReadMode.PressDown;
                        cd.isSinglePress      = true;
                        cd._readModeMigrated  = true;
                        MarkDirty();
                    });
                menu.AddItem(new GUIContent("Chế độ/GIỮ  (GetKey)"), cd.readMode == TISButtonReadMode.Hold,
                    () =>
                    {
                        SafeRecord("Đổi chế độ nhấn");
                        cd.readMode           = TISButtonReadMode.Hold;
                        cd.isSinglePress      = false;
                        cd._readModeMigrated  = true;
                        MarkDirty();
                    });
                menu.AddItem(new GUIContent("Chế độ/NHẢ  (GetKeyUp)"), cd.readMode == TISButtonReadMode.PressUp,
                    () =>
                    {
                        SafeRecord("Đổi chế độ nhấn");
                        cd.readMode           = TISButtonReadMode.PressUp;
                        cd.isSinglePress      = false;
                        cd._readModeMigrated  = true;
                        MarkDirty();
                    });
                menu.AddSeparator("");
            }

            menu.AddItem(new GUIContent("Dịch lên ↑"),   false, () => MoveAction(cm, ci, -1));
            menu.AddItem(new GUIContent("Dịch xuống ↓"),  false, () => MoveAction(cm, ci, +1));
            menu.AddItem(new GUIContent("Nhân đôi"),      false, () => DuplicateAction(cm, ci));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Xoá"),           false, () => RemoveAction(cm, ci));
            menu.ShowAsContext();
        }
    }
}
#endif