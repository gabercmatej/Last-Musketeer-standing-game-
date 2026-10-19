using System.Collections.Generic;
using Musketeers.Data;
using UnityEditor;
using UnityEngine;

namespace Musketeers.EditorTools.Studio
{
    // The window-side half of copy / paste / copy-mask: resolves the selection, records one Undo group,
    // runs the pure rules in StudioPaste, and marks the assets unsaved. Never writes to disk.
    public static class StudioClipboard
    {
        public struct Entry
        {
            public string Key, Label;
            public IFitTarget Fit;
            public UnityEngine.Object Owner;
        }

        static string Who(StudioState s) => s.OverrideMusketeer ? s.CharId : null;

        public static bool CanCopy(StudioState s) => StudioTargets.Resolve(s).Valid;
        public static bool CanPaste(StudioState s) => s.HasClipboard && StudioTargets.Resolve(s).Valid;

        public static void Copy(StudioState s)
        {
            var t = StudioTargets.Resolve(s);
            if (!t.Valid)
                return;
            s.Clipboard = FitEdit.Copy(t.Fit, s.CharId, s.Pose, s.CopyIncludeMask, StudioMasks.DrawsFront(s));
            s.HasClipboard = true;
            s.ClipboardName = t.Name;
            s.ClipboardPose = s.Pose;
        }

        public static string Describe(StudioState s) =>
            !s.HasClipboard ? "Clipboard: empty" :
            $"Clipboard: {s.ClipboardName} · {s.ClipboardPose} · mask {(s.Clipboard.HasMask ? "yes" : "no")}";

        // Everything else in the selected item's slot (cosmetics: including disabled ones; weapons: the other FitSets).
        public static List<Entry> Others(StudioState s)
        {
            var list = new List<Entry>();
            var sel = s.Selection;
            if (sel.Kind == SelectionKind.Cosmetic)
            {
                foreach (var it in s.Catalog.BySlot(sel.Slot))
                {
                    if (it != null && it.Id != sel.ItemId)
                        list.Add(new Entry { Key = it.Id, Label = it.DisplayName + (it.EnabledInV1 ? "" : " (off)"), Fit = it, Owner = it });
                }
            }
            else if (sel.Kind == SelectionKind.Weapon)
            {
                bool curActive = StudioTargets.ActiveFor(s, sel.IsMusket);
                AddWeapon(list, s.Catalog.Musket, true, sel.IsMusket, curActive);
                AddWeapon(list, s.Catalog.Rapier, false, !sel.IsMusket, curActive);
            }
            return list;
        }

        static void AddWeapon(List<Entry> list, WeaponDef w, bool musket, bool isSelected, bool curActive)
        {
            if (w == null)
                return;
            foreach (bool active in new[] { false, true })
            {
                if (isSelected && active == curActive)
                    continue;
                string state = active ? (musket ? "aimed" : "raised") : "rest";
                list.Add(new Entry { Key = (musket ? "musket:" : "rapier:") + state, Label = (musket ? "Musket " : "Rapier ") + state, Fit = w.For(active), Owner = w });
            }
        }

        static void Finish(StudioState s, int group, IEnumerable<UnityEngine.Object> owners)
        {
            foreach (var o in owners)
            {
                if (o is CosmeticDef cd)
                    cd.Tuned = true;
                s.MarkDirty(o);
            }
            Undo.CollapseUndoOperations(group);
            s.Touch();
        }

        static int Begin(string name, IEnumerable<UnityEngine.Object> owners)
        {
            Undo.IncrementCurrentGroup();
            int g = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(name);
            foreach (var o in owners)
                Undo.RecordObject(o, name);
            return g;
        }

        public static void PasteThisPose(StudioState s)
        {
            var t = StudioTargets.Resolve(s);
            if (!s.HasClipboard || !t.Valid)
                return;
            var owners = new[] { t.Owner };
            int g = Begin("Paste fit", owners);
            StudioPaste.ThisPose(s.Clipboard, t.Fit, Who(s), s.Pose);
            Finish(s, g, owners);
        }

        public static void PasteAllPoses(StudioState s)
        {
            var t = StudioTargets.Resolve(s);
            if (!s.HasClipboard || !t.Valid)
                return;
            var owners = new[] { t.Owner };
            int g = Begin("Paste fit to all poses", owners);
            StudioPaste.AllPoses(s.Clipboard, t.Fit, Who(s));
            Finish(s, g, owners);
        }

        public static int PasteOthers(StudioState s)
        {
            if (!s.HasClipboard)
                return 0;
            var others = Others(s);
            var fits = new List<IFitTarget>();
            var ticks = new List<bool>();
            var owners = new List<UnityEngine.Object>();
            foreach (var e in others)
            {
                bool on = s.PasteChecked.Contains(e.Key);
                fits.Add(e.Fit);
                ticks.Add(on);
                if (on && !owners.Contains(e.Owner))
                    owners.Add(e.Owner);
            }
            if (owners.Count == 0)
                return 0;
            int g = Begin("Paste fit to other items", owners);
            int n = StudioPaste.ToChecked(s.Clipboard, fits, ticks, Who(s), s.PasteOthersAllPoses, s.Pose);
            Finish(s, g, owners);
            return n;
        }

        // Body masks the selection relates to: boots -> both boot masks, rapier / aimed musket -> the hand mask.
        public static List<BodyMaskId> BodyMaskIds(StudioState s)
        {
            var ids = new List<BodyMaskId>();
            var sel = s.Selection;
            if (sel.Kind == SelectionKind.Cosmetic)
            {
                var it = s.Catalog.Get(sel.ItemId);
                if (it != null && it.Slot == CosmeticSlot.Boots)
                {
                    ids.Add(BodyMaskId.BootBack);
                    ids.Add(BodyMaskId.BootFront);
                }
            }
            else if (sel.Kind == SelectionKind.Weapon && (!sel.IsMusket || StudioTargets.ActiveFor(s, true)))
                ids.Add(BodyMaskId.Hand);
            return ids;
        }

        public static void CopyBodyMasks(StudioState s)
        {
            var ids = BodyMaskIds(s);
            if (ids.Count == 0 || s.CopySource < 0 || s.CopySource >= s.Roster.Count)
                return;
            var src = s.Roster.Get(s.CopySource);
            if (src == null || src.Fit == null)
                return;
            var targets = new List<CharacterFit>();
            for (int i = 0; i < s.Roster.Count; i++)
            {
                var c = s.Roster.Get(i);
                if (i != s.CopySource && i < s.CopyTargets.Length && s.CopyTargets[i] && c != null && c.Fit != null)
                    targets.Add(c.Fit);
            }
            if (targets.Count == 0)
                return;
            var poses = s.CopyAllPoses ? (IList<PoseKind>)FitEdit.StudioPoses : new[] { s.Pose };
            var owners = new List<UnityEngine.Object>();
            foreach (var t in targets)
                owners.Add(t);
            int g = Begin("Copy mask from " + src.DisplayName, owners);
            StudioPaste.CopyMask(src.Fit, targets, poses, ids);
            foreach (var o in owners)
                s.MarkDirty(o);
            Undo.CollapseUndoOperations(g);
            s.Touch();
        }
    }
}
