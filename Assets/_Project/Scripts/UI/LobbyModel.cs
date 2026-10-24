using Musketeers.Data;
using System.Collections.Generic;
using Musketeers.Game;
using Musketeers.Net;
using UnityEngine;

namespace Musketeers.UI
{
    // The offline lobby's state: you in slot 0 plus bots, your character, the map, everyone's outfit.
    // Your choices persist in PlayerPrefs (outfit per character); bots wear nothing (FIXES_v0.2 §1.3).
    // Online play (Day 5): the lobby's actions go to the host through this instead of changing the model directly.
    // The state comes back as a LobbyNet snapshot (LobbyModel.ApplyOnline). Null offline.
    public interface ILobbyBackend
    {
        void Pick(int character);
        void SetReady(bool ready);
        void SetName(string name);
        void SetLoadout(string serialized);
        void SetSettings(int map, int pointsToWin, int hearts);
        void DefaultSettings();
        void AddBot();
        void RemoveBot();
    }

    public sealed class LobbyModel
    {
        const string CharacterKey = "Musketeers.Character";
        const string PlayersKey = "Musketeers.Players";
        const string MapKey = "Musketeers.Map";
        const string LoadoutKey = "Musketeers.Loadout.";
        public const string PointsKey = "Musketeers.PointsToWin";
        public const string HeartsKey = "Musketeers.Hearts";

        // FIXES_v0.2 §3.2 rule 8: the match rules a lobby can set.
        public const int MinPointsToWin = 6, MaxPointsToWin = 30, DefaultPointsToWin = 12;   // FIXES_4E §3.2: 6-30
        public const int MinHearts = 1, MaxHearts = 5, DefaultHearts = 3;

        readonly CharacterRoster _roster;
        readonly CosmeticCatalog _catalog;
        readonly Loadout[] _bots = new Loadout[4];
        readonly string[] _takenBy = new string[4];   // Day 5: who holds each musketeer; offline everyone is free
        readonly bool[] _botNotReady = new bool[4];   // dev/screenshots only: bots are always ready otherwise

        List<LobbyEntry> _seats;                       // online: the seats, me first (null offline)
        public ILobbyBackend Backend { get; private set; }
        public bool Online => Backend != null;

        public string Name = "";
        public int Character;
        public int Players;
        public int Map;
        public int MapCount;

        int _pointsToWin = DefaultPointsToWin;
        int _hearts = DefaultHearts;
        public int PointsToWin { get => _pointsToWin; set => _pointsToWin = Mathf.Clamp(value, MinPointsToWin, MaxPointsToWin); }
        public int Hearts { get => _hearts; set => _hearts = Mathf.Clamp(value, MinHearts, MaxHearts); }

        // Day-5 lobby state, local for now (FIXES_v0.2 §2.4): offline you are the host, everyone is ready.
        // A non-host (dev flag, later a joined player) has a Ready toggle and starts Not ready.
        public bool IsHost { get; private set; } = true;
        public bool MyReady { get; private set; } = true;

        public void SetHost(bool host)
        {
            IsHost = host;
            MyReady = host;
        }

        public void ToggleReady()
        {
            if (IsHost)
                return;
            MyReady = !MyReady;
            Backend?.SetReady(MyReady);
        }

        // Slot 0 is you: a host counts as ready; bots are always ready. Online: the host's flags from the snapshot.
        public bool IsReady(int slot)
        {
            if (_seats != null)
                return slot >= 0 && slot < _seats.Count && _seats[slot].Ready;
            if (slot == 0)
                return IsHost || MyReady;
            return slot < 0 || slot >= _botNotReady.Length || !_botNotReady[slot];
        }

        public bool[] Ready()
        {
            var r = new bool[Players];
            for (int i = 0; i < r.Length; i++)
                r[i] = IsReady(i);
            return r;
        }

        public bool WaitingForResults
        {
            get { if (_seats != null) foreach (var seat in _seats) if (seat.InResults) return true; return false; }
        }
        public bool AllReady()
        {
            for (int i = 0; i < Players; i++)
            {
                if (!IsReady(i))
                    return false;
            }
            return true;
        }

        // Dev only (screenshot of the "Start anyway?" dialog).
        public void DevSetBotReady(int slot, bool ready)
        {
            if (slot > 0 && slot < _botNotReady.Length)
                _botNotReady[slot] = !ready;
        }

        public int RosterCount => _roster != null ? _roster.Count : 4;

        public LobbyModel(CharacterRoster roster, CosmeticCatalog catalog, int mapCount)
        {
            _roster = roster;
            _catalog = catalog;
            MapCount = Mathf.Max(1, mapCount);
            Character = Mathf.Clamp(PlayerPrefs.GetInt(CharacterKey, 0), 0, RosterCount - 1);
            Players = Mathf.Clamp(PlayerPrefs.GetInt(PlayersKey, 4), 2, Mathf.Min(4, RosterCount));
            Map = Mathf.Clamp(PlayerPrefs.GetInt(MapKey, 0), 0, MapCount - 1);
            PointsToWin = PlayerPrefs.GetInt(PointsKey, DefaultPointsToWin);
            Hearts = PlayerPrefs.GetInt(HeartsKey, DefaultHearts);
            for (int i = 1; i < 4; i++)
                RerollBot(i);
        }

        // ---------- online (Day 5) ----------

        // Switches the model to online mode: actions go to `backend`, state comes from ApplyOnline.
        public void EnterOnline(ILobbyBackend backend)
        {
            Backend = backend;
            _seats = new List<LobbyEntry>();
        }

        // Back to the offline lobby: your saved choices return, nobody holds a musketeer, you host again.
        public void ExitOnline()
        {
            Backend = null;
            _seats = null;
            for (int i = 0; i < _takenBy.Length; i++)
                _takenBy[i] = null;
            SetHost(true);
            Character = Mathf.Clamp(PlayerPrefs.GetInt(CharacterKey, 0), 0, RosterCount - 1);
            Players = Mathf.Clamp(PlayerPrefs.GetInt(PlayersKey, 4), 2, Mathf.Min(4, RosterCount));
            Map = Mathf.Clamp(PlayerPrefs.GetInt(MapKey, 0), 0, MapCount - 1);
            PointsToWin = PlayerPrefs.GetInt(PointsKey, DefaultPointsToWin);
            Hearts = PlayerPrefs.GetInt(HeartsKey, DefaultHearts);
        }

        // Loads a LobbyNet snapshot. Seats are shown with you first and the others in host order, so slot 0 stays "me"
        // for the stage and wardrobe. Returns false if your own seat is not in it (yet).
        public bool ApplyOnline(IReadOnlyList<LobbyEntry> entries, ulong myClientId, LobbySettingsData settings, bool isHost)
        {
            if (!Online)
                return false;
            int me = -1;
            for (int i = 0; i < entries.Count; i++)
            {
                if (!entries[i].IsBot && entries[i].ClientId == myClientId)
                    me = i;
            }
            if (me < 0)
                return false;
            _seats.Clear();
            _seats.Add(entries[me]);
            for (int i = 0; i < entries.Count; i++)
            {
                if (i != me)
                    _seats.Add(entries[i]);
            }
            Players = _seats.Count;
            Name = _seats[0].Name;
            Character = Mathf.Clamp(_seats[0].Character, 0, Mathf.Max(0, RosterCount - 1));
            Map = Mathf.Clamp(settings.Map, 0, MapCount - 1);
            PointsToWin = settings.PointsToWin;
            Hearts = settings.Hearts;
            IsHost = isHost;
            MyReady = isHost || _seats[0].Ready;
            for (int i = 0; i < _takenBy.Length; i++)
                _takenBy[i] = null;
            for (int i = 1; i < _seats.Count; i++)
            {
                int c = _seats[i].Character;
                if (c >= 0 && c < _takenBy.Length)
                    _takenBy[c] = _seats[i].IsBot ? "Bot" : _seats[i].Name;
            }
            return true;
        }

        public int BotCount
        {
            get
            {
                int n = _seats == null ? Mathf.Max(0, Players - 1) : 0;
                if (_seats != null)
                {
                    foreach (var s in _seats)
                    {
                        if (s.IsBot)
                            n++;
                    }
                }
                return n;
            }
        }

        public int HumanCount => Online ? Players - BotCount : 1;
        public int MaxBots => Mathf.Max(0, Mathf.Min(4, RosterCount) - HumanCount);
        public int MinBots => Online ? 0 : 1;

        public bool Rename(string raw)
        {
            string clean = NameRules.Clean(raw);
            if (!NameRules.IsValid(clean, out _))
                return false;
            PlayerPrefs.SetString("Musketeers.PlayerName", clean);
            PlayerPrefs.Save();
            if (Online)
                Backend.SetName(clean);
            else
                Name = clean;
            return true;
        }

        // Stage name tags: online each seat's own name ("Bot" for bots); offline null (AppFlow builds them).
        public string[] SeatNames()
        {
            if (_seats == null)
                return null;
            var names = new string[_seats.Count];
            for (int i = 0; i < names.Length; i++)
                names[i] = _seats[i].IsBot ? "Bot" : _seats[i].Name;
            return names;
        }

        // Who else holds this musketeer (null = free). Always null offline; Day 5 / dev sets it.
        public string TakenBy(int character) => character >= 0 && character < _takenBy.Length ? _takenBy[character] : null;

        public void SetTaken(int character, string name)
        {
            if (character >= 0 && character < _takenBy.Length)
                _takenBy[character] = string.IsNullOrEmpty(name) ? null : name;
        }

        public bool CanPick(int character) => character >= 0 && character < RosterCount && TakenBy(character) == null;

        // The lobby's Default button (FIXES_4E §3.2): Points 12, Hearts 3.
        public void ResetRules()
        {
            PointsToWin = DefaultPointsToWin;
            Hearts = DefaultHearts;
            Backend?.DefaultSettings();
        }

        // The host changed the map or a rule in the lobby: tell the network (a no-op offline).
        public void PushSettings() => Backend?.SetSettings(Map, PointsToWin, Hearts);

        // Online bots are added and removed on the host; the snapshot brings the result back.
        public void AddBot() => Backend?.AddBot();
        public void RemoveBot() => Backend?.RemoveBot();

        // The picker chose a musketeer: online the host decides, and your outfit for it follows.
        public void PickCharacter(int character)
        {
            if (Backend == null)
            {
                Character = character;
                return;
            }
            // Online the host may refuse (someone else took it first), so Character only changes with the snapshot.
            Backend.Pick(character);
            Backend.SetLoadout(LoadoutOfCharacter(character).Serialize());
        }

        // Explicit Default restores this character's feather and cape without altering saved outfits implicitly.
        public void ClearMyLoadout() => SetMyLoadout(_roster?.Get(Character)?.DefaultLoadout ?? default);

        // Copies the lobby's rules into a match setup (hearts per round, points to win).
        public void ApplyTo(MatchSetup setup)
        {
            setup.Config.PointsToWin = PointsToWin;
            setup.Config.MaxHp = Hearts;
        }

        // Slot 0 is you; bots take the other characters in roster order (§4: characters are unique).
        public int[] Characters()
        {
            if (_seats != null)
            {
                var online = new int[_seats.Count];
                for (int i = 0; i < online.Length; i++)
                    online[i] = _seats[i].Character;
                return online;
            }
            var chars = new int[Players];
            chars[0] = Character;
            for (int i = 1, c = 0; i < Players; c++)
            {
                if (c != Character)
                    chars[i++] = c;
            }
            return chars;
        }

        public Loadout LoadoutFor(int slot)
        {
            if (_seats != null)
            {
                if (slot == 0)
                    return MyLoadout;
                if (slot < 0 || slot >= _seats.Count)
                    return default;
                if (_seats[slot].IsBot)
                    return DefaultForCharacter(_seats[slot].Character);
                var l = Loadout.Parse(_seats[slot].Loadout, default);
                return _catalog != null ? _catalog.Sanitize(l) : l;
            }
            if (slot < 0 || slot >= Players) return default;
            return slot == 0 ? MyLoadout : DefaultForCharacter(Characters()[slot]);
        }

        Loadout DefaultForCharacter(int character)
        {
            var outfit = _roster != null ? _roster.Get(character)?.DefaultLoadout ?? default : default;
            return _catalog != null ? _catalog.Sanitize(outfit) : outfit;
        }

        public Loadout[] Loadouts()
        {
            var l = new Loadout[Players];
            for (int i = 0; i < Players; i++)
                l[i] = LoadoutFor(i);
            return l;
        }

        public Loadout MyLoadout => LoadoutOfCharacter(Character);

        // What you wear on a given musketeer (saved per character).
        public Loadout LoadoutOfCharacter(int character)
        {
            var def = _roster != null ? _roster.Get(character) : null;
            var fallback = def != null ? def.DefaultLoadout : default;
            var l = Loadout.Parse(PlayerPrefs.GetString(LoadoutKey + Id(character), ""), fallback);
            return _catalog != null ? _catalog.Sanitize(l) : l;
        }

        public void Equip(CosmeticSlot slot, string id) => SetMyLoadout(MyLoadout.With(slot, id));

        public void SetMyLoadout(Loadout l)
        {
            PlayerPrefs.SetString(LoadoutKey + Id(Character), l.Serialize());
            PlayerPrefs.Save();
            Backend?.SetLoadout(l.Serialize());
        }

        public void RandomizeMine()
        {
            if (_catalog != null)
                SetMyLoadout(_catalog.RandomLoadout());
        }

        // Bots use their character defaults at render time, never player preferences or randomized outfits.
        public void RerollBot(int slot)
        {
            if (slot > 0 && slot < 4)
                _bots[slot] = default;
        }

        string Id(int character)
        {
            var def = _roster != null ? _roster.Get(character) : null;
            return def != null && !string.IsNullOrEmpty(def.Id) ? def.Id : character.ToString();
        }

        public void Save()
        {
            PlayerPrefs.SetInt(CharacterKey, Character);
            if (!Online)
                PlayerPrefs.SetInt(PlayersKey, Players);   // online the count includes other people
            PlayerPrefs.SetInt(MapKey, Map);
            PlayerPrefs.SetInt(PointsKey, PointsToWin);
            PlayerPrefs.SetInt(HeartsKey, Hearts);
            PlayerPrefs.Save();
        }
    }
}
