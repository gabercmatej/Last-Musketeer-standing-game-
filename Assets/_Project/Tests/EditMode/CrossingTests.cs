using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    // §6 Phase 2.2 (rule change 2026-10-18, narrowed 2026-10-20 by FIXES §3.2 rule 1): movers bonk when they
    // enter the same tile or swap tiles. A dash only enters its landing tile, so flying over someone or
    // crossing a path is not a collision.
    public class CrossingTests
    {
        [Test]
        public void DashesTowardEachOther_PassOverEachOther_NoBonk()
        {
            // A lands on 2, B lands on 1: different tiles, no swap.
            Scenario.From("A..B")
                .Plan('A', Act.Dash(Dir.E))
                .Plan('B', Act.Dash(Dir.W))
                .ResolveTick()
                .AssertBoard(".BA.")
                .AssertNoEvent(SimEventKind.Bonk);
        }

        [Test]
        public void DashesOntoEachOthersStartTiles_ExactSwap_Bonk()
        {
            var s = Scenario.From("A.B")
                .Plan('A', Act.Dash(Dir.E))
                .Plan('B', Act.Dash(Dir.W))
                .ResolveTick()
                .AssertBoard("A.B")
                .AssertHp('A', 2)
                .AssertHp('B', 2)
                .AssertEventCount(SimEventKind.Bonk, 2);
            Assert.That(s.Unit('A').LastAttacker, Is.EqualTo(-1));
            Assert.That(s.Unit('B').LastAttacker, Is.EqualTo(-1));
        }

        [Test]
        public void DashOverUnitWalkingTheOtherWay_NoBonk()
        {
            // B steps into A's flown-over tile while A lands on the tile B just left.
            Scenario.From("A.B")
                .Plan('A', Act.Dash(Dir.E))
                .Plan('B', Act.Move(Dir.W))
                .ResolveTick()
                .AssertBoard(".BA")
                .AssertNoEvent(SimEventKind.Bonk);
        }

        [Test]
        public void PerpendicularDashes_CrossingMiddles_NoBonk()
        {
            Scenario.From(@"
                .B.
                A..
                ...")
                .Plan('A', Act.Dash(Dir.E))
                .Plan('B', Act.Dash(Dir.S))
                .ResolveTick()
                .AssertPos('A', 2, 1)
                .AssertPos('B', 1, 0)
                .AssertNoEvent(SimEventKind.Bonk);
        }

        [Test]
        public void DashFollowingAWalker_DoesNotBonk()
        {
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
        public void CrossingOverAPit_NobodyBonksOrFalls()
        {
            // Both fly over the pit tile and land on different tiles.
            Scenario.From(@"
                .B.
                A~.
                ...")
                .Plan('A', Act.Dash(Dir.E))
                .Plan('B', Act.Dash(Dir.S))
                .ResolveTick()
                .AssertAlive('A')
                .AssertAlive('B')
                .AssertPos('A', 2, 1)
                .AssertPos('B', 1, 0)
                .AssertNoEvent(SimEventKind.Fell);
        }

        [Test]
        public void WalkersSwappingPlaces_StillBonk()
        {
            Scenario.From("AB")
                .Plan('A', Act.Move(Dir.E))
                .Plan('B', Act.Move(Dir.W))
                .ResolveTick()
                .AssertBoard("AB")
                .AssertHp('A', 2)
                .AssertHp('B', 2);
        }
    }
}
