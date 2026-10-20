using System;
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
        public event Action QuitClicked;
        public event Action SettingsClicked;

        readonly VisualElement _root;
        readonly Label _round, _turn, _timer, _phase, _bannerText, _devBadge;
        readonly VisualElement _banner;
        readonly VisualElement _scores;
        readonly Button _speed;
        readonly VisualElement _gritBox, _specialSlot;
        readonly VisualElement _topStrip, _topLeft, _leftCol, _rightCol, _bottomBar;
        CameraRig _rig;
        VisualElement[] _scoreEls = new VisualElement[0];
        Label[] _points = new Label[0];
        VisualElement[] _crowns = new VisualElement[0];
        VisualElement[] _locks = new VisualElement[0];
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
            // Speed and Log are debugging aids: only in the Editor and development builds (L still opens the log).
            bool dev = Debug.isDebugBuild || Application.isEditor;
            hudRoot.Q("dev-buttons").style.display = dev ? DisplayStyle.Flex : DisplayStyle.None;
            _root.RegisterCallback<GeometryChangedEvent>(_ => ApplyLayout());
            var log = hudRoot.Q<Button>("log-toggle");
            var quit = hudRoot.Q<Button>("quit");
            var settings = hudRoot.Q<Button>("hud-settings");
            foreach (var b in new[] { _speed, log, quit, settings })
                b.focusable = false;
            quit.AddToClassList("btn--back");
            _speed.clicked += () => SpeedClicked?.Invoke();
            log.clicked += () => LogClicked?.Invoke();
            quit.clicked += () => QuitClicked?.Invoke();
            settings.clicked += () => SettingsClicked?.Invoke();
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
            var r = HudLayout.Compute(_rig.ArenaPanelRect(w, h), w, h);
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
                name.style.color = Color.Lerp(colors[i], Color.white, 0.3f);
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
                _scores.Add(el);
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

        public void SetScores(int[] points, int crowned, GameState round, int lockMask, bool planning)
        {
            for (int i = 0; i < _scoreEls.Length && i < points.Length; i++)
            {
                _points[i].text = points[i].ToString();
                _crowns[i].style.display = crowned == i ? DisplayStyle.Flex : DisplayStyle.None;
                bool alive = round != null && i < round.Units.Length && round.Units[i].Alive;
                _scoreEls[i].EnableInClassList("score--dead", !alive);
                _locks[i].style.display = planning && alive && (lockMask & (1 << i)) != 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        public void SetRound(int round, bool suddenDeath, int turn)
        {
            _round.text = suddenDeath ? $"Round {round}: SUDDEN DEATH" : $"Round {round}";
            _turn.text = $"Turn {turn}";
        }

        public void SetTimer(float seconds, bool show)
        {
            _timer.style.visibility = show ? Visibility.Visible : Visibility.Hidden;
            int s = Mathf.CeilToInt(seconds);
            _timer.text = s.ToString();
            _timer.EnableInClassList("timer--low", s <= 5);
        }

        public void SetPhase(string text) => _phase.text = text;

        public void SetSpeed(float speed) => _speed.text = $"Speed {speed:0.#}x";

        public void SetDevBadge(bool on) => _devBadge.style.display = on && (Debug.isDebugBuild || Application.isEditor) ? DisplayStyle.Flex : DisplayStyle.None;

        // The announcer ribbon: pops in, holds, fades.
        public void Banner(string text, Color color, float seconds)
        {
            _bannerTween.Stop();
            _bannerText.text = text;
            _bannerText.style.color = color;
            _banner.style.opacity = 1f;
            _banner.style.scale = new Scale(new Vector3(0.6f, 0.6f, 1f));
            _bannerTween = Tween.Custom(0f, 1f, 0.22f, v => _banner.style.scale = new Scale(Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, v)), Ease.OutBack)
                .Chain(Tween.Custom(1f, 0f, 0.45f, v => _banner.style.opacity = v, startDelay: seconds));
        }
    }
}
