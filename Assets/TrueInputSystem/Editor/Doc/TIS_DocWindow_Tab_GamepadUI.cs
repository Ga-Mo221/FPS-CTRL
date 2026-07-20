// ============================================================
//  TrueInputSystem — TIS_DocWindow.Tab_GamepadUI.cs
//  Tab: Gamepad UI Setup
// ============================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TrueInputSystem.Editor
{
    public partial class TISDocWindow : EditorWindow
    {
        // ══════════════════════════════════════════════════════
        //  TAB 7 — GAMEPAD UI SETUP
        // ══════════════════════════════════════════════════════

        private void DrawTab_GamepadUI()
        {
            BeginContentScroll("gamepadui");
            PageTitle("Gamepad UI Setup",
                "Navigation 3 lớp: Tab (LT/RT) → SubTab (LB/RB) → Content (DPad/Stick). " +
                "Mỗi lớp dùng một component riêng, kết nối qua TISUINavigationContext.");

            // ── Kiến trúc tổng quan ───────────────────────────
            H2("Kiến Trúc 3 Lớp");
            CodeBlock(
                "// LT / RT  ──→  Tab layer       (alwaysActive, virtual selected)\n" +
                "// LB / RB  ──→  SubTab layer    (alwaysActive, virtual selected)\n" +
                "// DPad/Stick ─→  Content layer  (EventSystem focus thật)\n\n" +
                "// Khi Tab đổi  → TISUINavigationContext redirect SubTab + Content focus\n" +
                "// Khi SubTab đổi → TISUINavigationContext redirect Content focus");

            // ── Components cần thiết ─────────────────────────
            Sep();
            H2("Các Component Cần Thiết");
            ApiHeader("Component", "AddComponentMenu", "Vai trò");
            ApiRow("TISGamepadUIButton",      "TIS/Demo/TIS Gamepad UI Button",       "Gắn lên từng Button. Xử lý input + navigation cho layer đó.", "blue");
            ApiRow("TISGamepadUIButtonGroup", "TIS/Demo/TIS Gamepad UI Button Group", "Gắn lên parent chứa nhiều Content button. Tự tính navUp/Down/Left/Right.", "blue");
            ApiRow("TISUINavigationContext",  "TIS/Demo/TIS UI Navigation Context",   "Singleton. Redirect focus khi Tab/SubTab đổi. Đặt 1 cái trong scene.", "purple");
            ApiRow("TISGamepadVirtualCursor", "TIS/Demo/TIS Gamepad Virtual Cursor",  "Con trỏ ảo điều khiển bằng left stick. Tùy chọn — dùng khi cần click tự do.", "amber");

            // ════════════════════════════════════════════════════
            //  PHẦN 1: TISGamepadUIButton
            // ════════════════════════════════════════════════════
            Sep();
            H2("1 — Setup TISGamepadUIButton");
            Note("Kế thừa từ UnityEngine.UI.Button. Gắn lên từng button trong UI. Navigation Unity mặc định tự tắt (Navigation.Mode.None) — TIS tự xử lý.");

            GUILayout.Space(4);
            H2("Inspector Fields Quan Trọng");
            ApiHeader("Field", "Type", "Mô tả");
            ApiRow("layer",           "NavigationLayer", "Tab / SubTab / Content — xác định lớp của button này.", "purple");
            ApiRow("inputSource",     "InputSource",     "Default (DPad+Stick) / LB_RB / LT_RT — nguồn input tương ứng với layer.", "purple");
            ApiRow("navigationMode",  "NavMode",         "FourDirectional / Horizontal / Vertical — hướng di chuyển cho phép.", "purple");
            ApiRow("holdDelay",       "float",           "Giây giữa 2 lần di chuyển khi giữ nút. Mặc định 0.2f.", "amber");
            ApiRow("alwaysActive",    "bool",            "true cho Tab và SubTab — giữ visual Highlighted dù không có EventSystem focus.", "green");
            ApiRow("isFirstSelected", "bool",            "true cho Content button đầu tiên — tự nhận focus sau 1 frame khi GameObject bật.", "green");
            ApiRow("navUp/Down/Left/Right", "Button",    "Gán thủ công hoặc dùng TISGamepadUIButtonGroup để tự tính.", "blue");
            ApiRow("onSelect",        "UnityEvent",      "Fire khi button được select. Dùng để bật panel, đổi nội dung.", "purple");
            ApiRow("onDeselect",      "UnityEvent",      "Fire khi button bị deselect.", "purple");
            ApiRow("onBecameActiveInLayer", "UnityEvent<Button>", "Fire khi Tab/SubTab đổi qua global input. TISUINavigationContext lắng nghe event này.", "purple");

            GUILayout.Space(8);
            H2("Cấu Hình Điển Hình Cho Từng Layer");
            CodeBlock(
                "// ── Tab Button (LT/RT, nằm ngang) ──────────────────────\n" +
                "layer          = NavigationLayer.Tab\n" +
                "inputSource    = InputSource.LT_RT\n" +
                "navigationMode = NavMode.Horizontal\n" +
                "alwaysActive   = true\n" +
                "// navLeft / navRight → Tab kế bên\n\n" +
                "// ── SubTab Button (LB/RB, nằm ngang) ────────────────────\n" +
                "layer          = NavigationLayer.SubTab\n" +
                "inputSource    = InputSource.LB_RB\n" +
                "navigationMode = NavMode.Horizontal\n" +
                "alwaysActive   = true\n" +
                "// navLeft / navRight → SubTab kế bên\n\n" +
                "// ── Content Button (DPad/Stick) ──────────────────────────\n" +
                "layer          = NavigationLayer.Content\n" +
                "inputSource    = InputSource.Default\n" +
                "navigationMode = NavMode.FourDirectional\n" +
                "alwaysActive   = false\n" +
                "isFirstSelected = true   // chỉ bật trên button đầu tiên của mỗi panel");

            // ════════════════════════════════════════════════════
            //  PHẦN 2: TISGamepadUIButtonGroup
            // ════════════════════════════════════════════════════
            Sep();
            H2("2 — Setup TISGamepadUIButtonGroup");
            Note("Gắn lên GameObject cha chứa các Content button. Gọi RebuildNavigation() để tự tính navUp/Down/Left/Right cho tất cả button con. Chỉ xét con trực tiếp (direct children) có TISGamepadUIButton và activeInHierarchy.");

            GUILayout.Space(4);
            ApiHeader("Field", "Type", "Mô tả");
            ApiRow("layoutType",       "GroupLayoutType",      "VerticalList / HorizontalRow / Grid — chế độ sắp xếp.", "purple");
            ApiRow("gridColumns",      "int",                  "Số cột (chỉ dùng khi Grid). Min = 1.", "amber");
            ApiRow("gridVerticalWrap", "GridVerticalWrapMode", "ColumnWrap: mỗi cột wrap riêng. GlobalWrap: Left/Right tuyến tính toàn hàng.", "purple");

            GUILayout.Space(8);
            CodeBlock(
                "// Gọi sau khi thay đổi layout hoặc bật/tắt button con\n" +
                "GetComponent<TISGamepadUIButtonGroup>().RebuildNavigation();\n\n" +
                "// Hoặc gọi từ Editor script khi setup xong trong Inspector\n" +
                "// (nút RebuildNavigation() có trong Custom Inspector của component)");

            GUILayout.Space(4);
            H2("Wrap-Around Cho Từng Layout");
            CodeBlock(
                "// VerticalList (danh sách dọc)\n" +
                "//   ↑ tại đầu  → nhảy về cuối\n" +
                "//   ↓ tại cuối → nhảy về đầu\n\n" +
                "// HorizontalRow (hàng ngang)\n" +
                "//   ← tại trái → nhảy về phải cùng\n" +
                "//   → tại phải → nhảy về trái cùng\n\n" +
                "// Grid — ColumnWrap (mặc định)\n" +
                "//   ↑ tại đầu cột → wrap về cuối cột đó\n" +
                "//   ← / → wrap trong cùng hàng\n\n" +
                "// Grid — GlobalWrap\n" +
                "//   ← / → tuyến tính qua toàn bộ các ô (như đọc sách)\n" +
                "//   ↑ / ↓ vẫn wrap trong cùng cột");

            // ════════════════════════════════════════════════════
            //  PHẦN 3: TISUINavigationContext
            // ════════════════════════════════════════════════════
            Sep();
            H2("3 — Setup TISUINavigationContext");
            Note("Singleton. Đặt đúng 1 component trong scene. Nếu có 2 instance, cái sau tự hủy trong Awake(). Không cần kéo thả vào script khác — dùng TISUINavigationContext.Instance.");

            GUILayout.Space(4);
            Step(1, "Tạo GameObject → Add Component → TISUINavigationContext", "Đặt ở vị trí dễ tìm trong Hierarchy, ví dụ: UI Root / Navigation Manager.");
            Step(2, "Khai báo Tab Mapping",
                "Mỗi TabEntry gồm:\n" +
                "  • tabButton      — TISGamepadUIButton của Tab đó\n" +
                "  • firstSubTab    — SubTab sẽ được highlight khi Tab này active\n" +
                "  • firstContent   — Content button sẽ được focus khi Tab này active\n" +
                "  (firstContent bị ghi đè bởi firstContent của firstSubTab nếu có)");
            Step(3, "Khai báo SubTab Mapping",
                "Mỗi SubTabEntry gồm:\n" +
                "  • subTabButton   — TISGamepadUIButton của SubTab đó\n" +
                "  • firstContent   — Content button sẽ được focus khi SubTab này active");
            Step(4, "Assign Initial Selection",
                "defaultTab     → Tab sẽ được SetAsActiveInLayer() khi Start()\n" +
                "defaultSubTab  → SubTab sẽ được SetAsActiveInLayer() khi Start()\n" +
                "defaultContent → Button sẽ được EventSystem.SetSelectedGameObject() khi Start()");

            GUILayout.Space(8);
            H2("Luồng Xử Lý Khi Tab Đổi");
            CodeBlock(
                "// 1. Người chơi nhấn RT\n" +
                "// 2. TISGamepadUIButton (Tab layer) gọi HandleGlobalLayerInput()\n" +
                "// 3. Tab mới được SetAsActiveInLayer() → fire onBecameActiveInLayer\n" +
                "// 4. TISUINavigationContext.OnTabBecameActive() nhận callback:\n" +
                "//      a. firstSubTab.SetAsActiveInLayer()  → highlight SubTab đầu tiên\n" +
                "//      b. MoveContentFocus(firstContent)    → EventSystem focus về Content đầu\n\n" +
                "// Khi SubTab đổi (LB/RB):\n" +
                "// 1. TISUINavigationContext.OnSubTabBecameActive()\n" +
                "//      a. MoveContentFocus(firstContent) → focus Content đầu của SubTab mới");

            GUILayout.Space(4);
            H2("Code — Subscribe Thủ Công (Không Dùng Context)");
            CodeBlock(
                "// Nếu chỉ có 1 layer Tab (không có SubTab), không cần UINavigationContext\n" +
                "// Tự subscribe onBecameActiveInLayer:\n\n" +
                "[SerializeField] TISGamepadUIButton _tabA, _tabB;\n" +
                "[SerializeField] TISGamepadUIButton _firstContentA, _firstContentB;\n\n" +
                "void Start()\n" +
                "{\n" +
                "    _tabA.SetAsActiveInLayer();\n" +
                "    _tabA.onBecameActiveInLayer.AddListener(_ =>\n" +
                "        EventSystem.current.SetSelectedGameObject(_firstContentA.gameObject));\n" +
                "    _tabB.onBecameActiveInLayer.AddListener(_ =>\n" +
                "        EventSystem.current.SetSelectedGameObject(_firstContentB.gameObject));\n" +
                "}\n\n" +
                "// Query button đang active trong layer từ bất kỳ script:\n" +
                "var activeTab = TISGamepadUIButton.GetActiveInLayer(\n" +
                "    TISGamepadUIButton.NavigationLayer.Tab);");

            // ════════════════════════════════════════════════════
            //  PHẦN 4: TISGamepadVirtualCursor
            // ════════════════════════════════════════════════════
            Sep();
            H2("4 — Setup TISGamepadVirtualCursor (Tùy Chọn)");
            Note("Dùng khi cần con trỏ ảo tự do thay vì navigation theo nút. Left stick di chuyển cursor, buttonSouth (A/Cross) click. Inject vào InputSystem như chuột thật — tương thích mọi Unity UI Button, Toggle, Slider.");

            GUILayout.Space(4);
            Step(1, "Tạo UI Image làm con trỏ", "Tạo Image trong Canvas → đặt Pivot = center. Đây là hình ảnh hiện trên màn hình.");
            Step(2, "Add Component → TISGamepadVirtualCursor", "Gắn lên bất kỳ GameObject nào trong scene.");
            Step(3, "Assign Inspector",
                "cursorTransform → RectTransform của Image con trỏ vừa tạo\n" +
                "parentCanvas    → Canvas chứa Image đó (Render Mode: Screen Space Overlay hoặc Camera)\n" +
                "cursorSpeed     → tốc độ di chuyển cursor (mặc định 1000)");
            Step(4, "Tự đăng ký", "Component tự gọi bridge.RegisterGamepadCursor(this) trong Awake(). Không cần làm gì thêm.");

            GUILayout.Space(8);
            H2("Bật / Tắt Cursor Từ Code");
            CodeBlock(
                "// Khi mở menu: bật cursor\n" +
                "void OnOpenMenu()\n" +
                "    => TISInputBridge.Instance.SetActiveGamepadCursor(true);\n" +
                "// Cursor tự reset về center màn hình và sync InputSystem khi bật\n\n" +
                "// Khi đóng menu: tắt cursor\n" +
                "void OnCloseMenu()\n" +
                "    => TISInputBridge.Instance.SetActiveGamepadCursor(false);\n\n" +
                "// Điều kiện hoạt động:\n" +
                "// 1. TISGamepadVirtualCursor component tồn tại trong scene\n" +
                "// 2. Gamepad.current != null\n" +
                "// 3. SetActiveGamepadCursor(true) đã được gọi\n" +
                "// 4. InputSystemUIInputModule có trong scene (EventSystem)");

            Warn("TISGamepadVirtualCursor và navigation button (TISGamepadUIButton) không nên dùng đồng thời — cursor inject chuột ảo vào EventSystem, có thể conflict với focus của Content layer. Tắt cursor trước khi bật navigation và ngược lại.");

            // ════════════════════════════════════════════════════
            //  PHẦN 5: Disable/Enable Navigation
            // ════════════════════════════════════════════════════
            Sep();
            H2("5 — Khóa / Mở Navigation (Panel Overlay)");
            Body("Khi mở popup hoặc dialog đè lên, gọi DisableNavigation() trên các button bên dưới để tránh input leak.");
            CodeBlock(
                "[SerializeField] TISGamepadUIButton[] _mainButtons;\n" +
                "[SerializeField] TISGamepadUIButton   _firstPopupButton;\n\n" +
                "void OnOpenPopup()\n" +
                "{\n" +
                "    // Khóa toàn bộ button bên dưới\n" +
                "    foreach (var btn in _mainButtons)\n" +
                "        btn.DisableNavigation();\n\n" +
                "    // Focus về button đầu tiên trong popup\n" +
                "    EventSystem.current.SetSelectedGameObject(_firstPopupButton.gameObject);\n" +
                "}\n\n" +
                "void OnClosePopup()\n" +
                "{\n" +
                "    // Mở lại navigation\n" +
                "    foreach (var btn in _mainButtons)\n" +
                "        btn.EnableNavigation();\n" +
                "}");

            Note("DisableNavigation() tự reset visual về Normal để button không bị 'frozen' ở trạng thái sáng. EnableNavigation() chỉ bật lại flag — visual sẽ update theo frame tiếp theo khi button nhận focus.");

            EndContentScroll();
        }

    }  // partial class TISDocWindow
}
#endif
