using Musketeers.Sim;
using UnityEngine;

namespace Musketeers.Data
{
    // One musketeer's personal content (GAME_DESIGN §10). Sprites are optional: when they're empty the
    // game draws a tinted placeholder circle, so art can be swapped in by just assigning it here.
    [CreateAssetMenu(menuName = "Musketeers/Character", fileName = "Character")]
    public sealed class CharacterDef : ScriptableObject
    {
        public string Id = "musketeer";        // folder name under Art/Characters and Audio/Voice
        public string DisplayName = "Musketeer";
        public Color Color = Color.white;
        public Ability Ability;
        public string AbilityName = "Signature";
        [TextArea] public string AbilityDescription = "";
        public Sprite Portrait;                // menu/HUD; null = colored swatch

        [Header("Poses (side view facing right; null = placeholder)")]
        public PoseArt Idle = new PoseArt();
        public PoseArt IdleB = new PoseArt();  // optional breathing frame
        public PoseArt Hurt = new PoseArt();
        public PoseArt Victory = new PoseArt { Front = true };
        public PoseArt Lunge = new PoseArt();

        [Header("Anchors and masks per pose (Musketeers/Fitting Editor)")]
        public CharacterFit Fit;

        [Header("Default outfit (cosmetic ids, empty = none)")]
        public Loadout DefaultLoadout;

        public Sprite Body => Idle != null ? Idle.Sprite : null;

        public PoseArt Art(PoseKind kind)
        {
            switch (kind)
            {
                case PoseKind.IdleB: return IdleB;
                case PoseKind.Hurt: return Hurt;
                case PoseKind.Victory: return Victory;
                case PoseKind.Lunge: return Lunge;
                default: return Idle;
            }
        }

        public bool HasPose(PoseKind kind)
        {
            var p = Art(kind);
            return p != null && p.Sprite != null;
        }

        // The pose actually drawn for `kind`: Idle when that pose has no art yet.
        public PoseKind Resolve(PoseKind kind) => HasPose(kind) ? kind : PoseKind.Idle;

        public PoseArt Pose(PoseKind kind) => Art(Resolve(kind));

        // The fit for the pose actually drawn; an unfitted pose borrows Idle's, then the defaults.
        public PoseFit FitFor(PoseKind kind)
        {
            var k = Resolve(kind);
            var f = Fit != null ? Fit.Get(k) ?? Fit.Get(PoseKind.Idle) : null;
            return f ?? new PoseFit { Pose = k };
        }
    }
}
