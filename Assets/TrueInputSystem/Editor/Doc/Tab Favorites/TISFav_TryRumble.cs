// ============================================================
//  TrueInputSystem — TISFav_TryRumble.cs
//  Favorites Sub-Tab Module: TryRumble
// ============================================================
#if UNITY_EDITOR
namespace TrueInputSystem.Editor
{
    public class TISFav_TryRumble : IFavSubTabModule
    {
        public string Label => "TryRumble";

        public void Draw(TISDocWindow win)
        {
            win.FavApiBox(
                "TISInputBridge",
                "TryRumble / StopRumble",
                "void",
                "Rung tay cầm gamepad — ngắn có thời gian hoặc liên tục cho đến khi StopRumble().",
                win.C_AccentAmber);

            win.H2("Cú Pháp — 3 Overload");
            win.CodeBlock(
                "// Rung trong dur giây rồi tự dừng:\n" +
                "_input.TryRumble(float lowFreq, float highFreq, float duration);\n\n" +
                "// Rung liên tục — phải gọi StopRumble() thủ công:\n" +
                "_input.TryRumble(float lowFreq, float highFreq);\n\n" +
                "// Dừng rung ngay lập tức:\n" +
                "_input.StopRumble();");

            win.Sep();
            win.H2("Ví Dụ — Impact / Hit");
            win.CodeBlock(
                "void OnHit()\n" +
                "{\n" +
                "    _input.TryRumble(0.6f, 0.3f, 0.15f);  // mạnh - nhẹ - 150ms\n" +
                "}");

            win.Sep();
            win.H2("Ví Dụ — Charge (Rung Liên Tục)");
            win.CodeBlock(
                "void OnBeginCharge()   => _input.TryRumble(0.2f, 0.1f);  // bắt đầu charge\n" +
                "void OnReleaseCharge() => _input.StopRumble();            // thả ra");

            win.Sep();
            win.H2("Ví Dụ — Theo Sức Mạnh Đòn");
            win.CodeBlock(
                "void OnAttack(float power) // power: 0 → 1\n" +
                "{\n" +
                "    _input.TryRumble(\n" +
                "        Mathf.Lerp(0.1f, 0.8f, power),   // lowFreq\n" +
                "        Mathf.Lerp(0.05f, 0.5f, power),  // highFreq\n" +
                "        Mathf.Lerp(0.05f, 0.3f, power)   // duration\n" +
                "    );\n" +
                "}");

            win.Tip("lowFreq = rung nặng (HD Rumble trái). highFreq = rung nhỏ/nhanh (HD Rumble phải). Cả hai trong dải 0–1.");
            win.Note("TryRumble chỉ hoạt động khi enableRumble = true trong TISGamepadSettings và có gamepad kết nối.");
        }
    }
}
#endif
