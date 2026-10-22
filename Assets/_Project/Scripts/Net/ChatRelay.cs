using System.Collections.Generic;
using System.Text;
using Musketeers.Game;

namespace Musketeers.Net
{
    // Host-side chat rules (docs/plans/010 B5): text is cleaned and capped at 80 characters, and each sender may post
    // at most one message per second (the excess is dropped). Pure, so it is EditMode-tested.
    public sealed class ChatRelay
    {
        public const float MinInterval = 1f;
        public const int EmoteCount = 4;

        readonly Dictionary<ulong, float> _last = new Dictionary<ulong, float>();

        // text null/empty is fine for an emote (emote >= 0). Returns false when the message is dropped.
        public bool Accept(ulong sender, string text, int emote, float now, out string clean)
        {
            clean = "";
            if (emote >= EmoteCount)
                return false;
            if (emote < 0)
            {
                clean = Clean(text);
                if (clean.Length == 0)
                    return false;
            }
            if (_last.TryGetValue(sender, out float last) && now - last < MinInterval)
                return false;
            _last[sender] = now;
            return true;
        }

        public void Reset() => _last.Clear();

        // No control characters, trimmed, at most ChatModel.MaxLength characters.
        public static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (!char.IsControl(c))
                    sb.Append(c);
            }
            string s = sb.ToString().Trim();
            if (s.Length > ChatModel.MaxLength)
                s = s.Substring(0, ChatModel.MaxLength).TrimEnd();
            return s;
        }
    }
}
