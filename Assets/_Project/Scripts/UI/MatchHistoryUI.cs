using System;
using Musketeers.Data;
using Musketeers.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Musketeers.UI
{
    // Reuses the results card's standings/badge vocabulary inside a scrollable history window.
    public sealed class MatchHistoryUI
    {
        readonly VisualElement _root;
        readonly ScrollView _list;
        readonly Button _historyTab, _leaderboardTab;
        readonly MatchHistoryStore _store;
        readonly CharacterRoster _roster;
        bool _leaderboard;
        public bool Open => _root.style.display.value != DisplayStyle.None;

        public MatchHistoryUI(VisualElement parent, MatchHistoryStore store, CharacterRoster roster)
        {
            _store = store;
            _roster = roster;
            _root = new VisualElement { name = "match-history" };
            _root.AddToClassList("screen");
            _root.AddToClassList("screen--modal");
            var panel = new VisualElement();
            panel.AddToClassList("panel");
            panel.style.width = Length.Percent(88);
            panel.style.maxWidth = 1040;
            panel.style.height = Length.Percent(86);
            panel.style.paddingTop = 18;
            panel.style.paddingBottom = 18;
            panel.style.paddingLeft = 22;
            panel.style.paddingRight = 22;
            var title = new Label("Match Logs");
            title.AddToClassList("title");
            panel.Add(title);
            var close = new Button(Close) { text = "×", tooltip = "Close Match Logs", focusable = false };
            close.AddToClassList("btn");
            close.style.position = Position.Absolute;
            close.style.right = 12;
            close.style.top = 10;
            close.style.width = 38;
            panel.Add(close);
            var note = new Label("This browser only · Shared history is planned for v1.1.");
            note.AddToClassList("subtitle");
            panel.Add(note);
            var rule = new Label("Completed online matches with 2–4 humans and no bots. Shared first place counts as a win for each musketeer.");
            rule.style.whiteSpace = WhiteSpace.Normal;
            rule.style.fontSize = 13;
            panel.Add(rule);
            var tabs = new VisualElement();
            tabs.AddToClassList("info-tabs");
            _historyTab = Tab(tabs, "Match history", false);
            _leaderboardTab = Tab(tabs, "Character leaderboard", true);
            panel.Add(tabs);
            _list = new ScrollView();
            _list.style.flexGrow = 1;
            _list.style.minHeight = 0;
            panel.Add(_list);
            _root.Add(panel);
            _root.RegisterCallback<ClickEvent>(e => { if (e.target == _root) Close(); });
            parent.Add(_root);
            Close();
        }

        Button Tab(VisualElement into, string name, bool leaderboard)
        {
            var button = new Button(() => { _leaderboard = leaderboard; Render(); }) { text = name, focusable = false };
            button.AddToClassList("btn");
            button.AddToClassList("info-tab");
            into.Add(button);
            return button;
        }

        public void Show()
        {
            _root.BringToFront();
            _root.style.display = DisplayStyle.Flex;
            Render();
        }
        public void Close() => _root.style.display = DisplayStyle.None;

        CharacterDef Character(string id)
        {
            if (_roster != null)
                foreach (var character in _roster.Characters)
                    if (character != null && character.Id == id) return character;
            return null;
        }

        void Render()
        {
            _historyTab.EnableInClassList("info-tab--on", !_leaderboard);
            _leaderboardTab.EnableInClassList("info-tab--on", _leaderboard);
            _list.Clear();
            if (_store.Error != null) { _list.Add(new Label(_store.Error)); return; }
            if (_leaderboard) { RenderLeaderboard(); return; }
            if (_store.Data.Matches.Count == 0)
            {
                _list.Add(new Label("No eligible matches saved in this browser yet."));
                return;
            }
            for (int i = 0; i < _store.Data.Matches.Count; i++)
                _list.Add(MatchCard(_store.Data.Matches[i], i + 1));
        }

        VisualElement MatchCard(MatchHistoryRecord match, int number)
        {
            var card = new VisualElement();
            card.style.marginBottom = 12;
            card.style.paddingBottom = 8;
            card.style.borderBottomWidth = 1;
            card.style.borderBottomColor = new Color(1, 1, 1, 0.2f);
            var summary = new Button { focusable = false, tooltip = "Expand match details" };
            summary.AddToClassList("btn");
            summary.style.height = StyleKeyword.Auto;
            summary.style.flexDirection = FlexDirection.Column;
            summary.style.alignItems = Align.Stretch;
            summary.Add(new Label($"#{number} · {match.MapId} · {match.CompletedUtc}"));
            var compact = new VisualElement();
            compact.style.flexDirection = FlexDirection.Row;
            compact.style.flexWrap = Wrap.Wrap;
            foreach (var player in match.Players)
            {
                var chip = new VisualElement();
                chip.style.flexDirection = FlexDirection.Row;
                chip.style.alignItems = Align.Center;
                chip.style.marginRight = 18;
                var character = Character(player.CharacterId);
                var icon = UiArt.Image("total-swatch", character?.Portrait, character != null ? character.Color : Color.white);
                icon.style.width = icon.style.height = 30;
                chip.Add(icon);
                chip.Add(new Label($"{ResultsLayout.Ordinal(player.Place)} {player.Name}"));
                compact.Add(chip);
            }
            summary.Add(compact);
            card.Add(summary);
            var detail = new VisualElement();
            detail.style.display = DisplayStyle.None;
            detail.style.paddingTop = 35;
            var x = new Button(() => detail.style.display = DisplayStyle.None) { text = "×", tooltip = "Collapse match", focusable = false };
            x.AddToClassList("btn");
            x.style.position = Position.Absolute;
            x.style.top = 2;
            x.style.right = 0;
            x.style.width = 32;
            detail.Add(x);
            foreach (var player in match.Players) detail.Add(Standing(player));
            summary.clicked += () => detail.style.display = detail.style.display.value == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;
            card.Add(detail);
            return card;
        }

        VisualElement Standing(HistoryPlayer player)
        {
            var row = new VisualElement();
            row.AddToClassList("standing");
            row.style.flexWrap = Wrap.Wrap;
            var ordinal = new Label(ResultsLayout.Ordinal(player.Place));
            ordinal.AddToClassList("standing-place");
            ordinal.EnableInClassList("standing-place--first", player.Place == 1);
            row.Add(ordinal);
            var character = Character(player.CharacterId);
            row.Add(UiArt.Image("total-swatch", character?.Portrait, character != null ? character.Color : Color.white));
            var info = new VisualElement();
            info.AddToClassList("standing-info");
            info.style.minWidth = 210;
            var name = new Label(player.Name);
            name.AddToClassList("standing-name");
            name.style.color = character != null ? Color.Lerp(character.Color, Color.white, 0.3f) : Color.white;
            info.Add(name);
            var stats = new Label($"{player.Points} pts · {player.RoundWins} round wins\n{player.Kills} kills · {player.Deaths} deaths");
            stats.AddToClassList("standing-points");
            stats.style.whiteSpace = WhiteSpace.Normal;
            info.Add(stats);
            row.Add(info);
            var badges = new VisualElement();
            badges.AddToClassList("standing-badges");
            foreach (var badge in player.Badges)
            {
                var chip = new VisualElement { tooltip = badge.Description };
                chip.AddToClassList("badge-chip");
                chip.Add(UiArt.Image("badge-icon", UiArt.Get(GameArt.Badge(badge.Id)), new Color(1f, .8f, .3f)));
                var label = new Label(badge.Name);
                label.AddToClassList("badge-name");
                chip.Add(label);
                badges.Add(chip);
            }
            row.Add(badges);
            return row;
        }

        void RenderLeaderboard()
        {
            if (_roster == null) return;
            var ids = new string[_roster.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = _roster.Get(i).Id;
            int rank = 0;
            foreach (var total in _store.Data.Leaderboard(ids))
            {
                var character = Character(total.CharacterId);
                var row = new VisualElement();
                row.AddToClassList("standing");
                row.Add(new Label($"{++rank}."));
                row.Add(UiArt.Image("total-swatch", character?.Portrait, character != null ? character.Color : Color.white));
                var name = new Label(character != null ? character.DisplayName : total.CharacterId);
                name.style.flexGrow = 1;
                row.Add(name);
                row.Add(new Label($"{total.Wins} match wins"));
                _list.Add(row);
            }
        }
    }
}
