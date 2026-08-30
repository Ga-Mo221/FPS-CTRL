// ============================================================
//  TrueInputSystem — TISBindingPathOptions.cs  [Editor Only]
//  Tập trung toàn bộ danh sách dropdown binding path tại đây.
//
//  Label format:  "Tên hiển thị          <Device>/path"
//  → Đã loại bỏ hoàn toàn Odin Inspector.
// ============================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using TrueInputSystem; // Cần thiết cho TISVirtualControlID và TISInputActionType

namespace TrueInputSystem.Editor
{
    /// <summary>
    /// Cấu trúc thay thế cho ValueDropdownItem của Odin.
    /// Giúp lưu trữ cặp hiển thị (Label) và giá trị thực (Path).
    /// </summary>
    public struct TISBindingOption
    {
        public string Label;
        public string Path;

        public TISBindingOption(string label, string path)
        {
            Label = label;
            Path = path;
        }
    }

    public static class TISBindingPathOptions
    {
        // ── Column width cho phần path (căn phải) ─────────────
        private const int LabelCol = 28; 

        // ══════════════════════════════════════════════════════
        //  PC — KEYBOARD + MOUSE
        // ══════════════════════════════════════════════════════

        public static IEnumerable<TISBindingOption> PC()
        {
            // ── Letters ──
            yield return Header("Keyboard | Letters");
            foreach (var key in new[] { "w","a","s","d","q","e","r","f","g",
                                         "z","x","c","v","b","t","y","u","i",
                                         "o","p","h","j","k","l","n","m" })
                yield return Item(key.ToUpper(), $"<Keyboard>/{key}");

            // ── Special ──
            yield return Header("Keyboard | Special");
            yield return Item("Space",               "<Keyboard>/space");
            yield return Item("Enter",               "<Keyboard>/enter");
            yield return Item("Escape",              "<Keyboard>/escape");
            yield return Item("Tab",                 "<Keyboard>/tab");
            yield return Item("Backspace",           "<Keyboard>/backspace");
            yield return Item("Delete",              "<Keyboard>/delete");
            yield return Item("Insert",              "<Keyboard>/insert");
            yield return Item("Home",                "<Keyboard>/home");
            yield return Item("End",                 "<Keyboard>/end");
            yield return Item("Page Up",             "<Keyboard>/pageUp");
            yield return Item("Page Down",           "<Keyboard>/pageDown");

            // ── Arrows ──
            yield return Header("Keyboard | Arrows");
            yield return Item("Arrow Up",            "<Keyboard>/upArrow");
            yield return Item("Arrow Down",          "<Keyboard>/downArrow");
            yield return Item("Arrow Left",          "<Keyboard>/leftArrow");
            yield return Item("Arrow Right",         "<Keyboard>/rightArrow");

            // ── Digits ──
            yield return Header("Keyboard / Digits");
            for (int i = 0; i <= 9; i++)
                yield return Item(i.ToString(),      $"<Keyboard>/{i}");

            // ── Function Keys ──
            yield return Header("Keyboard / F-Keys");
            for (int i = 1; i <= 12; i++)
                yield return Item($"F{i}",           $"<Keyboard>/f{i}");

            // ── Numpad ──
            yield return Header("Keyboard / Numpad");
            for (int i = 0; i <= 9; i++)
                yield return Item($"Numpad {i}",     $"<Keyboard>/numpad{i}");
            yield return Item("Numpad Enter",        "<Keyboard>/numpadEnter");
            yield return Item("Numpad +",            "<Keyboard>/numpadPlus");
            yield return Item("Numpad -",            "<Keyboard>/numpadMinus");
            yield return Item("Numpad .",            "<Keyboard>/numpadPeriod");
            yield return Item("Numpad /",            "<Keyboard>/numpadDivide");
            yield return Item("Numpad *",            "<Keyboard>/numpadMultiply");

            // ── Mouse ──
            yield return Header("Mouse");
            yield return Item("Left Button",         "<Mouse>/leftButton");
            yield return Item("Right Button",        "<Mouse>/rightButton");
            yield return Item("Middle Button",       "<Mouse>/middleButton");
            yield return Item("Delta  (camera pan)", "<Mouse>/delta");
            yield return Item("Scroll Y",            "<Mouse>/scroll/y");
            yield return Item("Scroll X",            "<Mouse>/scroll/x");
            yield return Item("Back Button",         "<Mouse>/backButton");
            yield return Item("Forward Button",      "<Mouse>/forwardButton");

            // ── Modifier Keys ──
            yield return Header("Keyboard | Modifiers");
            yield return Item("Left Shift",          "<Keyboard>/leftShift");
            yield return Item("Right Shift",         "<Keyboard>/rightShift");
            yield return Item("Left Ctrl",           "<Keyboard>/leftCtrl");
            yield return Item("Right Ctrl",          "<Keyboard>/rightCtrl");
            yield return Item("Left Alt",            "<Keyboard>/leftAlt");
            yield return Item("Right Alt",           "<Keyboard>/rightAlt");
            yield return Item("Left Command",        "<Keyboard>/leftCommand");
            yield return Item("Right Command",       "<Keyboard>/rightCommand");

            // ── Composite ──
            yield return Header("Composite (2D Vector)");
            yield return Item("WASD",                "<Keyboard>/wasd");
            yield return Item("IJKL",                "<Keyboard>/ijkl");
            yield return Item("Arrow Keys",          "<Keyboard>/arrowKeys");
        }

        // ══════════════════════════════════════════════════════
        //  PC — MODIFIER ONLY
        // ══════════════════════════════════════════════════════

        public static IEnumerable<TISBindingOption> PCModifier()
        {
            yield return Item("(none — không dùng modifier)", "");
            yield return Header("Shift");
            yield return Item("Left Shift",          "<Keyboard>/leftShift");
            yield return Item("Right Shift",         "<Keyboard>/rightShift");
            yield return Header("Ctrl");
            yield return Item("Left Ctrl",           "<Keyboard>/leftCtrl");
            yield return Item("Right Ctrl",          "<Keyboard>/rightCtrl");
            yield return Header("Alt");
            yield return Item("Left Alt",            "<Keyboard>/leftAlt");
            yield return Item("Right Alt",           "<Keyboard>/rightAlt");
            yield return Header("Command  (Mac)");
            yield return Item("Left Command",        "<Keyboard>/leftCommand");
            yield return Item("Right Command",       "<Keyboard>/rightCommand");
        }
        
        // ══════════════════════════════════════════════════════
        //  GAMEPAD — MODIFIER ONLY
        // ══════════════════════════════════════════════════════

        public static IEnumerable<TISBindingOption> GamepadModifier()
        {
            yield return Item("(none — không dùng modifier)", "");
            yield return Header("Face Buttons");
            yield return Item("South (A)  (A | Cross)",      "<Gamepad>/buttonSouth");
            yield return Item("North (Y)   (Y | Triangle)",  "<Gamepad>/buttonNorth");
            yield return Item("East (B)    (B | Circle)",    "<Gamepad>/buttonEast");
            yield return Item("West (X)    (X | Square)",    "<Gamepad>/buttonWest");
            yield return Header("Shoulder");
            yield return Item("Left Shoulder (LB|L1)",  "<Gamepad>/leftShoulder");
            yield return Item("Right Shoulder (RB|R1)", "<Gamepad>/rightShoulder");
            yield return Header("Trigger (Button)");
            yield return Item("Left Trigger Button",    "<Gamepad>/leftTriggerButton");
            yield return Item("Right Trigger Button",   "<Gamepad>/rightTriggerButton");
            yield return Header("Stick Press");
            yield return Item("Left Stick Press (L3)",  "<Gamepad>/leftStickPress");
            yield return Item("Right Stick Press (R3)", "<Gamepad>/rightStickPress");
            yield return Header("D-Pad");
            yield return Item("D-Pad Up",    "<Gamepad>/dpad/up");
            yield return Item("D-Pad Down",  "<Gamepad>/dpad/down");
            yield return Item("D-Pad Left",  "<Gamepad>/dpad/left");
            yield return Item("D-Pad Right", "<Gamepad>/dpad/right");
            yield return Header("System");
            yield return Item("Start",  "<Gamepad>/start");
            yield return Item("Select", "<Gamepad>/select");
        }

        // ══════════════════════════════════════════════════════
        //  GAMEPAD
        // ══════════════════════════════════════════════════════

        public static IEnumerable<TISBindingOption> Gamepad()
        {
            yield return Header("Face Buttons");
            yield return Item("South (A)  (A | Cross)",     "<Gamepad>/buttonSouth");
            yield return Item("North (Y)   (Y | Triangle)",  "<Gamepad>/buttonNorth");
            yield return Item("East (B)    (B | Circle)",    "<Gamepad>/buttonEast");
            yield return Item("West (X)    (X | Square)",    "<Gamepad>/buttonWest");

            yield return Header("Shoulder x Trigger");
            yield return Item("Left Shoulder (LB|L1)",   "<Gamepad>/leftShoulder");
            yield return Item("Right Shoulder (RB|R1)",  "<Gamepad>/rightShoulder");
            yield return Item("Left Trigger (LT|L2)",    "<Gamepad>/leftTrigger");
            yield return Item("Right Trigger (RT|R2)",   "<Gamepad>/rightTrigger");
            yield return Header("Trigger as Button");
            yield return Item("Left Trigger Button",  "<Gamepad>/leftTriggerButton");
            yield return Item("Right Trigger Button", "<Gamepad>/rightTriggerButton");

            yield return Header("Left Stick");
            yield return Item("Left Stick (Vector2)",  "<Gamepad>/leftStick");
            yield return Item("Left Stick Press (L3)", "<Gamepad>/leftStickPress");
            yield return Item("Left Stick Up",         "<Gamepad>/leftStick/up");
            yield return Item("Left Stick Down",       "<Gamepad>/leftStick/down");
            yield return Item("Left Stick Left",       "<Gamepad>/leftStick/left");
            yield return Item("Left Stick Right",      "<Gamepad>/leftStick/right");

            yield return Header("Right Stick");
            yield return Item("Right Stick (Vector2)", "<Gamepad>/rightStick");
            yield return Item("Right Stick Press (R3)","<Gamepad>/rightStickPress");
            yield return Item("Right Stick Up",        "<Gamepad>/rightStick/up");
            yield return Item("Right Stick Down",      "<Gamepad>/rightStick/down");
            yield return Item("Right Stick Left",      "<Gamepad>/rightStick/left");
            yield return Item("Right Stick Right",     "<Gamepad>/rightStick/right");

            yield return Header("D-Pad");
            yield return Item("D-Pad (Vector2)",  "<Gamepad>/dpad");
            yield return Item("D-Pad Up",         "<Gamepad>/dpad/up");
            yield return Item("D-Pad Down",        "<Gamepad>/dpad/down");
            yield return Item("D-Pad Left",        "<Gamepad>/dpad/left");
            yield return Item("D-Pad Right",       "<Gamepad>/dpad/right");

            yield return Header("System");
            yield return Item("Start",  "<Gamepad>/start");
            yield return Item("Select", "<Gamepad>/select");
        }

        // ══════════════════════════════════════════════════════
        //  MOBILE — TISVirtualControlID
        // ══════════════════════════════════════════════════════

        // P3 FIX #19: Moved to TISConstants.MaxMobileButtons
        public const int MaxMobileButtons = TISConstants.MaxMobileButtons;

        public static IEnumerable<TISBindingOption> Mobile()
        {
            yield return Item("(none)",                          "");
            yield return Header("Axes  — trả về Vector2");
            yield return Item("Virtual Joystick",  TISVirtualControlID.TISVirtualJoystick);
            yield return Item("Camera Zone",       TISVirtualControlID.CameraZone);
            yield return Header($"Buttons  — trả về bool");
            for (int i = 0; i < MaxMobileButtons; i++)
                yield return Item($"Button {i,-2} (Button_{i})", TISVirtualControlID.ButtonByIndex(i));
        }

        // ══════════════════════════════════════════════════════
        //  TYPE INFERENCE
        // ══════════════════════════════════════════════════════

        public static TISInputActionType InferType(string path)
        {
            if (string.IsNullOrEmpty(path)) return TISInputActionType.Button;

            if (path.EndsWith("/leftStick",  System.StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith("/rightStick", System.StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith("/dpad",       System.StringComparison.OrdinalIgnoreCase) ||
                path.IndexOf("/wasd",        System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("/ijkl",        System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("/arrowKeys",   System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("<Mouse>/delta",System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                (path.IndexOf("/scroll",      System.StringComparison.OrdinalIgnoreCase) >= 0 && !path.EndsWith("/x") && !path.EndsWith("/y")) ||
                path == TISVirtualControlID.TISVirtualJoystick ||
                path == TISVirtualControlID.CameraZone)
                return TISInputActionType.Vector2;

            if (path.IndexOf("TriggerButton", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return TISInputActionType.Button;

            if (path.IndexOf("Trigger",     System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.EndsWith("/scroll/x",  System.StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith("/scroll/y",  System.StringComparison.OrdinalIgnoreCase))
                return TISInputActionType.Axis;

            return TISInputActionType.Button;
        }

        // ══════════════════════════════════════════════════════
        //  PRIVATE HELPERS
        // ══════════════════════════════════════════════════════

        private static TISBindingOption Item(string name, string path)
        {
            string label = string.IsNullOrEmpty(path)
                ? name
                : $"{name.PadRight(LabelCol)}  {path}";
            return new TISBindingOption(label, path);
        }

        private static TISBindingOption Header(string label)
            => new TISBindingOption($"── {label} ──", "");
    }
}
#endif