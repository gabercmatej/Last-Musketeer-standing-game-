using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    // GAME_DESIGN §5 Dash and §6 Phase 2.1 dash rules.
    public class DashTests
    {
        [Test]
        public void Dash_MovesTwoTiles_CostsOneGrit()
        {
            Scenario.From("A...")
                .Plan('A', Act.Dash(Dir.E))
                .ResolveTick()
                .AssertBoard("..A.")
                .AssertGrit('A', 1)
                .AssertEvent(SimEventKind.Dashed, 'A');
        }

        [Test]
        public void Dash_LeapsOneTilePit()
        {
            Scenario.From("A~.")
                .Plan('A', Act.Dash(Dir.E))
                .ResolveTick()
                .AssertAlive('A')
                .AssertBoard(".~A")
                .AssertEvent(SimEventKind.Leaped, 'A');
        }

        [Test]
        public void Dash_TwoTilePit_FallsIntoSecond()
        {
            var s = Scenario.From("A~~.B")
                .Plan('A', Act.Dash(Dir.E))
                .ResolveTick()
                .AssertDead('A')
                .AssertNoEvent(SimEventKind.Leaped);
            Assert.That(s.Find(SimEventKind.Fell, 'A').Value.To, Is.EqualTo(new Int2(2, 0)), "falls into the second pit tile");
        }

        [Test]
        public void Dash_SecondTileCrate_StopsOnFirst()
        {
            Scenario.From("A.o")
                .Plan('A', Act.Dash(Dir.E))
                .ResolveTick()
                .AssertBoard(".Ao");
        }

        [Test]
        public void Dash_SecondTileBlockedAfterPit_StopsInPitAndFalls()
        {
            Scenario.From("A~#.B")
                .Plan('A', Act.Dash(Dir.E))
                .ResolveTick()
                .AssertDead('A')
                .AssertSelfKill('A');
        }

        [Test]
        public void Dash_FirstTilePillar_Cancelled_GritStillSpent()
        {
            // DECISIONS: costs are paid in Phase 0, so a dash cancelled in Phase 2 keeps its cost.
            Scenario.From("A#..")
                .Plan('A', Act.Dash(Dir.E))
                .ResolveTick()
                .AssertBoard("A#..")
                .AssertGrit('A', 1)
                .AssertEvent(SimEventKind.MoveBlocked, 'A');
        }

        [Test]
        public void Dash_UnitInMiddleTile_Cancelled()
        {
            Scenario.From("AB..")
                .Plan('A', Act.Dash(Dir.E))
                .ResolveTick()
                .AssertBoard("AB..")
                .AssertHp('A', 3)
                .AssertHp('B', 3);
        }

        [Test]
        public void Dash_UnitInMiddleThatMovesAway_StillCancelled()
        {
            // §6: the middle tile is checked at the start of the tick.
            Scenario.From("AB..")
                .Plan('A', Act.Dash(Dir.E))
                .Plan('B', Act.Move(Dir.E))
                .ResolveTick()
                .AssertBoard("A.B.");
        }

        [Test]
        public void Dash_DestinationHeldByStationaryUnit_StaysOnStartTile()
        {
            // DECISIONS: "blocked" (Phase 2.3) keeps the mover where it was; only solid second tiles shorten a dash.
            Scenario.From("A.B")
                .Plan('A', Act.Dash(Dir.E))
                .ResolveTick()
                .AssertBoard("A.B")
                .AssertHp('A', 3)
                .AssertEvent(SimEventKind.MoveBlocked, 'A');
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

        [Test]
        public void Dash_SameTileAsWalker_BothBonk()
        {
            Scenario.From("A..B")
                .Plan('A', Act.Dash(Dir.E))
                .Plan('B', Act.Move(Dir.W))
                .ResolveTick()
                .AssertBoard("A..B")
                .AssertHp('A', 2)
                .AssertHp('B', 2);
        }
    }
}
