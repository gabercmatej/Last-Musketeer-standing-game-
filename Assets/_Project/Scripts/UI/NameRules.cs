using System.Text;

namespace Musketeers.UI
{
    // Player-name rules (FIXES_v0.2 §2.1): 1-20 characters, any Unicode letter (č š ž ...), digits, spaces and . ' -.
    // Pure C#, so it is unit-tested without the engine.
    public static class NameRules
    {
        public const int MaxLength = 20;

        // Trims and collapses runs of spaces to one.
        public static string Clean(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return "";
            var sb = new StringBuilder(raw.Length);
            bool lastSpace = true;   // true at the start also trims leading spaces
            foreach (char c in raw)
            {
                if (c == ' ')
                {
                    if (!lastSpace)
                        sb.Append(' ');
                    lastSpace = true;
                }
                else
                {
                    sb.Append(c);
                    lastSpace = false;
                }
            }
            if (sb.Length > 0 && sb[sb.Length - 1] == ' ')
                sb.Length--;
            return sb.ToString();
        }

        public static bool IsValid(string cleaned, out string hint)
        {
            hint = "";
            if (string.IsNullOrEmpty(cleaned))
            {
                hint = "Type a name first.";
                return false;
            }
            if (cleaned.Length > MaxLength)
            {
                hint = "20 characters at most.";
                return false;
            }
            foreach (char c in cleaned)
            {
                bool ok = char.IsLetter(c) || char.IsDigit(c) || c == ' ' || c == '.' || c == '\'' || c == '-';
                if (!ok)
                {
                    hint = "Letters, numbers, spaces and . ' - only.";
                    return false;
                }
            }
            return true;
        }
    }
}
