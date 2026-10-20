using UnityEngine;

namespace Musketeers.Data
{
    // One map's tileset (ART_GUIDE §4.4). Every sprite is optional: an empty slot falls back to a
    // generated placeholder, and the colors tint those placeholders.
    [CreateAssetMenu(menuName = "Musketeers/Board Theme", fileName = "BoardTheme")]
    public sealed class BoardTheme : ScriptableObject
    {
        [Header("Sprites (optional)")]
        public Sprite[] FloorVariants = new Sprite[0];   // picked per tile by TileVariant.Pick
        public Sprite Pillar;
        public Sprite Crate;
        public Sprite CrateBroken;                       // debris left where a crate was destroyed
        public Sprite Pit;
        public Sprite CrackOverlay;                      // on tiles about to crumble
        public Sprite WarnOverlay;
        public Sprite BackgroundArt;                     // fills the screen behind the board

        [Header("Layout")]
        // Where the board sits on BackgroundArt: normalized 0-1 in the art's UV space, y up. The background
        // is scaled and placed so this rect lands exactly on the board's world rect.
        public Rect ArenaRect = new Rect(0.15f, 0.10f, 0.70f, 0.75f);
        // Walkable tiles alternate between the floor sprite and the same sprite turned 90 degrees (chess look).
        public bool ChessRotate;
        // > 0: pit tiles draw only this central fraction of the Pit sprite, so neighbouring water tiles read as
        // one body of water (the art's own border would make separate pools). 0 = the whole sprite.
        [Range(0f, 1f)] public float PitInnerCrop;
        // A thin dark edge on walkable tiles where they border water (island and bridge edges).
        public bool WaterEdge;

        [Header("Placeholder colors")]
        public Color FloorA = new Color(0.36f, 0.33f, 0.29f);
        public Color FloorB = new Color(0.40f, 0.37f, 0.32f);
        public Color PillarColor = new Color(0.62f, 0.62f, 0.66f);
        public Color CrateColor = new Color(0.62f, 0.42f, 0.22f);
        public Color PitColor = new Color(0.05f, 0.06f, 0.10f);
        public Color WarnColor = new Color(0.95f, 0.15f, 0.12f, 0.55f);
        public Color KegColor = new Color(0.30f, 0.18f, 0.10f);
        public Color Background = new Color(0.09f, 0.08f, 0.11f);

        public Sprite Floor(int x, int y) =>
            FloorVariants != null && FloorVariants.Length > 0 ? FloorVariants[TileVariant.Pick(x, y, FloorVariants.Length)] : null;
    }
}
