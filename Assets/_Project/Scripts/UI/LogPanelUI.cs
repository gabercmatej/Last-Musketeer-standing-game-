using Musketeers.Game;
using Musketeers.Sim;
using UnityEngine.UIElements;

namespace Musketeers.UI
{
    // Collapsible text log of every turn (debugging, and fun to read afterwards).
    public sealed class LogPanelUI
    {
        const int MaxLines = 600;
        readonly VisualElement _panel;
        readonly ScrollView _scroll;

        public bool Open { get; private set; }

        public LogPanelUI(VisualElement hudRoot)
        {
            _panel = hudRoot.Q("log-panel");
            _scroll = hudRoot.Q<ScrollView>("log-scroll");
            SetOpen(false);
        }

        public void SetOpen(bool open)
        {
            Open = open;
            _panel.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void Clear() => _scroll.Clear();

        public void Line(string text, string cls = null)
        {
            var l = new Label(text);
            l.AddToClassList("log-line");
            if (cls != null)
                l.AddToClassList(cls);
            _scroll.Add(l);
            while (_scroll.contentContainer.childCount > MaxLines)
                _scroll.contentContainer.RemoveAt(0);
            _scroll.schedule.Execute(() => _scroll.scrollOffset = new UnityEngine.Vector2(0, float.MaxValue));
        }

        public void Turn(TurnPlayback p, bool water = false)
        {
            Line($"Round {p.RoundNumber}, turn {p.RoundTurn}", "log-line--turn");
            for (int t = 0; t < p.TickCount; t++)
            {
                Line($"Tick {t + 1}", "log-line--tick");
                Events(p.TickLogs[t], p.Names, p.Before.Board, water);
            }
            if (p.Outcome.TurnStart != null && p.Outcome.TurnStart.Count > 0)
            {
                Line("Turn start", "log-line--tick");
                Events(p.Outcome.TurnStart, p.Names, p.Before.Board, water);
            }
        }

        // Ring warnings and crumbles produce one event per tile; show each line once.
        void Events(EventLog log, string[] names, Board board = null, bool water = false)
        {
            string last = null;
            for (int i = 0; i < log.Count; i++)
            {
                var e = log[i];
                if (e.Kind == SimEventKind.ParryRaised)
                    continue;
                string text = EventText.Describe(e, names, log, board, water);
                if (text == last)
                    continue;
                last = text;
                bool big = e.Kind == SimEventKind.Died || e.Kind == SimEventKind.RoundEnded || e.Kind == SimEventKind.KegExploded;
                Line(text, big ? "log-line--big" : null);
            }
        }
    }
}
