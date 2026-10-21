using Musketeers.Data;
using Musketeers.Sim;
using PrimeTween;
using UnityEngine;
using UnityEngine.Rendering;

namespace Musketeers.Presentation
{
    // One musketeer on the board: the paper-doll rig standing on its tile with a ground shadow, hearts
    // and the musket state over its head, grit pips and the name under its feet, the Crown when crowned.
    // Only ever shows state; never changes it.
    public sealed class UnitView : MonoBehaviour
    {
        const int MaxPips = 5;
        const float HeartStep = 0.26f;
        const float MusketScale = 0.82f;   // the loaded/unloaded indicator: big enough to read at a glance
        const float MusketSlot = 0.72f;    // horizontal room the icon takes next to the hearts
        public const float FeetY = -0.3f;    // feet sit low on the tile; the body rises into the row above
        public const float RigScale = 1.35f;  // a musketeer is ~1.5 tiles tall (1.62 was too big)

        public int Slot { get; private set; }
        public Int2 GridPos { get; private set; }
        public Transform Body => _bodyRoot;   // nudges, punches and falls move this (rig + shadow)
        public CharacterRig Rig => _rig;
        public Color Color { get; private set; }
        public string CharacterId { get; private set; }

        ArtLibrary _art;
        Transform _bodyRoot;
        CharacterRig _rig;
        SpriteRenderer _shadow;
        SpriteRenderer _shield;
        Tween _shieldTween;
        SpriteRenderer _select;
        SpriteRenderer _crown;
        SpriteRenderer _musketIcon;
        SpriteRenderer[] _hearts;
        SpriteRenderer[] _pips;
        SortingGroup _group;
        WorldLabel _name;
        Sprite _heartFull, _heartEmpty, _loaded, _empty;
        bool _realHearts, _realMusket;
        bool _alive = true;
        bool _dying;            // lying down and fading; the board leaves a dying unit alone until it is gone
        int _hp;

        public void Build(ArtLibrary art, WorldLabels labels, int slot, int character, string displayName, int maxHp, Loadout loadout)
        {
            _art = art;
            Slot = slot;
            Color = art.CharacterColor(character);
            CharacterId = art.Character(character)?.Id;
            _group = gameObject.AddComponent<SortingGroup>();

            _bodyRoot = new GameObject("Body").transform;
            _bodyRoot.SetParent(transform, false);

            _shadow = art.MakeRenderer("Shadow", _bodyRoot, art.Sprite(GameArt.Shadow, PlaceholderArt.SoftCircle), new Color(0, 0, 0, art.Has(GameArt.Shadow) ? 0.85f : 0.35f), -40);
            _shadow.transform.localPosition = new Vector3(0f, FeetY + 0.01f, 0f);
            _shadow.transform.localScale = art.Has(GameArt.Shadow) ? new Vector3(0.95f, 1.6f, 1f) : new Vector3(0.7f, 0.22f, 1f);

            _select = art.MakeRenderer("Select", _bodyRoot, PlaceholderArt.Ring, new Color(1f, 1f, 1f, 0.9f), -30);
            _select.transform.localPosition = new Vector3(0f, FeetY, 0f);
            _select.transform.localScale = new Vector3(0.95f, 0.42f, 1f);
            _select.enabled = false;

            var rigGo = new GameObject("Rig");
            rigGo.transform.SetParent(_bodyRoot, false);
            rigGo.transform.localPosition = new Vector3(0f, FeetY, 0f);
            rigGo.transform.localScale = Vector3.one * RigScale;
            _rig = rigGo.AddComponent<CharacterRig>();
            _rig.Build(art, art.Character(character), loadout, Color);

            // Parry: a shimmering crescent in the musketeer's colour held out in front (it flips with facing).
            _shield = art.MakeRenderer("Shield", _bodyRoot, PlaceholderArt.Crescent, ShieldColor(1f), 40);
            _shield.transform.localPosition = new Vector3(0.38f, FeetY + 0.62f, 0f);
            _shield.transform.localScale = new Vector3(1.7f, 1.7f, 1f);
            _shield.enabled = false;

            // Status over the head: hearts, then the musket icon.
            _heartFull = art.Sprite(GameArt.HeartFull, PlaceholderArt.Heart);
            _heartEmpty = art.Sprite(GameArt.HeartEmpty, PlaceholderArt.Heart);
            _realHearts = art.Has(GameArt.HeartFull);
            float headY = FeetY + 1.38f * RigScale;
            _hearts = new SpriteRenderer[maxHp];
            for (int i = 0; i < maxHp; i++)
            {
                _hearts[i] = art.MakeRenderer("Heart" + i, transform, _heartFull, Color.white, 60);
                _hearts[i].transform.localScale = Vector3.one * (_realHearts ? 0.34f : 0.32f);
                _hearts[i].transform.localPosition = new Vector3((i - (maxHp - 1) * 0.5f) * HeartStep - MusketSlot * 0.5f, headY, 0f);
            }
            _loaded = art.Sprite(GameArt.MusketLoaded, PlaceholderArt.Musket);
            _empty = art.Sprite(GameArt.MusketEmpty, PlaceholderArt.Musket);
            _realMusket = art.Has(GameArt.MusketLoaded);
            _musketIcon = art.MakeRenderer("MusketIcon", transform, _loaded, Color.white, 60);
            _musketIcon.transform.localScale = Vector3.one * (_realMusket ? MusketScale : MusketScale * 0.96f);
            _musketIcon.transform.localPosition = new Vector3((maxHp - 1) * 0.5f * HeartStep + MusketSlot * 0.5f + 0.02f, headY + 0.02f, 0f);

            _pips = new SpriteRenderer[MaxPips];
            var grit = art.Sprite(GameArt.Grit, PlaceholderArt.Circle);
            bool realGrit = art.Has(GameArt.Grit);
            for (int i = 0; i < MaxPips; i++)
            {
                _pips[i] = art.MakeRenderer("Grit" + i, transform, grit, Color.white, 60);
                _pips[i].transform.localScale = Vector3.one * (realGrit ? 0.15f : 0.13f);
                _pips[i].transform.localPosition = new Vector3((i - (MaxPips - 1) * 0.5f) * 0.14f, FeetY - 0.13f, 0f);
            }
            _crown = art.MakeRenderer("Crown", _bodyRoot, art.CrownSprite, art.Has(GameArt.Crown) ? Color.white : new Color(1f, 0.82f, 0.2f), 50);
            _crown.transform.localScale = Vector3.one * (art.Has(GameArt.Crown) ? 0.28f : 0.3f);
            _crown.transform.localPosition = new Vector3(0f, headY + 0.2f, 0f);
            _crown.enabled = false;

            _name = labels.Create(displayName, "unit-name");
            _name.Follow = transform;
            _name.Offset = new Vector3(0f, FeetY - 0.3f, 0f);
            _name.Element.style.color = Color.Lerp(Color, Color.white, 0.35f);
            // Spawns on the left half look right, on the right half look left (toward the middle).
            _rig.SetFacingRight(slot % 2 == 0);
        }

        void OnDestroy()
        {
            _name?.Destroy();
            _shieldTween.Stop();
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

        public void UpdateSorting(Int2 p) => _group.sortingOrder = Layers.Unit(p.Y);

        // Logical position moves now; the transform is tweened by the caller.
        public void SetGridPosLogical(Int2 p)
        {
            GridPos = p;
            UpdateSorting(p);
        }

        // World position of the feet and of the musket's barrel tip pointing along d (for FX).
        public Vector3 Feet => transform.localPosition + new Vector3(0f, FeetY, 0f);
        public Vector3 BarrelTip(Dir d) => transform.localPosition + new Vector3(0f, FeetY + 0.6f, 0f) + BoardMath.DirVector(d) * 0.85f;

        public void SetFacing(Dir d) => _rig.Face(d);
        public void Aim(Dir d) => _rig.Aim(d);
        public void Rest() => _rig.Rest();
        public void Recoil(float seconds) => _rig.Recoil(seconds);
        public void FlashPose(PoseKind pose, float seconds) { if (_alive) _rig.FlashPose(pose, seconds); }
        public void HoldMusket(float seconds) { if (_alive) _rig.HoldMusket(seconds); }
        public void SetPose(PoseKind pose) => _rig.SetPose(pose);

        public void SetSelected(bool selected) => _select.enabled = selected && _alive;

        Color ShieldColor(float a)
        {
            var c = Color.Lerp(Color, Color.white, 0.35f);
            c.a = a;
            return c;
        }

        public void SetParry(bool on)
        {
            on = on && _alive;
            _shieldTween.Stop();
            _shield.enabled = on;
            _rig.SetRapier(on);
            if (!on)
                return;
            float side = _rig.FacingRight ? 1f : -1f;
            _shield.flipX = side < 0f;
            _shield.transform.localPosition = new Vector3(0.38f * side, FeetY + 0.62f, 0f);
            // Gentle shimmer: the arc breathes in size and brightness.
            _shieldTween = Tween.Custom(this, 0f, 1f, 0.45f, (self, v) =>
            {
                self._shield.color = self.ShieldColor(Mathf.Lerp(0.55f, 1f, v));
                self._shield.transform.localScale = new Vector3(1.7f, 1.7f, 1f) * Mathf.Lerp(0.94f, 1.06f, v);
            }, Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo);
        }

        public void SetHp(int hp)
        {
            _hp = hp;
            for (int i = 0; i < _hearts.Length; i++)
            {
                bool full = i < hp;
                _hearts[i].sprite = full ? _heartFull : _heartEmpty;
                _hearts[i].color = _realHearts ? Color.white : full ? new Color(0.95f, 0.25f, 0.3f) : new Color(0.25f, 0.22f, 0.24f, 0.8f);
            }
        }

        public int Hp => _hp;

        public void SetGrit(int grit)
        {
            for (int i = 0; i < _pips.Length; i++)
                _pips[i].color = i < grit ? new Color(1f, 0.9f, 0.45f) : new Color(0.15f, 0.15f, 0.18f, 0.55f);
        }

        public void SetLoaded(bool loaded)
        {
            _musketIcon.sprite = loaded ? _loaded : _empty;
            _musketIcon.color = _realMusket ? Color.white : loaded ? new Color(0.95f, 0.92f, 0.85f) : new Color(0.4f, 0.38f, 0.36f, 0.55f);
        }

        bool _matchPoint;

        // Match point (FIXES_4E §8.7): the name under the feet turns gold.
        public void SetMatchPoint(bool on)
        {
            if (on == _matchPoint || _name == null)
                return;
            _matchPoint = on;
            _name.Element.EnableInClassList("unit-name--match", on);
            _name.Element.style.color = on ? new Color(1f, 0.88f, 0.4f) : Color.Lerp(Color, Color.white, 0.35f);
        }

        public void SetCrowned(bool crowned) => _crown.enabled = crowned && _alive;

        public void SyncStats(Unit u, bool crowned)
        {
            SetHp(u.Hp);
            SetGrit(u.Grit);
            SetLoaded(u.Loaded);
            SetAlive(u.Alive, false);
            SetCrowned(crowned);
        }

        // Dead units stay where they fell, lying down and faded; a pit fall also shrinks them away.
        // `instant` (a Sync after playback) removes a dead unit from the board at once; otherwise a death lies
        // down for a beat, fades and then goes. Coming back alive (a new round) shows everything again.
        public void SetAlive(bool alive, bool fell, bool instant = true)
        {
            _alive = alive;
            if (alive)
                SetOnBoard(true);
            foreach (var h in _hearts) h.enabled = alive;
            foreach (var p in _pips) p.enabled = alive;
            _musketIcon.enabled = alive;
            _shadow.enabled = alive || !fell;
            if (!alive)
            {
                _shield.enabled = false;
                _crown.enabled = false;
                _select.enabled = false;
                _rig.SetRapier(false);
                _rig.ShowWeapons(false);
                _rig.SetPose(PoseKind.Hurt);
                _rig.SetTint(new Color(0.55f, 0.55f, 0.6f, 0.55f));
                _rig.transform.localRotation = Quaternion.Euler(0, 0, _rig.FacingRight ? 80f : -80f);
                _rig.transform.localPosition = new Vector3(_rig.FacingRight ? 0.32f : -0.32f, FeetY + 0.12f, 0f);
            }
            else
            {
                _rig.ShowWeapons(true);
                _rig.SetTint(Color.white);
                _rig.transform.localRotation = Quaternion.identity;
                _rig.transform.localPosition = new Vector3(0f, FeetY, 0f);
                if (_rig.Pose != PoseKind.Victory)
                    _rig.SetPose(PoseKind.Idle);
            }
            _bodyRoot.localScale = Vector3.one;
            _name.Element.style.opacity = 1f;
            if (alive)
                return;
            if (instant)
            {
                SetOnBoard(false);
                return;
            }
            _rig.SetTint(Color.white);
            _dying = true;
            Tween.Custom(this, 1f, 0f, 0.4f, (self, v) => self._rig.SetTint(new Color(1f, 1f, 1f, v)), Ease.InQuad, startDelay: 0.55f)
                .OnComplete(this, self => { self._dying = false; if (!self._alive) self.SetOnBoard(false); });
        }

        void SetOnBoard(bool on)
        {
            gameObject.SetActive(on);
            _name?.SetVisible(on);
        }

        public bool Alive => _alive;
        public bool IsDying => _dying;

        public void Flash(Color c, float duration)
        {
            if (_alive)
                _rig.Flash(c, duration);
        }
    }
}
