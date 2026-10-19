using System;
using System.Collections.Generic;
using Musketeers.Data;
using UnityEditor;
using UnityEngine;

namespace Musketeers.EditorTools.Studio
{
    public enum StudioWeapon { Rest, MusketAimed, RapierRaised }

    public enum SelectionKind { None, Cosmetic, Weapon, Anchor }

    // What is selected on the canvas: an item (slot + id), a weapon ("musket" / "rapier") or an anchor.
    public struct StudioSelection
    {
        public SelectionKind Kind;
        public CosmeticSlot Slot;
        public string ItemId;      // cosmetic id, or "musket" / "rapier"
        public AnchorId Anchor;

        public static StudioSelection None => default;
        public static StudioSelection Cosmetic(CosmeticSlot slot, string id) => new StudioSelection { Kind = SelectionKind.Cosmetic, Slot = slot, ItemId = id };
        public static StudioSelection Weapon(bool musket) => new StudioSelection { Kind = SelectionKind.Weapon, ItemId = musket ? "musket" : "rapier" };
        public static StudioSelection OfAnchor(AnchorId a) => new StudioSelection { Kind = SelectionKind.Anchor, Anchor = a };
        public bool IsMusket => Kind == SelectionKind.Weapon && ItemId == "musket";
    }

    // Everything the Wardrobe Studio has open. Plain data; the panels read and change it.
    public sealed class StudioState
    {
        public CharacterRoster Roster;
        public CosmeticCatalog Catalog;
        public int Char;
        public PoseKind Pose = PoseKind.Idle;
        public StudioWeapon Weapon = StudioWeapon.Rest;
        public readonly bool[] SlotOn = new bool[4];
        public readonly string[] SlotItem = { "", "", "", "" };   // Loadout.Slots order
        public bool MusketOn = true, RapierOn = true;
        public StudioSelection Selection;
        public bool LinkAllPoses;
        public bool OverrideMusketeer;
        public bool ShowMasks = true;
        public bool AnchorMode;
        public float Zoom = 1.1f;
        public readonly HashSet<UnityEngine.Object> Unsaved = new HashSet<UnityEngine.Object>();
        public readonly StudioMaskSession Mask = new StudioMaskSession();
        public bool MaskThisPose;                 // "This pose only" for item masks
        public int CopySource;                    // "Copy mask from": roster index (default Matej)
        public bool[] CopyTargets = { true, true, true, true, true, true, true, true };
        public bool CopyAllPoses;
        public FitClip Clipboard;
        public bool HasClipboard;
        public string ClipboardName = "";
        public PoseKind ClipboardPose;
        public bool CopyIncludeMask = true;
        public bool PasteOpen;
        public readonly HashSet<string> PasteChecked = new HashSet<string>();
        public bool PasteOthersAllPoses;
        public int Rev;   // bumps whenever the composed images must be redrawn

        public StudioState(CharacterRoster roster, CosmeticCatalog catalog)
        {
            Roster = roster;
            Catalog = catalog;
            for (int i = 0; i < roster.Count; i++)
            {
                if (roster.Get(i) != null && roster.Get(i).Id == "matej")
                {
                    Char = i;
                    CopySource = i;
                }
            }
            var l = Character != null ? Character.DefaultLoadout : default;
            foreach (var slot in Loadout.Slots)
            {
                int si = SlotIndex(slot);
                string id = l.Get(slot);
                if (catalog.Get(id) == null)
                {
                    var all = catalog.BySlot(slot);
                    id = all.Count > 0 ? all[0].Id : "";
                }
                SlotItem[si] = id;
                SlotOn[si] = id != "";
            }
            RefreshUnsavedFromDirty();
        }

        public static int SlotIndex(CosmeticSlot slot) => Array.IndexOf(Loadout.Slots, slot);
        public CharacterDef Character => Roster.Get(Char);
        public string CharId => Character != null ? Character.Id : "";
        public bool HasUnsaved => Unsaved.Count > 0;
        public void Touch() => Rev++;

        public void MarkDirty(UnityEngine.Object o)
        {
            if (o == null)
                return;
            EditorUtility.SetDirty(o);
            Unsaved.Add(o);
        }

        // After a domain reload the Studio forgets what it touched; the assets are still dirty in memory.
        public void RefreshUnsavedFromDirty()
        {
            foreach (var o in Candidates())
            {
                if (o != null && EditorUtility.IsDirty(o))
                    Unsaved.Add(o);
            }
        }

        IEnumerable<UnityEngine.Object> Candidates()
        {
            foreach (var c in Catalog.Items)
                yield return c;
            yield return Catalog.Musket;
            yield return Catalog.Rapier;
            foreach (var ch in Roster.Characters)
            {
                if (ch != null)
                    yield return ch.Fit;
            }
        }

        public List<CosmeticDef> Outfit()
        {
            var list = new List<CosmeticDef>();
            foreach (var slot in Loadout.Slots)
            {
                int si = SlotIndex(slot);
                var it = SlotOn[si] ? Catalog.Get(SlotItem[si]) : null;
                if (it != null)
                    list.Add(it);
            }
            return list;
        }

        public RigCompositor.Options Options(Color32 background) => new RigCompositor.Options
        {
            Weapons = true,
            ShowMusket = MusketOn,
            ShowRapier = RapierOn,
            MusketAimed = Weapon == StudioWeapon.MusketAimed,
            Raised = Weapon == StudioWeapon.RapierRaised,
            Background = background,
            NoMasks = !ShowMasks,
        };

        public void Wear(CosmeticSlot slot, string id)
        {
            int si = SlotIndex(slot);
            SlotItem[si] = id;
            SlotOn[si] = true;
            Touch();
        }
    }
}
