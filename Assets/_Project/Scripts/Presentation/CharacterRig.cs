using System.Collections.Generic;
using Musketeers.Data;
using Musketeers.Sim;
using PrimeTween;
using UnityEngine;
using UnityEngine.Rendering;

namespace Musketeers.Presentation
{
    // A musketeer drawn as a paper doll (ART_GUIDE §0, FIXES_v0.2 §1.2): body pose plus cosmetics placed
    // by CosmeticLayout, one boot drawn at each foot, the musket in the back hand (behind the body, aims
    // and recoils) and the rapier in the front hand with the hand redrawn over its grip. The rig's origin
    // is the feet; facing left mirrors the whole rig. Without art it falls back to a tinted circle.
    //
    // Masks: equipped footwear hides the body's own boots (SpriteMasks from the pose's boot polygons),
    // and the front-hand overlay is the body sprite shown only inside the hand polygon. SpriteMasks ignore
    // SortingGroups, so every rig sorts its renderers inside its own group from a unique order base and
    // each mask's custom range covers just its own target renderer.
    public sealed class CharacterRig : MonoBehaviour
    {
        const float RaisedAngle = 62f;    // rapier up for a parry
        const float BreathX = 0.008f / 3f; // FIXES_v0.2 §1.3: a third of the Day 4 breathing
        const float BreathY = 0.018f / 3f;
        const int BaseSlots = 30;

        static int _nextBase;

        ArtLibrary _art;
        CharacterDef _def;
        int _base;
        Transform _mirror;      // flips for facing
        Transform _breath;      // idle breathing squash (pivot = feet)
        SpriteRenderer _body;
        SpriteRenderer _hand;   // front-hand overlay (body sprite inside the hand mask)
        SpriteMask _handMask;
        SpriteMask _bootMaskBack, _bootMaskFront;
        Transform _musketPivot;
        SpriteRenderer _musket;
        SpriteRenderer _rapier;
        readonly Dictionary<RigPart, SpriteRenderer> _parts = new Dictionary<RigPart, SpriteRenderer>();
        readonly Dictionary<CosmeticSlot, CosmeticDef> _equipped = new Dictionary<CosmeticSlot, CosmeticDef>();
        readonly List<SpriteRenderer> _all = new List<SpriteRenderer>();
        readonly Dictionary<SpriteRenderer, float> _shade = new Dictionary<SpriteRenderer, float>();
        Color _tint = Color.white;
        Color _placeholderColor = Color.white;
        bool _placeholder;
        bool _facingRight = true;
        bool _weapons = true;
        bool _raised;
        bool _breathB;          // showing the IdleB breathing frame
        float? _aimAngle;       // null = resting at the hand anchor's angle
        PoseKind _pose = PoseKind.Idle;
        Tween _poseTimer;
        Tween _breathTween;

        public PoseKind Pose => _pose;
        public bool FacingRight => _facingRight;
        public Transform Mirror => _mirror;

        public void Build(ArtLibrary art, CharacterDef def, Loadout loadout, Color color)
        {
            _art = art;
            _def = def;
            _placeholderColor = color;
            _base = -15000 + (_nextBase++ % BaseSlots) * 1000;
            var group = gameObject.GetComponent<SortingGroup>();
            if (group == null)
                group = gameObject.AddComponent<SortingGroup>();
            group.sortingOrder = 0;
            _mirror = new GameObject("Mirror").transform;
            _mirror.SetParent(transform, false);
            _breath = new GameObject("Breath").transform;
            _breath.SetParent(_mirror, false);

            _placeholder = def == null || def.Body == null;
            _body = Add("Body", _breath, null, RigPart.Body);
            _parts[RigPart.Cape] = Add("Cape", _breath, null, RigPart.Cape);
            _parts[RigPart.BootBack] = Add("BootBack", _breath, null, RigPart.BootBack);
            _parts[RigPart.BootFront] = Add("BootFront", _breath, null, RigPart.BootFront);
            _parts[RigPart.Face] = Add("Face", _breath, null, RigPart.Face);
            _parts[RigPart.Hat] = Add("Hat", _breath, null, RigPart.Hat);

            _musketPivot = new GameObject("MusketPivot").transform;
            _musketPivot.SetParent(_breath, false);
            _musket = Add("Musket", _musketPivot, art.Sprite(GameArt.Musket, PlaceholderArt.Musket), RigPart.Musket);
            _rapier = Add("Rapier", _breath, art.Sprite(GameArt.Rapier, null), RigPart.Rapier);
            _hand = Add("FrontHand", _breath, null, RigPart.Hand);
            _hand.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            _handMask = Mask("HandMask", Order(RigPart.Hand));
            _bootMaskBack = Mask("BootMaskBack", Order(RigPart.Body));
            _bootMaskFront = Mask("BootMaskFront", Order(RigPart.Body));

            SetLoadout(loadout);
            SetPose(PoseKind.Idle);
            StartBreathing();
        }

        WeaponDef _musketDef, _rapierDef;

        int Order(RigPart part) => _base + CosmeticLayout.Order(part);

        SpriteRenderer Add(string name, Transform parent, Sprite sprite, RigPart part)
        {
            var sr = _art.MakeRenderer(name, parent, sprite, Color.white, Order(part));
            _all.Add(sr);
            _shade[sr] = 1f;
            return sr;
        }

        // A mask that only affects renderers sorted exactly at `order` (this rig's own target).
        SpriteMask Mask(string name, int order)
        {
            var m = new GameObject(name).AddComponent<SpriteMask>();
            m.transform.SetParent(_breath, false);
            m.isCustomRangeActive = true;
            m.frontSortingOrder = order;
            m.backSortingOrder = order - 1;
            m.alphaCutoff = 0.5f;
            m.enabled = false;
            return m;
        }

        void OnDestroy()
        {
            _poseTimer.Stop();
            _breathTween.Stop();
            if (_breath != null) Tween.StopAll(_breath);
            if (_musketPivot != null) Tween.StopAll(_musketPivot);
            if (_musket != null) Tween.StopAll(_musket.transform);
        }

        public void SetLoadout(Loadout loadout)
        {
            var catalog = _art.Cosmetics;
            foreach (var slot in Loadout.Slots)
            {
                var item = catalog != null ? catalog.Get(loadout.Get(slot)) : null;
                _equipped[slot] = item != null && item.Slot == slot ? item : null;
            }
            Layout();
        }

        public void SetPose(PoseKind pose)
        {
            _poseTimer.Stop();
            _pose = pose;
            Layout();
        }

        // A pose for a moment (hurt, lunge), then back to idle — unless another pose was set meanwhile.
        public void FlashPose(PoseKind pose, float seconds)
        {
            SetPose(pose);
            var mine = pose;
            _poseTimer = Tween.Delay(this, Mathf.Max(0.05f, seconds), r => { if (r._pose == mine) r.SetPose(PoseKind.Idle); });
        }

        // The pose actually drawn: Idle alternates with IdleB while breathing, when that art exists.
        PoseKind Drawn => _pose == PoseKind.Idle && _breathB && _def.HasPose(PoseKind.IdleB) ? PoseKind.IdleB : _def.Resolve(_pose);

        void Layout()
        {
            if (_body == null)
                return;
            if (_placeholder)
            {
                _body.sprite = PlaceholderArt.Circle;
                _body.transform.localPosition = new Vector3(0f, 0.38f, 0f);
                _body.transform.localScale = Vector3.one * 0.74f;
                foreach (var sr in _parts.Values)
                    sr.enabled = false;
                _hand.enabled = false;
                _rapier.enabled = false;
                _musket.enabled = _weapons;
                _musket.transform.localScale = Vector3.one * 0.5f;
                _musketPivot.localPosition = new Vector3(0.3f, 0.2f, 0f);
                _musketPivot.localRotation = Quaternion.Euler(0, 0, _aimAngle ?? -38f);
                ApplyTint();
                return;
            }
            var kind = Drawn;
            var art = _def.Art(kind);
            var fit = _def.FitFor(kind);
            _body.sprite = art.Sprite;

            Item(RigPart.Cape, art, fit, kind, CosmeticSlot.Cape, false);
            Item(RigPart.Face, art, fit, kind, CosmeticSlot.Face, false);
            Item(RigPart.Hat, art, fit, kind, CosmeticSlot.Hat, false);
            bool back = Item(RigPart.BootBack, art, fit, kind, CosmeticSlot.Boots, true);
            bool front = Item(RigPart.BootFront, art, fit, kind, CosmeticSlot.Boots, false);

            // Footwear on: hide the body's own boots so the new ones never sit on top of them.
            bool masked = SetMask(_bootMaskBack, back || front ? fit.BootPathBack : null);
            masked |= SetMask(_bootMaskFront, back || front ? fit.BootPathFront : null);
            _body.maskInteraction = masked ? SpriteMaskInteraction.VisibleOutsideMask : SpriteMaskInteraction.None;

            // Musket: back hand, behind the body (its own hand covers the grip).
            // Temporary weapon defs (identity fit) until the runtime switches to WeaponDef assets.
            if (_musketDef == null) _musketDef = WeaponDef.Temporary(null, true);
            if (_rapierDef == null) _rapierDef = WeaponDef.Temporary(null, false);
            _musketDef.Sprite = _musket.sprite;
            _rapierDef.Sprite = _rapier.sprite;
            var m = CosmeticLayout.Weapon(fit, _musketDef, _def.Id, kind, false);
            _musket.enabled = _weapons && m.Valid;
            if (m.Valid)
            {
                _musketPivot.localPosition = m.Position;
                _musket.transform.localScale = new Vector3(m.Scale.x, m.Scale.y, 1f);
            }
            // Rapier: front hand, in front of the body, with the hand overlay over its grip.
            var r = CosmeticLayout.Weapon(fit, _rapierDef, _def.Id, kind, false, _raised ? RaisedAngle - fit.HandFront.Rotation : 0f);
            _rapier.enabled = _weapons && r.Valid;
            if (r.Valid)
                Put(_rapier, r);
            _hand.sprite = art.Sprite;
            bool hand = _rapier.enabled && SetMask(_handMask, fit.HandPath);
            if (!_rapier.enabled)
                SetMask(_handMask, null);
            _hand.enabled = hand;
            ApplyAim();
            ApplyTint();
        }

        bool Item(RigPart part, PoseArt art, PoseFit fit, PoseKind kind, CosmeticSlot slot, bool backFoot)
        {
            var sr = _parts[part];
            var p = CosmeticLayout.Place(art, fit, _def.Id, kind, _equipped[slot], backFoot);
            sr.enabled = p.Valid;
            _shade[sr] = p.Valid ? p.Shade : 1f;
            if (p.Valid)
                Put(sr, p);
            return p.Valid;
        }

        void Put(SpriteRenderer sr, LayerPlacement p)
        {
            sr.sprite = p.Sprite;
            sr.sortingOrder = _base + p.Order;
            sr.transform.localPosition = p.Position;
            sr.transform.localRotation = Quaternion.Euler(0, 0, p.Angle);
            sr.transform.localScale = new Vector3(p.Scale.x, p.Scale.y, 1f);
        }

        static bool SetMask(SpriteMask mask, BezierPath poly)
        {
            var sprite = PolygonMask.Sprite(poly, out var origin);
            mask.enabled = sprite != null;
            if (sprite != null)
            {
                mask.sprite = sprite;
                mask.transform.localPosition = origin;
            }
            return sprite != null;
        }

        // ---------- facing and weapons ----------

        public void SetFacingRight(bool right)
        {
            _facingRight = right;
            _mirror.localScale = new Vector3(right ? 1f : -1f, 1f, 1f);
        }

        // East/west turn the body; north/south keep it and tilt the musket up or down.
        public void Face(Dir d)
        {
            if (d == Dir.E) SetFacingRight(true);
            else if (d == Dir.W) SetFacingRight(false);
        }

        // Points the musket along a board direction (in the rig's mirrored space).
        public void Aim(Dir d)
        {
            Face(d);
            _aimAngle = d == Dir.N ? 80f : d == Dir.S ? -80f : 0f;
            ApplyAim();
        }

        public void Rest()
        {
            _aimAngle = null;
            ApplyAim();
        }

        void ApplyAim()
        {
            if (_musketPivot == null || _placeholder)
                return;
            var fit = _def.FitFor(Drawn);
            _musketPivot.localRotation = Quaternion.Euler(0, 0, _aimAngle ?? fit.HandBack.Rotation);
        }

        public void Recoil(float duration)
        {
            if (_musket == null)
                return;
            var t = _musket.transform;
            Tween.StopAll(t);
            t.localPosition = Vector3.zero;
            Tween.LocalPosition(t, new Vector3(-0.12f, 0.02f, 0f), duration * 0.3f, Ease.OutQuad)
                .Chain(Tween.LocalPosition(t, Vector3.zero, duration * 0.7f, Ease.OutBack));
        }

        // Parry: the rapier comes up (the musket stays in the back hand).
        public void SetRapier(bool on)
        {
            _raised = on;
            Layout();
        }

        public void ShowWeapons(bool show)
        {
            _weapons = show;
            Layout();
        }

        // ---------- look ----------

        public void SetTint(Color c)
        {
            _tint = c;
            ApplyTint();
        }

        public Color Tint => _tint;

        void ApplyTint()
        {
            foreach (var sr in _all)
            {
                if (sr == null)
                    continue;
                var baseColor = _placeholder && sr == _body ? _placeholderColor : Color.white;
                float shade = _shade.TryGetValue(sr, out var s) ? s : 1f;
                var c = baseColor * _tint;
                sr.color = new Color(c.r * shade, c.g * shade, c.b * shade, c.a);
            }
        }

        // Briefly tints the whole rig (hit flash) and returns to the current tint.
        public void Flash(Color c, float duration)
        {
            var back = _tint;
            Tween.Custom(0f, 1f, Mathf.Max(0.02f, duration), t =>
            {
                if (this == null) return;
                _tint = Color.Lerp(c, back, t);
                ApplyTint();
            }, Ease.OutQuad);
        }

        void StartBreathing()
        {
            _breathTween.Stop();
            _breath.localScale = Vector3.one;
            float phase = Random.value;
            bool hasB = _def != null && _def.HasPose(PoseKind.IdleB);
            _breathTween = Tween.Custom(0f, 1f, 1.4f, t =>
            {
                if (_breath == null) return;
                float s = Mathf.Sin((t + phase) * Mathf.PI * 2f);
                _breath.localScale = new Vector3(1f - s * BreathX, 1f + s * BreathY, 1f);
                if (hasB && (s < 0f) != _breathB)
                {
                    _breathB = s < 0f;
                    if (_pose == PoseKind.Idle)
                        Layout();
                }
            }, Ease.Linear, -1);
        }
    }
}
