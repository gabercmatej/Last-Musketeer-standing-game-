using System;
using System.Collections.Generic;
using Musketeers.Data;
using Musketeers.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Musketeers.UI
{
    public struct MenuChoice
    {
        public string Name;
        public int Players;      // 2..4
        public int Character;    // roster index for the human
        public uint? Seed;       // dev seed, null = random
    }

    // Name (remembered in PlayerPrefs), player count, character pick, Play vs Bots. Host/Join wait for Day 4.
    public sealed class MainMenuUI
    {
        const string NameKey = "Musketeers.PlayerName";
        const string PlayersKey = "Musketeers.Players";
        const string CharacterKey = "Musketeers.Character";

        public event Action<MenuChoice> PlayClicked;

        readonly VisualElement _root;
        readonly TextField _name;
        readonly TextField _seed;
        readonly List<Button> _countButtons = new List<Button>();
        readonly List<VisualElement> _cards = new List<VisualElement>();
        int _players;
        int _character;

        public MainMenuUI(VisualElement menuRoot, CharacterRoster roster)
        {
            _root = menuRoot.Q("menu");
            _name = menuRoot.Q<TextField>("name-field");
            _seed = menuRoot.Q<TextField>("seed-field");
            _name.value = PlayerPrefs.GetString(NameKey, "");
            _players = Mathf.Clamp(PlayerPrefs.GetInt(PlayersKey, 4), 2, 4);
            _character = Mathf.Clamp(PlayerPrefs.GetInt(CharacterKey, 0), 0, Mathf.Max(0, (roster != null ? roster.Count : 4) - 1));

            for (int n = 2; n <= 4; n++)
            {
                int count = n;
                var b = menuRoot.Q<Button>($"players-{n}");
                b.focusable = false;
                b.clicked += () => { _players = count; RefreshCount(); };
                _countButtons.Add(b);
            }

            var grid = menuRoot.Q("characters");
            int chars = roster != null ? roster.Count : 0;
            for (int i = 0; i < chars; i++)
            {
                int index = i;
                var c = roster.Get(i);
                var card = new VisualElement();
                card.AddToClassList("char-card");
                var swatch = new VisualElement();
                swatch.AddToClassList("char-swatch");
                swatch.style.backgroundColor = c.Color;
                if (c.Portrait != null)
                {
                    swatch.style.backgroundImage = new StyleBackground(c.Portrait);
                    swatch.style.backgroundColor = Color.clear;
                }
                var name = new Label(c.DisplayName);
                name.AddToClassList("char-name");
                name.style.color = Color.Lerp(c.Color, Color.white, 0.35f);
                var ability = new Label(c.AbilityName);
                ability.AddToClassList("char-ability");
                var desc = new Label(c.AbilityDescription);
                desc.AddToClassList("char-desc");
                card.Add(swatch);
                card.Add(name);
                card.Add(ability);
                card.Add(desc);
                card.RegisterCallback<ClickEvent>(_ => { _character = index; RefreshCards(); });
                grid.Add(card);
                _cards.Add(card);
            }

            var play = menuRoot.Q<Button>("play-bots");
            play.focusable = false;
            play.clicked += Play;
            var host = menuRoot.Q<Button>("host");
            var join = menuRoot.Q<Button>("join");
            host.SetEnabled(false);
            join.SetEnabled(false);
            menuRoot.Q("dev-row").style.display = DevFlags.Available ? DisplayStyle.Flex : DisplayStyle.None;

            RefreshCount();
            RefreshCards();
        }

        public void SetVisible(bool visible) => _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        public MenuChoice Current()
        {
            uint? seed = null;
            if (DevFlags.Available && uint.TryParse(_seed.value?.Trim(), out uint s))
                seed = s;
            return new MenuChoice { Name = (_name.value ?? "").Trim(), Players = _players, Character = _character, Seed = seed };
        }

        void Play()
        {
            var c = Current();
            PlayerPrefs.SetString(NameKey, c.Name);
            PlayerPrefs.SetInt(PlayersKey, c.Players);
            PlayerPrefs.SetInt(CharacterKey, c.Character);
            PlayerPrefs.Save();
            PlayClicked?.Invoke(c);
        }

        void RefreshCount()
        {
            for (int i = 0; i < _countButtons.Count; i++)
                _countButtons[i].EnableInClassList("seg--on", i + 2 == _players);
        }

        void RefreshCards()
        {
            for (int i = 0; i < _cards.Count; i++)
                _cards[i].EnableInClassList("char-card--on", i == _character);
        }
    }
}
