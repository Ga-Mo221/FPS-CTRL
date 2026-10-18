// ============================================================
//  TrueInputSystem — TISCode_GyroTouchpad.cs
//  Code Sub-Tab Module: Gyro & Touchpad (TIS Motion)
// ============================================================
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace GaMo.TrueInputSystem.Editor
{
    public class TISCode_GyroTouchpad : ICodeSubTabModule
    {
        public string Label => "Gyro & Touchpad";

        public void Draw(TISDocWindow win)
        {
            win.PageTitle("Gyro & Touchpad",
                "Gyro + touchpad tay cầm (DualShock 4, DualSense, Switch Pro) — bind như mọi nút qua thiết bị ảo <TISMotion>.");

            win.H2("Setup (không cần add component)");
            win.Step(1, "Bind trong Setup Tool",
                "Cài Đặt Action › Binding Gamepad: chọn <TISMotion>/gyro, /touchpad… — hoặc với binding Left / Right Stick bật \"+ Gyro\" / \"+ Touchpad\".");
            win.Step(2, "Tinh chỉnh",
                "Cài Đặt Nền Tảng › Gamepad › Motion: Gyro Sensitivity, lọc, calibration, Touchpad Look Sensitivity.");
            win.Step(3, "Provider",
                "Motion › Providers: Use SDL3 (tay cầm thật trên PC), Simulate (Editor) để test bằng bàn phím. Chỉ khởi động khi profile có binding gyro / touchpad.");

            win.Sep();
            win.H2("Look = Right Stick + Gyro + Touchpad");
            win.Body("Bật \"+ Gyro\" / \"+ Touchpad\" cho action Look → đọc LookDelta như bình thường, không cần code thêm:");
            win.CodeBlock(
                "Vector2 look = TISInputBridge.Instance.LookDelta;\n" +
                "// = right stick + gyro × Gyro Sensitivity + touchpad × Touchpad Look Sensitivity\n" +
                "_yaw   += look.x;\n" +
                "_pitch -= look.y;");

            win.Sep();
            win.H2("Dùng Riêng Gyro / Touchpad");
            win.Body("Tạo action riêng, Binding Gamepad = path <TISMotion>. Loại action tự suy (Vector2 / Axis / Button). Action khác Look nhận đơn vị thô:");
            win.CodeBlock(
                "var tis = TISInputBridge.Instance;\n\n" +
                "// Action \"Tilt\"   : <TISMotion>/gyro            → độ/giây\n" +
                "Vector2 tilt  = tis.GetVector2Action(\"Tilt\");\n\n" +
                "// Action \"Steer\"  : <TISMotion>/gyro/x          → Axis, độ/giây\n" +
                "float steer   = tis.GetAxisAction(\"Steer\") * 0.02f;\n\n" +
                "// Action \"Swipe\"  : <TISMotion>/touchpad        → bề rộng touchpad / giây\n" +
                "Vector2 swipe = tis.GetVector2Action(\"Swipe\");\n\n" +
                "// Action \"PadTap\" : <TISMotion>/touchpadPress   → Button\n" +
                "if (tis.IsSkillDown(\"PadTap\")) OpenMap();");

            win.Sep();
            win.H2("Đọc Trực Tiếp (không qua action)");
            win.CodeBlock(
                "var m = TISInputBridge.Instance.Motion;\n\n" +
                "if (m.HasGyro)\n" +
                "    Debug.Log($\"Gyro {m.GyroRate} °/s  (aim {m.GyroAim})\");\n\n" +
                "if (m.HasTouchpad && m.IsTouching)\n" +
                "    Debug.Log($\"Touch {m.TouchpadPosition}  v={m.TouchpadVelocity}\");\n\n" +
                "if (m.TouchpadPressedThisFrame) Debug.Log(\"Touchpad click\");");

            win.Sep();
            win.H2("Calibration (chống drift)");
            win.Body("Mặc định tự hiệu chỉnh khi tay cầm đặt yên (Motion Settings › Auto Calibrate). Calibration thủ công cho màn hình Settings:");
            win.CodeBlock(
                "var m = TISInputBridge.Instance.Motion;\n\n" +
                "void OnCalibrateButton() => m.BeginGyroCalibration();   // \"Đặt tay cầm lên bàn…\"\n\n" +
                "IEnumerator CalibrateFor(float seconds)\n" +
                "{\n" +
                "    m.BeginGyroCalibration();\n" +
                "    yield return new WaitForSecondsRealtime(seconds);\n" +
                "    bool ok = m.EndGyroCalibration();\n" +
                "    PlayerPrefs.SetString(\"GyroOffset\", JsonUtility.ToJson(m.GyroProcessor.Offset));\n" +
                "}\n\n" +
                "// Lần mở game sau:\n" +
                "m.GyroProcessor.SetOffset(JsonUtility.FromJson<Vector3>(PlayerPrefs.GetString(\"GyroOffset\")));");

            win.Sep();
            win.H2("Debug");
            win.CodeBlock(
                "// Bảng gyro / touchpad góc trên-trái (Editor + Development Build)\n" +
                "TISInputBridge.Instance.ShowMotionDebugGUI = true;\n\n" +
                "// Source đang dùng (cắm / rút tay cầm)\n" +
                "TISInputBridge.Instance.Motion.OnActiveSourceChanged += src =>\n" +
                "    Debug.Log(src != null ? $\"Motion: {src.Name}\" : \"Motion: không có tay cầm\");");

            win.Sep();
            win.H2("Provider Tự Viết (Console / SDK riêng)");
            win.Body("Implement ITISMotionSource rồi đăng ký. Priority cao nhất & đang kết nối được dùng (Simulated 0, SDL3 100, Console 200):");
            win.CodeBlock(
                "public class MyConsoleMotion : MonoBehaviour, ITISMotionSource\n" +
                "{\n" +
                "    public string  Name           => \"PS5 (SDK)\";\n" +
                "    public int     Priority       => 200;\n" +
                "    public bool    IsConnected    => _connected;\n" +
                "    public bool    HasMotion      => true;\n" +
                "    public bool    HasTouchpad    => true;\n" +
                "    public int     MaxTouchPoints => 2;\n" +
                "    public Vector3 Gyro  { get; private set; }   // độ/giây, X phải · Y lên · Z về người chơi\n" +
                "    public Vector3 Accel { get; private set; }   // g\n" +
                "    public bool    TouchpadPressed { get; private set; }\n\n" +
                "    void OnEnable()  => TISInputBridge.Instance.RegisterMotionSource(this);\n" +
                "    void OnDisable() => TISInputBridge.Instance?.UnregisterMotionSource(this);\n\n" +
                "    public void Tick(float dt) { /* đọc SDK → Gyro, Accel, touch */ }\n" +
                "    public bool TryGetTouch(int i, out TISTouchPoint p) { p = default; return false; }\n" +
                "    bool _connected;\n" +
                "}");

            win.Note("Gyro / touchpad trên PC đọc qua SDL3.dll đi kèm TIS (DualShock 4, DualSense, Switch Pro, Joy-Con). Tay cầm chỉ chạy XInput (Xbox, Flydigi qua đầu thu 2.4G) không có dữ liệu gyro.");
            win.Tip("Test trong Editor không cần tay cầm: Motion › Providers › Simulate (Editor). I/J/K/L/U/O = gyro, Right Shift = nhanh, giữ T + chuột = touchpad, G = click.");
        }
    }
}
#endif
