using System;
using Musketeers.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace Musketeers.UI
{
    // Between rounds: who won, every point gained this round with its reason, and the totals.
    public sealed class ScoreboardUI
    {
        public event Action ContinueClicked;

        readonly VisualElement _root;
        readonly Label _title, _subtitle;
        readonly VisualElement _gains, _totals;
        readonly Button _continue;

        public ScoreboardUI(VisualElement root)
        {
            _root = root.Q("scoreboard");
            _title = root.Q<Label>("score-title");
            _subtitle = root.Q<Label>("score-subtitle");
            _gains = root.Q("gains");
            _totals = root.Q("totals");
            _continue = root.Q<Button>("continue");
            _continue.focusable = false;
            _continue.clicked += () => ContinueClicked?.Invoke();
            SetVisible(false);
        }

        public void SetVisible(bool visible) => _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        public void Show(RoundResult r, MatchState m, string[] names, Color[] colors)
        {
            _title.text = r.SuddenDeath ? $"Round {r.RoundNumber} (sudden death)" : $"Round {r.RoundNumber}";
            _subtitle.text = r.Winner >= 0 ? $"{names[r.Winner]} is the last musketeer standing!" : "A draw. Nobody is left standing.";
            if (r.Winner >= 0)
                _subtitle.style.color = colors[r.Winner];
            else
                _subtitle.style.color = StyleKeyword.Null;

            _gains.Clear();
            if (r.Gains.Count == 0)
                _gains.Add(Row("", "No points this round.", Color.white));
            foreach (var g in r.Gains)
            {
                string reason;
                switch (g.Reason)
                {
                    case PointReason.LastStanding: reason = "last one standing"; break;
                    case PointReason.CrownKill: reason = $"took the crown off {names[g.Victim]}"; break;
                    default: reason = $"took down {names[g.Victim]}"; break;
                }
                _gains.Add(Row($"+{g.Amount}", $"{names[g.Player]}: {reason}", colors[g.Player]));
            }
            Totals(_totals, m, names, colors);
            _continue.text = m.MatchOver ? "See results" : "Next round";
            SetVisible(true);
        }

        public void SetCountdown(int seconds, bool matchOver) =>
            _continue.text = (matchOver ? "See results" : "Next round") + (seconds > 0 ? $"  ({seconds})" : "");

        static VisualElement Row(string amount, string text, Color color)
        {
            var row = new VisualElement();
            row.AddToClassList("gain");
            var a = new Label(amount);
            a.AddToClassList("gain-amount");
            var t = new Label(text);
            t.AddToClassList("gain-text");
            t.style.color = Color.Lerp(color, Color.white, 0.3f);
            row.Add(a);
            row.Add(t);
            return row;
        }

        // Shared with the results screen: a row per player, sorted by points, with a bar toward the goal.
        public static void Totals(VisualElement into, MatchState m, string[] names, Color[] colors)
        {
            into.Clear();
            int n = m.PlayerCount;
            var order = new int[n];
            for (int i = 0; i < n; i++)
                order[i] = i;
            Array.Sort(order, (a, b) => m.Points[b] != m.Points[a] ? m.Points[b].CompareTo(m.Points[a]) : a.CompareTo(b));
            foreach (int i in order)
            {
                var row = new VisualElement();
                row.AddToClassList("total");
                var sw = new VisualElement();
                sw.AddToClassList("total-swatch");
                sw.style.backgroundColor = colors[i];
                var name = new Label(names[i]);
                name.AddToClassList("total-name");
                var wins = new Label(m.RoundWins[i] > 0 ? $"{m.RoundWins[i]} round win{(m.RoundWins[i] == 1 ? "" : "s")}" : "");
                wins.AddToClassList("total-wins");
                var bar = new VisualElement();
                bar.AddToClassList("total-bar");
                var fill = new VisualElement();
                fill.AddToClassList("total-fill");
                fill.style.width = Length.Percent(Mathf.Clamp01((float)m.Points[i] / Mathf.Max(1, m.Cfg.PointsToWin)) * 100f);
                fill.style.backgroundColor = colors[i];
                bar.Add(fill);
                var pts = new Label($"{m.Points[i]} / {m.Cfg.PointsToWin}");
                pts.AddToClassList("total-points");
                row.Add(sw);
                row.Add(name);
                row.Add(wins);
                row.Add(bar);
                row.Add(pts);
                into.Add(row);
            }
        }
    }
}
