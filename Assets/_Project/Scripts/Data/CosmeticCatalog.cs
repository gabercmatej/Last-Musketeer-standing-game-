using System.Collections.Generic;
using UnityEngine;

namespace Musketeers.Data
{
    // Every wardrobe item, in display order. Built by Tools/Musketeers/Import Art.
    [CreateAssetMenu(menuName = "Musketeers/Cosmetic Catalog", fileName = "CosmeticCatalog")]
    public sealed class CosmeticCatalog : ScriptableObject
    {
        public List<CosmeticDef> Items = new List<CosmeticDef>();

        public CosmeticDef Get(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            foreach (var c in Items)
            {
                if (c != null && c.Id == id)
                    return c;
            }
            return null;
        }

        public List<CosmeticDef> BySlot(CosmeticSlot slot)
        {
            var list = new List<CosmeticDef>();
            foreach (var c in Items)
            {
                if (c != null && c.Slot == slot)
                    list.Add(c);
            }
            return list;
        }

        // Drops ids that no longer exist, so a stale PlayerPrefs value never breaks the rig.
        public Loadout Sanitize(Loadout l)
        {
            foreach (var slot in Loadout.Slots)
            {
                var item = Get(l.Get(slot));
                if (item == null || item.Slot != slot)
                    l = l.With(slot, "");
            }
            return l;
        }

        // A random outfit for a bot: always a hat and boots, a cape or face item about half the time.
        // Cosmetic only, so it uses Unity's RNG (never the sim's).
        public Loadout RandomLoadout()
        {
            var l = new Loadout();
            foreach (var slot in Loadout.Slots)
            {
                var items = BySlot(slot);
                bool optional = slot == CosmeticSlot.Face || slot == CosmeticSlot.Cape;
                if (items.Count == 0 || (optional && Random.value < 0.5f))
                    continue;
                l = l.With(slot, items[Random.Range(0, items.Count)].Id);
            }
            return l;
        }
    }
}
