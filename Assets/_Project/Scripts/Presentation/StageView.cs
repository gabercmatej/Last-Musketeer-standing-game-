using Musketeers.Data;
using PrimeTween;
using UnityEngine;
using UnityEngine.Rendering;

namespace Musketeers.Presentation
{
    // The lobby and results "stage" (Among Us style): Matej's tavern background with the musketeers
    // standing on the floor, built from the same CharacterRig as the arena. It lives off to the side of
    // the board (x = 100) and the camera frames it when a menu screen needs it.
    public sealed class StageView : MonoBehaviour
    {
        public static readonly Vector3 Center = new Vector3(100f, 0f, 0f);
        public const float Width = 16f;
        public const float Height = 9f;
        // Feet on the stage floor of the lobby art (about 71% down the screen); the line-up is nudged right so the
        // game-master panel in the top-left corner never covers the first musketeer.
        const float FloorY = -1.9f;
        const float Scale = 2.15f;
        const float LineupShift = 1.6f;
        const float BackgroundFill = 1.15f;

        [SerializeField] private ArtLibrary _art;
        [SerializeField] private WorldLabels _labels;

        SpriteRenderer _bg, _bgFront;
        Sprite _bgSprite;
        Transform _root;
        readonly CharacterRig[] _rigs = new CharacterRig[4];
        readonly Transform[] _spots = new Transform[4];
        readonly WorldLabel[] _names = new WorldLabel[4];
        readonly WorldLabel[] _tags = new WorldLabel[4];
        readonly WorldLabel[] _status = new WorldLabel[4];
        readonly bool[] _hasStatus = new bool[4];
        SpriteRenderer _crown;
        int _count;

        public bool Visible => _root != null && _root.gameObject.activeSelf;

        void Ensure()
        {
            if (_root != null)
                return;
            _root = new GameObject("Stage").transform;
            _root.SetParent(transform, false);
            _root.localPosition = Center;
            _bg = _art.MakeRenderer("Background", _root, _art.Sprite(GameArt.LobbyBackground, null), Color.white, -100);
            _bgFront = _art.MakeRenderer("BackgroundFront", _root, null, new Color(1f, 1f, 1f, 0f), -99);
            _bgSprite = _bg.sprite;
            Cover(_bg);
        }

        // Cover-fit: the art fills the stage frame, cropping what does not fit.
        static void Cover(SpriteRenderer r)
        {
            if (r.sprite == null)
                return;
            var b = r.sprite.bounds.size;
            float k = Mathf.Max(Width * BackgroundFill / b.x, Height * BackgroundFill / b.y);
            r.transform.localScale = new Vector3(k, k, 1f);
        }

        // Swaps the stage background (a map's lobby art). fade > 0 crossfades: the new art fades in over the old.
        public void SetBackground(Sprite sprite, float fade)
        {
            Ensure();
            if (sprite == null || sprite == _bgSprite)
                return;
            _bgSprite = sprite;
            Tween.StopAll(_bgFront);
            if (fade <= 0f || !_root.gameObject.activeInHierarchy)
            {
                _bg.sprite = sprite;
                Cover(_bg);
                _bgFront.color = new Color(1f, 1f, 1f, 0f);
                return;
            }
            _bgFront.sprite = sprite;
            Cover(_bgFront);
            _bgFront.color = new Color(1f, 1f, 1f, 0f);
            Tween.Alpha(_bgFront, 1f, fade, Ease.InOutSine).OnComplete(() =>
            {
                _bg.sprite = sprite;
                Cover(_bg);
                _bgFront.color = new Color(1f, 1f, 1f, 0f);
            });
        }

        public void Hide()
        {
            if (_root != null)
                _root.gameObject.SetActive(false);
            for (int i = 0; i < 4; i++)
            {
                _names[i]?.SetVisible(false);
                _tags[i]?.SetVisible(false);
                _status[i]?.SetVisible(false);
            }
        }

        // Rebuilds the line-up: characters[i] wearing loadouts[i], named names[i]. Under the name goes either a
        // status line (ready[] given: "Ready" green / "Not ready" red, the lobby) or tags[i] (results points).
        public void Show(int count, int[] characters, string[] names, string[] tags, Loadout[] loadouts, bool[] ready = null)
        {
            Ensure();
            _root.gameObject.SetActive(true);
            _count = count;
            if (_crown != null)
                _crown.enabled = false;
            for (int i = 0; i < 4; i++)
            {
                if (_spots[i] != null)
                    Destroy(_spots[i].gameObject);
                _spots[i] = null;
                _rigs[i] = null;
                _names[i]?.SetVisible(false);
                _tags[i]?.SetVisible(false);
                _status[i]?.SetVisible(false);
                _hasStatus[i] = ready != null && i < ready.Length;
                if (i >= count)
                    continue;
                var spot = new GameObject("Spot " + i);
                spot.transform.SetParent(_root, false);
                var group = spot.AddComponent<SortingGroup>();
                group.sortingOrder = 10 + i;
                var shadow = _art.MakeRenderer("Shadow", spot.transform, _art.Sprite(GameArt.Shadow, PlaceholderArt.SoftCircle), new Color(0, 0, 0, 0.7f), -50);
                shadow.transform.localScale = _art.Has(GameArt.Shadow) ? new Vector3(1f, 1.7f, 1f) : new Vector3(0.75f, 0.22f, 1f);
                var rigGo = new GameObject("Rig");
                rigGo.transform.SetParent(spot.transform, false);
                var rig = rigGo.AddComponent<CharacterRig>();
                rig.Build(_art, _art.Character(characters[i]), loadouts[i], _art.CharacterColor(characters[i]));
                rig.SetFacingRight(i < count / 2.0f);
                rig.ShowWeapons(false);
                _spots[i] = spot.transform;
                _rigs[i] = rig;

                _names[i] = _names[i] ?? _labels.Create("", "stage-name");
                _names[i].Text = names[i];
                _names[i].Element.style.color = Color.Lerp(_art.CharacterColor(characters[i]), Color.white, 0.3f);
                _names[i].Follow = spot.transform;
                _tags[i] = _tags[i] ?? _labels.Create("", "stage-tag");
                _tags[i].Text = tags != null && i < tags.Length ? tags[i] : "";
                _tags[i].Follow = spot.transform;
                _status[i] = _status[i] ?? _labels.Create("", "stage-status");
                _status[i].Follow = spot.transform;
                if (_hasStatus[i])
                    SetStatus(i, ready[i]);
                Place(i, new Vector3(Spot(i, count), FloorY, 0f), Scale, true);
                SetLabelsVisible(i, true);
            }
        }

        // The lobby's Ready / Not ready line under a name (Bots are always ready).
        void SetStatus(int i, bool ready)
        {
            var l = _status[i];
            l.Text = ready ? "Ready" : "Not ready";
            l.Element.style.color = ready ? new Color(0.42f, 0.89f, 0.42f) : new Color(1f, 0.42f, 0.42f);
        }

        public void SetReady(bool[] ready)
        {
            for (int i = 0; i < _count && ready != null && i < ready.Length; i++)
            {
                if (_status[i] != null && _hasStatus[i])
                    SetStatus(i, ready[i]);
            }
        }

        static float Spot(int i, int count)
        {
            float spacing = count <= 2 ? 4.2f : count == 3 ? 3.4f : 2.95f;
            return (i - (count - 1) * 0.5f) * spacing + LineupShift;
        }

        void Place(int i, Vector3 local, float scale, bool instant)
        {
            var t = _spots[i];
            if (t == null)
                return;
            Tween.StopAll(t);
            if (instant)
            {
                t.localPosition = local;
                t.localScale = Vector3.one * scale;
            }
            else
            {
                Tween.LocalPosition(t, local, 0.35f, Ease.OutCubic);
                Tween.Scale(t, scale, 0.35f, Ease.OutCubic);
            }
            float top = 1.36f * scale;
            if (_names[i] != null) _names[i].Offset = new Vector3(0f, top + 0.45f, 0f);
            if (_tags[i] != null) _tags[i].Offset = new Vector3(0f, top + 0.02f, 0f);
            if (_status[i] != null) _status[i].Offset = new Vector3(0f, -0.34f, 0f);   // under the feet, on the plain floor
        }

        void SetLabelsVisible(int i, bool on)
        {
            _names[i]?.SetVisible(on);
            _tags[i]?.SetVisible(on && !_hasStatus[i] && !string.IsNullOrEmpty(_tags[i].Text));
            _status[i]?.SetVisible(on && _hasStatus[i]);
        }

        public void SetLoadout(int slot, Loadout loadout) => _rigs[Mathf.Clamp(slot, 0, 3)]?.SetLoadout(loadout);

        // Wardrobe open: the player's musketeer steps forward on the left, the others step back out of view.
        public void Focus(int slot)
        {
            for (int i = 0; i < _count; i++)
            {
                bool me = i == slot;
                if (_spots[i] == null) continue;
                _spots[i].gameObject.SetActive(me);
                SetLabelsVisible(i, me);
                _status[i]?.SetVisible(false);   // the Ready line is lobby-wide news, not for the fitting room
                if (me)
                {
                    Place(i, new Vector3(-3.2f, FloorY + 0.1f, 0f), 3.3f, false);
                    _rigs[i].SetFacingRight(true);
                }
            }
        }

        public void Unfocus()
        {
            for (int i = 0; i < _count; i++)
            {
                if (_spots[i] == null) continue;
                _spots[i].gameObject.SetActive(true);
                SetLabelsVisible(i, true);
                Place(i, new Vector3(Spot(i, _count), FloorY, 0f), Scale, false);
                _rigs[i].SetFacingRight(i < _count / 2.0f);
            }
        }

        // A quick hop when an item is equipped or a character picked.
        public void Bounce(int slot)
        {
            var r = slot >= 0 && slot < 4 ? _rigs[slot] : null;
            if (r == null) return;
            Tween.StopAll(r.transform);
            r.transform.localPosition = Vector3.zero;
            Tween.LocalPositionY(r.transform, 0.18f, 0.12f, Ease.OutQuad, 2, CycleMode.Yoyo);
        }

        // Results: the winner big in the victory pose wearing the Crown; everyone else smaller, wincing.
        public void ShowPodium(int winner)
        {
            if (_root == null)
                return;
            int side = 0;
            for (int i = 0; i < _count; i++)
            {
                if (_spots[i] == null) continue;
                bool win = i == winner;
                _rigs[i].SetPose(win ? PoseKind.Victory : PoseKind.Hurt);
                var group = _spots[i].GetComponent<SortingGroup>();
                group.sortingOrder = win ? 30 : 10 + i;
                if (win)
                    Place(i, new Vector3(-3.8f, FloorY - 0.45f, 0f), 2.7f, true);
                else
                {
                    // Losers flank the winner, the third one a step behind (the results card covers x > ~1).
                    float x = side == 0 ? -6.8f : side == 1 ? -0.9f : -5.5f;
                    float y = side == 2 ? FloorY + 0.9f : FloorY + 0.35f;
                    Place(i, new Vector3(x, y, 0f), side == 2 ? 1.7f : 2f, true);
                    _rigs[i].SetFacingRight(x < -3.8f);
                    if (side == 2) group.sortingOrder = 5;
                    side++;
                }
            }
            if (winner >= 0 && winner < _count && _spots[winner] != null)
            {
                if (_crown == null)
                    _crown = _art.MakeRenderer("Crown", _root, _art.CrownSprite, _art.Has(GameArt.Crown) ? Color.white : new Color(1f, 0.82f, 0.2f), 60);
                _crown.enabled = true;
                _crown.transform.SetParent(_spots[winner], false);
                _crown.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                _crown.transform.localScale = Vector3.one * 0.3f;
                Tween.StopAll(_crown.transform);
                Tween.LocalPositionY(_crown.transform, 1.55f, 0.8f, Ease.InOutSine, -1, CycleMode.Yoyo);
                // The winner's name and points float above the Crown.
                float above = 1.75f * _spots[winner].localScale.y;
                if (_names[winner] != null) _names[winner].Offset = new Vector3(0f, above + 0.35f, 0f);
                if (_tags[winner] != null) _tags[winner].Offset = new Vector3(0f, above + 0.02f, 0f);
            }
        }
    }
}
