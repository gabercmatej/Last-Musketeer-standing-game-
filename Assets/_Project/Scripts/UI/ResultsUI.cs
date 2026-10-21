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
    // the winner, the standings in rank order with every badge each player earned, Play again / Quit.
    // Badges start hidden and are revealed one by one (pop, shine sweep, a sound each).
    public sealed class ResultsUI
    {
        public const float RevealStart = 1.2f;
        public const float RevealGap = 0.35f;

        public event Action AgainClicked;
        public event Action MenuClicked;

        readonly VisualElement _root;
        readonly Label _winner, _line;
        readonly VisualElement _standings;

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
            again.clicked += () => AgainClicked?.Invoke();
            quit.clicked += () => MenuClicked?.Invoke();
            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            if (!visible)
                Stop();
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // Cancels the pending badge reveals (leaving the screen).
        public void Stop() => Tween.StopAll(this);

        public void Show(MatchState m, string[] names, Color[] colors, Sprite[] portraits)
        {
            Stop();
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
                var pts = new Label($"{m.Points[i]} pts");
                pts.AddToClassList("standing-points");
                info.Add(name);
                info.Add(pts);
                var badges = new VisualElement();
                badges.AddToClassList("standing-badges");
                foreach (var a in awards)
                {
                    if (a.Player != i)
                        continue;
                    var chip = MakeBadge(a);
                    badges.Add(chip);
                    chips.Add(chip);
                }
                row.Add(ord);
                row.Add(sw);
                row.Add(info);
                row.Add(badges);
                _standings.Add(row);
            }

            for (int k = 0; k < chips.Count; k++)
            {
                var chip = chips[k];
                int index = k;
                Tween.Delay(this, RevealStart + RevealGap * k, _ => Reveal(chip, index));
            }
            SetVisible(true);
        }

        static VisualElement MakeBadge(Award a)
        {
            var chip = new VisualElement { tooltip = a.Description };
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
            UiArt.Sfx("SFX/match/award_reveal", Mathf.Min(1f, 0.45f + 0.07f * index));   // a little louder with each badge
            Tween.Custom(this, 0f, 1.18f, 0.22f, (_, v) =>
            {
                chip.style.scale = new Scale(new Vector3(v, v, 1f));
                chip.style.opacity = Mathf.Clamp01(v / 0.8f);
            }, Ease.OutQuad).OnComplete(() =>
                Tween.Custom(this, 1.18f, 1f, 0.14f, (_, v) => chip.style.scale = new Scale(new Vector3(v, v, 1f)), Ease.InOutSine));
            var shine = chip.Q("shine");
            Tween.Custom(this, 0f, 1f, 0.4f, (_, t) =>
            {
                shine.style.translate = new Translate(Mathf.Lerp(-30f, 64f, t), 0f);
                shine.style.opacity = Mathf.Sin(t * Mathf.PI);
            }, Ease.InOutSine, startDelay: 0.2f);
        }
    }
}
