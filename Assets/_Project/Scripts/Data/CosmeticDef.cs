using System;
using System.Collections.Generic;
using UnityEngine;

namespace Musketeers.Data
{
    // Where an item sits relative to its anchor: the offset is in anchor space (it turns and scales with
    // the anchor), rotation adds to the anchor's, scale multiplies it.
    [Serializable]
    public struct ItemFit
    {
        public Vector2 Offset;
        public float Rotation;
        public float Scale;

        public float S => Scale > 0f ? Scale : 1f;

        public ItemFit(float x, float y, float scale, float rotation = 0f)
        {
            Offset = new Vector2(x, y);
            Scale = scale;
            Rotation = rotation;
        }
    }

    // A fit for one musketeer (Character set), one pose (AnyPose off), or both.
    [Serializable]
    public sealed class FitOverride
    {
        public string Character = "";  // CharacterDef.Id, "" = every musketeer
        public bool AnyPose = true;
        public PoseKind Pose;
        public ItemFit Fit;

        public bool ForCharacter => !string.IsNullOrEmpty(Character);
    }

    // One wardrobe item (GAME_DESIGN §10, FIXES_v0.2 §1.2). The global Offset/Scale/Rotation place it on
    // every musketeer and pose; overrides replace them for one musketeer, one pose, or one of each.
    [CreateAssetMenu(menuName = "Musketeers/Cosmetic", fileName = "Cosmetic")]
    public sealed class CosmeticDef : ScriptableObject
    {
        public string Id = "item";
        public string DisplayName = "Item";
        public CosmeticSlot Slot;
        public Sprite Sprite;          // side view, facing right (boots: one boot)
        public Sprite FrontSprite;     // facing the camera (victory pose); null = use Sprite
        public bool EnabledInV1 = true;
        public Vector2 Offset;
        public float Scale = 1f;
        public float Rotation;
        public List<FitOverride> Overrides = new List<FitOverride>();
        public bool Tuned;             // true once fitted by hand; the importer then leaves the fit alone

        public ItemFit Global
        {
            get => new ItemFit { Offset = Offset, Scale = Scale, Rotation = Rotation };
            set { Offset = value.Offset; Scale = value.Scale; Rotation = value.Rotation; }
        }

        // Most specific wins: musketeer + pose, then pose, then musketeer, then global. A pose override
        // beats a musketeer one because poses differ more than heads do (the victory pose is front-on).
        public ItemFit FitFor(string character, PoseKind pose)
        {
            int best = -1;
            var fit = Global;
            if (Overrides == null)
                return fit;
            foreach (var o in Overrides)
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

        public FitOverride FindOverride(string character, bool anyPose, PoseKind pose)
        {
            if (Overrides == null)
                return null;
            foreach (var o in Overrides)
            {
                if (o != null && (o.Character ?? "") == (character ?? "") && o.AnyPose == anyPose && (anyPose || o.Pose == pose))
                    return o;
            }
            return null;
        }
    }
}
