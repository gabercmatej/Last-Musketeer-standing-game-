using System;
using UnityEngine;

namespace Musketeers.Data
{
    public enum CosmeticSlot { Hat, Face, Cape, Boots }

    // The body poses a musketeer has (ART_GUIDE §4.1). Lunge is Matej's extra pose (shove, bull rush).
    public enum PoseKind { Idle, Hurt, Victory, Lunge }

    // Where cosmetics attach on one pose, in world units from the sprite's pivot (the feet), for the
    // character facing right. HeadAngle tilts the hat and face with the head (the hurt pose leans).
    [Serializable]
    public struct PoseAnchors
    {
        public Vector2 HeadTop;   // hat
        public Vector2 Face;      // face items
        public Vector2 Back;      // cape (drawn behind the body)
        public Vector2 Feet;      // boots
        public Vector2 Hand;      // musket grip
        public float HeadAngle;   // degrees, counter-clockwise
        public float FeetStretch; // widens the boots for a wider stance (0 = 1 = as drawn)
        public bool HideBoots;    // stance too wide for a pair of boots (the lunge): the body's own boots show

        public Vector2 For(CosmeticSlot slot)
        {
            switch (slot)
            {
                case CosmeticSlot.Hat: return HeadTop;
                case CosmeticSlot.Face: return Face;
                case CosmeticSlot.Cape: return Back;
                default: return Feet;
            }
        }
    }

    [Serializable]
    public sealed class PoseArt
    {
        public Sprite Sprite;
        public bool Front;          // drawn facing the camera (the victory pose): cosmetics use their front sprites
        public PoseAnchors Anchors;
        public bool AnchorsSet;     // false until the importer (or Matej) placed them; re-imports keep set anchors
    }
}
