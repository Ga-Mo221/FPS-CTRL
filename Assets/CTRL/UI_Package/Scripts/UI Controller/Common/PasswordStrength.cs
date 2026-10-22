using System.Text.RegularExpressions;

namespace Game.UI
{
    /// <summary>
    /// Client-side password-strength estimate for the register meter. Pure function,
    /// no dependencies. <see cref="Score"/> returns 0 for empty, otherwise 1..4 — which
    /// maps directly to how many of the four meter bars light up. Kept deliberately
    /// simple and transparent; it's UX guidance, not a security control (PlayFab
    /// enforces its own password rules on register).
    /// </summary>
    public static class PasswordStrength
    {
        public static int Score(string password)
        {
            if (string.IsNullOrEmpty(password)) return 0;

            int s = 0;
            if (password.Length >= 8) s++;
            bool hasLower = Regex.IsMatch(password, "[a-z]");
            bool hasUpper = Regex.IsMatch(password, "[A-Z]");
            bool hasDigit = Regex.IsMatch(password, "[0-9]");
            bool hasSymbol = Regex.IsMatch(password, "[^a-zA-Z0-9]");
            if (hasLower && hasUpper) s++;
            if (hasDigit) s++;
            if (hasSymbol) s++;
            if (password.Length >= 12 && s < 4) s++;      // reward real length
            if (password.Length < 6 && s > 1) s = 1;       // too short can't be "strong"

            return s < 1 ? 1 : (s > 4 ? 4 : s);
        }

        public static string Label(int score)
        {
            switch (score)
            {
                case 1: return "YẾU";
                case 2: return "TRUNG BÌNH";
                case 3: return "KHÁ";
                case 4: return "MẠNH";
                default: return "";
            }
        }
    }
}
