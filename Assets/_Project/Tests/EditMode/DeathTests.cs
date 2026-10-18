using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    // GAME_DESIGN §6 Phase 6, kill credit and round end.
    public class DeathTests
    {
        [Test]
        public void LethalDamageBeforeShots_UnitStillFires_DoubleKO()
        {
            // B slams A into the pillar (lethal, Phase 3) but A still fires in Phase 4. Phase 2 damage
            // can only hit movers, who can't also fire, so Phase 3 is where this happens in play.
            var s = Scenario.From("#AB")
                .WithHp('A', 1)
                .WithHp('B', 1)
                .Plan('A', Act.Fire(Dir.E))
                .Plan('B', Act.Shove(Dir.W))
                .ResolveTick()
                .AssertDead('A')
                .AssertDead('B')
                .AssertKilledBy('A', 'B')
                .AssertKilledBy('B', 'A')
                .AssertEvent(SimEventKind.RoundEnded);
            Assert.That(s.State.RoundWinner, Is.EqualTo(-1), "everyone died on the same tick: draw");
        }

        [Test]
        public void LethalBonkInMovement_DiesAtEndOfTick()
        {
            var s = Scenario.From("A.B")
                .WithHp('A', 1)
                .WithHp('B', 1)
                .Plan('A', Act.Move(Dir.E))
                .Plan('B', Act.Move(Dir.W))
                .ResolveTick()
                .AssertDead('A')
                .AssertDead('B');
            var died = s.Find(SimEventKind.Died, 'A').Value;
            Assert.That(died.Phase, Is.EqualTo(SimPhase.Deaths));
            Assert.That(died.Reason, Is.EqualTo((byte)DeathCause.Damage));
        }

        [Test]
        public void UnitThatFellEarlierInTick_DoesNotShoot()
        {
            Scenario.From("BA~")
                .Plan('B', Act.Shove(Dir.E))
                .Plan('A', Act.Fire(Dir.W))
                .ResolveTick()
                .AssertDead('A')
                .AssertHp('B', 3)
                .AssertNoEvent(SimEventKind.ShotFired);
        }

        [Test]
        public void KillCredit_AttackedLastTurn_GoesToAttacker()
        {
            Scenario.From("A~..B")
                .WithTurn(2)
                .WithLastAttacker('A', 'B', 1)
                .Plan('A', Act.Move(Dir.E))
                .ResolveTick()
                .AssertKilledBy('A', 'B');
        }

        [Test]
        public void KillCredit_AttackedTwoTurnsAgo_IsSelfKill()
        {
            Scenario.From("A~..B")
                .WithTurn(3)
                .WithLastAttacker('A', 'B', 1)
                .Plan('A', Act.Move(Dir.E))
                .ResolveTick()
                .AssertSelfKill('A');
        }

        [Test]
        public void KillCredit_ShotDeath_GoesToShooter()
        {
            Scenario.From("A..B")
                .WithHp('B', 1)
                .Plan('A', Act.Fire(Dir.E))
                .ResolveTick()
                .AssertKilledBy('B', 'A');
        }

        [Test]
        public void KillCredit_TwoShootersSameTick_FirstInInitiativeWins()
        {
            // Turn 1 with 3 players: initiative order is C, A, B. C shoots first, so C gets credit.
            Scenario.From(@"
                ..C
                A.B")
                .WithHp('B', 1)
                .Plan('A', Act.Fire(Dir.E))
                .Plan('C', Act.Fire(Dir.S))
                .ResolveTick()
                .AssertHp('B', 0)
                .AssertKilledBy('B', 'C');
        }

        [Test]
        public void RoundEnds_WhenOneLeft()
        {
            var s = Scenario.From("A..B")
                .WithHp('B', 1)
                .Plan('A', Act.Fire(Dir.E))
                .ResolveTick()
                .AssertEvent(SimEventKind.RoundEnded, 'A');
            Assert.That(s.State.RoundOver, Is.True);
            Assert.That(s.State.RoundWinner, Is.EqualTo(0));
        }

        [Test]
        public void RoundContinues_WhileTwoAlive()
        {
            var s = Scenario.From("A.B.C")
                .WithHp('B', 1)
                .Plan('A', Act.Fire(Dir.E))
                .ResolveTick()
                .AssertDead('B')
                .AssertNoEvent(SimEventKind.RoundEnded);
            Assert.That(s.State.RoundOver, Is.False);
        }

        [Test]
        public void ResolveTurn_StopsTicksAfterRoundEnds()
        {
            var s = Scenario.From("A...B")
                .WithHp('B', 1)
                .PlanTurn('A', Act.Fire(Dir.E), Act.Move(Dir.E), Act.Move(Dir.E))
                .ResolveTurn()
                .AssertPos('A', 0, 0);
            foreach (var e in s.Log.Events)
                Assert.That(e.Tick, Is.EqualTo(0), $"no events after the round ended: {e}");
        }
    }
}
