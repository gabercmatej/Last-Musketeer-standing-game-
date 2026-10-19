using Musketeers.Sim;
using UnityEngine;

namespace Musketeers.Data
{
    // One musketeer's personal content (GAME_DESIGN §10). Sprites are optional: when they're empty the
    // game draws a tinted placeholder circle, so art can be swapped in by just assigning it here.
    [CreateAssetMenu(menuName = "Musketeers/Character", fileName = "Character")]
    public sealed class CharacterDef : ScriptableObject
    {
        public string DisplayName = "Musketeer";
        public Color Color = Color.white;
        public Ability Ability;
        public string AbilityName = "Signature";
        [TextArea] public string AbilityDescription = "";
        public Sprite Body;        // top-down body; null = placeholder circle
        public Sprite Portrait;    // menu/HUD; null = colored swatch
    }
}
