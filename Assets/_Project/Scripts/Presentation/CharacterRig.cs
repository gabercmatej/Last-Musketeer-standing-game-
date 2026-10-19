using System.Collections.Generic;
using Musketeers.Data;
using Musketeers.Sim;
using PrimeTween;
using UnityEngine;

namespace Musketeers.Presentation
{
    // A musketeer drawn as a paper doll (ART_GUIDE §0): body pose + cosmetics placed by CosmeticLayout,
    // plus the musket held at the hand anchor (aims, recoils) and the rapier for parries. The rig's
    // origin is the feet. Facing left mirrors the whole rig. Without art it falls back to a tinted circle.
    public sealed class CharacterRig : MonoBehaviour
    {
        const float MusketScale = 0.72f;
        const float RapierScale = 0.72f;
        const float RestAngle = -38f;      // musket held low, barrel forward and down

        ArtLibrary _art;
        CharacterDef _def;
        Transform _mirror;      // flips for facing
        Transform _breath;      // idle breathing squash (pivot = feet)
        SpriteRenderer _body;
        Transform _musketPivot;
        SpriteRenderer _musket;
        SpriteRenderer _rapier;
        readonly Dictionary<CosmeticSlot, SpriteRenderer> _items = new Dictionary<CosmeticSlot, SpriteRenderer>();
        readonly Dictionary<CosmeticSlot, CosmeticDef> _equipped = new Dictionary<CosmeticSlot, CosmeticDef>();
        readonly List<SpriteRenderer> _all = new List<SpriteRenderer>();
        Color _tint = Color.white;
        Color _placeholderColor = Color.white;
        bool _placeholder;
        bool _facingRight = true;
        float _aimAngle = RestAngle;
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
            _mirror = new GameObject("Mirror").transform;
            _mirror.SetParent(transform, false);
            _breath = new GameObject("Breath").transform;
            _breath.SetParent(_mirror, false);

            _placeholder = def == null || def.Body == null;
            _body = Add("Body", _breath, null, 0);
            foreach (var slot in Loadout.Slots)
                _items[slot] = Add(slot.ToString(), _breath, null, CosmeticDef.DefaultOrder(slot));

            _musketPivot = new GameObject("MusketPivot").transform;
            _musketPivot.SetParent(_breath, false);
            _musket = Add("Musket", _musketPivot, art.Sprite(GameArt.Musket, PlaceholderArt.Musket), 30);
            _musket.transform.localScale = Vector3.one * (art.Has(GameArt.Musket) ? MusketScale : 0.5f);
            _rapier = Add("Rapier", _musketPivot, art.Sprite(GameArt.Rapier, null), 31);
            _rapier.transform.localScale = Vector3.one * RapierScale;
            _rapier.enabled = false;

            SetLoadout(loadout);
            SetPose(PoseKind.Idle);
            StartBreathing();
        }

        SpriteRenderer Add(string name, Transform parent, Sprite sprite, int order)
        {
            var sr = _art.MakeRenderer(name, parent, sprite, Color.white, order);
            _all.Add(sr);
            return sr;
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

        void Layout()
        {
            if (_body == null)
                return;
            if (_placeholder)
            {
                _body.sprite = PlaceholderArt.Circle;
                _body.transform.localPosition = new Vector3(0f, 0.38f, 0f);
                _body.transform.localScale = Vector3.one * 0.74f;
                foreach (var sr in _items.Values)
                    sr.enabled = false;
                _musketPivot.localPosition = new Vector3(0.3f, 0.2f, 0f);
                ApplyTint();
                return;
            }
            var pose = _def.Pose(_pose);
            _body.sprite = pose.Sprite;
            _body.transform.localPosition = Vector3.zero;
            _body.transform.localScale = Vector3.one;
            foreach (var slot in Loadout.Slots)
            {
                var sr = _items[slot];
                var p = CosmeticLayout.Place(pose, _equipped[slot]);
                sr.enabled = p.Valid;
                if (!p.Valid)
                    continue;
                sr.sprite = p.Sprite;
                sr.sortingOrder = p.Order;
                sr.transform.localPosition = p.Position;
                sr.transform.localRotation = Quaternion.Euler(0, 0, p.Angle);
                sr.transform.localScale = new Vector3(p.Scale * p.ScaleX, p.Scale, 1f);
            }
            _musketPivot.localPosition = pose.Anchors.Hand;
            // The victory pose has both hands busy (fist up, hand on hip): no weapon.
            bool armed = !pose.Front;
            _musket.enabled = armed && !_rapier.enabled;
            if (!armed) _rapier.enabled = false;
            ApplyAim();
            ApplyTint();
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
            _aimAngle = RestAngle;
            ApplyAim();
        }

        void ApplyAim()
        {
            if (_musketPivot != null)
                _musketPivot.localRotation = Quaternion.Euler(0, 0, _rapier != null && _rapier.enabled ? 62f : _aimAngle);
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

        // Parry: rapier up instead of the musket.
        public void SetRapier(bool on)
        {
            if (_rapier == null)
                return;
            _rapier.enabled = on && _rapier.sprite != null && !_def.Pose(_pose).Front;
            _musket.enabled = !_rapier.enabled && !(_def != null && _def.Pose(_pose).Front);
            ApplyAim();
        }

        public void ShowWeapons(bool show)
        {
            if (_musketPivot != null)
                _musketPivot.gameObject.SetActive(show);
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
                sr.color = baseColor * _tint;
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
            _breathTween = Tween.Custom(0f, 1f, 1.4f, t =>
            {
                if (_breath == null) return;
                float s = Mathf.Sin((t + phase) * Mathf.PI * 2f);
                _breath.localScale = new Vector3(1f - s * 0.008f, 1f + s * 0.018f, 1f);
            }, Ease.Linear, -1);
        }
    }
}
