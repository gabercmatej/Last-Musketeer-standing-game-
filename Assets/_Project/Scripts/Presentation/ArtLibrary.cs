using Musketeers.Data;
using UnityEngine;

namespace Musketeers.Presentation
{
    // The scene's single source of art: data assets with real sprites, falling back to PlaceholderArt.
    // Also owns the unlit sprite material, so the board renders without any 2D lights.
    public sealed class ArtLibrary : MonoBehaviour
    {
        [SerializeField] private CharacterRoster _roster;
        [SerializeField] private BoardTheme _theme;
        [SerializeField] private Material _spriteMaterial;

        public CharacterRoster Roster => _roster;
        public BoardTheme Theme => _theme;
        public Material SpriteMaterial => _spriteMaterial;

        public Sprite FloorSprite => _theme != null && _theme.Floor != null ? _theme.Floor : PlaceholderArt.Floor;
        public Sprite PillarSprite => _theme != null && _theme.Pillar != null ? _theme.Pillar : PlaceholderArt.Pillar;
        public Sprite CrateSprite => _theme != null && _theme.Crate != null ? _theme.Crate : PlaceholderArt.Crate;
        public Sprite PitSprite => _theme != null && _theme.Pit != null ? _theme.Pit : PlaceholderArt.Pit;
        public Sprite KegSprite => _theme != null && _theme.Keg != null ? _theme.Keg : PlaceholderArt.Keg;
        public Sprite CrownSprite => _theme != null && _theme.Crown != null ? _theme.Crown : PlaceholderArt.Crown;

        public CharacterDef Character(int index) => _roster != null ? _roster.Get(index) : null;

        public Color CharacterColor(int index)
        {
            var c = Character(index);
            return c != null ? c.Color : Color.HSVToRGB(index * 0.25f % 1f, 0.7f, 0.95f);
        }

        public string CharacterName(int index)
        {
            var c = Character(index);
            return c != null && !string.IsNullOrEmpty(c.DisplayName) ? c.DisplayName : $"Musketeer {index + 1}";
        }

        public Sprite BodySprite(int index)
        {
            var c = Character(index);
            return c != null && c.Body != null ? c.Body : PlaceholderArt.Circle;
        }

        // A SpriteRenderer with the project material, parented and sorted.
        public SpriteRenderer MakeRenderer(string name, Transform parent, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            if (_spriteMaterial != null)
                sr.sharedMaterial = _spriteMaterial;
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
