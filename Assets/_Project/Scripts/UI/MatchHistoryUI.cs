using System;
using System.Globalization;
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

        public static string FormatCompletedTime(string utc)
        {
            if (!DateTimeOffset.TryParse(utc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var instant))
            {
                if (!DateTimeOffset.TryParseExact(utc, "yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal, out instant)) return utc ?? "";
            }
            return instant.ToLocalTime().ToString("dd MMM yyyy '·' HH:mm", CultureInfo.InvariantCulture);
        }

        VisualElement MatchCard(MatchHistoryRecord match, int number)
        {
            var card = new VisualElement();
            card.style.marginBottom = 7;
            card.style.paddingBottom = 4;
            card.style.borderBottomWidth = 1;
            card.style.borderBottomColor = new Color(1, 1, 1, 0.2f);
            var summary = new Button { focusable = false, tooltip = "Expand match details" };
            summary.AddToClassList("btn");
            summary.style.height = StyleKeyword.Auto;
            summary.style.flexDirection = FlexDirection.Column;
            summary.style.alignItems = Align.Stretch;
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row; header.style.justifyContent = Justify.SpaceBetween;
            header.style.flexWrap = Wrap.Wrap; header.style.paddingBottom = 3;
            string map = match.MapId == "bridge" ? "The Bridge" : match.MapId == "courtyard" ? "The Courtyard" : match.MapId;
            var heading = new Label($"#{number} · {map}"); heading.AddToClassList("history-card-heading");
            var date = new Label(FormatCompletedTime(match.CompletedUtc)); date.AddToClassList("history-card-date");
            header.Add(heading); header.Add(date); summary.Add(header);
            var compact = new VisualElement();
            compact.style.flexDirection = FlexDirection.Row;
            compact.style.flexWrap = Wrap.Wrap;
            foreach (var player in match.Players)
            {
                var chip = new VisualElement();
                chip.style.flexDirection = FlexDirection.Row;
                chip.style.alignItems = Align.Center;
                chip.style.flexBasis = Length.Percent(match.Players.Count == 3 ? 100f / 3f : 50f);
                chip.style.flexShrink = 0; chip.style.minWidth = 0; chip.style.minHeight = 34;
                chip.style.paddingRight = 8;
                var character = Character(player.CharacterId);
                var icon = UiArt.Image("total-swatch", character?.Portrait, character != null ? character.Color : Color.white);
                icon.style.width = icon.style.height = 26; icon.style.flexShrink = 0;
                chip.Add(icon);
                var place = new Label(ResultsLayout.Ordinal(player.Place));
                place.AddToClassList("history-place"); place.AddToClassList("history-place--" + player.Place);
                chip.Add(place);
                var name = new Label(player.Name) { tooltip = player.Name };
                name.AddToClassList("history-player-name");
                name.style.color = character != null ? Color.Lerp(character.Color, Color.white, 0.5f) : Color.white;
                chip.Add(name);
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
                // Overlay coordinates follow the image's fitted rectangle, including letterboxing.
                var content = new VisualElement { pickingMode = PickingMode.Ignore };
                content.style.position = Position.Absolute; row.Add(content);
                row.RegisterCallback<GeometryChangedEvent>(e =>
                {
                    var sprite = banner.sprite;
                    float aspect = sprite != null ? sprite.rect.width / sprite.rect.height : 2070f / 160f;
                    float width = Mathf.Min(e.newRect.width, e.newRect.height * aspect);
                    float height = width / aspect;
                    content.style.left = (e.newRect.width - width) * 0.5f;
                    content.style.top = (e.newRect.height - height) * 0.5f;
                    content.style.width = width; content.style.height = height;
                });
                string portraitId = total.CharacterId == "aljaz" ? "aljaž" : total.CharacterId;
                var portrait = new Image { sprite = UiArt.Get("UI/leaderboard/" + portraitId + "_leaderboard") ?? character?.Portrait,
                    scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
                portrait.style.position = Position.Absolute; portrait.style.left = Length.Percent(13);
                portrait.style.width = Length.Percent(24); portrait.style.height = Length.Percent(100); content.Add(portrait);
                var name = new Label(character != null ? character.DisplayName : total.CharacterId);
                name.AddToClassList("leaderboard-character-name");
                name.style.position = Position.Absolute; name.style.left = Length.Percent(41); name.style.right = Length.Percent(22);
                name.style.top = name.style.bottom = 0;
                name.style.fontSize = 30; name.style.unityTextAlign = TextAnchor.MiddleLeft; content.Add(name);
                var wins = new Label(total.Wins.ToString());
                wins.style.position = Position.Absolute; wins.style.left = Length.Percent(84.9f); wins.style.top = Length.Percent(13);
                wins.style.width = Length.Percent(12); wins.style.height = Length.Percent(55);
                wins.style.fontSize = Mathf.Min(30f, 90f / Mathf.Max(1, total.Wins.ToString().Length));
                wins.style.color = Color.white; wins.style.unityTextAlign = TextAnchor.MiddleCenter; content.Add(wins);
                _list.Add(row);
            }
        }
    }
}
