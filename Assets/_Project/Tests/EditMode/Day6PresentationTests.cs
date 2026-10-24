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
    }
}
