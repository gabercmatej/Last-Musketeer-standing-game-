using UnityEngine;

namespace Musketeers.Data
{
    public struct LayerPlacement
    {
        public bool Valid;
        public Sprite Sprite;
        public Vector2 Position;   // sprite centre, world units from the body pivot (facing right)
        public float Angle;        // degrees, counter-clockwise
        public float Scale;
        public float ScaleX;       // extra horizontal stretch (boots on a wide stance), 1 = none
        public int Order;          // relative to the body
    }

    // The one place that decides where an item sits on a pose. CharacterRig (runtime) and the Wardrobe
    // Preview (editor) both use it, so what Matej tunes is exactly what the game draws. Facing left is a
    // mirror of the whole rig, so placements are always computed facing right.
    public static class CosmeticLayout
    {
        public static LayerPlacement Place(PoseArt pose, CosmeticDef item)
        {
            if (pose == null || item == null)
                return default;
            bool front = pose.Front && item.FrontSprite != null;
            var sprite = front ? item.FrontSprite : item.Sprite;
            if (sprite == null)
                return default;
            var a = pose.Anchors;
            if (item.Slot == CosmeticSlot.Boots && a.HideBoots)
                return default;
            float angle = item.Slot == CosmeticSlot.Hat || item.Slot == CosmeticSlot.Face ? a.HeadAngle : 0f;
            Vector2 offset = front ? item.FrontOffset : item.Offset;
            return new LayerPlacement
            {
                Valid = true,
                Sprite = sprite,
                Position = a.For(item.Slot) + Rotate(offset, angle),
                Angle = angle,
                Scale = front ? item.FrontScale : item.Scale,
                ScaleX = item.Slot == CosmeticSlot.Boots && a.FeetStretch > 0f ? a.FeetStretch : 1f,
                Order = item.SortOrder,
            };
        }

        public static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }
}
