using System;
using Musketeers.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace Musketeers.UI
{
    // Match over: the winner, final standings, Play again / Menu.
    public sealed class ResultsUI
    {
        public event Action AgainClicked;
        public event Action MenuClicked;

        readonly VisualElement _root;
        readonly Label _winner, _line;
        readonly VisualElement _final;

        public ResultsUI(VisualElement root)
        {
            _root = root.Q("results");
            _winner = root.Q<Label>("winner");
            _line = root.Q<Label>("winner-line");
            _final = root.Q("final");
            var again = root.Q<Button>("again");
            var menu = root.Q<Button>("menu");
            again.focusable = false;
            menu.focusable = false;
            again.clicked += () => AgainClicked?.Invoke();
            menu.clicked += () => MenuClicked?.Invoke();
            SetVisible(false);
        }

        public void SetVisible(bool visible) => _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        public void Show(MatchState m, string[] names, Color[] colors)
        {
            int w = m.MatchWinner;
            _winner.text = w >= 0 ? names[w] : "Nobody";
            _winner.style.color = w >= 0 ? colors[w] : Color.white;
            _line.text = w >= 0 ? $"{m.Points[w]} points, {m.RoundWins[w]} round wins, {m.Stats.Kills[w]} kills" : "";
            ScoreboardUI.Totals(_final, m, names, colors);
            SetVisible(true);
        }
    }
}
