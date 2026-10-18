using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    // GAME_DESIGN §6: initiative = (slot + turn) % playerCount, lowest first.
    public class InitiativeTests
    {
        [Test]
        public void Initiative_FollowsFormula()
        {
            Assert.That(TurnResolver.Initiative(0, 1, 4), Is.EqualTo(1));
            Assert.That(TurnResolver.Initiative(3, 1, 4), Is.EqualTo(0));
            Assert.That(TurnResolver.Initiative(1, 2, 2), Is.EqualTo(1));
        }

        [Test]
        public void InitiativeOrder_RotatesWithTurn()
        {
            Assert.That(TurnResolver.InitiativeOrder(1, 4), Is.EqualTo(new[] { 3, 0, 1, 2 }));
            Assert.That(TurnResolver.InitiativeOrder(2, 4), Is.EqualTo(new[] { 2, 3, 0, 1 }));
            Assert.That(TurnResolver.InitiativeOrder(4, 4), Is.EqualTo(new[] { 0, 1, 2, 3 }));
            Assert.That(TurnResolver.InitiativeOrder(1, 2), Is.EqualTo(new[] { 1, 0 }));
        }

        [Test]
        public void PushesApplyInTargetInitiativeOrder_Turn1()
        {
            // B and C are both pushed toward x=2. Turn 1 order is D,A,B,C: B lands first, C bonks into it.
            Scenario.From("AB.CD")
                .WithTurn(1)
                .Plan('A', Act.Shove(Dir.E))
                .Plan('D', Act.Shove(Dir.W))
                .ResolveTick()
                .AssertBoard("A.BCD")
                .AssertHp('B', 2)
                .AssertHp('C', 2);
        }

        [Test]
        public void PushesApplyInTargetInitiativeOrder_Turn2()
        {
            // Turn 2 order is C,D,A,B: now C lands first and B bonks into it.
            Scenario.From("AB.CD")
                .WithTurn(2)
                .Plan('A', Act.Shove(Dir.E))
                .Plan('D', Act.Shove(Dir.W))
                .ResolveTick()
                .AssertBoard("ABC.D")
                .AssertHp('B', 2)
                .AssertHp('C', 2);
        }
    }
}
