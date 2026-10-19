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
        public PoseArt Hurt = new PoseArt();
        public PoseArt Victory = new PoseArt { Front = true };
        public PoseArt Lunge = new PoseArt();

        [Header("Default outfit (cosmetic ids, empty = none)")]
        public Loadout DefaultLoadout;

        public Sprite Body => Idle != null ? Idle.Sprite : null;

        // The requested pose, or Idle when that pose has no art yet.
        public PoseArt Pose(PoseKind kind)
        {
            PoseArt p;
            switch (kind)
            {
                case PoseKind.Hurt: p = Hurt; break;
                case PoseKind.Victory: p = Victory; break;
                case PoseKind.Lunge: p = Lunge; break;
                default: p = Idle; break;
            }
            return p != null && p.Sprite != null ? p : Idle;
        }
    }
}
