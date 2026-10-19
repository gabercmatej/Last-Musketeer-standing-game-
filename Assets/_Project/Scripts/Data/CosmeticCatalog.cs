using System.Collections.Generic;
using UnityEngine;

namespace Musketeers.Data
{
    // Every wardrobe item, in display order. Built by Tools/Musketeers/Import Art. Items with
    // EnabledInV1 off stay here (the Wardrobe Studio still shows them) but never reach a player.
    [CreateAssetMenu(menuName = "Musketeers/Cosmetic Catalog", fileName = "CosmeticCatalog")]
    public sealed class CosmeticCatalog : ScriptableObject
    {
        public List<CosmeticDef> Items = new List<CosmeticDef>();
        public WeaponDef Musket;   // the weapons live here so the runtime rig and the editor find the same assets
        public WeaponDef Rapier;

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

        // `enabledOnly`: what the wardrobe offers (FIXES_v0.2 §1.2 fallback for items that don't fit yet).
        public List<CosmeticDef> BySlot(CosmeticSlot slot, bool enabledOnly = false)
        {
            var list = new List<CosmeticDef>();
            foreach (var c in Items)
            {
                if (c != null && c.Slot == slot && (!enabledOnly || c.EnabledInV1))
                    list.Add(c);
            }
            return list;
        }

        // Drops ids that no longer exist or were disabled, so a stale PlayerPrefs value never breaks the rig.
        public Loadout Sanitize(Loadout l)
        {
            foreach (var slot in Loadout.Slots)
            {
                var item = Get(l.Get(slot));
                if (item == null || item.Slot != slot || !item.EnabledInV1)
                    l = l.With(slot, "");
            }
            return l;
        }

        // A random outfit from the enabled items (the wardrobe's dice and the Wardrobe Studio's outfit test):
        // always a hat and boots, a cape or face item about half the time. Cosmetic only, so Unity's RNG.
        public Loadout RandomLoadout(bool enabledOnly = true)
        {
            var l = new Loadout();
            foreach (var slot in Loadout.Slots)
            {
                var items = BySlot(slot, enabledOnly);
                bool optional = slot == CosmeticSlot.Face || slot == CosmeticSlot.Cape;
                if (items.Count == 0 || (optional && Random.value < 0.5f))
                    continue;
                l = l.With(slot, items[Random.Range(0, items.Count)].Id);
            }
            return l;
        }
    }
}
