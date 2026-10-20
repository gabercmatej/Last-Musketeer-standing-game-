using Musketeers.Game;
using Musketeers.Presentation;
using Musketeers.UI;
using NUnit.Framework;
using UnityEngine;

namespace Musketeers.Sim.Tests
{
    // The HUD lives around the arena (FIXES §3.3): region maths, the grouped bar and the Fire/Reload states.
    public class HudLayoutTests
    {
        // 1280x720 reference with the Courtyard board about x 192-1088, y 86-640.
        static readonly Rect Wide = new Rect(192f, 86f, 896f, 554f);

        [Test]
        public void Wide_RegionsSurroundTheArena_AndNeverCoverIt()
        {
            var r = HudLayout.Compute(Wide, 1280f, 720f);
            Assert.That(r.Overlap, Is.False);
            Assert.That(r.Top.yMax, Is.LessThanOrEqualTo(Wide.yMin + 0.01f));
            Assert.That(r.Bottom.yMin, Is.GreaterThanOrEqualTo(Wide.yMax - 0.01f));
            Assert.That(r.Left.xMax, Is.LessThanOrEqualTo(Wide.xMin + 0.01f));
            Assert.That(r.Right.xMin, Is.GreaterThanOrEqualTo(Wide.xMax - 0.01f));
            Assert.That(r.Top.height, Is.LessThanOrEqualTo(HudLayout.MaxTop));
            Assert.That(r.Bottom.height, Is.LessThanOrEqualTo(HudLayout.MaxBottom));
            Assert.That(r.RoundX, Is.GreaterThanOrEqualTo(r.Left.xMax));
        }

        [Test]
        public void SixteenTen_ColumnsShrinkButStayWide_AndTextGoesCompact()
        {
            // 1152x720 panel, margins about 128.
            var arena = new Rect(128f, 86f, 896f, 554f);
            var r = HudLayout.Compute(arena, 1152f, 720f);
            Assert.That(r.Left.width, Is.GreaterThanOrEqualTo(HudLayout.MinCol));
            Assert.That(r.Right.width, Is.GreaterThanOrEqualTo(HudLayout.MinCol));
            Assert.That(r.Compact, Is.True);
            Assert.That(r.Left.xMin, Is.GreaterThanOrEqualTo(0f));
            Assert.That(r.Right.xMax, Is.LessThanOrEqualTo(1152f));
        }

        [Test]
        public void FourThree_ColumnsKeepMinWidth_OverlapTheBoardEdge_AndStayOnScreen()
        {
            var arena = new Rect(32f, 86f, 896f, 554f);   // 960x720 panel
            var r = HudLayout.Compute(arena, 960f, 720f);
            Assert.That(r.Overlap, Is.True);
            Assert.That(r.Left.width, Is.EqualTo(HudLayout.MinCol).Within(0.01f));
            Assert.That(r.Right.width, Is.EqualTo(HudLayout.MinCol).Within(0.01f));
            Assert.That(r.Left.xMin, Is.GreaterThanOrEqualTo(0f));
            Assert.That(r.Right.xMax, Is.LessThanOrEqualTo(960f));
            Assert.That(r.Left.xMax, Is.GreaterThan(arena.xMin));   // overlaps the board edge
            Assert.That(r.Left.xMax, Is.LessThan(r.Right.xMin));    // but the columns never meet
            Assert.That(r.RoundX, Is.GreaterThanOrEqualTo(r.Left.xMax));
        }

        [Test]
        public void StripHeights_AreClamped()
        {
            var r = HudLayout.Compute(new Rect(200f, 10f, 800f, 700f), 1280f, 720f);
            Assert.That(r.Top.height, Is.EqualTo(HudLayout.MinTop));
            Assert.That(r.Bottom.height, Is.EqualTo(HudLayout.MinBottom));
            r = HudLayout.Compute(new Rect(200f, 300f, 800f, 100f), 1280f, 720f);
            Assert.That(r.Top.height, Is.EqualTo(HudLayout.MaxTop));
            Assert.That(r.Bottom.height, Is.EqualTo(HudLayout.MaxBottom));
        }

        [Test]
        public void ActionBar_IsGroupedMovementCombatSpecial_AndHotkeysFollowTheVisualOrder()
        {
            int last = -1;
            for (int i = 0; i < ActionInfo.Bar.Length; i++)
            {
                int g = ActionInfo.Group(ActionInfo.Bar[i]);
                Assert.That(g, Is.GreaterThanOrEqualTo(last));
                last = g;
            }
            Assert.That(ActionInfo.Group(ActionType.CatchBreath), Is.EqualTo(0));
            Assert.That(ActionInfo.Group(ActionType.Keg), Is.EqualTo(1));
            Assert.That(ActionInfo.Group(ActionType.Signature), Is.EqualTo(2));
            for (int i = 0; i < 8; i++)
                Assert.That(ActionInfo.Hotkey(ActionInfo.Bar[i]), Is.EqualTo((i + 1).ToString()));
            Assert.That(ActionInfo.Hotkey(ActionType.Signature), Is.EqualTo("Q"));
        }

        [Test]
        public void FireAndReload_AreGreyedByTheProjectedMusketState()
        {
            var s = Scenario.From("A..B");
            var b = new PlanBuilder();
            b.Begin(s.State, Scenario.Slot('A'), s.Config);
            // Loaded at the start: Fire is fine, Reload is greyed.
            Assert.That(b.IssueFor(ActionType.Fire), Is.EqualTo(PlanIssue.None));
            Assert.That(b.IssueFor(ActionType.Reload), Is.EqualTo(PlanIssue.AlreadyLoaded));
            // After a Fire the musket is empty at the next tick: Fire greyed, Reload active.
            Assert.That(b.Choose(ActionType.Fire) && b.CommitDirection(Dir.E));
            Assert.That(b.IssueFor(ActionType.Fire), Is.EqualTo(PlanIssue.Unloaded));
            Assert.That(b.IssueFor(ActionType.Reload), Is.EqualTo(PlanIssue.None));
        }

        [Test]
        public void PlannedSwitcheroo_ReadsSwapWithName()
        {
            var a = PlannedAction.Switcheroo(2);
            var c = ScriptableObject.CreateInstance<Musketeers.Data.CharacterDef>();
            try
            {
                c.Ability = Ability.Switcheroo;
                var t = ActionInfo.Short(a, c, new[] { "Matej", "Matic", "Nik", "Aljaz" });
                Assert.That(t, Is.EqualTo("Swap with Nik"));
            }
            finally { Object.DestroyImmediate(c); }
        }
    }
}
