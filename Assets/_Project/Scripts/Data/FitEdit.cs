using System;
using System.Collections.Generic;
using UnityEngine;

namespace Musketeers.Data
{
    // Anything the Wardrobe Studio can fit: a cosmetic, or one weapon state (Rest / Active).
    public interface IFitTarget
    {
        string Id { get; }
        ItemFit Global { get; set; }          // = the Idle master fit
        List<FitOverride> Overrides { get; }  // never null
        ItemMasks Masks { get; }              // never null
        int CheckedPoses { get; set; }        // bitmask, bit = (int)PoseKind
    }

    [Serializable]
    public sealed class FitSet : IFitTarget
    {
        public string Name;
        public ItemFit Fit = new ItemFit(0f, 0f, 1f);
        public List<FitOverride> FitOverrides = new List<FitOverride>();
        public ItemMasks ItemMasks = new ItemMasks();
        public int Checked;

        public string Id => Name;
        public ItemFit Global { get => Fit; set => Fit = value; }
        public List<FitOverride> Overrides => FitOverrides ?? (FitOverrides = new List<FitOverride>());
        public ItemMasks Masks => ItemMasks ?? (ItemMasks = new ItemMasks());
        public int CheckedPoses { get => Checked; set => Checked = value; }
    }

    [Serializable]
    public struct FitClip
    {
        public ItemFit Fit;
        public bool HasMask;
        public BezierPath Mask;
        public bool MaskFromFront;   // the mask was drawn on a pose that shows the FrontSprite
    }

    // The one set of rules for editing item fits per pose (Plan 006 data model). Idle = the master (Global);
    // a pose override makes that pose "custom"; a musketeer override beats both for that musketeer.
    public static class FitEdit
    {
        public static readonly PoseKind[] StudioPoses = { PoseKind.Idle, PoseKind.Lunge, PoseKind.Hurt, PoseKind.Victory };

        // Most specific wins: musketeer + pose, then pose, then musketeer, then global.
        public static ItemFit Resolve(IFitTarget t, string character, PoseKind pose)
        {
            int best = -1;
            var fit = t.Global;
            foreach (var o in t.Overrides)
            {
                if (o == null)
                    continue;
                bool charOk = !o.ForCharacter || o.Character == character;
                bool poseOk = o.AnyPose || o.Pose == pose;
                if (!charOk || !poseOk)
                    continue;
                int rank = (o.ForCharacter ? 1 : 0) + (o.AnyPose ? 0 : 2);
                if (rank > best)
                {
                    best = rank;
                    fit = o.Fit;
                }
            }
            return fit;
        }

        static FitOverride Find(IFitTarget t, string character, PoseKind pose)
        {
            foreach (var o in t.Overrides)
            {
                if (o != null && (o.Character ?? "") == (character ?? "") && !o.AnyPose && o.Pose == pose)
                    return o;
            }
            return null;
        }

        public static bool IsCustom(IFitTarget t, PoseKind pose) => pose != PoseKind.Idle && Find(t, "", pose) != null;

        public static bool HasMusketeerOverride(IFitTarget t, string character, PoseKind pose) =>
            !string.IsNullOrEmpty(character) && Find(t, character, pose) != null;

        public static void Set(IFitTarget t, PoseKind pose, string character, ItemFit fit)
        {
            if (string.IsNullOrEmpty(character))
            {
                if (pose == PoseKind.Idle)
                {
                    t.Global = fit;
                    return;
                }
                character = "";
            }
            var o = Find(t, character, pose);
            if (o == null)
            {
                o = new FitOverride { Character = character, AnyPose = false, Pose = pose };
                t.Overrides.Add(o);
            }
            o.Fit = fit;
        }

        public static void ResetToIdle(IFitTarget t, PoseKind pose)
        {
            t.Overrides.RemoveAll(o => o != null && !o.ForCharacter && !o.AnyPose && o.Pose == pose);
        }

        public static void RemoveMusketeerOverride(IFitTarget t, string character, PoseKind pose)
        {
            if (string.IsNullOrEmpty(character))
                return;
            t.Overrides.RemoveAll(o => o != null && o.Character == character && !o.AnyPose && o.Pose == pose);
        }

        // "Link all poses": the edit's delta is applied to every pose, so per-pose differences survive.
        public static void SetLinked(IFitTarget t, string character, PoseKind editedPose, ItemFit newFit, IReadOnlyList<PoseKind> poses)
        {
            var old = Resolve(t, character, editedPose);
            var dOff = newFit.Offset - old.Offset;
            float dRot = newFit.Rotation - old.Rotation;
            float ratio = newFit.S / old.S;
            ItemFit Apply(ItemFit f) => new ItemFit(f.Offset.x + dOff.x, f.Offset.y + dOff.y, f.S * ratio, f.Rotation + dRot);

            if (string.IsNullOrEmpty(character))
            {
                // Non-custom poses inherit from Global, so they move once with it.
                t.Global = Apply(t.Global);
                foreach (var o in t.Overrides)
                {
                    if (o == null || o.ForCharacter || o.AnyPose)
                        continue;
                    for (int i = 0; i < poses.Count; i++)
                    {
                        if (poses[i] == o.Pose)
                        {
                            o.Fit = Apply(o.Fit);
                            break;
                        }
                    }
                }
                return;
            }
            // Resolve everything first so one pose's write can't change another's starting point.
            var results = new List<ItemFit>();
            foreach (var p in poses)
                results.Add(Apply(Resolve(t, character, p)));
            for (int i = 0; i < poses.Count; i++)
                Set(t, poses[i], character, results[i]);
        }

        public static bool IsChecked(IFitTarget t, PoseKind pose) => (t.CheckedPoses & (1 << (int)pose)) != 0;

        public static void SetChecked(IFitTarget t, PoseKind pose, bool on)
        {
            int bit = 1 << (int)pose;
            t.CheckedPoses = on ? t.CheckedPoses | bit : t.CheckedPoses & ~bit;
        }

        public static FitClip Copy(IFitTarget t, string character, PoseKind pose, bool withMask, bool drawsFrontSprite)
        {
            var clip = new FitClip { Fit = Resolve(t, character, pose) };
            if (withMask)
            {
                var m = t.Masks.For(pose, drawsFrontSprite);
                clip.HasMask = m != null;
                clip.Mask = m?.Clone();
                clip.MaskFromFront = m != null && drawsFrontSprite;
            }
            return clip;
        }

        // A front mask only goes into a per-pose mask of a pose where the target draws its FrontSprite, never into
        // Base; a side mask goes into Base (Idle + everyone) or a per-pose mask of a side-drawing pose.
        public static void Paste(FitClip clip, IFitTarget t, string character, PoseKind pose, bool targetDrawsFront = false)
        {
            Set(t, pose, character, clip.Fit);
            if (!clip.HasMask || clip.Mask == null || clip.MaskFromFront != targetDrawsFront)
                return;
            if (!clip.MaskFromFront && pose == PoseKind.Idle && string.IsNullOrEmpty(character))
                t.Masks.Base = clip.Mask.Clone();
            else
                t.Masks.SetForPose(pose, clip.Mask.Clone());
        }
    }
}
