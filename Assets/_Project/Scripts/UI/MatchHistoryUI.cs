using System;
using Musketeers.Data;
using Musketeers.Game;
using Musketeers.Net;
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
        readonly SharedHistoryClient _shared;
        readonly Label _title, _note;
        string _expandedMatchId;
        public event Action RefreshRequested;
        MatchHistoryData DisplayData => _shared != null ? _shared.Data : _store.Data;
        public bool Open => _root.style.display.value != DisplayStyle.None;

        public MatchHistoryUI(VisualElement parent, MatchHistoryStore store, CharacterRoster roster, SharedHistoryClient shared = null)
        {
            _store = store;
            _shared = shared;
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
            var crown = new Image { sprite = UiArt.Get(GameArt.Crown), scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            crown.style.width = 64; crown.style.height = 34; crown.style.alignSelf = Align.Center; panel.Add(crown);
            _title = new Label("Match History");
            _title.AddToClassList("title"); _title.style.fontSize = 29; _title.style.height = 36;
            _title.style.marginBottom = 3; _title.style.unityTextAlign = TextAnchor.MiddleCenter;
            _title.style.color = new Color(1f, 0.84f, 0.35f); panel.Add(_title);
            var close = new Button(Close) { text = "×", tooltip = "Close Match Logs", focusable = false };
            close.AddToClassList("btn");
            close.style.position = Position.Absolute;
            close.style.right = 12;
            close.style.top = 10;
            close.style.width = 38;
            panel.Add(close);
            _note = new Label(shared != null ? shared.Status : "Local match archive");
            _note.style.fontSize = 12; _note.style.whiteSpace = WhiteSpace.Normal; panel.Add(_note);
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
            if (_shared != null) _shared.Changed += () => { if (Open) Render(); };
            _root.schedule.Execute(() => { if (Open) RefreshRequested?.Invoke(); }).Every(20000);
            Close();
        }

        Button Tab(VisualElement into, string name, bool leaderboard)
        {
            var button = new Button(() => { _leaderboard = leaderboard; Render(); }) { text = name, focusable = false };
            button.AddToClassList("btn");
            button.AddToClassList("info-tab");
            button.style.flexDirection = FlexDirection.Row;
            var icon = new Image { sprite = UiArt.Get("UI/leaderboard/" + (leaderboard ? "leaderboard-tab" : "history-tab")), scaleMode = ScaleMode.ScaleToFit };
            icon.style.width = 34; icon.style.height = 28; icon.style.marginRight = 8; button.Insert(0, icon);
            into.Add(button);
            return button;
        }

        public void Show()
        {
            _root.BringToFront();
            _root.style.display = DisplayStyle.Flex;
            Render();
            RefreshRequested?.Invoke();
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
            _title.text = _leaderboard ? "Character Leaderboard" : "Match History";
            _note.text = _shared != null ? _shared.Status : "Local match archive";
            _list.Clear();
            if (_shared == null && _store.Error != null) { _list.Add(new Label(_store.Error)); return; }
            if (_leaderboard) { RenderLeaderboard(); return; }
            // Local fallback is explicitly labeled and never contributes to the shared standings.
            var history = _shared != null && _shared.SaveFailed ? _store.Data : DisplayData;
            if (_shared != null && _shared.SaveFailed) _note.text = "Shared save failed · showing this browser's local archive. Character rankings still use shared records only.";
            if (history.Matches.Count == 0)
            {
                _list.Add(new Label("No eligible shared matches yet. Finish an online match with at least two humans and no bots."));
                return;
            }
            for (int i = 0; i < history.Matches.Count; i++)
                _list.Add(MatchCard(history.Matches[i], i + 1));
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
            detail.style.display = _expandedMatchId == match.MatchId ? DisplayStyle.Flex : DisplayStyle.None;
            detail.style.paddingTop = 35;
            var x = new Button(() => { _expandedMatchId = null; detail.style.display = DisplayStyle.None; }) { text = "×", tooltip = "Collapse match", focusable = false };
            x.AddToClassList("btn");
            x.style.position = Position.Absolute;
            x.style.top = 2;
            x.style.right = 0;
            x.style.width = 32;
            detail.Add(x);
            foreach (var player in match.Players) detail.Add(Standing(player));
            summary.clicked += () => { bool show = detail.style.display.value == DisplayStyle.None; _expandedMatchId = show ? match.MatchId : null; detail.style.display = show ? DisplayStyle.Flex : DisplayStyle.None; };
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
            foreach (var total in DisplayData.Leaderboard(ids))
            {
                var character = Character(total.CharacterId);
                rank++;
                var row = new VisualElement { name = "character-rank-" + rank };
                row.style.height = 84; row.style.minHeight = 84; row.style.marginBottom = 7;
                var banner = new Image { sprite = UiArt.Get("UI/leaderboard/rank-" + rank), scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                banner.StretchToParentSize(); row.Add(banner);
                string portraitId = total.CharacterId == "aljaz" ? "aljaž" : total.CharacterId;
                var portrait = new Image { sprite = UiArt.Get("UI/leaderboard/" + portraitId + "_leaderboard") ?? character?.Portrait,
                    scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                portrait.style.position = Position.Absolute; portrait.style.left = Length.Percent(12);
                portrait.style.width = Length.Percent(26); portrait.style.height = 84; row.Add(portrait);
                var name = new Label(character != null ? character.DisplayName : total.CharacterId);
                name.style.position = Position.Absolute; name.style.left = Length.Percent(41); name.style.top = 23;
                name.style.fontSize = 28; name.style.color = new Color(1f, 0.88f, 0.55f);
                name.style.unityFontStyleAndWeight = FontStyle.Bold; row.Add(name);
                var wins = new Label(total.Wins.ToString());
                wins.style.position = Position.Absolute; wins.style.right = Length.Percent(4); wins.style.top = 8;
                wins.style.width = Length.Percent(14); wins.style.fontSize = 30;
                wins.style.color = Color.white; wins.style.unityTextAlign = TextAnchor.MiddleCenter; row.Add(wins);
                _list.Add(row);
            }
        }
    }
}
