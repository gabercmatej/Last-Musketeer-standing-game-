using System;
using Musketeers.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace Musketeers.UI
{
    // Between lobby and arena: splash art, the map name, a random gameplay tip and a bar that fills for a
    // few seconds (time to read the tip; the arena itself builds instantly).
    public sealed class LoadingUI
    {
        public const float Seconds = 3f;

        readonly VisualElement _root;
        readonly Label _map, _tip;
        readonly VisualElement _fill;
        readonly string[] _tips;
        float _left = -1f;
        Action _done;

        public bool Active => _left >= 0f;

        public LoadingUI(VisualElement root, TextAsset tips)
        {
            _root = root.Q("loading");
            _map = root.Q<Label>("loading-map");
            _tip = root.Q<Label>("loading-tip");
            _fill = root.Q("loading-fill");
            _tips = tips != null ? tips.text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries) : new[] { "Plan in secret. Everything resolves at once." };
            UiArt.Set(root.Q("loading-bg"), UiArt.Get("UI/backgrounds/loading") ?? UiArt.Get(GameArt.SplashArt) ?? UiArt.Get(GameArt.KeyArt), new Color(0.08f, 0.07f, 0.12f));
            SetVisible(false);
        }

        public void SetVisible(bool visible) => _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        public void Show(string mapName, float seconds, Action done)
        {
            _map.text = mapName;
            _tip.text = _tips[UnityEngine.Random.Range(0, _tips.Length)].Trim();
            _fill.style.width = Length.Percent(0);
            _left = seconds;
            _total = Mathf.Max(0.01f, seconds);
            _done = done;
            SetVisible(true);
        }

        float _total = Seconds;

        public void Tick(float dt)
        {
            if (_left < 0f)
                return;
            _left -= dt;
            _fill.style.width = Length.Percent(Mathf.Clamp01(1f - _left / _total) * 100f);
            if (_left <= 0f)
            {
                _left = -1f;
                SetVisible(false);
                var d = _done;
                _done = null;
                d?.Invoke();
            }
        }

        public void Cancel()
        {
            _left = -1f;
            _done = null;
            SetVisible(false);
        }
    }
}
