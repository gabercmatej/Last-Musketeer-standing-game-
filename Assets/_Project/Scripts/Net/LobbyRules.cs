using System.Collections.Generic;
using System.Text;

namespace Musketeers.Net
{
    // One lobby seat as plain data (no NGO types): a human or a bot, its musketeer, ready flag and outfit.
    public struct LobbyEntry
    {
        public ulong ClientId;   // meaningless for bots
        public string Name;      // "" for bots
        public int Character;
        public bool Ready;
        public bool IsBot;
        public string Loadout;   // Loadout.Serialize() text, "" = nothing
    }

    // The match rules a lobby can set (FIXES_v0.2 §3.2 rule 8, FIXES_4E §3.2).
    public struct LobbySettingsData
    {
        public int Map;
        public int PointsToWin;
        public int Hearts;
    }

    public enum JoinResult { Ok, Full, MatchInProgress, AlreadyJoined }

    // The pure lobby rules: seats, unique musketeers, ready flags, bots, settings, name hygiene.
    // The host owns one LobbyState and LobbyNet mirrors it into NetworkList/NetworkVariable; nothing here
    // knows about NGO, so it is EditMode-tested directly (docs/plans/010 A8).
    public static class LobbyRules
    {
        public const int MaxPlayers = 4;                       // bots count (GAME_DESIGN §3)
        public const int MinPointsToWin = 6, MaxPointsToWin = 30, DefaultPointsToWin = 12;
        public const int MinHearts = 1, MaxHearts = 5, DefaultHearts = 3;
        public const int MaxNameLength = 20;
        public const int MaxNameBytes = 63;                    // FixedString64Bytes holds 61 bytes + length, keep headroom
        public const int MaxLoadoutBytes = 120;                // FixedString128Bytes
        public const string FallbackName = "Player";

        public static int ClampPoints(int v) => v < MinPointsToWin ? MinPointsToWin : v > MaxPointsToWin ? MaxPointsToWin : v;
        public static int ClampHearts(int v) => v < MinHearts ? MinHearts : v > MaxHearts ? MaxHearts : v;

        public static LobbySettingsData DefaultSettings(int map = 0) =>
            new LobbySettingsData { Map = map, PointsToWin = DefaultPointsToWin, Hearts = DefaultHearts };

        // Strict check used by the host on incoming settings: out-of-range is rejected, not clamped.
        public static bool IsValidSettings(LobbySettingsData s, int mapCount) =>
            s.Map >= 0 && s.Map < mapCount &&
            s.PointsToWin >= MinPointsToWin && s.PointsToWin <= MaxPointsToWin &&
            s.Hearts >= MinHearts && s.Hearts <= MaxHearts;

        // Same character set as the menu's NameRules: letters of any script (č š ž ć đ ...), digits, space . ' -.
        // Collapses spaces, drops everything else, caps at 20 characters AND 63 UTF-8 bytes. May return "".
        public static string SanitizeName(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return "";
            var sb = new StringBuilder(raw.Length);
            bool lastSpace = true;
            foreach (char c in raw)
            {
                if (c == ' ')
                {
                    if (!lastSpace)
                        sb.Append(' ');
                    lastSpace = true;
                    continue;
                }
                bool ok = char.IsLetter(c) || char.IsDigit(c) || c == '.' || c == '\'' || c == '-';
                if (!ok)
                    continue;
                if (sb.Length >= MaxNameLength || Encoding.UTF8.GetByteCount(sb.ToString()) + Encoding.UTF8.GetByteCount(c.ToString()) > MaxNameBytes)
                    break;
                sb.Append(c);
                lastSpace = false;
            }
            while (sb.Length > 0 && sb[sb.Length - 1] == ' ')
                sb.Length--;
            return sb.ToString();
        }

        // Outfit text must fit the wire field; anything longer is ignored by the host.
        public static bool IsValidLoadout(string text) =>
            text != null && Encoding.UTF8.GetByteCount(text) <= MaxLoadoutBytes;
    }

    // The host's authoritative lobby: up to four seats (humans + bots), each with a unique musketeer.
    public sealed class LobbyState
    {
        readonly int _characterCount;
        readonly ulong _hostId;
        readonly List<LobbyEntry> _entries = new List<LobbyEntry>(LobbyRules.MaxPlayers);

        public LobbySettingsData Settings;
        public bool MatchInProgress;

        public IReadOnlyList<LobbyEntry> Entries => _entries;
        public int Count => _entries.Count;
        public int MapCount { get; }

        public LobbyState(int characterCount, int mapCount, ulong hostClientId = 0)
        {
            _characterCount = characterCount < 1 ? 1 : characterCount;
            MapCount = mapCount < 1 ? 1 : mapCount;
            _hostId = hostClientId;
            Settings = LobbyRules.DefaultSettings();
        }

        public int IndexOf(ulong clientId)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (!_entries[i].IsBot && _entries[i].ClientId == clientId)
                    return i;
            }
            return -1;
        }

        public int HumanCount
        {
            get
            {
                int n = 0;
                foreach (var e in _entries)
                {
                    if (!e.IsBot)
                        n++;
                }
                return n;
            }
        }

        // Who holds this musketeer (-1 = free). `except` skips one seat (the asker's own).
        public int HolderOf(int character, int except = -1)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (i != except && _entries[i].Character == character)
                    return i;
            }
            return -1;
        }

        int FirstFreeCharacter()
        {
            for (int c = 0; c < _characterCount; c++)
            {
                if (HolderOf(c) < 0)
                    return c;
            }
            return -1;
        }

        // A human arrives. They get the first free musketeer; the picker lets them swap afterwards.
        public JoinResult TryAddHuman(ulong clientId, string name, out int index)
        {
            index = IndexOf(clientId);
            if (index >= 0)
                return JoinResult.AlreadyJoined;
            if (MatchInProgress)
                return JoinResult.MatchInProgress;
            int character = _entries.Count >= LobbyRules.MaxPlayers ? -1 : FirstFreeCharacter();
            if (character < 0)
                return JoinResult.Full;
            string clean = LobbyRules.SanitizeName(name);
            _entries.Add(new LobbyEntry
            {
                ClientId = clientId,
                Name = clean.Length > 0 ? clean : LobbyRules.FallbackName,
                Character = character,
                Ready = clientId == _hostId,   // the host is always ready
                IsBot = false,
                Loadout = "",
            });
            index = _entries.Count - 1;
            return JoinResult.Ok;
        }

        // Unique musketeers: a taken one is refused (the picker greys it out, this is the host's safety net).
        public bool TryPickCharacter(ulong clientId, int character)
        {
            int i = IndexOf(clientId);
            if (i < 0 || character < 0 || character >= _characterCount || HolderOf(character, i) >= 0)
                return false;
            var e = _entries[i];
            e.Character = character;
            _entries[i] = e;
            return true;
        }

        public bool TrySetReady(ulong clientId, bool ready)
        {
            int i = IndexOf(clientId);
            if (i < 0)
                return false;
            var e = _entries[i];
            e.Ready = clientId == _hostId || ready;
            _entries[i] = e;
            return true;
        }

        public bool TrySetLoadout(ulong clientId, string loadout)
        {
            int i = IndexOf(clientId);
            if (i < 0 || !LobbyRules.IsValidLoadout(loadout))
                return false;
            var e = _entries[i];
            e.Loadout = loadout;
            _entries[i] = e;
            return true;
        }

        // Bots take the first free musketeer, never wear cosmetics and are always ready (FIXES_v0.2 §1.3).
        public bool TryAddBot()
        {
            if (MatchInProgress || _entries.Count >= LobbyRules.MaxPlayers)
                return false;
            int character = FirstFreeCharacter();
            if (character < 0)
                return false;
            _entries.Add(new LobbyEntry { Name = "", Character = character, Ready = true, IsBot = true, Loadout = "" });
            return true;
        }

        public bool TryRemoveBot()
        {
            if (MatchInProgress)
                return false;
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                if (_entries[i].IsBot)
                {
                    _entries.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public bool Remove(ulong clientId)
        {
            int i = IndexOf(clientId);
            if (i < 0)
                return false;
            _entries.RemoveAt(i);
            return true;
        }

        public bool TrySetSettings(LobbySettingsData s)
        {
            if (!LobbyRules.IsValidSettings(s, MapCount))
                return false;
            Settings = s;
            return true;
        }

        public void ResetRules()
        {
            Settings.PointsToWin = LobbyRules.DefaultPointsToWin;
            Settings.Hearts = LobbyRules.DefaultHearts;
        }

        public bool AllReady()
        {
            foreach (var e in _entries)
            {
                if (!e.Ready)
                    return false;
            }
            return true;
        }

        // A match needs at least two seats.
        public bool CanStart => _entries.Count >= 2;
    }
}
