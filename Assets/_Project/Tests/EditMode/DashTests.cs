using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    // GAME_DESIGN §5 Dash and §6 Phase 2.1/2.2 dash rules (FIXES_v0.2 §3.2 rule 1): 2 tiles, flown over
    // crates, kegs, units and pits; only a pillar on the middle tile blocks it; the landing must be free.
    public class DashTests
    {
        [Test]
        public void Dash_MovesTwoTiles_CostsOneGrit()
        {
            Scenario.From("A...")
                .Plan('A', Act.Dash(Dir.E))
                .ResolveTick()
                .AssertBoard("..A.")
                .AssertGrit('A', 2)
                .AssertEvent(SimEventKind.Dashed, 'A')
                .AssertNoEvent(SimEventKind.Leaped);
        }

        [Test]
        public void Dash_FliesOverAOneTilePit()
        {
            Scenario.From("A~.")
                .Plan('A', Act.Dash(Dir.E))
                .ResolveTick()
                .AssertAlive('A')
                .AssertBoard(".~A")
                .AssertEvent(SimEventKind.Leaped, 'A');
        }

        [Test]
        public void Dash_FliesOverACrate_WhichStays()
        {
            Scenario.From("Ao.")
                .Plan('A', Act.Dash(Dir.E))
                .ResolveTick()
                .AssertBoard(".oA")
                .AssertTile(1, 0, TileType.Crate)
                .AssertEvent(SimEventKind.Leaped, 'A');
        }

        [Test]
        public void Dash_FliesOverAKeg_WhichStays()
        {
            Scenario.From("AK.")
                .Plan('A', Act.Dash(Dir.E))
                .ResolveTick()
                .AssertBoard(".KA")
                .AssertKegAt(1, 0)
                .AssertEvent(SimEventKind.Leaped, 'A');
        }

        [Test]
        public void Dash_FliesOverAUnit_NoBonk_NoDamage()
        {
            Scenario.From("AB.")
                .Plan('A', Act.Dash(Dir.E))
                .ResolveTick()
                .AssertBoard(".BA")
                .AssertHp('A', 3)
                .AssertHp('B', 3)
                .AssertNoEvent(SimEventKind.Bonk)
                .AssertEvent(SimEventKind.Leaped, 'A');
        }

        [Test]
        public void Dash_LandingInAPit_Falls()
        {
            var s = Scenario.From("A~~.B")
                .Plan('A', Act.Dash(Dir.E))
                .ResolveTick()
                .AssertDead('A');
            Assert.That(s.Find(SimEventKind.Fell, 'A').Value.To, Is.EqualTo(new Int2(2, 0)), "falls into the landing tile");
        }

        [Test]
        public void Dash_MiddleTilePillar_Blocked_GritStillSpent()
        {
            // DECISIONS: costs are paid in Phase 0, so a dash that fails keeps its cost.
            Scenario.From("A#..")
                .Plan('A', Act.Dash(Dir.E))
                .ResolveTick()
                .AssertBoard("A#..")
                .AssertGrit('A', 2)
                .AssertEvent(SimEventKind.MoveBlocked, 'A');
        }

        [TestCase("A.#.")]
        [TestCase("A.o.")]
        [TestCase("A.K.")]
        public void Dash_LandingSolid_Fails_StaysPut_GritSpent(string row)
        {
            var s = Scenario.From(row)
                .Plan('A', Act.Dash(Dir.E))
                .ResolveTick()
                .AssertPos('A', 0, 0)
                .AssertGrit('A', 2)
                .AssertEvent(SimEventKind.MoveBlocked, 'A')
                .AssertNoEvent(SimEventKind.Dashed);
            Assert.That(s.Find(SimEventKind.MoveBlocked, 'A').Value.Reason, Is.EqualTo((byte)BlockReason.Obstacle));
        }

        [Test]
        public void Dash_LandingHeldByStationaryUnit_StaysOnStartTile_NoDamage()
        {
            Scenario.From("A.B")
                .Plan('A', Act.Dash(Dir.E))
                .ResolveTick()
                .AssertBoard("A.B")
                .AssertHp('A', 3)
                .AssertHp('B', 3)
                .AssertEvent(SimEventKind.MoveBlocked, 'A');
        }

        [Test]
        public void Dash_LandingUnitMovesAway_DashSucceeds()
        {
            Scenario.From(@"
                ...
                A.B")
                .Plan('A', Act.Dash(Dir.E))
                .Plan('B', Act.Move(Dir.N))
                .ResolveTick()
                .AssertPos('A', 2, 0)
                .AssertPos('B', 2, 1)
                .AssertNoEvent(SimEventKind.Bonk);
        }

        [Test]
        public void Dash_SharedLandingTileWithWalker_BothBonk()
        {
            Scenario.From("A..B")
                .Plan('A', Act.Dash(Dir.E))
                .Plan('B', Act.Move(Dir.W))
                .ResolveTick()
                .AssertBoard("A..B")
                .AssertHp('A', 2)
                .AssertHp('B', 2)
                .AssertEventCount(SimEventKind.Bonk, 2);
        }

        [Test]
        public void Dash_SharedLandingTileWithAnotherDash_BothBonk()
        {
            Scenario.From("A...B")
                .Plan('A', Act.Dash(Dir.E))
                .Plan('B', Act.Dash(Dir.W))
                .ResolveTick()
                .AssertBoard("A...B")
                .AssertHp('A', 2)
                .AssertHp('B', 2);
        }

        [Test]
        public void Dash_WalkerSteppingIntoTheFlownOverTile_NoBonk()
        {
            Scenario.From(@"
                .B.
                A..")
                .Plan('A', Act.Dash(Dir.E))
                .Plan('B', Act.Move(Dir.S))
                .ResolveTick()
                .AssertPos('A', 2, 0)
                .AssertPos('B', 1, 0)
                .AssertNoEvent(SimEventKind.Bonk);
        }

        [Test]
        public void Dash_WithoutGrit_FizzlesAtNoCost()
        {
            Scenario.From("A..")
                .WithGrit('A', 0)
                .Plan('A', Act.Dash(Dir.E))
                .ResolveTick()
                .AssertBoard("A..")
                .AssertGrit('A', 0)
                .AssertEvent(SimEventKind.Fizzled, 'A');
        }
    }
}
