using System;
using System.Collections.Generic;

namespace Musketeers.Data
{
    [Serializable]
    public sealed class PoseMask
    {
        public PoseKind Pose;
        public BezierPath Path = new BezierPath();
    }

    // Masks on one item (Plan 006): one base path in the item sprite's local space, plus optional per-pose
    // replacements.
    [Serializable]
    public sealed class ItemMasks
    {
        public BezierPath Base = new BezierPath();
        public List<PoseMask> PerPose = new List<PoseMask>();

        PoseMask Find(PoseKind pose)
        {
            if (PerPose == null)
                return null;
            foreach (var m in PerPose)
            {
                if (m != null && m.Pose == pose)
                    return m;
            }
            return null;
        }

        // A pose with its own entry uses it, and an entry with no valid path means "no mask on this pose".
        // Only poses without an entry fall back to Base, and only when they draw the side sprite (a
        // front-facing pose drawing FrontSprite is a different picture, so Base doesn't apply).
        public BezierPath For(PoseKind pose, bool drawsFrontSprite)
        {
            var own = Find(pose);
            if (own != null)
                return own.Path != null && own.Path.Valid ? own.Path : null;
            if (!drawsFrontSprite && Base != null && Base.Valid)
                return Base;
            return null;
        }

        public bool IsCustom(PoseKind pose) => Find(pose) != null;

        public void SetForPose(PoseKind pose, BezierPath path)
        {
            if (PerPose == null)
                PerPose = new List<PoseMask>();
            var m = Find(pose);
            if (m == null)
                PerPose.Add(new PoseMask { Pose = pose, Path = path });
            else
                m.Path = path;
        }

        public void ResetToBase(PoseKind pose)
        {
            PerPose?.RemoveAll(m => m != null && m.Pose == pose);
        }
    }
}
