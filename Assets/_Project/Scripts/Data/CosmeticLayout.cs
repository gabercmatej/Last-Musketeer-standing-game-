using UnityEngine;

namespace Musketeers.Data
{
    // FIXES_v0.2 §1.2 layer order, back to front. Face and hat sit above the front boot and below the
    // rapier, so a blade raised past the face is still in front of it.
    public enum RigPart { Cape, BootBack, Musket, Body, BootFront, Face, Hat, Rapier, Hand }

    public struct LayerPlacement
    {
        public bool Valid;
        public Sprite Sprite;
        public Vector2 Position;   // sprite pivot, world units from the body pivot (facing right)
        public float Angle;        // degrees, counter-clockwise
        public Vector2 Scale;      // x < 0 mirrors (the far boot on a front-facing pose)
        public int Order;          // relative to the body (0)
        public float Shade;        // colour multiplier, 1 = as drawn
    }

    // The one place that decides where an item or weapon sits on a pose. CharacterRig (runtime) and the
    // Fitting Editor (editor) both use it, so what Matej fits is exactly what the game draws. Facing left
    // mirrors the whole rig, so placements are always computed facing right.
    public static class CosmeticLayout
    {
        public const float BackBootShade = 0.82f;  // "slightly darker": it's the far leg

        public static int Order(RigPart part)
        {
            switch (part)
            {
                case RigPart.Cape: return -20;
                case RigPart.BootBack: return -15;
                case RigPart.Musket: return -10;
                case RigPart.Body: return 0;
                case RigPart.BootFront: return 5;
                case RigPart.Face: return 10;
                case RigPart.Hat: return 15;
                case RigPart.Rapier: return 20;
                default: return 25;
            }
        }

        public static AnchorId AnchorFor(CosmeticSlot slot, bool backFoot)
        {
            switch (slot)
            {
                case CosmeticSlot.Hat: return AnchorId.HeadTop;
                case CosmeticSlot.Face: return AnchorId.Face;
                case CosmeticSlot.Cape: return AnchorId.Back;
                default: return backFoot ? AnchorId.FootBack : AnchorId.FootFront;
            }
        }

        public static RigPart PartFor(CosmeticSlot slot, bool backFoot)
        {
            switch (slot)
            {
                case CosmeticSlot.Hat: return RigPart.Hat;
                case CosmeticSlot.Face: return RigPart.Face;
                case CosmeticSlot.Cape: return RigPart.Cape;
                default: return backFoot ? RigPart.BootBack : RigPart.BootFront;
            }
        }

        // `kind` is the pose asked for; the character resolves it (a missing pose draws Idle).
        public static LayerPlacement Place(CharacterDef c, PoseKind kind, CosmeticDef item, bool backFoot = false)
        {
            if (c == null)
                return default;
            var k = c.Resolve(kind);
            return Place(c.Art(k), c.FitFor(k), c.Id, k, item, backFoot);
        }

        public static LayerPlacement Place(PoseArt pose, PoseFit fit, string character, PoseKind kind, CosmeticDef item, bool backFoot)
        {
            if (pose == null || fit == null || item == null)
                return default;
            bool front = pose.Front && item.FrontSprite != null;
            var sprite = front ? item.FrontSprite : item.Sprite;
            if (sprite == null)
                return default;
            var a = fit.Get(AnchorFor(item.Slot, backFoot));
            var f = item.FitFor(character, kind);
            float s = a.S * f.S;
            // On a front-facing pose the far boot is the near one mirrored (left and right foot).
            bool mirror = backFoot && pose.Front;
            var offset = f.Offset;
            if (mirror)
                offset.x = -offset.x;
            return new LayerPlacement
            {
                Valid = true,
                Sprite = sprite,
                Position = a.Position + Rotate(offset * a.S, a.Rotation),
                Angle = a.Rotation + (mirror ? -f.Rotation : f.Rotation),
                Scale = new Vector2(mirror ? -s : s, s),
                // Front-on, both feet are equally near: the far boot draws in front of the legs too.
                Order = mirror ? Order(RigPart.BootFront) - 1 : Order(PartFor(item.Slot, backFoot)),
                Shade = backFoot && !pose.Front && item.Slot == CosmeticSlot.Boots ? BackBootShade : 1f,
            };
        }

        // A weapon at its hand (Plan 006 layering: body -> weapon -> front-hand overlay). The resting musket
        // is in the back hand, behind the body; the aimed musket and the rapier (rest or raised) are in the
        // front hand, in front of the body and below the front-hand overlay. The sprite's pivot is the grip;
        // `extraAngle` adds to the fitted angle (aiming north/south, parrying).
        public static LayerPlacement Weapon(PoseFit fit, WeaponDef weapon, string character, PoseKind pose, bool active, float extraAngle = 0f)
        {
            if (fit == null || weapon == null || weapon.Sprite == null || (weapon.Musket ? fit.NoMusket : fit.NoRapier))
                return default;
            bool restMusket = weapon.Musket && !active;
            var a = restMusket ? fit.HandBack : fit.HandFront;
            var f = FitEdit.Resolve(weapon.For(active), character, pose);
            return new LayerPlacement
            {
                Valid = true,
                Sprite = weapon.Sprite,
                Position = a.Position + Rotate(f.Offset * a.S, a.Rotation),
                Angle = a.Rotation + f.Rotation + extraAngle,
                Scale = Vector2.one * (a.S * f.S),
                Order = Order(restMusket ? RigPart.Musket : RigPart.Rapier),
                Shade = 1f,
            };
        }

        // The mask an item draws on this pose (null = none).
        public static BezierPath ItemMask(CosmeticDef item, PoseArt pose, PoseKind kind) =>
            item == null ? null : ((IFitTarget)item).Masks.For(kind, pose != null && pose.Front && item.FrontSprite != null);

        public static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }
}
