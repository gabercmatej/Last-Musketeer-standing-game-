using UnityEngine;

namespace Musketeers.Data
{
    // Board and object art (docs/ART_AND_AUDIO.md). Every sprite is optional: an empty slot falls back
    // to a generated placeholder, and the colors tint those placeholders.
    [CreateAssetMenu(menuName = "Musketeers/Board Theme", fileName = "BoardTheme")]
    public sealed class BoardTheme : ScriptableObject
    {
        [Header("Sprites (optional)")]
        public Sprite Floor;
        public Sprite Pillar;
        public Sprite Crate;
        public Sprite Pit;
        public Sprite Keg;
        public Sprite Crown;

        [Header("Placeholder colors")]
        public Color FloorA = new Color(0.36f, 0.33f, 0.29f);
        public Color FloorB = new Color(0.40f, 0.37f, 0.32f);
        public Color PillarColor = new Color(0.62f, 0.62f, 0.66f);
        public Color CrateColor = new Color(0.62f, 0.42f, 0.22f);
        public Color PitColor = new Color(0.05f, 0.06f, 0.10f);
        public Color WarnColor = new Color(0.95f, 0.15f, 0.12f, 0.55f);
        public Color KegColor = new Color(0.30f, 0.18f, 0.10f);
        public Color Background = new Color(0.09f, 0.08f, 0.11f);
    }
}
