using System;
using UnityEngine;

namespace Musketeers.Data
{
    public enum CosmeticSlot { Hat, Face, Cape, Boots }

    // The body poses a musketeer has (ART_GUIDE §4.1). Lunge is Matej's extra pose (shove, bull rush).
    // IdleB is the optional breathing frame; it's appended so serialized pose values stay stable.
    public enum PoseKind { Idle, Hurt, Victory, Lunge, IdleB }

    // FIXES_v0.2 §1.2: the seven attachment points every pose has.
    public enum AnchorId { HeadTop, Face, Back, FootBack, FootFront, HandBack, HandFront }

    // A point on a pose, in world units from the sprite's pivot (the feet), for the character facing
    // right. Rotation is degrees counter-clockwise. Scale is the local size (HeadTop's is the head size),
    // so an item fitted on one musketeer scales to the others. 0 counts as 1 (an unset struct).
    [Serializable]
    public struct Anchor
    {
        public Vector2 Position;
        public float Rotation;
        public float Scale;

        public float S => Scale > 0f ? Scale : 1f;

        public Anchor(float x, float y, float rotation = 0f, float scale = 1f)
        {
            Position = new Vector2(x, y);
            Rotation = rotation;
            Scale = scale;
        }
    }

    [Serializable]
    public sealed class PoseArt
    {
        public Sprite Sprite;
        public bool Front;          // drawn facing the camera (the victory pose): cosmetics use their front sprites
    }
}
