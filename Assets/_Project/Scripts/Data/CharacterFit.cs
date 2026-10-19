using System;
using System.Collections.Generic;
using UnityEngine;

namespace Musketeers.Data
{
    // How one pose wears things (FIXES_v0.2 §1.2): the seven anchors, the polygons around the body's own
    // boots (hidden when footwear is on) and around the front hand (redrawn over the rapier's grip).
    // Polygons are in the same pose space as the anchors.
    public enum BodyMaskId { BootBack, BootFront, Hand }

    [Serializable]
    public sealed class PoseFit : ISerializationCallbackReceiver
    {
        public PoseKind Pose;
        public Anchor HeadTop = new Anchor(0f, 1.1f);
        public Anchor Face = new Anchor(0.1f, 0.9f);
        public Anchor Back = new Anchor(-0.2f, 0.7f);
        public Anchor FootBack = new Anchor(-0.08f, 0f);
        public Anchor FootFront = new Anchor(0.08f, 0f);
        public Anchor HandBack = new Anchor(-0.2f, 0.4f, -38f, 0.72f);
        public Anchor HandFront = new Anchor(0.15f, 0.4f, -60f, 0.72f);
        public BezierPath BootPathBack = new BezierPath();
        public BezierPath BootPathFront = new BezierPath();
        public BezierPath HandPath = new BezierPath();
        // legacy, migrated on load
        [HideInInspector] public Vector2[] BootMaskBack = new Vector2[0];
        [HideInInspector] public Vector2[] BootMaskFront = new Vector2[0];
        [HideInInspector] public Vector2[] HandMask = new Vector2[0];
        public bool NoMusket;       // both hands busy in this pose (the victory fist): no musket
        public bool NoRapier;

        public BezierPath BodyMask(BodyMaskId id)
        {
            switch (id)
            {
                case BodyMaskId.BootBack: return BootPathBack;
                case BodyMaskId.BootFront: return BootPathFront;
                default: return HandPath;
            }
        }

        public void SetBodyMask(BodyMaskId id, BezierPath p)
        {
            if (p == null)
                p = new BezierPath();
            switch (id)
            {
                case BodyMaskId.BootBack: BootPathBack = p; break;
                case BodyMaskId.BootFront: BootPathFront = p; break;
                default: HandPath = p; break;
            }
        }

        public static AnchorId BodyMaskAnchor(BodyMaskId id)
        {
            switch (id)
            {
                case BodyMaskId.BootBack: return AnchorId.FootBack;
                case BodyMaskId.BootFront: return AnchorId.FootFront;
                default: return AnchorId.HandFront;
            }
        }

        public void OnBeforeSerialize() { }

        // Old assets and FitDefaults.json stored the masks as polygons; they become corner-only paths.
        public void OnAfterDeserialize()
        {
            BootPathBack = Migrate(BootPathBack, BootMaskBack);
            BootPathFront = Migrate(BootPathFront, BootMaskFront);
            HandPath = Migrate(HandPath, HandMask);
            BootMaskBack = new Vector2[0];
            BootMaskFront = new Vector2[0];
            HandMask = new Vector2[0];
        }

        static BezierPath Migrate(BezierPath path, Vector2[] legacy)
        {
            if ((path == null || !path.Valid) && PolygonMask.Valid(legacy))
                return BezierPath.FromPolygon(legacy);
            return path ?? new BezierPath();
        }

        public Anchor Get(AnchorId id)
        {
            switch (id)
            {
                case AnchorId.HeadTop: return HeadTop;
                case AnchorId.Face: return Face;
                case AnchorId.Back: return Back;
                case AnchorId.FootBack: return FootBack;
                case AnchorId.FootFront: return FootFront;
                case AnchorId.HandBack: return HandBack;
                default: return HandFront;
            }
        }

        public void Set(AnchorId id, Anchor a)
        {
            switch (id)
            {
                case AnchorId.HeadTop: HeadTop = a; break;
                case AnchorId.Face: Face = a; break;
                case AnchorId.Back: Back = a; break;
                case AnchorId.FootBack: FootBack = a; break;
                case AnchorId.FootFront: FootFront = a; break;
                case AnchorId.HandBack: HandBack = a; break;
                default: HandFront = a; break;
            }
        }

        // Copies everything but the pose id (the Fitting Editor's "copy anchors from pose X").
        public void CopyFrom(PoseFit o)
        {
            HeadTop = o.HeadTop; Face = o.Face; Back = o.Back; FootBack = o.FootBack; FootFront = o.FootFront;
            HandBack = o.HandBack; HandFront = o.HandFront;
            BootPathBack = o.BootPathBack.Clone();
            BootPathFront = o.BootPathFront.Clone();
            HandPath = o.HandPath.Clone();
            NoMusket = o.NoMusket;
            NoRapier = o.NoRapier;
        }
    }

    // One per musketeer (Data/Fits/<id>.asset). Import Art creates it once and never overwrites it, so
    // the Fitting Editor's work survives re-imports.
    [CreateAssetMenu(menuName = "Musketeers/Character Fit", fileName = "CharacterFit")]
    public sealed class CharacterFit : ScriptableObject
    {
        public List<PoseFit> Poses = new List<PoseFit>();

        public PoseFit Get(PoseKind kind)
        {
            foreach (var p in Poses)
            {
                if (p != null && p.Pose == kind)
                    return p;
            }
            return null;
        }

        public PoseFit GetOrAdd(PoseKind kind)
        {
            var p = Get(kind);
            if (p == null)
            {
                p = new PoseFit { Pose = kind };
                Poses.Add(p);
            }
            return p;
        }
    }
}
