// ============================================================
//  TrueInputSystem — TISCode_MobileInputSetup.cs
//  Code Sub-Tab Module: MobileSetup
// ============================================================
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace GaMo.TrueInputSystem.Editor
{
    public class TISCode_MobileInputSetup : ICodeSubTabModule
    {
        public string Label => "MobileSetup";

        public void Draw(TISDocWindow win)
        {
            win.PageTitle("TISMobileInputSetup",
                "Khởi tạo mobile UI, toggle features, chia vùng joystick/camera.");

            win.H2("Subscribe SetupCompleted & Check IsReady");
            win.CodeBlock(
                "[SerializeField] TISMobileInputSetup _mobileSetup;\n\n" +
                "void Start()\n" +
                "{\n" +
                "    if (_mobileSetup.IsReady)\n" +
                "        OnMobileReady(_mobileSetup);\n" +
                "    else\n" +
                "        _mobileSetup.SetupCompleted += OnMobileReady;\n" +
                "}\n\n" +
                "void OnMobileReady(TISMobileInputSetup setup)\n" +
                "{\n" +
                "    setup.DatTyLeVung(0.4f);  // 40% joystick / 60% camera\n" +
                "}");

            win.Sep();
            win.H2("Settings UI — Toggle & Setter Methods");
            win.CodeBlock(
                "// Toggle (bật/tắt)\n" +
                "rotateToggle.onValueChanged.AddListener(val => _mobileSetup.DatXoayCamera(val));\n" +
                "zoomToggle.onValueChanged.AddListener(val   => _mobileSetup.DatZoom(val));\n" +
                "joystickToggle.onValueChanged.AddListener(val => _mobileSetup.DatJoystick(val));\n\n" +
                "// Slider điều chỉnh tỉ lệ joystick (0.25 - 0.75)\n" +
                "joystickSizeSlider.onValueChanged.AddListener(val => _mobileSetup.DatTyLeVung(val));\n\n" +
                "// Hoặc dùng Toggle methods (flip giá trị hiện tại)\n" +
                "void OnCameraLockButton() => _mobileSetup.BatTatXoayCamera();");

            win.Sep();
            win.H2("Sync Zone Sau Khi Thay Đổi Layout");
            win.CodeBlock(
                "_mobileSetup.DatTyLeVung(0.35f);\n" +
                "_mobileSetup.DongBoTyLe(); // Đồng bộ joystick + camera zone\n\n" +
                "_mobileSetup.ApDungCaiDatCamera();");

            win.Note("TISMobileInputSetup chỉ active trên mobile platform. Trong Editor nó luôn active để test. Trên PC build component tự SetActive(false).");
        }
    }
}
#endif
