using Musketeers.Data;
using UnityEngine;

namespace Musketeers.Presentation
{
    // The scene's single source of art: data assets with real sprites, falling back to PlaceholderArt.
    // Also owns the unlit sprite material, so the board renders without any 2D lights.
    public sealed class ArtLibrary : MonoBehaviour
    {
        [SerializeField] private CharacterRoster _roster;
        [SerializeField] private BoardTheme _theme;          // default theme (first map's)
        [SerializeField] private GameArt _gameArt;
        [SerializeField] private CosmeticCatalog _cosmetics;
        [SerializeField] private MapCatalog _maps;
        [SerializeField] private AudioLibrary _audio;
        [SerializeField] private Material _spriteMaterial;

        BoardTheme _current;

        public CharacterRoster Roster => _roster;
        public BoardTheme Theme => _current != null ? _current : _theme;
        public GameArt Game => _gameArt;
        public CosmeticCatalog Cosmetics => _cosmetics;
        public MapCatalog Maps => _maps;
        public AudioLibrary Audio => _audio;
        public Material SpriteMaterial => _spriteMaterial;

        public void SetTheme(BoardTheme theme) => _current = theme;

        // Real art from GameArt by key, or the placeholder.
        public Sprite Sprite(string key, Sprite fallback)
        {
            var s = _gameArt != null ? _gameArt.Get(key) : null;
            return s != null ? s : fallback;
        }

        public bool Has(string key) => _gameArt != null && _gameArt.Get(key) != null;

        public Sprite FloorSprite(int x, int y)
        {
            var s = Theme != null ? Theme.Floor(x, y) : null;
            return s != null ? s : PlaceholderArt.Floor;
        }

        public Sprite PillarSprite => Theme != null && Theme.Pillar != null ? Theme.Pillar : PlaceholderArt.Pillar;
        public Sprite CrateSprite => Theme != null && Theme.Crate != null ? Theme.Crate : PlaceholderArt.Crate;
        public Sprite PitSprite
        {
            get
            {
                if (Theme == null || Theme.Pit == null)
                    return PlaceholderArt.Pit;
                return Theme.PitInnerCrop > 0.01f && Theme.PitInnerCrop < 0.99f ? Cropped(Theme.Pit, Theme.PitInnerCrop) : Theme.Pit;
            }
        }

        // The central `fraction` of a sprite as its own one-unit sprite (cached), made in code so it needs no extra asset.
        readonly System.Collections.Generic.Dictionary<(Sprite, float), Sprite> _cropped = new System.Collections.Generic.Dictionary<(Sprite, float), Sprite>();

        Sprite Cropped(Sprite source, float fraction)
        {
            if (_cropped.TryGetValue((source, fraction), out var cached) && cached != null)
                return cached;
            var r = source.textureRect;
            float w = r.width * fraction, h = r.height * fraction;
            var inner = new Rect(r.x + (r.width - w) * 0.5f, r.y + (r.height - h) * 0.5f, w, h);
            var s = UnityEngine.Sprite.Create(source.texture, inner, new Vector2(0.5f, 0.5f), w, 0, SpriteMeshType.FullRect);
            s.name = source.name + "_inner";
            _cropped[(source, fraction)] = s;
            return s;
        }
        public Sprite KegSprite => Sprite(GameArt.Keg, PlaceholderArt.Keg);
        public Sprite CrownSprite => Sprite(GameArt.Crown, PlaceholderArt.Crown);

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
