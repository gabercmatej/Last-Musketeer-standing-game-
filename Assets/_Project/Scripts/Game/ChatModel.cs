using System;
using System.Collections.Generic;

namespace Musketeers.Game
{
    // The in-match chat log (FIXES §2.7): pure data. Messages are trimmed, empty ones are ignored, text is capped at
    // 80 characters and only the last 6 are kept. Offline it is local only; Day 5 feeds it from the network.
    public sealed class ChatModel
    {
        public const int MaxLength = 80;
        public const int Keep = 6;

        public struct Message
        {
            public int Slot;
            public string Text;
            public float Time;     // seconds on whatever clock the caller uses (UI: unscaled time)
            public bool Emote;     // a "*laughs*" style line posted by an emote button
        }

        readonly List<Message> _messages = new List<Message>();

        public IReadOnlyList<Message> Messages => _messages;
        public event Action<Message> Added;

        // Returns true when a message was added.
        public bool Add(int slot, string text, float time, bool emote = false)
        {
            if (text == null)
                return false;
            text = text.Trim();
            if (text.Length == 0)
                return false;
            if (text.Length > MaxLength)
                text = text.Substring(0, MaxLength).TrimEnd();
            var m = new Message { Slot = slot, Text = text, Time = time, Emote = emote };
            _messages.Add(m);
            while (_messages.Count > Keep)
                _messages.RemoveAt(0);
            Added?.Invoke(m);
            return true;
        }

        public void Clear() => _messages.Clear();
    }
}
