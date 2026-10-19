using System.Collections.Generic;
using Musketeers.Data;
using UnityEngine;

namespace Musketeers.EditorTools.Studio
{
    // The checklist: every enabled item x pose, plus each weapon state x pose.
    public static class StudioProgress
    {
        public struct Row
        {
            public IFitTarget Target;
            public UnityEngine.Object Owner;
            public string Label;
            public string ItemId;      // cosmetic id, or "musket" / "rapier"
            public CosmeticSlot Slot;
            public bool IsWeapon, Musket, Active;
            public PoseKind Pose;
            public bool Checked => FitEdit.IsChecked(Target, Pose);
        }

        public static List<Row> Build(CosmeticCatalog catalog)
        {
            var rows = new List<Row>();
            foreach (var item in catalog.Items)
            {
                if (item == null || !item.EnabledInV1)
                    continue;
                foreach (var p in FitEdit.StudioPoses)
                    rows.Add(new Row { Target = item, Owner = item, Label = item.DisplayName + " · " + p, ItemId = item.Id, Slot = item.Slot, Pose = p });
            }
            AddWeapon(rows, catalog.Musket, true);
            AddWeapon(rows, catalog.Rapier, false);
            return rows;
        }

        static void AddWeapon(List<Row> rows, WeaponDef w, bool musket)
        {
            if (w == null)
                return;
            foreach (bool active in new[] { false, true })
            {
                string state = active ? (musket ? "aimed" : "raised") : "rest";
                foreach (var p in FitEdit.StudioPoses)
                    rows.Add(new Row { Target = w.For(active), Owner = w, Label = (musket ? "Musket " : "Rapier ") + state + " · " + p, ItemId = musket ? "musket" : "rapier", IsWeapon = true, Musket = musket, Active = active, Pose = p });
            }
        }

        public static int CountChecked(List<Row> rows)
        {
            int n = 0;
            foreach (var r in rows)
            {
                if (r.Checked)
                    n++;
            }
            return n;
        }

        // Clicking a row opens that item, pose and weapon state.
        public static void Jump(StudioState s, Row r)
        {
            s.Pose = r.Pose;
            s.AnchorMode = false;
            if (r.IsWeapon)
            {
                if (r.Musket) s.MusketOn = true; else s.RapierOn = true;
                s.Weapon = !r.Active ? StudioWeapon.Rest : (r.Musket ? StudioWeapon.MusketAimed : StudioWeapon.RapierRaised);
                s.Selection = StudioSelection.Weapon(r.Musket);
            }
            else
            {
                s.Wear(r.Slot, r.ItemId);
                s.Selection = StudioSelection.Cosmetic(r.Slot, r.ItemId);
            }
            s.Touch();
        }
    }
}
