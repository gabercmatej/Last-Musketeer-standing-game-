using System;
using System.Collections.Generic;
using Musketeers.Data;

namespace Musketeers.EditorTools.Studio
{
    // Paste and "copy mask from" rules, with no window or Undo in them (the callers record Undo first).
    public static class StudioPaste
    {
        // Weapon fit sets are relative to per-pose hand anchors, so they only ever take a paste on one pose.
        public static bool AllowsAllPoses(IFitTarget t) => !(t is FitSet);

        static bool Front(Func<IFitTarget, PoseKind, bool> drawsFront, IFitTarget t, PoseKind p) =>
            drawsFront != null && drawsFront(t, p);

        // `drawsFront(target, pose)` = the target draws its FrontSprite on that pose (null = never).
        public static void ThisPose(FitClip clip, IFitTarget t, string characterOrNull, PoseKind pose, Func<IFitTarget, PoseKind, bool> drawsFront = null) =>
            FitEdit.Paste(clip, t, characterOrNull, pose, Front(drawsFront, t, pose));

        // Everyone: Global = the clip and every other pose goes back to inheriting it. With a musketeer
        // override: that musketeer's override on every pose. Masks: a side mask becomes the item's Base and
        // clears the per-pose masks of side poses; a front mask goes only to the front poses. A pose the mask
        // doesn't apply to keeps its own mask. Refused (false, nothing changed) for weapons.
        public static bool AllPoses(FitClip clip, IFitTarget t, string characterOrNull, Func<IFitTarget, PoseKind, bool> drawsFront = null, bool allowAllPoses = true)
        {
            if (!allowAllPoses || !AllowsAllPoses(t))
                return false;
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
                if (!clip.MaskFromFront)
                    t.Masks.Base = clip.Mask.Clone();
                foreach (var p in FitEdit.StudioPoses)
                {
                    bool front = Front(drawsFront, t, p);
                    if (front != clip.MaskFromFront)
                        continue;
                    if (front)
                        t.Masks.SetForPose(p, clip.Mask.Clone());
                    else
                        t.Masks.ResetToBase(p);
                }
            }
            return true;
        }

        // The checklist: only candidates whose box is ticked get the paste. Weapons always take this pose only.
        public static int ToChecked(FitClip clip, IList<IFitTarget> candidates, IList<bool> ticked, string characterOrNull, bool allPoses, PoseKind pose, Func<IFitTarget, PoseKind, bool> drawsFront = null)
        {
            int n = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (i >= ticked.Count || !ticked[i] || candidates[i] == null)
                    continue;
                if (allPoses && AllowsAllPoses(candidates[i]))
                    AllPoses(clip, candidates[i], characterOrNull, drawsFront);
                else
                    ThisPose(clip, candidates[i], characterOrNull, pose, drawsFront);
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
