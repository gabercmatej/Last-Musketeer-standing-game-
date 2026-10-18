using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    // GAME_DESIGN §5 Reload / Parry / Catch Breath / fizzles, §6 Phase 0 and Phase 1.
    public class SelfActionTests
    {
        [Test]
        public void CatchBreath_AddsOneGrit()
        {
            Scenario.From("A.B")
                .Plan('A', Act.CatchBreath())
                .ResolveTick()
                .AssertGrit('A', 3)
                .AssertEvent(SimEventKind.CaughtBreath, 'A');
        }

        [Test]
        public void CatchBreath_CapsAtMaxGrit()
        {
            var s = Scenario.From("A.B")
                .WithGrit('A', 5)
                .Plan('A', Act.CatchBreath())
                .ResolveTick()
                .AssertGrit('A', 5);
            Assert.That(s.Find(SimEventKind.CaughtBreath, 'A').Value.Amount, Is.EqualTo(0));
        }

        [Test]
        public void Reload_LoadsMusket()
        {
            Scenario.From("A.B")
                .WithLoaded('A', false)
                .Plan('A', Act.Reload())
                .ResolveTick()
                .AssertLoaded('A', true)
                .AssertEvent(SimEventKind.Reloaded, 'A');
        }

        [Test]
        public void Reload_WhileLoaded_IsWastedNotFizzled()
        {
            Scenario.From("A.B")
                .Plan('A', Act.Reload())
                .ResolveTick()
                .AssertLoaded('A', true)
                .AssertNoEvent(SimEventKind.Fizzled);
        }

        [Test]
        public void Parry_CostsOneGrit_AndLastsOnlyThisTick()
        {
            var s = Scenario.From("A.B")
                .Plan('A', Act.Parry())
                .ResolveTick()
                .AssertGrit('A', 1)
                .AssertEvent(SimEventKind.ParryRaised, 'A');
            Assert.That(s.Unit('A').Parrying, Is.False);
        }

        [Test]
        public void Parry_WithoutGrit_Fizzles_SoShotHits()
        {
            Scenario.From("A..B")
                .WithGrit('B', 0)
                .Plan('A', Act.Fire(Dir.E))
                .Plan('B', Act.Parry())
                .ResolveTick()
                .AssertHp('B', 2)
                .AssertGrit('B', 0)
                .AssertEvent(SimEventKind.Fizzled, 'B')
                .AssertNoEvent(SimEventKind.Reflected);
        }

        [Test]
        public void Fire_Unloaded_FizzlesAtNoCost()
        {
            var s = Scenario.From("A..B")
                .WithLoaded('A', false)
                .Plan('A', Act.Fire(Dir.E))
                .ResolveTick()
                .AssertHp('B', 3)
                .AssertGrit('A', 2)
                .AssertLoaded('A', false)
                .AssertNoEvent(SimEventKind.ShotFired);
            var e = s.Find(SimEventKind.Fizzled, 'A').Value;
            Assert.That(e.Reason, Is.EqualTo((byte)FizzleReason.NotLoaded));
            Assert.That(e.Amount, Is.EqualTo((int)ActionType.Fire));
        }

        [Test]
        public void ReloadThenFire_InSameTurn_Works()
        {
            Scenario.From("A..B")
                .WithLoaded('A', false)
                .PlanTurn('A', Act.Reload(), Act.Fire(Dir.E), Act.CatchBreath())
                .ResolveTurn()
                .AssertHp('B', 2)
                .AssertLoaded('A', false)
                .AssertGrit('A', 3);
        }

        [Test]
        public void FireTwiceWithoutReload_SecondFizzles()
        {
            Scenario.From("A..B")
                .PlanTurn('A', Act.Fire(Dir.E), Act.Fire(Dir.E), Act.CatchBreath())
                .ResolveTurn()
                .AssertHp('B', 2)
                .AssertEventCount(SimEventKind.Fizzled, 1);
        }

        [Test]
        public void UnplannedTicks_AreIdle_NoGrit_AndTurnAdvances()
        {
            // Rule change 2026-10-18: unplanned ticks Idle; only a planned Catch Breath gives Grit.
            var s = Scenario.From("A..B")
                .ResolveTurn()
                .AssertGrit('A', 2)
                .AssertGrit('B', 2)
                .AssertEventCount(SimEventKind.CaughtBreath, 0)
                .AssertEventCount(SimEventKind.Fizzled, 0);
            Assert.That(s.State.Turn, Is.EqualTo(2));
        }

        [Test]
        public void DefaultPlannedAction_IsIdle()
        {
            Assert.That(default(PlannedAction).Type, Is.EqualTo(ActionType.Idle));
            Assert.That(new TurnPlan(2)[1].Type, Is.EqualTo(ActionType.Idle));
        }

        [Test]
        public void Idle_DoesNothing_EvenWhenPlannedNextToCatchBreath()
        {
            Scenario.From("A..B")
                .WithGrit('A', 1).WithGrit('B', 1)
                .Plan('A', Act.Idle())
                .Plan('B', Act.CatchBreath())
                .ResolveTick()
                .AssertGrit('A', 1)
                .AssertGrit('B', 2)
                .AssertNoEvent(SimEventKind.CaughtBreath, 'A')
                .AssertEvent(SimEventKind.CaughtBreath, 'B')
                .AssertPos('A', 0, 0);
        }

        [Test]
        public void AllCatchBreathPlan_GivesGritEveryTick()
        {
            // §9: disconnected players Catch Breath every tick; the host sends this plan for them.
            var s = Scenario.From("A..B").WithGrit('A', 0);
            TurnResolver.ResolveTurn(s.State, new[] { TurnPlan.AllCatchBreath(0), new TurnPlan(1) }, s.Config);
            Assert.That(s.Unit('A').Grit, Is.EqualTo(3));
            Assert.That(s.Unit('B').Grit, Is.EqualTo(2));
        }

        [Test]
        public void Plans_WithDuplicateSlot_Throw()
        {
            // A default TurnPlan claims slot 0; it must not silently replace slot 0's real plan.
            var s = Scenario.From("A..B");
            var real = new TurnPlan(0) { A0 = Act.Fire(Dir.E) };
            Assert.Throws<System.ArgumentException>(() =>
                TurnResolver.ResolveTick(s.State, new[] { real, default(TurnPlan) }, 0, s.Config));
        }

        [Test]
        public void CatchBreathGrit_IsSpendableNextTick()
        {
            Scenario.From("A...B")
                .WithGrit('A', 0)
                .PlanTurn('A', Act.CatchBreath(), Act.Dash(Dir.E), Act.CatchBreath())
                .ResolveTurn()
                .AssertPos('A', 2, 0)
                .AssertGrit('A', 1);
        }
    }
}
