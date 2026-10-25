// [Aemy 2026-10-11] FILE MOI. Ten ngan cua nut tay cam de hien tren so do controller (Settings > Phim bam).
using System.Collections.Generic;
using GaMo.TrueInputSystem;

namespace Game.UI
{
    /// <summary>
    /// Doi Input System path cua tay cam -> ten ngan kieu Xbox ("&lt;Gamepad&gt;/buttonSouth" -> "A").
    /// Chi lo hien thi (SRP); khong dung font ky tu dac biet (mui ten) de tranh thieu glyph trong TMP font.
    /// </summary>
    public static class GamepadGlyphNames
    {
        private static readonly Dictionary<string, string> s_Names = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "buttonSouth", "A" },      { "buttonEast", "B" },
            { "buttonWest", "X" },       { "buttonNorth", "Y" },
            { "leftShoulder", "LB" },    { "rightShoulder", "RB" },
            { "leftTrigger", "LT" },     { "rightTrigger", "RT" },
            { "leftStickPress", "L3" },  { "rightStickPress", "R3" },
            { "start", "Start" },        { "select", "Select" },
            { "dpad/up", "D-Up" },       { "dpad/down", "D-Down" },
            { "dpad/left", "D-Left" },   { "dpad/right", "D-Right" },
            { "dpad", "D-Pad" },
            { "leftStick", "L" },        { "rightStick", "R" },
        };

        /// <summary>"&lt;Gamepad&gt;/dpad/up" | "/XInputControllerWindows/dpad/up" -> "D-Up". Rong -> "—".</summary>
        public static string Short(string path)
        {
            if (string.IsNullOrEmpty(path)) return "—";
            string rel = StripDevice(path);
            string name;
            if (s_Names.TryGetValue(rel, out name)) return name;
            return TISRebindingSystem.GetShortDisplayName(path);
        }

        // Bo phan thiet bi dau path: "<Gamepad>/x" hoac "/DeviceName/x" -> "x".
        private static string StripDevice(string path)
        {
            int start;
            if (path.StartsWith("<")) { start = path.IndexOf('>'); start = start < 0 ? 0 : start + 1; }
            else if (path.StartsWith("/")) { start = path.IndexOf('/', 1); if (start < 0) start = 0; }
            else start = 0;
            if (start < path.Length && path[start] == '/') start++;
            return path.Substring(start);
        }
    }
}
