// ============================================================
//  TrueInputSystem — TIS_DocWindow.Tab_FAQ.cs
//  Tab: FAQ & Xử Lý Lỗi
//  Thêm câu hỏi mới: gọi thêm 1 dòng FaqItem(id, "...", "...")
//  với id tăng dần, không trùng với id đã có.
// ══════════════════════════════════════════════════════

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TrueInputSystem.Editor
{
    public partial class TISDocWindow
    {
        // ══════════════════════════════════════════════════════
        //  TAB 10 — FAQ
        // ══════════════════════════════════════════════════════

        private void DrawTab_FAQ()
        {
            BeginContentScroll("faq");
            PageTitle("FAQ & Xử Lý Lỗi", "Các lỗi thường gặp và cách khắc phục nhanh.");

            FaqItem(0, "GetSkillAction() luôn trả về false dù đã nhấn phím?",
                "Kiểm tra lần lượt:\n" +
                "1. actionName trong code có khớp với profile không? (case-insensitive nhưng phải đúng tên)\n" +
                "2. Mở tab Debug của TISInputBridge → nhấn phím → cột Active có chuyển YES không?\n" +
                "3. Action Map có enabledByDefault = true không? Nếu false → gọi SetActionMapActive() để bật.\n" +
                "4. bindingPC có đúng path không? Dùng dropdown trong Setup Tool thay vì gõ tay.\n" +
                "5. Simulate Mobile In Editor có đang bật không? Nếu bật, keyboard không trigger nữa.");

            FaqItem(1, "Joystick mobile xuất hiện sai vị trí / bay ra ngoài màn hình?",
                "Nguyên nhân thường là anchor mismatch.\n" +
                "TISVirtualJoystick v6 đã fix bằng cách đặt anchor CENTER trong Awake().\n" +
                "Nếu dùng prefab joystick tự tạo: đảm bảo RectTransform anchor = center, pivot = center.");

            FaqItem(2, "TISMobileSkillButton inject nhưng GetSkillAction() không đọc được?",
                "GetSkillAction() chỉ check TISMobileButtonRegistry khi ĐỒNG THỜI:\n" +
                "(1) Platform là mobile HOẶC _simulateMobileInEditor = true trong Inspector, VÀ\n" +
                "(2) action đó có bindingMobile không trống trong profile.\n" +
                "Nếu bindingMobile trống → hàm đọc InputAction thật thay vì registry.");

            FaqItem(3, "IsJumping không nhận từ TISMobileSkillButton?",
                "IsJumping đọc InputAction trực tiếp, không qua registry.\n" +
                "Cần patch thêm trong TISInputBridge.cs — xem tab Nâng Cao.");

            FaqItem(4, "MaxMobileButtons không đủ — cần nhiều hơn 16 button?",
                "Tăng MaxMobileButtons trong TISBindingPathOptions.cs lên 24, 32, 48...\n" +
                "Runtime không bị giới hạn — TISMobileButtonRegistry dùng Dictionary<string, bool>.\n" +
                "ButtonByIndex(20) → \"Button_20\" hoàn toàn hợp lệ.");

            FaqItem(5, "SwapProfile bị mất trạng thái button mobile?",
                "Đây là hành vi đúng — SwapProfile() tự gọi TISMobileButtonRegistry.ClearAll()\n" +
                "để tránh button bị stuck pressed sau khi đổi profile.\n" +
                "Nếu muốn giữ state, chỉ dùng SetActionMapActive() thay vì SwapProfile().");

            FaqItem(6, "Rumble tay cầm không hoạt động?",
                "Kiểm tra lần lượt:\n" +
                "1. enableRumble = true trong TISInputProfileConfig → tab Gamepad\n" +
                "2. Gamepad.current != null (người chơi chưa kết nối tay cầm)\n" +
                "3. Gọi đúng TryRumble() qua TISInputBridge.Instance\n" +
                "4. Gọi StopRumble() trong OnDisable() / OnApplicationQuit()\n" +
                "Rumble không hoạt động trên mobile — enableRumble = false trong profile mobile.");

            FaqItem(7, "Pinch zoom và kéo camera bị conflict?",
                "TISTouchCameraZone v3+ đã xử lý: pinch chỉ nhận khi CẢ 2 ngón ở trong camera zone.\n" +
                "Ngón joystick (nửa trái) bị loại trừ hoàn toàn.\n" +
                "Khi đang pinch, camera rotation tự tạm dừng.");

            FaqItem(8, "Rebinding không lưu sau khi thoát game?",
                "Phải gọi TISRebindingSystem.LoadAndApply(bridge) trong Awake() sau khi TISInputBridge khởi tạo.\n" +
                "Rebinding tự lưu vào JSON sau mỗi lần rebind thành công — không cần gọi Save() thủ công.\n" +
                "File JSON ở: Application.persistentDataPath/TIS_Bindings_{profileName}.json");

            FaqItem(9, "TISDirectionalSkillButton không fire OnDirectionCast?",
                "Kiểm tra:\n" +
                "1. CastMode = DragRelease: cần kéo đủ dragThresholdPx (mặc định 40px) rồi nhả.\n" +
                "   Quick tap dưới quickTapMaxSeconds → cast với direction = Vector2.zero.\n" +
                "2. Ngón tay không vào Cancel Zone khi nhả (Cancel Zone hủy cast).\n" +
                "3. IsDisabled = false và SetCooldownFill = 0 (sẵn sàng).\n" +
                "4. Script subscribe đúng event: btn.OnDirectionCast += (name, dir) => {}");

            FaqItem(10, "TISTargetScanner không tìm được target?",
                "Kiểm tra lần lượt:\n" +
                "1. Component gắn lên Camera GameObject? (yêu cầu RequireComponent(typeof(Camera)))\n" +
                "2. targetTags khớp với tag của enemy? Hoặc bật useLayerInsteadOfTag và chọn đúng Layer.\n" +
                "3. requireLineOfSight = true? Kiểm tra obstacleLayers không chặn tầm nhìn.\n" +
                "4. scanAngle đủ rộng chưa? Thử tăng lên 15–20° để test.\n" +
                "5. maxDistance đủ lớn? 0 = không giới hạn.\n" +
                "6. Gọi ForceScan() sau khi spawn enemy để quét ngay không chờ interval.");

            FaqItem(11, "TISMobileInputSetup không khởi tạo trên PC build?",
                "Đây là hành vi đúng — TISMobileInputSetup tự SetActive(false) trên non-mobile.\n" +
                "Trong Editor nó luôn active để test.\n" +
                "Nếu muốn test trên PC: bật Simulate Mobile In Editor trong TISInputBridge Inspector.");

            FaqItem(12, "Gamepad Virtual Cursor không di chuyển?",
                "Kiểm tra:\n" +
                "1. cursorTransform và parentCanvas đã assign trong Inspector.\n" +
                "2. Đã gọi SetActiveGamepadCursor(true).\n" +
                "3. Gamepad.current != null (tay cầm đang kết nối).\n" +
                "4. EventSystem trong scene có InputSystemUIInputModule không?\n" +
                "   Cursor dùng QueueDeltaStateEvent → cần InputSystem UI Module.\n" +
                "5. cursorTransform.gameObject.SetActive(true) — cursor image có đang visible không?");

            FaqItem(13, "Interactable và SetDisabled khác nhau như thế nào?",
                "Interactable (property get/set) — ưu tiên CAO NHẤT:\n" +
                "  false = button hoàn toàn bị khóa, không nhận touch, visual mờ nhất.\n" +
                "  Dùng khi: nhân vật chết, stage lock, chưa unlock skill.\n\n" +
                "SetDisabled(bool) — ưu tiên THẤP HƠN Interactable:\n" +
                "  Chặn input tạm thời, có thể bật lại sau (cooldown, stun, charge).\n" +
                "  SetCooldownFill() tự gọi SetDisabled(true) khi fill < 1.\n\n" +
                "Thứ tự visual: !Interactable > Disabled > Pressed > Normal.\n" +
                "Nếu Interactable=false thì Disabled bị bỏ qua hoàn toàn.");

            FaqItem(14, "IsSkillDown / IsSkillHeld / IsSkillReleased dùng khi nào?",
                "Đây là shortcut methods, tương đương GetSkillAction với mode tường minh:\n" +
                "  IsSkillDown(name)     → GetSkillAction(name, PressDown) — 1 frame khi nhấn\n" +
                "  IsSkillHeld(name)     → GetSkillAction(name, Hold)      — true suốt khi giữ\n" +
                "  IsSkillReleased(name) → GetSkillAction(name, PressUp)   — 1 frame khi nhả\n\n" +
                "Dùng khi cần mode khác với readMode đã config trong profile,\n" +
                "hoặc khi cần cả 3 event trên cùng 1 action (charge mechanic).");

            FaqItem(15, "Làm sao đổi giao diện button mobile lúc runtime?",
                "Dùng SetVisualMode(VisualMode mode) để chuyển giữa ColorTint và SpriteSwap:\n" +
                "    btn.SetVisualMode(TISMobileSkillButton.VisualMode.SpriteSwap);\n" +
                "    btn.SetVisualMode(TISMobileSkillButton.VisualMode.ColorTint);\n\n" +
                "Dùng khi: đổi skin nhân vật, chuyển theme UI, test A/B.\n" +
                "Hàm tự gọi UpdateVisual() ngay sau khi đổi mode.");

            FaqItem(16, "TISGamepadUIButton không di chuyển focus sang button khác?",
                "Kiểm tra lần lượt:\n" +
                "1. navUp / navDown / navLeft / navRight đã assign trong Inspector chưa?\n" +
                "2. isNavigationEnabled = true? Nếu DisableNavigation() đã gọi → gọi EnableNavigation() để bật lại.\n" +
                "3. layer = Content và button đang có EventSystem focus không? Dùng Debug → currentSelectedGameObject.\n" +
                "4. Gamepad.current != null (tay cầm đang kết nối).\n" +
                "5. holdDelay quá cao? Thử giảm xuống 0.15f.\n" +
                "6. Button đích có activeInHierarchy = true không? Inactive button bị bỏ qua.");

            FaqItem(17, "Tab / SubTab không chuyển khi nhấn LT/RT hoặc LB/RB?",
                "Tab và SubTab layer dùng alwaysActive = true và xử lý input trong HandleGlobalLayerInput().\n" +
                "Kiểm tra:\n" +
                "1. alwaysActive = true trong button Tab/SubTab.\n" +
                "2. SetAsActiveInLayer() đã được gọi khi khởi tạo scene (thường từ UINavigationContext).\n" +
                "3. inputSource đúng: Tab dùng LT_RT, SubTab dùng LB_RB.\n" +
                "4. navigationMode phù hợp: Horizontal nếu Tab nằm ngang, Vertical nếu dọc.\n" +
                "5. Button tab đích (navLeft/navRight) có activeInHierarchy = true không?");

            GUILayout.Space(8);
            Note("Mở cửa sổ Documentation từ trong Unity: Tools → True Input System → Documentation. Hoặc bấm nút ? trong Setup Tool.");

            EndContentScroll();
        }

        private void FaqItem(int id, string question, string answer)
        {
            bool isOpen = _faqOpen.Contains(id);

            // Question bar
            Rect qRect = GUILayoutUtility.GetRect(0, 36);
            EditorGUI.DrawRect(qRect, isOpen ? BgElevated : BgSurface);
            EditorGUI.DrawRect(new Rect(qRect.x, qRect.y, isOpen ? 3 : 1, qRect.height),
                               isOpen ? AccentBlue : BorderSub);
            EditorGUI.DrawRect(new Rect(qRect.x, qRect.yMax - 1, qRect.width, 1), BorderSub);

            // Toggle arrow
            var arrSt = new GUIStyle(EditorStyles.label)
            {
                fontSize = 14, fontStyle = FontStyle.Bold,
                normal = { textColor = isOpen ? AccentBlue : TxtMuted },
                alignment = TextAnchor.MiddleRight,
                padding = new RectOffset(0, 12, 0, 0),
            };
            GUI.Label(new Rect(qRect.xMax - 30, qRect.y, 30, qRect.height), isOpen ? "▲" : "▼", arrSt);

            // Question text
            var qSt = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12, fontStyle = isOpen ? FontStyle.Bold : FontStyle.Normal,
                wordWrap = false,
                normal = { textColor = isOpen ? TxtPrimary : TxtSecond },
                padding = new RectOffset(12, 36, 0, 0),
            };
            GUI.Label(new Rect(qRect.x, qRect.y, qRect.width - 30, qRect.height), "Q: " + question, qSt);

            // Click handler
            if (Event.current.type == EventType.MouseDown && qRect.Contains(Event.current.mousePosition))
            {
                if (isOpen) _faqOpen.Remove(id);
                else _faqOpen.Add(id);
                Event.current.Use();
                Repaint();
            }

            // Answer
            if (isOpen)
            {
                Rect aRect = GUILayoutUtility.GetRect(0, 0);
                var aSt = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 12, richText = true, wordWrap = true,
                    normal = { textColor = TxtSecond, background = Tex(FaqAnswerBg) },
                    padding = new RectOffset(18, 14, 10, 12),
                    margin = new RectOffset(0, 0, 0, 0),
                };
                var content = new GUIContent("A: " + answer);
                float h = aSt.CalcHeight(content, aRect.width);
                Rect answerRect = GUILayoutUtility.GetRect(aRect.width, h);
                EditorGUI.DrawRect(new Rect(answerRect.x, answerRect.y, 3, answerRect.height), AccentBlue);
                EditorGUI.DrawRect(new Rect(answerRect.x, answerRect.yMax - 1, answerRect.width, 1), BorderSub);
                GUI.Label(answerRect, content, aSt);
            }

            GUILayout.Space(2);
        }  // FaqItem

    }  // partial class TISDocWindow
}
#endif
