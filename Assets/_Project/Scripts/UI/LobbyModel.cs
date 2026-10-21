using Musketeers.Data;
using Musketeers.Game;
using UnityEngine;

namespace Musketeers.UI
{
    // The offline lobby's state: you in slot 0 plus bots, your character, the map, everyone's outfit.
    // Your choices persist in PlayerPrefs (outfit per character); bots wear nothing (FIXES_v0.2 §1.3).
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
            if (!IsHost)
                MyReady = !MyReady;
        }

        // Slot 0 is you: a host counts as ready; bots are always ready.
        public bool IsReady(int slot)
        {
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
        }

        // The wardrobe's Default button (FIXES_4E §3.3): Hat, Face and Cape to none. Boots are not selectable.
        public void ClearMyLoadout() => SetMyLoadout(default);

        // Copies the lobby's rules into a match setup (hearts per round, points to win).
        public void ApplyTo(MatchSetup setup)
        {
            setup.Config.PointsToWin = PointsToWin;
            setup.Config.MaxHp = Hearts;
        }

        // Slot 0 is you; bots take the other characters in roster order (§4: characters are unique).
        public int[] Characters()
        {
            var chars = new int[Players];
            chars[0] = Character;
            for (int i = 1, c = 0; i < Players; c++)
            {
                if (c != Character)
                    chars[i++] = c;
            }
            return chars;
        }

        public Loadout LoadoutFor(int slot) => slot == 0 ? MyLoadout : _bots[slot];

        public Loadout[] Loadouts()
        {
            var l = new Loadout[Players];
            for (int i = 0; i < Players; i++)
                l[i] = LoadoutFor(i);
            return l;
        }

        public Loadout MyLoadout
        {
            get
            {
                var def = _roster != null ? _roster.Get(Character) : null;
                var fallback = def != null ? def.DefaultLoadout : default;
                var l = Loadout.Parse(PlayerPrefs.GetString(LoadoutKey + Id(Character), ""), fallback);
                return _catalog != null ? _catalog.Sanitize(l) : l;
            }
        }

        public void Equip(CosmeticSlot slot, string id) => SetMyLoadout(MyLoadout.With(slot, id));

        public void SetMyLoadout(Loadout l)
        {
            PlayerPrefs.SetString(LoadoutKey + Id(Character), l.Serialize());
            PlayerPrefs.Save();
        }

        public void RandomizeMine()
        {
            if (_catalog != null)
                SetMyLoadout(_catalog.RandomLoadout());
        }

        // Bots never wear cosmetics (FIXES_v0.2 §1.3): bare head, their own boots.
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
            PlayerPrefs.SetInt(PlayersKey, Players);
            PlayerPrefs.SetInt(MapKey, Map);
            PlayerPrefs.SetInt(PointsKey, PointsToWin);
            PlayerPrefs.SetInt(HeartsKey, Hearts);
            PlayerPrefs.Save();
        }
    }
}
