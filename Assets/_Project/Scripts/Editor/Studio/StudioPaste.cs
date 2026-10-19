using System.Collections.Generic;
using Musketeers.Data;

namespace Musketeers.EditorTools.Studio
{
    // Paste and "copy mask from" rules, with no window or Undo in them (the callers record Undo first).
    public static class StudioPaste
    {
        public static void ThisPose(FitClip clip, IFitTarget t, string characterOrNull, PoseKind pose) =>
            FitEdit.Paste(clip, t, characterOrNull, pose);

        // Everyone: Global = the clip and every other pose goes back to inheriting it. With a musketeer
        // override: that musketeer's override on every pose. A pasted mask becomes the item's Base and the
        // per-pose masks go.
        public static void AllPoses(FitClip clip, IFitTarget t, string characterOrNull)
        {
            if (string.IsNullOrEmpty(characterOrNull))
            {
                t.Global = clip.Fit;
                foreach (var p in FitEdit.StudioPoses)
                {
                    if (p != PoseKind.Idle)
                        FitEdit.ResetToIdle(t, p);
                }
            }
            else
            {
                foreach (var p in FitEdit.StudioPoses)
                    FitEdit.Set(t, p, characterOrNull, clip.Fit);
            }
            if (clip.HasMask && clip.Mask != null)
            {
                t.Masks.Base = clip.Mask.Clone();
                t.Masks.PerPose?.Clear();
            }
        }

        // The checklist: only candidates whose box is ticked get the paste.
        public static int ToChecked(FitClip clip, IList<IFitTarget> candidates, IList<bool> ticked, string characterOrNull, bool allPoses, PoseKind pose)
        {
            int n = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (i >= ticked.Count || !ticked[i] || candidates[i] == null)
                    continue;
                if (allPoses)
                    AllPoses(clip, candidates[i], characterOrNull);
                else
                    ThisPose(clip, candidates[i], characterOrNull, pose);
                n++;
            }
            return n;
        }

        // "Copy mask from <musketeer>": every target x pose x mask id, through the anchors.
        public static void CopyMask(CharacterFit from, IList<CharacterFit> targets, IList<PoseKind> poses, IList<BodyMaskId> ids)
        {
            foreach (var to in targets)
            {
                if (to == null || to == from)
                    continue;
                foreach (var pose in poses)
                {
                    foreach (var id in ids)
                        BodyMaskTransfer.Copy(from, to, pose, id);
                }
            }
        }
    }
}
