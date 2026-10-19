using System;
using System.Collections.Generic;
using Musketeers.Data;
using Musketeers.Game;
using Musketeers.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace Musketeers.UI
{
    // Match over: the winner (standing on the stage behind this card in the victory pose), the badges
    // everyone earned, final standings with portraits, Play again / Back to lobby.
    public sealed class ResultsUI
    {
        public event Action AgainClicked;
        public event Action MenuClicked;

        readonly VisualElement _root;
        readonly Label _winner, _line;
        readonly VisualElement _final, _awards;

        public ResultsUI(VisualElement root)
        {
            _root = root.Q("results");
            _winner = root.Q<Label>("winner");
            _line = root.Q<Label>("winner-line");
            _final = root.Q("final");
            _awards = root.Q("awards");
            var again = root.Q<Button>("again");
            var menu = root.Q<Button>("menu");
            again.focusable = false;
            menu.focusable = false;
            menu.AddToClassList("btn--back");
            again.clicked += () => AgainClicked?.Invoke();
            menu.clicked += () => MenuClicked?.Invoke();
            SetVisible(false);
        }

        public void SetVisible(bool visible) => _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        public void Show(MatchState m, string[] names, Color[] colors, Sprite[] portraits)
        {
            int w = m.MatchWinner;
            _winner.text = w >= 0 ? names[w] : "Nobody";
            _winner.style.color = w >= 0 ? Color.Lerp(colors[w], Color.white, 0.25f) : Color.white;
            _line.text = w >= 0 ? $"{m.Points[w]} points, {m.RoundWins[w]} round wins, {m.Stats.Kills[w]} kills" : "";
            // One tile per badge, listing everyone who earned it (ties share).
            _awards.Clear();
            var awards = Awards.Compute(m.Stats);
            var done = new HashSet<string>();
            foreach (var a in awards)
            {
                if (!done.Add(a.Id))
                    continue;
                var el = new VisualElement();
                el.AddToClassList("award");
                el.Add(UiArt.Image("award-badge", UiArt.Get(GameArt.Badge(a.Id)), new Color(1f, 0.8f, 0.3f)));
                var n = new Label(a.Name);
                n.AddToClassList("award-name");
                el.Add(n);
                var who = new List<string>();
                foreach (var b in awards)
                {
                    if (b.Id == a.Id)
                        who.Add(names[b.Player]);
                }
                var whoLabel = new Label(string.Join(", ", who));
                whoLabel.AddToClassList("award-who");
                if (who.Count == 1)
                    whoLabel.style.color = Color.Lerp(colors[a.Player], Color.white, 0.35f);
                el.tooltip = a.Description;
                el.Add(whoLabel);
                _awards.Add(el);
            }
            ScoreboardUI.Totals(_final, m, names, colors, portraits);
            SetVisible(true);
        }
    }
}
