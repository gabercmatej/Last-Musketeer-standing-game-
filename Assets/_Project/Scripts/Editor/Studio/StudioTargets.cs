using Musketeers.Data;
using UnityEngine;

namespace Musketeers.EditorTools.Studio
{
    public struct StudioTarget
    {
        public bool Valid;
        public IFitTarget Fit;
        public UnityEngine.Object Owner;   // the asset to Undo / SetDirty
        public AnchorId Anchor;            // what the item hangs on
        public string Name;
        public bool IsWeapon;
    }

    // Selection -> the fit set to edit, the asset that owns it, and the anchor it hangs on.
    public static class StudioTargets
    {
        public static bool ActiveFor(StudioState s, bool musket) =>
            musket ? s.Weapon == StudioWeapon.MusketAimed : s.Weapon == StudioWeapon.RapierRaised;

        public static StudioTarget Resolve(StudioState s, bool backFoot = false) => Resolve(s, s.Selection, backFoot);

        public static StudioTarget Resolve(StudioState s, StudioSelection sel, bool backFoot = false)
        {
            if (sel.Kind == SelectionKind.Cosmetic)
            {
                var item = s.Catalog.Get(sel.ItemId);
                if (item == null)
                    return default;
                return new StudioTarget { Valid = true, Fit = item, Owner = item, Anchor = CosmeticLayout.AnchorFor(item.Slot, backFoot), Name = item.DisplayName };
            }
            if (sel.Kind == SelectionKind.Weapon)
            {
                bool musket = sel.IsMusket;
                var def = musket ? s.Catalog.Musket : s.Catalog.Rapier;
                if (def == null)
                    return default;
                bool active = ActiveFor(s, musket);
                return new StudioTarget
                {
                    Valid = true, IsWeapon = true, Fit = def.For(active), Owner = def,
                    Anchor = musket && !active ? AnchorId.HandBack : AnchorId.HandFront,
                    Name = (musket ? "Musket" : "Rapier") + (active ? (musket ? " (aimed)" : " (raised)") : " (rest)"),
                };
            }
            return default;
        }

        // The far boot on a front-facing pose is drawn mirrored (CosmeticLayout.Place).
        public static bool Mirrored(CharacterDef c, PoseKind pose, bool backFoot) =>
            backFoot && c != null && c.Pose(pose).Front;
    }
}
