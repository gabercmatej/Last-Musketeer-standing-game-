using Musketeers.Sim;
using PrimeTween;
using UnityEngine;
using UnityEngine.Rendering;

namespace Musketeers.Presentation
{
    // One musketeer on the board: tinted body, facing notch, name, 3 hearts, grit pips (max 5), musket
    // loaded/unloaded, crown when crowned, dimmed when dead. Built in code from ArtLibrary, so real art
    // is just a sprite on the CharacterDef. Only ever shows state; never changes it.
    public sealed class UnitView : MonoBehaviour
    {
        const int MaxPips = 5;

        public int Slot { get; private set; }
        public Int2 GridPos { get; private set; }
        public Transform Body => _bodyRoot;
        public Color Color { get; private set; }

        Transform _bodyRoot;
        SpriteRenderer _body;
        SpriteRenderer _outline;
        SpriteRenderer _facing;
        SpriteRenderer _shield;
        SpriteRenderer _select;
        SpriteRenderer _crown;
        SpriteRenderer _musket;
        SpriteRenderer[] _hearts;
        SpriteRenderer[] _pips;
        SortingGroup _group;
        WorldLabel _name;
        bool _alive = true;
        Color _bodyTint;
        int _hp;

        public void Build(ArtLibrary art, WorldLabels labels, int slot, int character, string displayName, int maxHp)
        {
            Slot = slot;
            Color = art.CharacterColor(character);
            _group = gameObject.AddComponent<SortingGroup>();

            _bodyRoot = new GameObject("Body").transform;
            _bodyRoot.SetParent(transform, false);
            _select = art.MakeRenderer("Select", _bodyRoot, PlaceholderArt.Ring, new Color(1f, 1f, 1f, 0.9f), 0);
            _select.transform.localScale = Vector3.one * 1.08f;
            _select.enabled = false;
            _outline = art.MakeRenderer("Outline", _bodyRoot, PlaceholderArt.Circle, Color.Lerp(Color, Color.black, 0.55f), 1);
            _outline.transform.localScale = Vector3.one * 0.86f;
            var bodySprite = art.BodySprite(character);
            // Real art keeps its own colors; the placeholder circle is tinted.
            _bodyTint = bodySprite == PlaceholderArt.Circle ? Color : Color.white;
            _body = art.MakeRenderer("Sprite", _bodyRoot, bodySprite, _bodyTint, 2);
            _body.transform.localScale = Vector3.one * 0.74f;
            _facing = art.MakeRenderer("Facing", _bodyRoot, PlaceholderArt.Triangle, Color.Lerp(Color, Color.white, 0.6f), 3);
            _facing.transform.localScale = Vector3.one * 0.22f;
            _shield = art.MakeRenderer("Shield", _bodyRoot, PlaceholderArt.Ring, new Color(0.7f, 0.95f, 1f, 0.95f), 4);
            _shield.transform.localScale = Vector3.one * 1.0f;
            _shield.enabled = false;

            _hearts = new SpriteRenderer[maxHp];
            for (int i = 0; i < maxHp; i++)
            {
                _hearts[i] = art.MakeRenderer("Heart" + i, transform, PlaceholderArt.Heart, new Color(0.95f, 0.25f, 0.3f), 6);
                _hearts[i].transform.localScale = Vector3.one * 0.2f;
                _hearts[i].transform.localPosition = new Vector3((i - (maxHp - 1) * 0.5f) * 0.19f, 0.5f, 0f);
            }
            _pips = new SpriteRenderer[MaxPips];
            for (int i = 0; i < MaxPips; i++)
            {
                _pips[i] = art.MakeRenderer("Grit" + i, transform, PlaceholderArt.Circle, Color.white, 6);
                _pips[i].transform.localScale = Vector3.one * 0.11f;
                _pips[i].transform.localPosition = new Vector3((i - (MaxPips - 1) * 0.5f) * 0.12f, -0.47f, 0f);
            }
            _musket = art.MakeRenderer("Musket", transform, PlaceholderArt.Musket, Color.white, 6);
            _musket.transform.localScale = Vector3.one * 0.3f;
            _musket.transform.localPosition = new Vector3(0.42f, -0.3f, 0f);
            _musket.transform.localRotation = Quaternion.Euler(0, 0, 35f);
            _crown = art.MakeRenderer("Crown", transform, art.CrownSprite, new Color(1f, 0.82f, 0.2f), 7);
            _crown.transform.localScale = Vector3.one * 0.3f;
            _crown.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            _crown.enabled = false;

            _name = labels.Create(displayName, "unit-name");
            _name.Follow = transform;
            _name.Offset = new Vector3(0f, -0.72f, 0f);
            _name.Element.style.color = Color.Lerp(Color, Color.white, 0.35f);
            SetFacing(Dir.S);
        }

        void OnDestroy()
        {
            _name?.Destroy();
            Tween.StopAll(transform);
            if (_bodyRoot != null)
                Tween.StopAll(_bodyRoot);
        }

        public void SetGridPos(Int2 p)
        {
            GridPos = p;
            transform.localPosition = BoardMath.ToWorld(p);
            UpdateSorting(p);
        }

        public void UpdateSorting(Int2 p) => _group.sortingOrder = Layers.Unit + Layers.Row(p.Y);

        // Logical position moves now; the transform is tweened by the caller.
        public void SetGridPosLogical(Int2 p)
        {
            GridPos = p;
            UpdateSorting(p);
        }

        public void SetFacing(Dir d)
        {
            _facing.transform.localPosition = BoardMath.DirVector(d) * 0.3f;
            _facing.transform.localRotation = Quaternion.Euler(0, 0, BoardMath.DirAngle(d));
        }

        public void SetSelected(bool selected) => _select.enabled = selected && _alive;

        public void SetParry(bool on) => _shield.enabled = on && _alive;

        public void SetHp(int hp)
        {
            _hp = hp;
            for (int i = 0; i < _hearts.Length; i++)
                _hearts[i].color = i < hp ? new Color(0.95f, 0.25f, 0.3f) : new Color(0.25f, 0.22f, 0.24f, 0.8f);
        }

        public int Hp => _hp;

        public void SetGrit(int grit)
        {
            for (int i = 0; i < _pips.Length; i++)
                _pips[i].color = i < grit ? new Color(1f, 0.85f, 0.3f) : new Color(0.2f, 0.2f, 0.22f, 0.85f);
        }

        public void SetLoaded(bool loaded) =>
            _musket.color = loaded ? new Color(0.95f, 0.92f, 0.85f) : new Color(0.4f, 0.38f, 0.36f, 0.55f);

        public void SetCrowned(bool crowned) => _crown.enabled = crowned && _alive;

        public void SyncStats(Unit u, bool crowned)
        {
            SetHp(u.Hp);
            SetGrit(u.Grit);
            SetLoaded(u.Loaded);
            SetAlive(u.Alive, false);
            SetCrowned(crowned);
        }

        // Dead units stay as a dim marker where they fell; a pit fall also shrinks it away.
        public void SetAlive(bool alive, bool fell)
        {
            _alive = alive;
            float a = alive ? 1f : 0.28f;
            _body.color = WithAlpha(alive ? _bodyTint : Color.Lerp(_bodyTint, Color.gray, 0.7f), a);
            _outline.color = WithAlpha(Color.Lerp(Color, Color.black, 0.55f), a);
            _facing.enabled = alive;
            foreach (var h in _hearts) h.enabled = alive;
            foreach (var p in _pips) p.enabled = alive;
            _musket.enabled = alive;
            if (!alive)
            {
                _shield.enabled = false;
                _crown.enabled = false;
                _select.enabled = false;
            }
            _bodyRoot.localScale = alive ? Vector3.one : Vector3.one * (fell ? 0.35f : 0.8f);
            _name.Element.style.opacity = alive ? 1f : 0.45f;
        }

        public bool Alive => _alive;

        public void Flash(Color c, float duration)
        {
            if (!_alive)
                return;
            Tween.Color(_body, c, _bodyTint, duration, Ease.OutQuad);
        }

        static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
