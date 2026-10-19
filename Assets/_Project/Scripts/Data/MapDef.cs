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
    }
}
