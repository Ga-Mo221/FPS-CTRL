// ============================================================
//  TrueInputSystem — TISCode_GamepadUIButton.cs
//  Code Sub-Tab Module: Gamepad UI Btn
// ============================================================
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace GaMo.TrueInputSystem.Editor
{
    public class TISCode_GamepadUIButton : ICodeSubTabModule
    {
        public string Label => "Gamepad UI Btn";

        public void Draw(TISDocWindow win)
        {
            win.PageTitle("TISGamepadUIButton",
                "UI navigation gamepad nhiều lớp: Tab → SubTab → Content. Kế thừa Unity Button.");

            win.Body("Hệ thống phân cấp 3 lớp:\n" +
                 "  <b>Tab</b> (LT/RT) → <b>SubTab</b> (LB/RB) → <b>Content</b> (DPad/Stick).\n" +
                 "Tab và SubTab dùng <i>virtual selection</i> (alwaysActive), Content dùng EventSystem thật.");

            GUILayout.Space(8);
            win.H2("Setup — Khởi Tạo Layer Active");
            win.CodeBlock(
                "using TrueInputSystem;\n\n" +
                "public class UINavigationContext : MonoBehaviour\n" +
                "{\n" +
                "    [SerializeField] TISGamepadUIButton _defaultTab;\n" +
                "    [SerializeField] TISGamepadUIButton _defaultSubTab;\n" +
                "    [SerializeField] TISGamepadUIButton _defaultContent;\n\n" +
                "    void Start()\n" +
                "    {\n" +
                "        _defaultTab.SetAsActiveInLayer();\n" +
                "        _defaultSubTab.SetAsActiveInLayer();\n" +
                "        EventSystem.current.SetSelectedGameObject(_defaultContent.gameObject);\n" +
                "    }\n" +
                "}");

            win.Sep();
            win.H2("Lắng Nghe onBecameActiveInLayer — Redirect Content Focus");
            win.CodeBlock(
                "[SerializeField] TISGamepadUIButton _tabA;\n" +
                "[SerializeField] TISGamepadUIButton _firstContentOfTabA;\n\n" +
                "void Start()\n" +
                "{\n" +
                "    _tabA.onBecameActiveInLayer.AddListener(OnTabBecameActive);\n" +
                "}\n\n" +
                "void OnTabBecameActive(TISGamepadUIButton activeTab)\n" +
                "{\n" +
                "    if (activeTab == _tabA)\n" +
                "        EventSystem.current.SetSelectedGameObject(_firstContentOfTabA.gameObject);\n" +
                "}");

            win.Sep();
            win.H2("Bật / Tắt Navigation — Panel Overlay");
            win.CodeBlock(
                "[SerializeField] TISGamepadUIButton[] _mainMenuButtons;\n\n" +
                "void OnOpenPopup()\n" +
                "{\n" +
                "    foreach (var btn in _mainMenuButtons) btn.DisableNavigation();\n" +
                "}\n\n" +
                "void OnClosePopup()\n" +
                "{\n" +
                "    foreach (var btn in _mainMenuButtons) btn.EnableNavigation();\n" +
                "}");

            win.Sep();
            win.H2("Subscribe onSelect / onDeselect");
            win.CodeBlock(
                "[SerializeField] TISGamepadUIButton _inventoryTab;\n" +
                "[SerializeField] GameObject _inventoryPanel;\n\n" +
                "void Start()\n" +
                "{\n" +
                "    _inventoryTab.onSelect.AddListener(()   => _inventoryPanel.SetActive(true));\n" +
                "    _inventoryTab.onDeselect.AddListener(() => _inventoryPanel.SetActive(false));\n" +
                "}");

            win.Sep();
            win.H2("GetActiveInLayer — Query Từ Bất Kỳ Script Nào");
            win.CodeBlock(
                "var activeTab = TISGamepadUIButton.GetActiveInLayer(\n" +
                "    TISGamepadUIButton.NavigationLayer.Tab);\n\n" +
                "if (activeTab != null)\n" +
                "    Debug.Log(\"Tab active: \" + activeTab.name);\n\n" +
                "var activeSub = TISGamepadUIButton.GetActiveInLayer(\n" +
                "    TISGamepadUIButton.NavigationLayer.SubTab);");

            win.Sep();
            win.H2("NavMode & InputSource — Cấu Hình Điển Hình");
            win.CodeBlock(
                "// Tab nằm ngang (LT/RT)\n" +
                "tabButton.navigationMode = TISGamepadUIButton.NavMode.Horizontal;\n" +
                "tabButton.inputSource    = TISGamepadUIButton.InputSource.LT_RT;\n" +
                "tabButton.layer          = TISGamepadUIButton.NavigationLayer.Tab;\n" +
                "tabButton.alwaysActive   = true;\n\n" +
                "// SubTab nằm ngang (LB/RB)\n" +
                "subTabButton.navigationMode = TISGamepadUIButton.NavMode.Horizontal;\n" +
                "subTabButton.inputSource    = TISGamepadUIButton.InputSource.LB_RB;\n" +
                "subTabButton.layer          = TISGamepadUIButton.NavigationLayer.SubTab;\n" +
                "subTabButton.alwaysActive   = true;\n\n" +
                "// Content button (DPad/Stick)\n" +
                "contentButton.navigationMode  = TISGamepadUIButton.NavMode.FourDirectional;\n" +
                "contentButton.inputSource     = TISGamepadUIButton.InputSource.Default;\n" +
                "contentButton.layer           = TISGamepadUIButton.NavigationLayer.Content;\n" +
                "contentButton.isFirstSelected = true;");
        }
    }
}
#endif
