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

        readonly VisualElement _root;
        readonly Label _round, _turn, _timer, _phase, _banner, _devBadge;
        readonly VisualElement _scores;
        readonly Button _speed;
        VisualElement[] _scoreEls = new VisualElement[0];
        Label[] _points = new Label[0];
        VisualElement[] _crowns = new VisualElement[0];
        VisualElement[] _locks = new VisualElement[0];
        Tween _bannerTween;

        public HudUI(VisualElement hudRoot)
        {
            _root = hudRoot.Q("hud");
            _round = hudRoot.Q<Label>("round-label");
            _turn = hudRoot.Q<Label>("turn-label");
            _timer = hudRoot.Q<Label>("timer");
            _phase = hudRoot.Q<Label>("phase-label");
            _scores = hudRoot.Q("scores");
            _banner = hudRoot.Q<Label>("banner");
            _devBadge = hudRoot.Q<Label>("dev-badge");
            _speed = hudRoot.Q<Button>("speed");
            var log = hudRoot.Q<Button>("log-toggle");
            var quit = hudRoot.Q<Button>("quit");
            foreach (var b in new[] { _speed, log, quit })
                b.focusable = false;
            _speed.clicked += () => SpeedClicked?.Invoke();
            log.clicked += () => LogClicked?.Invoke();
            quit.clicked += () => QuitClicked?.Invoke();
        }

        public void SetVisible(bool visible) => _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        public void BuildScores(string[] names, Color[] colors)
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
                var name = new Label(names[i]);
                name.AddToClassList("score-name");
                name.style.color = Color.Lerp(colors[i], Color.white, 0.3f);
                var pts = new Label("0");
                pts.AddToClassList("score-points");
                var crown = Icon(PlaceholderArt.Crown, new Color(1f, 0.82f, 0.2f));
                var lockIcon = Icon(PlaceholderArt.Check, new Color(0.5f, 1f, 0.6f));
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

        public void SetDevBadge(bool on) => _devBadge.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;

        public void Banner(string text, Color color, float seconds)
        {
            _bannerTween.Stop();
            _banner.text = text;
            _banner.style.color = color;
            _banner.style.opacity = 1f;
            _bannerTween = Tween.Custom(1f, 0f, 0.5f, v => _banner.style.opacity = v, startDelay: seconds);
        }
    }
}
