using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    // Rule change 2026-10-18 (§6 Phase 2.2): movers whose paths cross or pass through each other in
    // the same tick bonk like a swap: neither moves, both take 1 damage, no kill credit.
    public class CrossingTests
    {
        [Test]
        public void DashesTowardEachOther_PassingThrough_Bonk()
        {
            // A would end on 2 passing 1, B on 1 passing 2.
            var s = Scenario.From("A..B")
                .Plan('A', Act.Dash(Dir.E))
                .Plan('B', Act.Dash(Dir.W))
                .ResolveTick()
                .AssertBoard("A..B")
                .AssertHp('A', 2)
                .AssertHp('B', 2)
                .AssertEventCount(SimEventKind.Bonk, 2);
            Assert.That(s.Unit('A').LastAttacker, Is.EqualTo(-1));
            Assert.That(s.Unit('B').LastAttacker, Is.EqualTo(-1));
        }

        [Test]
        public void DashThroughUnitMovingTheOtherWay_Bonks()
        {
            // B steps into A's middle tile while A dashes onto B's start tile.
            Scenario.From("A.B")
                .Plan('A', Act.Dash(Dir.E))
                .Plan('B', Act.Move(Dir.W))
                .ResolveTick()
                .AssertBoard("A.B")
                .AssertHp('A', 2)
                .AssertHp('B', 2);
        }

        [Test]
        public void PerpendicularWalkerIntoDashMiddle_Bonks()
        {
            Scenario.From(@"
                .B.
                A..")
                .Plan('A', Act.Dash(Dir.E))
                .Plan('B', Act.Move(Dir.S))
                .ResolveTick()
                .AssertBoard(@"
                .B.
                A..")
                .AssertHp('A', 2)
                .AssertHp('B', 2);
        }

        [Test]
        public void PerpendicularDashes_CrossingMiddles_Bonk()
        {
            Scenario.From(@"
                .B.
                A..
                ...")
                .Plan('A', Act.Dash(Dir.E))
                .Plan('B', Act.Dash(Dir.S))
                .ResolveTick()
                .AssertPos('A', 0, 1)
                .AssertPos('B', 1, 2)
                .AssertHp('A', 2)
                .AssertHp('B', 2);
        }

        [Test]
        public void DashFollowingAWalker_DoesNotBonk()
        {
            // A dashes onto the tile B is leaving; their paths never share a tile or an edge.
            Scenario.From("A.B.")
                .Plan('A', Act.Dash(Dir.E))
                .Plan('B', Act.Move(Dir.E))
                .ResolveTick()
                .AssertBoard("..AB")
                .AssertNoEvent(SimEventKind.Bonk);
        }

        [Test]
        public void WalkingAwayFromADashersPath_DoesNotBonk()
        {
            // B leaves the row A dashes along; B never enters A's tiles.
            Scenario.From(@"
                ...
                .B.
                A..")
                .Plan('A', Act.Dash(Dir.E))
                .Plan('B', Act.Move(Dir.N))
                .ResolveTick()
                .AssertPos('A', 2, 0)
                .AssertPos('B', 1, 2)
                .AssertNoEvent(SimEventKind.Bonk);
        }

        [Test]
        public void CrossingOverAPit_BonksAndNobodyFalls()
        {
            // Both want to pass through the pit tile: they collide above it and stay put.
            Scenario.From(@"
                .B.
                A~.
                ...")
                .Plan('A', Act.Dash(Dir.E))
                .Plan('B', Act.Dash(Dir.S))
                .ResolveTick()
                .AssertAlive('A')
                .AssertAlive('B')
                .AssertPos('A', 0, 1)
                .AssertNoEvent(SimEventKind.Fell);
        }
    }
}
