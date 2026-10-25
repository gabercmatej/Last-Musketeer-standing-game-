using Musketeers.Game;
using Musketeers.Sim;
using Musketeers.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace Musketeers.Sim.Tests
{
    public class Day6PresentationTests
    {
        [Test]
        public void OpponentPanel_ResolvedPlansOnly_HidesAndClearsAtNextPlanning()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/_Project/UI/HUD.uxml");
            var root = tree.CloneTree();
            var bar = new PlanBarUI(root);
            var panel = root.Q("opponent-plans");
            Assert.That(panel.style.display.value, Is.EqualTo(DisplayStyle.None));
            var state = Scenario.From("A.B\n...\nC.D").State;
            var plan = new TurnPlan(1) { A0 = PlannedAction.Move(Dir.E), A1 = PlannedAction.Fire(Dir.S), A2 = PlannedAction.Reload() };
            bar.ShowOpponents(new TurnPlayback { Before = state, Plans = new[] { plan }, Names = new[] { "A", "B", "C", "D" } }, 0, new[] { 0, 1, 2, 3 });
            Assert.That(panel.childCount, Is.EqualTo(3));
            Assert.That(panel.Q("opponent-0"), Is.Null);
            Assert.That(panel.Q("opponent-1").Q("move-2").tooltip, Is.EqualTo("Reload"));
            Assert.That(panel.Q("opponent-2").Q("move-0").tooltip, Is.EqualTo("Idle"));
            bar.HideOpponents();
            Assert.That(panel.childCount, Is.EqualTo(0));
            Assert.That(panel.style.display.value, Is.EqualTo(DisplayStyle.None));
        }
        [Test]
        public void OwnPlanner_RetainsIconsDirectionsAndLabelsThroughLockAndOpponentReveal()
        {
            var root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/_Project/UI/HUD.uxml").CloneTree();
            var bar = new PlanBarUI(root);
            var state = Scenario.From("A.B\n...\nC.D").State;
            var cfg = new GameConfig();
            var builder = new PlanBuilder();
            var plan = new TurnPlan(0) { A0 = PlannedAction.Move(Dir.E), A1 = PlannedAction.Fire(Dir.S), A2 = PlannedAction.Reload() };
            builder.Load(state, 0, cfg, plan, false);
            bar.Refresh(builder, cfg, null);
            var labels = root.Q("slots").Query<Label>(className: "slot-text").ToList();
            string[] expected = { labels[0].text, labels[1].text, labels[2].text };
            builder.SetLocked(true); bar.Refresh(builder, cfg, null);
            bar.ShowOpponents(new TurnPlayback { Before = state, Plans = new[] { new TurnPlan(1) } }, 0, new[] { 0, 1, 2, 3 });
            bar.SetMessage("Resolving..."); bar.HideOpponents();
            for (int tick = 0; tick < 3; tick++)
            {
                Assert.That(labels[tick].text, Is.EqualTo(expected[tick]));
                var slot = root.Q("slots").ElementAt(tick);
                Assert.That(slot.Q(className: "slot-icon").style.backgroundImage.value.sprite, Is.Not.Null);
                Assert.That(slot.Q(className: "slot-dir").style.display.value,
                    Is.EqualTo(tick < 2 ? DisplayStyle.Flex : DisplayStyle.None));
            }
            Assert.That(root.Q("plan-panel").style.display.value, Is.Not.EqualTo(DisplayStyle.None));
            builder.Begin(state, 0, cfg); bar.Refresh(builder, cfg, null);
            Assert.That(labels[0].text, Does.StartWith("Next"));
        }

        [Test]
        public void HistoryDate_EquivalentOffsetsAndLegacyUtcShowSameLocalTime()
        {
            string formatted = MatchHistoryUI.FormatCompletedTime("2026-10-25T07:28:00Z");
            Assert.That(formatted, Is.EqualTo(MatchHistoryUI.FormatCompletedTime("2026-10-25T09:28:00+02:00")));
            Assert.That(formatted, Is.EqualTo(MatchHistoryUI.FormatCompletedTime("2026-10-25 07:28 UTC")));
            Assert.That(formatted, Does.Contain(" Oct 2026 · "));
            Assert.That(formatted, Does.EndWith(":28"));
        }

        [Test]
        public void HistoryDate_UnknownOrMissingTimestampRemainsSafe()
        {
            Assert.That(MatchHistoryUI.FormatCompletedTime("Unknown"), Is.EqualTo("Unknown"));
            Assert.That(MatchHistoryUI.FormatCompletedTime(null), Is.Empty);
        }

    }
}
