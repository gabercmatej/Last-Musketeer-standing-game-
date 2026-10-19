using Musketeers.Data;
using UnityEngine;

namespace Musketeers.UI
{
    // The offline lobby's state: you in slot 0 plus bots, your character, the map, everyone's outfit.
    // Your choices persist in PlayerPrefs (outfit per character); bots get a random outfit when they join.
    public sealed class LobbyModel
    {
        const string CharacterKey = "Musketeers.Character";
        const string PlayersKey = "Musketeers.Players";
        const string MapKey = "Musketeers.Map";
        const string LoadoutKey = "Musketeers.Loadout.";

        readonly CharacterRoster _roster;
        readonly CosmeticCatalog _catalog;
        readonly Loadout[] _bots = new Loadout[4];

        public string Name = "";
        public int Character;
        public int Players;
        public int Map;
        public int MapCount;

        public int RosterCount => _roster != null ? _roster.Count : 4;

        public LobbyModel(CharacterRoster roster, CosmeticCatalog catalog, int mapCount)
        {
            _roster = roster;
            _catalog = catalog;
            MapCount = Mathf.Max(1, mapCount);
            Character = Mathf.Clamp(PlayerPrefs.GetInt(CharacterKey, 0), 0, RosterCount - 1);
            Players = Mathf.Clamp(PlayerPrefs.GetInt(PlayersKey, 4), 2, Mathf.Min(4, RosterCount));
            Map = Mathf.Clamp(PlayerPrefs.GetInt(MapKey, 0), 0, MapCount - 1);
            for (int i = 1; i < 4; i++)
                RerollBot(i);
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

        public void RerollBot(int slot)
        {
            if (slot > 0 && slot < 4)
                _bots[slot] = _catalog != null ? _catalog.RandomLoadout() : default;
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
            PlayerPrefs.Save();
        }
    }
}
