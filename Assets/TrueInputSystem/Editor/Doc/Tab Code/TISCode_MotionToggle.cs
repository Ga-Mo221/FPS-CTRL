// ============================================================
//  TrueInputSystem — TISCode_MotionToggle.cs
//  Code Sub-Tab Module: Bật / Tắt Gyro & Touchpad (toàn game / profile / action)
// ============================================================
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace GaMo.TrueInputSystem.Editor
{
    public class TISCode_MotionToggle : ICodeSubTabModule
    {
        public string Label => "Gyro Bật/Tắt";

        public void Draw(TISDocWindow win)
        {
            win.PageTitle("Bật / Tắt Gyro & Touchpad",
                "3 mức: toàn game → profile → action. Hiệu lực ngay, không rebuild profile.");

            win.H2("3 Mức Bật / Tắt");
            win.Body("Gyro / touchpad của một action chỉ chạy khi CẢ 3 mức đều bật:");
            win.CodeBlock(
                "var tis = TISInputBridge.Instance;\n\n" +
                "// 1. TOÀN GAME — không lưu, dùng cho trạng thái tạm (cutscene, menu)\n" +
                "tis.GyroEnabled     = false;\n" +
                "tis.TouchpadEnabled = false;\n\n" +
                "// 2. THEO PROFILE — mọi action của profile đang chạy\n" +
                "tis.SetProfileMotion(gyro: false, touchpad: true);\n" +
                "tis.SetProfileGyroEnabled(false);\n" +
                "tis.SetProfileTouchpadEnabled(true);\n\n" +
                "// 3. THEO ACTION — chỉ một action\n" +
                "tis.SetActionMotion(\"Look\", gyro: false, touchpad: true);\n" +
                "tis.SetActionGyroEnabled(\"Look\", false);\n" +
                "tis.SetActionTouchpadEnabled(\"Look\", true);");

            win.Sep();
            win.H2("Lưu Lựa Chọn Của Người Chơi");
            win.Body("Truyền save: true → lưu vào persistentDataPath/TIS_Motion_{profile}.json, TIS tự áp lại khi mở game / swap profile:");
            win.CodeBlock(
                "tis.SetActionGyroEnabled(\"Look\", false, save: true);\n" +
                "tis.SetProfileMotion(gyro: true, touchpad: false, save: true);\n\n" +
                "// Về mặc định của profile asset + xoá file đã lưu\n" +
                "tis.ResetMotionSettings();\n" +
                "// Chỉ về mặc định trong phiên, giữ file đã lưu\n" +
                "tis.ResetMotionSettings(deleteSaved: false);");

            win.Sep();
            win.H2("Kiểm Tra Trạng Thái");
            win.CodeBlock(
                "bool hasGyro   = tis.ActionHasGyro(\"Look\");          // có binding gyro không\n" +
                "bool gyroOn    = tis.IsActionGyroEnabled(\"Look\");    // đang THỰC SỰ chạy (cả 3 mức)\n" +
                "bool padOn     = tis.IsActionTouchpadEnabled(\"Look\");\n" +
                "bool profGyro  = tis.ProfileGyroEnabled;\n" +
                "bool profTouch = tis.ProfileTouchpadEnabled;");

            win.Sep();
            win.H2("Ví Dụ — Menu Settings");
            win.CodeBlock(
                "public class GyroSettingsUI : MonoBehaviour\n" +
                "{\n" +
                "    [SerializeField] Toggle _gyroAim;      // Gyro ngắm (action Look)\n" +
                "    [SerializeField] Toggle _touchLook;    // Touchpad xoay camera\n" +
                "    [SerializeField] Toggle _gyroAll;      // Gyro toàn profile\n" +
                "    TISInputBridge _tis;\n\n" +
                "    void OnEnable()\n" +
                "    {\n" +
                "        _tis = TISInputBridge.Instance;\n" +
                "        _tis.OnMotionSettingsChanged += Refresh;\n" +
                "        Refresh();\n\n" +
                "        _gyroAim.onValueChanged.AddListener(v   => _tis.SetActionGyroEnabled(\"Look\", v, save: true));\n" +
                "        _touchLook.onValueChanged.AddListener(v => _tis.SetActionTouchpadEnabled(\"Look\", v, save: true));\n" +
                "        _gyroAll.onValueChanged.AddListener(v   => _tis.SetProfileGyroEnabled(v, save: true));\n" +
                "    }\n\n" +
                "    void OnDisable() => _tis.OnMotionSettingsChanged -= Refresh;\n\n" +
                "    void Refresh()\n" +
                "    {\n" +
                "        _gyroAim.SetIsOnWithoutNotify(_tis.IsActionGyroEnabled(\"Look\"));\n" +
                "        _touchLook.SetIsOnWithoutNotify(_tis.IsActionTouchpadEnabled(\"Look\"));\n" +
                "        _gyroAll.SetIsOnWithoutNotify(_tis.ProfileGyroEnabled);\n" +
                "        _gyroAim.interactable = _tis.ActionHasGyro(\"Look\");   // ẩn nếu profile không bind gyro\n" +
                "    }\n" +
                "}");

            win.Note("Tắt theo action = vô hiệu đúng binding gyro / touchpad của action đó (binding override), áp dụng cho cả \"+ Gyro / + Touchpad\" lẫn binding <TISMotion>/... riêng. Action khác không bị ảnh hưởng.");
            win.Tip("Gyro chỉ khi ngắm: Motion Settings › Activation = WhileActionHeld, Action = \"ADS\" — không cần code.");
        }
    }
}
#endif
