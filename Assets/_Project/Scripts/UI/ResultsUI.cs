using System;
using System.Collections.Generic;
using Musketeers.Data;
using Musketeers.Game;
using Musketeers.Sim;
using PrimeTween;
using UnityEngine;
using UnityEngine.UIElements;

namespace Musketeers.UI
{
    // Match over (FIXES_v0.2 §2.5): the card on the right half of the screen (the podium stage is on the left):
    // the winner, the standings in rank order with every badge each player earned, Return to lobby / Quit.
    // Badges start hidden and are revealed one by one (pop, shine sweep, a sound each).
    public sealed class ResultsUI
    {
        public const float PlacementStart = 0.35f;
        public const float PlacementGap = 0.65f;
        public const float RevealStart = 0.6f;
        public const float RevealGap = 0.42f;      // FIXES_4E §6.1: about 20% slower than the old 0.35
        public const float RevealSlow = 1.2f;      // the pop / shine animations stretch by the same factor
        public const float BadgeVolume = 0.5f;     // the badge sound plays at 50% of its old volume
        const float PopupSeconds = 5f;

        public event Action AgainClicked;
        public event Action MenuClicked;
        public event Action SettingsClicked;
        public event Action MatchLogsClicked;

        readonly VisualElement _root;
        readonly Label _winner, _line;
        readonly VisualElement _standings;
        readonly VisualElement _popup, _popupIcon;
        readonly Label _popupName, _popupDesc, _popupWho;
        string[] _names = new string[0];
        Sequence _popupTween;

        public ResultsUI(VisualElement root)
        {
            _root = root.Q("results");
            _winner = root.Q<Label>("winner");
            _line = root.Q<Label>("winner-line");
            _standings = root.Q("standings");
            var again = root.Q<Button>("again");
            var quit = root.Q<Button>("menu");
            again.focusable = false;
            quit.focusable = false;
            again.clicked += () => AgainClicked?.Invoke();   // "Return to lobby"
            quit.clicked += () => MenuClicked?.Invoke();
            // Top-left: Fullscreen and Settings (no Leave: Quit does that).
            CornerButtons.AddTopLeftSet(root.Q("results-corner"), () => SettingsClicked?.Invoke(), null);
            CornerButtons.Add(root.Q("results-corner"), "leaderboard-history", "Match Logs", "Match history and character leaderboard", () => MatchLogsClicked?.Invoke());

            // The achievement-style popup a badge click opens.
            _popup = new VisualElement { name = "ach" };
            _popup.AddToClassList("ach");
            _popupIcon = new VisualElement { pickingMode = PickingMode.Ignore };
            _popupIcon.AddToClassList("ach-icon");
            var text = new VisualElement { pickingMode = PickingMode.Ignore };
            text.AddToClassList("ach-text");
            var kicker = new Label("BADGE EARNED") { pickingMode = PickingMode.Ignore };
            kicker.AddToClassList("ach-kicker");
            _popupName = new Label { pickingMode = PickingMode.Ignore };
            _popupName.AddToClassList("ach-name");
            _popupDesc = new Label { pickingMode = PickingMode.Ignore };
            _popupDesc.AddToClassList("ach-desc");
            _popupWho = new Label { pickingMode = PickingMode.Ignore };
            _popupWho.AddToClassList("ach-who");
            text.Add(kicker);
            text.Add(_popupName);
            text.Add(_popupDesc);
            text.Add(_popupWho);
            _popup.Add(_popupIcon);
            _popup.Add(text);
            var close = new Button(HidePopup) { text = "×", tooltip = "Close badge", focusable = false };
            close.AddToClassList("btn");
            close.style.position = Position.Absolute;
            close.style.right = 4;
            close.style.top = 4;
            close.style.width = 28;
            close.style.height = 28;
            _popup.Add(close);
            _popup.RegisterCallback<ClickEvent>(_ => HidePopup());
            _root.Add(_popup);
            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            if (!visible)
                Stop();
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // Cancels the pending badge reveals (leaving the screen).
        public void Stop()
        {
            Tween.StopAll(this);
            HidePopup();
        }

        void HidePopup()
        {
            _popupTween.Stop();
            _popup.style.display = DisplayStyle.None;
            _popup.style.opacity = 0f;
        }

        // Click on a badge: icon, name and description from Awards (FIXES_4E §6.2), who earned it, then it fades away.
        void ShowPopup(Award a)
        {
            _popupTween.Stop();
            _popupIcon.Clear();
            _popupIcon.Add(UiArt.Image("badge-art", UiArt.Get(GameArt.Badge(a.Id)), new Color(1f, 0.8f, 0.3f)));
            _popupName.text = a.Name;
            _popupDesc.text = a.Description;
            _popupWho.text = a.Player >= 0 && a.Player < _names.Length ? "Earned by " + _names[a.Player] : "";
            _popup.style.display = DisplayStyle.Flex;
            _popup.style.opacity = 0f;
            UiArt.Sfx("SFX/ui/wardrobe_equip", 0.5f);
            _popupTween = Tween.Custom(0f, 1f, 0.18f, v =>
            {
                _popup.style.opacity = v;
                _popup.style.translate = new Translate(0f, (1f - v) * 24f);
            }, Ease.OutQuad).Chain(Tween.Custom(1f, 0f, 0.5f, v => _popup.style.opacity = v, startDelay: PopupSeconds))
              .OnComplete(() => _popup.style.display = DisplayStyle.None);
        }

        public void Show(MatchState m, string[] names, Color[] colors, Sprite[] portraits)
        {
            Stop();
            _names = names;
            int n = m.PlayerCount;
            var places = m.Places ?? Scoring.Rank(m.Points, m.RoundPoints);
            var order = ResultsLayout.Order(places);

            var firsts = new List<int>();
            foreach (int i in order)
            {
                if (places[i] == 1)
                    firsts.Add(i);
            }
            if (firsts.Count > 1)
            {
                _winner.text = "Shared victory";
                _winner.style.color = new Color(1f, 0.85f, 0.4f);
                var who = new List<string>();
                foreach (int i in firsts)
                    who.Add(names[i]);
                _line.text = string.Join(" and ", who);
            }
            else
            {
                int w = firsts.Count == 1 ? firsts[0] : -1;
                _winner.text = w >= 0 ? names[w] : "Nobody";
                _winner.style.color = w >= 0 ? Color.Lerp(colors[w], Color.white, 0.25f) : Color.white;
                _line.text = w >= 0 ? $"{m.Points[w]} points, {m.RoundWins[w]} round wins, {m.Stats.Kills[w]} kills" : "";
            }

            var awards = Awards.Compute(m.Stats);
            _standings.Clear();
            var chips = new List<VisualElement>();
            foreach (int i in order)
            {
                var row = new VisualElement();
                row.AddToClassList("standing");
                var ord = new Label(ResultsLayout.Ordinal(places[i]));
                ord.AddToClassList("standing-place");
                if (places[i] == 1)
                    ord.AddToClassList("standing-place--first");
                var sw = UiArt.Image("total-swatch", portraits != null && i < portraits.Length ? portraits[i] : null, colors[i]);
                sw.style.borderLeftColor = sw.style.borderRightColor = sw.style.borderTopColor = sw.style.borderBottomColor = colors[i];
                var info = new VisualElement();
                info.AddToClassList("standing-info");
                var name = new Label(names[i]);
                name.AddToClassList("standing-name");
                name.style.color = Color.Lerp(colors[i], Color.white, 0.3f);
                var pts = new Label($"{m.Points[i]} pts · {m.RoundWins[i]} round wins\n{m.Stats.Kills[i]} kills · {m.Stats.Deaths[i]} deaths");
                pts.AddToClassList("standing-points");
                pts.style.whiteSpace = WhiteSpace.Normal;
                pts.style.fontSize = 13;
                info.Add(name);
                info.Add(pts);
                var badges = new VisualElement();
                badges.AddToClassList("standing-badges");
                foreach (var a in awards)
                {
                    if (a.Player != i)
                        continue;
                    var chip = MakeBadge(a);
                    var award = a;
                    chip.RegisterCallback<ClickEvent>(_ => ShowPopup(award));
                    badges.Add(chip);
                    chips.Add(chip);
                }
                row.Add(ord);
                row.Add(sw);
                row.Add(info);
                row.Add(badges);
                _standings.Add(row);
                row.style.opacity = 0f;
                int rankIndex = Array.IndexOf(order, i);
                Tween.Delay(this, PlacementStart + PlacementGap * (n - 1 - rankIndex), _ =>
                    Tween.Custom(this, 0f, 1f, 0.45f, (_, t) =>
                    {
                        row.style.opacity = t;
                        row.style.translate = new Translate((1f - t) * 30f, 0f);
                    }, Ease.OutCubic));
            }

            for (int k = 0; k < chips.Count; k++)
            {
                var chip = chips[k];
                int index = k;
                Tween.Delay(this, PlacementStart + PlacementGap * n + RevealStart + RevealGap * k, _ => Reveal(chip, index));
            }
            SetVisible(true);
        }

        static VisualElement MakeBadge(Award a)
        {
            var chip = new VisualElement { tooltip = a.Description, pickingMode = PickingMode.Ignore };   // clickable once revealed
            chip.AddToClassList("badge-chip");
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("badge-icon");
            icon.Add(UiArt.Image("badge-art", UiArt.Get(GameArt.Badge(a.Id)), new Color(1f, 0.8f, 0.3f)));
            var shine = new VisualElement { name = "shine", pickingMode = PickingMode.Ignore };
            shine.AddToClassList("badge-shine");
            icon.Add(shine);
            var label = new Label(a.Name) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("badge-name");
            chip.Add(icon);
            chip.Add(label);
            chip.style.opacity = 0f;
            chip.style.scale = new Scale(Vector3.zero);
            return chip;
        }

        // Pop 0 -> 1.18 -> 1 with a fade in, then one shine sweep across the badge.
        void Reveal(VisualElement chip, int index)
        {
            chip.pickingMode = PickingMode.Position;
            UiArt.Sfx("SFX/match/award_reveal", Mathf.Min(1f, 0.45f + 0.07f * index) * BadgeVolume);   // a little louder with each badge, at 50%
            Tween.Custom(this, 0f, 1.18f, 0.22f * RevealSlow, (_, v) =>
            {
                chip.style.scale = new Scale(new Vector3(v, v, 1f));
                chip.style.opacity = Mathf.Clamp01(v / 0.8f);
            }, Ease.OutQuad).OnComplete(() =>
                Tween.Custom(this, 1.18f, 1f, 0.14f * RevealSlow, (_, v) => chip.style.scale = new Scale(new Vector3(v, v, 1f)), Ease.InOutSine));
            var shine = chip.Q("shine");
            Tween.Custom(this, 0f, 1f, 0.4f * RevealSlow, (_, t) =>
            {
                shine.style.translate = new Translate(Mathf.Lerp(-30f, 64f, t), 0f);
                shine.style.opacity = Mathf.Sin(t * Mathf.PI);
            }, Ease.InOutSine, startDelay: 0.2f * RevealSlow);
        }
    }
}
