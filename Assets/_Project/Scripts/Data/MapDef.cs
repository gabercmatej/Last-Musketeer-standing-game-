using UnityEngine;

namespace Musketeers.Data
{
    // A playable map: the ASCII layout (GAME_DESIGN §3 legend) plus the tileset it's drawn with.
    // The board size comes from the text, so bigger maps are just bigger text files.
    [CreateAssetMenu(menuName = "Musketeers/Map", fileName = "Map")]
    public sealed class MapDef : ScriptableObject
    {
        public string Id = "map";
        public string DisplayName = "Map";
        [TextArea] public string Description = "";
        public TextAsset Layout;
        public BoardTheme Theme;

        // Per-map theming (FIXES_v0.2 §2.8): a future map just fills these in on its own asset.
        [Header("Lobby and results theming")]
        public Sprite LobbyBackground;
        public string LobbyMusic = "Music/arena_lobby";   // AudioLibrary key
        public Sprite ResultsBackground;
        // Normalized (0..1, y up) feet positions on the results art for places 1, 2, 3, 4.
        public Vector2[] PodiumSpots = new Vector2[4];
        public float PodiumScale = 1f;
    }
}
