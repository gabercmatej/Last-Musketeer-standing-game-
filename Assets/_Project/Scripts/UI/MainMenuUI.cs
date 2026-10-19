using System;
using Musketeers.Data;
using Musketeers.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Musketeers.UI
{
    // Title screen (GAME_DESIGN §9): splash art and logo, name (remembered), Play vs Bots, Settings,
    // Fullscreen. Host/Join are shown but wait for Day 5's online play.
    public sealed class MainMenuUI
    {
        const string NameKey = "Musketeers.PlayerName";

        public event Action PlayClicked;
        public event Action SettingsClicked;

        readonly VisualElement _root;
        readonly TextField _name;
        readonly TextField _seed;

        public string PlayerName => (_name.value ?? "").Trim();

        public uint? DevSeed => DevFlags.Available && uint.TryParse(_seed.value?.Trim(), out uint s) ? s : (uint?)null;

        public MainMenuUI(VisualElement menuRoot)
        {
            _root = menuRoot.Q("menu");
            _name = menuRoot.Q<TextField>("name-field");
            _seed = menuRoot.Q<TextField>("seed-field");
            _name.value = PlayerPrefs.GetString(NameKey, "");

            // Matej's splash art already has the logo painted in; the separate logo (or, without art, the
            // text title) only shows when there's no splash.
            bool splash = UiArt.Set(menuRoot.Q("menu-bg"), UiArt.Get(GameArt.SplashArt) ?? UiArt.Get(GameArt.KeyArt), new Color(0.08f, 0.07f, 0.12f));
            bool logo = !splash && UiArt.Set(menuRoot.Q("menu-logo"), GameArt.Logo);
            menuRoot.Q("menu-title").style.display = splash || logo ? DisplayStyle.None : DisplayStyle.Flex;
            menuRoot.Q("menu-logo").style.display = logo ? DisplayStyle.Flex : DisplayStyle.None;
            menuRoot.Q<Label>("menu-version").text = "v" + Application.version;

            var play = menuRoot.Q<Button>("play-bots");
            play.clicked += Play;
            var host = menuRoot.Q<Button>("host");
            var join = menuRoot.Q<Button>("join");
            host.SetEnabled(false);
            join.SetEnabled(false);
            host.tooltip = join.tooltip = "Online play is coming tomorrow.";
            menuRoot.Q<Button>("settings").clicked += () => SettingsClicked?.Invoke();
            menuRoot.Q<Button>("fullscreen").clicked += () => Screen.fullScreen = !Screen.fullScreen;
            menuRoot.Q("dev-row").style.display = DevFlags.Available ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (var b in _root.Query<Button>().ToList())
                b.focusable = false;
        }

        public void SetVisible(bool visible) => _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        void Play()
        {
            PlayerPrefs.SetString(NameKey, PlayerName);
            PlayerPrefs.Save();
            PlayClicked?.Invoke();
        }
    }
}
