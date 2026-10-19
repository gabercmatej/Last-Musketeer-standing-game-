using System;

namespace Musketeers.Data
{
    // What a player wears: one cosmetic id per slot ("" = nothing). Serialize() gives a short string
    // for PlayerPrefs now and LobbySlot on Day 5.
    [Serializable]
    public struct Loadout
    {
        public string Hat;
        public string Face;
        public string Cape;
        public string Boots;

        public string Get(CosmeticSlot slot)
        {
            switch (slot)
            {
                case CosmeticSlot.Hat: return Hat ?? "";
                case CosmeticSlot.Face: return Face ?? "";
                case CosmeticSlot.Cape: return Cape ?? "";
                default: return Boots ?? "";
            }
        }

        public Loadout With(CosmeticSlot slot, string id)
        {
            var l = this;
            id = id ?? "";
            switch (slot)
            {
                case CosmeticSlot.Hat: l.Hat = id; break;
                case CosmeticSlot.Face: l.Face = id; break;
                case CosmeticSlot.Cape: l.Cape = id; break;
                default: l.Boots = id; break;
            }
            return l;
        }

        public string Serialize() => $"{Hat ?? ""}|{Face ?? ""}|{Cape ?? ""}|{Boots ?? ""}";

        // Unknown or malformed text gives back `fallback`.
        public static Loadout Parse(string text, Loadout fallback)
        {
            if (string.IsNullOrEmpty(text))
                return fallback;
            var parts = text.Split('|');
            if (parts.Length != 4)
                return fallback;
            return new Loadout { Hat = parts[0], Face = parts[1], Cape = parts[2], Boots = parts[3] };
        }

        public static readonly CosmeticSlot[] Slots = { CosmeticSlot.Hat, CosmeticSlot.Face, CosmeticSlot.Cape, CosmeticSlot.Boots };
    }
}
