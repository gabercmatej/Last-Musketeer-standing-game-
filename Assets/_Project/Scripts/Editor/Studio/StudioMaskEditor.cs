using Musketeers.Data;
using UnityEngine;

namespace Musketeers.EditorTools.Studio
{
    // Which mask is being edited with the pen right now: an item mask (item-local space) or a body mask
    // (boot back / boot front / hand, pose space). The session ends when the selection, pose, musketeer or
    // weapon state changes.
    public sealed class StudioMaskSession
    {
        public bool Active, Body, PerPose;
        public BodyMaskId BodyId;
        public readonly StudioPen Pen = new StudioPen();
        string _item;
        PoseKind _pose;
        int _char;
        StudioWeapon _weapon;

        void Capture(StudioState s)
        {
            _item = s.Selection.ItemId;
            _pose = s.Pose;
            _char = s.Char;
            _weapon = s.Weapon;
            Pen.Reset();
            Active = true;
        }

        public void StartItem(StudioState s, bool perPose)
        {
            Body = false;
            PerPose = perPose;
            Capture(s);
        }

        public void StartBody(StudioState s, BodyMaskId id)
        {
            Body = true;
            BodyId = id;
            Capture(s);
        }

        public void Stop()
        {
            Active = false;
            Pen.Reset();
        }

        public bool Matches(StudioState s) =>
            Active && _item == s.Selection.ItemId && _pose == s.Pose && _char == s.Char && _weapon == s.Weapon;

        public void ValidateAgainst(StudioState s)
        {
            if (Active && !Matches(s))
                Stop();
        }

        // The path being edited (null if it can't exist). `create` adds the empty path to the data.
        public BezierPath Path(StudioState s, bool create, out UnityEngine.Object owner)
        {
            owner = null;
            if (Body)
            {
                var c = s.Character;
                if (c == null || c.Fit == null)
                    return null;
                var k = c.Resolve(s.Pose);
                var pf = create ? c.Fit.GetOrAdd(k) : c.Fit.Get(k);
                if (pf == null)
                    return null;
                owner = c.Fit;
                var p = pf.BodyMask(BodyId);
                if (p == null)
                {
                    p = new BezierPath();
                    pf.SetBodyMask(BodyId, p);
                }
                return p;
            }
            var t = StudioTargets.Resolve(s);
            if (!t.Valid)
                return null;
            owner = t.Owner;
            return StudioMasks.ItemPath(t.Fit, s.Pose, PerPose, create);
        }
    }

    // Small helpers for the data behind the mask UI.
    public static class StudioMasks
    {
        // The raw path of an item: Base, or the pose's own path (created empty when `create`).
        public static BezierPath ItemPath(IFitTarget t, PoseKind pose, bool perPose, bool create)
        {
            var m = t.Masks;
            if (!perPose)
            {
                if (m.Base == null && create)
                    m.Base = new BezierPath();
                return m.Base;
            }
            if (m.PerPose != null)
            {
                foreach (var pm in m.PerPose)
                {
                    if (pm == null || pm.Pose != pose)
                        continue;
                    if (pm.Path == null && create)
                        pm.Path = new BezierPath();
                    return pm.Path;
                }
            }
            if (!create)
                return null;
            var path = new BezierPath();
            m.SetForPose(pose, path);
            return path;
        }

        // A front-facing pose drawing the item's FrontSprite: a different picture, so only per-pose masks apply.
        public static bool DrawsFront(StudioState s)
        {
            var sel = s.Selection;
            if (sel.Kind != SelectionKind.Cosmetic || s.Character == null)
                return false;
            var item = s.Catalog.Get(sel.ItemId);
            return item != null && item.FrontSprite != null && s.Character.Pose(s.Pose).Front;
        }

        public static bool HasPoints(BezierPath p) => p != null && p.Points != null && p.Points.Count > 0;
    }
}
