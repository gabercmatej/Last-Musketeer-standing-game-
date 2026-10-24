using System;
using Musketeers.Game;
using Musketeers.Presentation;
using Musketeers.Sim;
using PrimeTween;
using UnityEngine;
using UnityEngine.UIElements;

namespace Musketeers.UI
{
    // Top bar (round, turn, timer, phase, scoreboard strip with points/crown/lock) plus the corner
    // buttons, the dev badge and a centre banner.
    public sealed class HudUI
    {
        public event Action SpeedClicked;
        public event Action LogClicked;
        public event Action LeaveClicked;
        public event Action SettingsClicked;

        readonly VisualElement _root;
        readonly Label _round, _turn, _timer, _phase, _bannerText, _devBadge;
        readonly VisualElement _banner;
        readonly VisualElement _scores;
        readonly Button _speed;
        readonly VisualElement _gritBox, _specialSlot;
        readonly VisualElement _topStrip, _topLeft, _leftCol, _rightCol, _bottomBar;
        CameraRig _rig;
        Rect _arena;
        HudLayout.Regions _regions;
        bool _hasRegions;
        float _panelW;
        VisualElement[] _scoreEls = new VisualElement[0];
        Label[] _points = new Label[0];
        VisualElement[] _crowns = new VisualElement[0];
        VisualElement[] _locks = new VisualElement[0];
        Label[] _names = new Label[0];
        Label[] _pops = new Label[0];
        bool[] _matchPoint = new bool[0];
        Color[] _nameColors = new Color[0];
        Tween[] _popTweens = new Tween[0];
        Sequence _bannerTween;

        public HudUI(VisualElement hudRoot)
        {
            _root = hudRoot.Q("hud");
            _round = hudRoot.Q<Label>("round-label");
            _turn = hudRoot.Q<Label>("turn-label");
            _timer = hudRoot.Q<Label>("timer");
            _phase = hudRoot.Q<Label>("phase-label");
            _scores = hudRoot.Q("scores");
            _banner = hudRoot.Q("banner");
            _bannerText = hudRoot.Q<Label>("banner-text");
            _devBadge = hudRoot.Q<Label>("dev-badge");
            _speed = hudRoot.Q<Button>("speed");
            _topStrip = hudRoot.Q("top-strip");
            _topLeft = hudRoot.Q("top-left");
            _leftCol = hudRoot.Q("left-col");
            _rightCol = hudRoot.Q("right-col");
            _bottomBar = hudRoot.Q("bottom-bar");
            _gritBox = hudRoot.Q("grit-box");
            _specialSlot = hudRoot.Q("special-slot");
            // Speed, Log, the dev badge and the L key are debugging aids: only in the Editor and development builds.
            hudRoot.Q("dev-buttons").style.display = DevFlags.Available ? DisplayStyle.Flex : DisplayStyle.None;
            _root.RegisterCallback<GeometryChangedEvent>(_ => ApplyLayout());
            var log = hudRoot.Q<Button>("log-toggle");
            foreach (var b in new[] { _speed, log })
                b.focusable = false;
            _speed.clicked += () => SpeedClicked?.Invoke();
            log.clicked += () => LogClicked?.Invoke();
            // Matej's icon buttons, same look as the menu's corner buttons.
            var icons = hudRoot.Q("hud-icons");
            // Same trio, order and size as the lobby (FIXES_4E §4.1): Fullscreen, Settings, Leave.
            CornerButtons.AddTopLeftSet(icons, () => SettingsClicked?.Invoke(), () => LeaveClicked?.Invoke(), "Leave", "Leave the match");
        }

        public void SetVisible(bool visible)
        {
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible)
                _root.schedule.Execute(ApplyLayout).ExecuteLater(0);
        }

        // The HUD lives around the arena, never on it: the camera reports where the board sits and the columns
        // and strips are placed in the margins (HudLayout; narrow screens shrink and then overlap the board edge).
        public void BindArena(CameraRig rig)
        {
            _rig = rig;
            rig.ArenaChanged += ApplyLayout;
        }

        public void ApplyLayout()
        {
            if (_rig == null)
                return;
            float w = _root.resolvedStyle.width, h = _root.resolvedStyle.height;
            if (float.IsNaN(w) || float.IsNaN(h) || w < 50f || h < 50f)
                return;
            _arena = _rig.ArenaPanelRect(w, h);
            var r = HudLayout.Compute(_arena, w, h);
            _regions = r;
            _hasRegions = true;
            _panelW = w;
            Place(_topStrip, r.Top);
            Place(_bottomBar, r.Bottom);
            Place(_leftCol, r.Left);
            Place(_rightCol, r.Right);
            _topLeft.style.left = r.RoundX;
            // Corner widgets under the columns: Grit bottom-left, the Signature button bottom-right.
            _gritBox.style.marginLeft = r.Left.x;
            _gritBox.style.width = r.Left.width;
            _specialSlot.style.marginRight = HudLayout.Pad;
            _specialSlot.style.width = r.Right.width;
            _root.EnableInClassList("hud--compact", r.Compact);
            _root.EnableInClassList("hud--overlap", r.Overlap);
        }

        static void Place(VisualElement e, Rect r)
        {
            e.style.left = r.x;
            e.style.top = r.y;
            e.style.width = r.width;
            e.style.height = r.height;
        }

        public void BuildScores(string[] names, Color[] colors, Sprite[] portraits = null)
        {
            _scores.Clear();
            int n = names.Length;
            _scoreEls = new VisualElement[n];
            _points = new Label[n];
            _crowns = new VisualElement[n];
            _locks = new VisualElement[n];
            _names = new Label[n];
            _pops = new Label[n];
            _matchPoint = new bool[n];
            _nameColors = new Color[n];
            _popTweens = new Tween[n];
            for (int i = 0; i < n; i++)
            {
                var el = new VisualElement { pickingMode = PickingMode.Ignore };
                el.AddToClassList("score");
                el.style.borderLeftColor = colors[i];
                el.style.borderRightColor = colors[i];
                el.style.borderTopColor = colors[i];
                el.style.borderBottomColor = colors[i];
                var portrait = UiArt.Image("score-portrait", portraits != null && i < portraits.Length ? portraits[i] : null, colors[i]);
                el.Add(portrait);
                var name = new Label(names[i]);
                name.AddToClassList("score-name");
                _nameColors[i] = Color.Lerp(colors[i], Color.white, 0.3f);
                name.style.color = _nameColors[i];
                var pts = new Label("0");
                pts.AddToClassList("score-points");
                var crownArt = UiArt.Get(Musketeers.Data.GameArt.CrownSmall);
                var lockArt = UiArt.Get(Musketeers.Data.GameArt.LockIn);
                var crown = Icon(crownArt != null ? crownArt : PlaceholderArt.Crown, crownArt != null ? Color.white : new Color(1f, 0.82f, 0.2f));
                var lockIcon = Icon(lockArt != null ? lockArt : PlaceholderArt.Check, lockArt != null ? Color.white : new Color(0.5f, 1f, 0.6f));
                el.Add(name);
                el.Add(pts);
                el.Add(crown);
                el.Add(lockIcon);
                var pop = new Label { pickingMode = PickingMode.Ignore };
                pop.AddToClassList("score-pop");
                el.Add(pop);
                _scores.Add(el);
                _names[i] = name;
                _pops[i] = pop;
                _scoreEls[i] = el;
                _points[i] = pts;
                _crowns[i] = crown;
                _locks[i] = lockIcon;
            }
        }

        static VisualElement Icon(Sprite s, Color tint)
        {
            var v = new VisualElement { pickingMode = PickingMode.Ignore };
            v.AddToClassList("score-icon");
            v.style.backgroundImage = new StyleBackground(s);
            v.style.unityBackgroundImageTintColor = tint;
            v.style.display = DisplayStyle.None;
            return v;
        }

        // matchPoint[i]: that player holds match point (FIXES_4E §8.7): gold border and name.
        // Called every frame: only touch a label when its number changed (4F §5, no per-frame string garbage).
        readonly int[] _shownPoints = { -1, -1, -1, -1, -1, -1, -1, -1 };
        int _shownTimer = -1;

        public void SetScores(int[] points, int crowned, GameState round, int lockMask, bool planning, bool[] matchPoint = null)
        {
            for (int i = 0; i < _scoreEls.Length && i < points.Length; i++)
            {
                if (i >= _shownPoints.Length || _shownPoints[i] != points[i])
                {
                    if (i < _shownPoints.Length)
                        _shownPoints[i] = points[i];
                    _points[i].text = points[i].ToString();
                }
                bool mp = matchPoint != null && i < matchPoint.Length && matchPoint[i];
                if (mp != _matchPoint[i])
                {
                    _matchPoint[i] = mp;
                    _scoreEls[i].EnableInClassList("score--match", mp);
                    _names[i].EnableInClassList("score-name--match", mp);
                    _names[i].style.color = mp ? new Color(1f, 0.88f, 0.4f) : _nameColors[i];
                }
                _crowns[i].style.display = crowned == i ? DisplayStyle.Flex : DisplayStyle.None;
                bool alive = round != null && i < round.Units.Length && round.Units[i].Alive;
                _scoreEls[i].EnableInClassList("score--dead", !alive);
                _locks[i].style.display = planning && alive && (lockMask & (1 << i)) != 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        // "+1" / "+2" pop on a score box the moment a kill is scored (FIXES_4E §8.6); the number itself follows via SetScores.
        public void PopScore(int slot, int amount)
        {
            if (slot < 0 || slot >= _pops.Length)
                return;
            _popTweens[slot].Stop();
            var pop = _pops[slot];
            pop.text = (amount > 0 ? "+" : "") + amount;
            var pts = _points[slot];
            _popTweens[slot] = Tween.Custom(0f, 1f, 0.9f, t =>
            {
                pop.style.opacity = t < 0.15f ? t / 0.15f : Mathf.Clamp01(1f - (t - 0.55f) / 0.45f);
                pop.style.translate = new Translate(0f, -14f * t);
                float bump = 1f + 0.5f * Mathf.Sin(Mathf.Clamp01(t * 2.2f) * Mathf.PI);
                pts.style.scale = new Scale(new Vector3(bump, bump, 1f));
            }, Ease.OutQuad).OnComplete(() =>
            {
                pop.style.opacity = 0f;
                pts.style.scale = new Scale(Vector3.one);
            });
        }

        public void SetRound(int round, int turn)
        {
            _round.text = $"Round {round}";
            _turn.text = $"Turn {turn}";
        }

        public void SetTimer(float seconds, bool show)
        {
            _timer.style.visibility = show ? Visibility.Visible : Visibility.Hidden;
            bool urgent = show && seconds > 0f && seconds <= 5f;
            float pulse = urgent ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f) : 0f;
            _timer.style.scale = new Scale(Vector3.one * (1f + pulse * 0.07f));
            _timer.style.color = urgent ? Color.Lerp(new Color(1f, 0.22f, 0.2f), Color.white, pulse * 0.35f) : StyleKeyword.Null;
            int s = Mathf.CeilToInt(seconds);
            if (s == _shownTimer)
                return;
            _shownTimer = s;
            _timer.text = s.ToString();
            _timer.EnableInClassList("timer--low", s <= 5);
        }

        public void SetPhase(string text) => _phase.text = text;

        public void SetSpeed(float speed) => _speed.text = $"Speed {speed:0.#}x";

        public void SetDevBadge(bool on) => _devBadge.style.display = on && DevFlags.Available ? DisplayStyle.Flex : DisplayStyle.None;

        // The announcer ribbon: small, in the top HUD strip beside the timer, never over the board (HudLayout.BannerRect).
        // big = the round-start version (EN GARDE!, DRAW!), a little larger. Pops in, holds, fades.
        public void Banner(string text, Color color, float seconds, bool big = false)
        {
            _bannerTween.Stop();
            float wantH = big ? 56f : 46f, wantW = big ? 340f : 300f;
            Rect rect = _hasRegions
                ? HudLayout.BannerRect(_arena, _regions, _panelW, wantH, wantW)
                : new Rect(640f + HudLayout.TimerClear, 2f, wantW, wantH);
            Place(_banner, rect);
            float font = Mathf.Min(big ? 34f : 28f, rect.height * 0.62f, rect.width * 0.9f / (Mathf.Max(1, text.Length) * 0.5f));
            _bannerText.style.fontSize = Mathf.Max(10f, font);
            _bannerText.text = text;
            _bannerText.style.color = color;
            _banner.style.opacity = 1f;
            _banner.style.scale = new Scale(new Vector3(0.7f, 0.7f, 1f));
            _bannerTween = Tween.Custom(0f, 1f, 0.2f, v => _banner.style.scale = new Scale(Vector3.one * Mathf.LerpUnclamped(0.7f, 1f, v)), Ease.OutBack)
                .Chain(Tween.Custom(1f, 0f, 0.45f, v => _banner.style.opacity = v, startDelay: seconds));
        }
    }
}
