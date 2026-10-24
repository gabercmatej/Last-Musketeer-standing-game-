using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    public class GrappinParryTests
    {
        [TestCase("A...B", 4)]
        [TestCase("A.~.B", 4)]
        [TestCase("AB", 1)]
        public void SuccessfulParryBlocksAllTargetEffects(string map, int x)
        {
            var s = Scenario.From(map).WithAbility('A', Ability.Grappin).WithGrit('A', 5)
                .WithGrit('B', 5).Plan('A', Act.Sig(Dir.E)).Plan('B', Act.Parry()).ResolveTick();
            s.AssertPos('B', x, 0).AssertAlive('B').AssertHp('B', 3).AssertGrit('A', 0)
                .AssertNoEvent(SimEventKind.Grappled).AssertNoEvent(SimEventKind.Damaged)
                .AssertNoEvent(SimEventKind.PointsScored).AssertEvent(SimEventKind.Parried, 'B');
            Assert.That(s.Unit('B').LastAttacker, Is.EqualTo(-1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NoOrUnfundedParryStillAllowsDrag(bool attemptsParry)
        {
            var s = Scenario.From("A...B").WithAbility('A', Ability.Grappin).WithGrit('A', 5)
                .WithGrit('B', 0).Plan('A', Act.Sig(Dir.E));
            if (attemptsParry) s.Plan('B', Act.Parry());
            s.ResolveTick().AssertPos('B', 1, 0).AssertGrit('A', 0);
            Assert.That(s.Unit('B').LastAttacker, Is.EqualTo(0));
        }

        [Test]
        public void ParryResolutionIsIdenticalAcrossReplicas()
        {
            var a = Scenario.From("A.~.B").WithAbility('A', Ability.Grappin).WithGrit('A', 5).WithGrit('B', 5);
            var b = a.State.Clone();
            var plans = new[] { new TurnPlan(0), new TurnPlan(1) };
            plans[0][0] = Act.Sig(Dir.E); plans[1][0] = Act.Parry();
            TurnResolver.ResolveTurn(a.State, plans, a.Config);
            TurnResolver.ResolveTurn(b, plans, a.Config);
            Assert.That(a.State.ComputeHash(), Is.EqualTo(b.ComputeHash()));
            Assert.That(b.Units[1].Alive, Is.True);
        }
    }
}
