using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    // GAME_DESIGN §6 Turn start and §3 The Cardinal's Guard closes in.
    public class TurnStartTests
    {
        const string Board9 = @"
            .........
            .........
            .........
            .........
            ....A....
            .........
            .........
            .........
            B........";

        [Test]
        public void Turn1_NoGrit_FromTurn2_PlusOne_Capped()
        {
            Scenario.From("A.B").WithTurn(1).TurnStart().AssertGrit('A', 2);
            Scenario.From("A.B").WithTurn(2).TurnStart().AssertGrit('A', 3);
            Scenario.From("A.B").WithTurn(3).WithGrit('B', 5).TurnStart().AssertGrit('B', 5);
        }

        [Test]
        public void DeadUnits_GetNoGrit()
        {
            var s = Scenario.From("A.B").WithTurn(2);
            s.Unit('B').Alive = false;
            s.TurnStart().AssertGrit('B', 2);
        }

        [TestCase(0, 5, 6)]
        [TestCase(1, 7, 8)]
        [TestCase(2, 9, 10)]
        [TestCase(3, 11, 12)]
        [TestCase(4, 13, 14)]   // the center falls on turn 14
        public void Schedule_RingWarnedThenFallsNextTurn(int ring, int warn, int fall)
        {
            var b = new Board(9, 9);
            var cfg = GameConfig.Default;
            Assert.That(Crumble.WarnedRing(warn, b, cfg), Is.EqualTo(ring));
            Assert.That(Crumble.FallingRing(fall, b, cfg), Is.EqualTo(ring));
            // The turn a ring falls is not also the turn the next ring is warned (§3: ring 1 is warned on 7).
            Assert.That(Crumble.WarnedRing(fall, b, cfg), Is.EqualTo(-1));
        }

        [Test]
        public void NothingHappens_BeforeTurn5_OrAfterTheCenterFell()
        {
            var b = new Board(9, 9);
            var cfg = GameConfig.Default;
            for (int t = 1; t <= 4; t++)
            {
                Assert.That(Crumble.WarnedRing(t, b, cfg), Is.EqualTo(-1), $"turn {t}");
                Assert.That(Crumble.FallingRing(t, b, cfg), Is.EqualTo(-1), $"turn {t}");
            }
            Assert.That(Crumble.WarnedRing(15, b, cfg), Is.EqualTo(-1));
            Assert.That(Crumble.FallingRing(16, b, cfg), Is.EqualTo(-1));
        }

        [Test]
        public void Turn5_WarnsOuterRing_WithoutChangingTheBoard()
        {
            var s = Scenario.From(Board9).WithTurn(5).TurnStart()
                .AssertEventCount(SimEventKind.TileWarned, 32)
                .AssertNoEvent(SimEventKind.TileCrumbled)
                .AssertTile(0, 0, TileType.Floor)
                .AssertAlive('B');
            Assert.That(Crumble.IsWarned(s.State, new Int2(0, 0), s.Config), Is.True);
            Assert.That(Crumble.IsWarned(s.State, new Int2(1, 1), s.Config), Is.False);
        }

        [Test]
        public void Turn6_OuterRingCrumbles_UnitOnItFalls()
        {
            Scenario.From(Board9).WithTurn(6).TurnStart()
                .AssertEventCount(SimEventKind.TileCrumbled, 32)
                .AssertEventCount(SimEventKind.TileWarned, 0)
                .AssertTile(0, 0, TileType.Pit)
                .AssertTile(8, 4, TileType.Pit)
                .AssertTile(1, 1, TileType.Floor)
                .AssertDead('B')
                .AssertSelfKill('B')
                .AssertAlive('A');
        }

        [Test]
        public void Turn7_WarnsSecondRing()
        {
            Scenario.From(Board9).WithTurn(7).TurnStart()
                .AssertEventCount(SimEventKind.TileWarned, 24)
                .AssertNoEvent(SimEventKind.TileCrumbled);
        }

        [Test]
        public void CrumbleFall_WithinKillCreditWindow_CreditsAttacker()
        {
            Scenario.From(Board9).WithTurn(6)
                .WithLastAttacker('B', 'A', 5)
                .TurnStart()
                .AssertDead('B')
                .AssertKilledBy('B', 'A');
        }

        [Test]
        public void CrumbleFall_EndsTheRound_WhenOneIsLeft()
        {
            var s = Scenario.From(Board9).WithTurn(6).TurnStart()
                .AssertEvent(SimEventKind.RoundEnded);
            Assert.That(s.State.RoundOver, Is.True);
            Assert.That(s.State.RoundWinner, Is.EqualTo(0));
        }

        [Test]
        public void CrumbleFall_TakesKegsAndCrates()
        {
            var s = Scenario.From(@"
                K...o
                .....
                ..A..
                .B...
                o....").WithTurn(6);
            s.TurnStart()
                .AssertNoKegs()
                .AssertTile(4, 4, TileType.Pit)
                .AssertTile(0, 0, TileType.Pit)
                .AssertAlive('A')
                .AssertAlive('B');
        }

        [Test]
        public void CenterFalls_OnTurn14_EveryoneLeftDies()
        {
            // Two units would need two tiles, so put them on the 3×3 center of a 7×7 board with a
            // config whose last ring (3) falls on turn 14 too.
            var cfg = new GameConfig { CrumbleStartTurn = 7, CrumbleEvery = 2 };
            var s = Scenario.From(@"
                .......
                .......
                .......
                ...A...
                .......
                .......
                .......", cfg);
            Assert.That(Crumble.FallingRing(14, s.State.Board, cfg), Is.EqualTo(3));
            s.WithTurn(14).TurnStart().AssertDead('A');
            Assert.That(s.State.RoundOver, Is.True);
            Assert.That(s.State.RoundWinner, Is.EqualTo(-1));
        }
    }
}
