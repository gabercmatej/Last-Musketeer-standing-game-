using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    // GAME_DESIGN §6 Phase 2: Move, collisions, blocked chains, rotations, falls.
    public class MovementTests
    {
        [Test]
        public void Move_GoesOneTile()
        {
            Scenario.From(@"
                ...
                .A.
                ...")
                .Plan('A', Act.Move(Dir.E))
                .ResolveTick()
                .AssertBoard(@"
                ...
                ..A
                ...")
                .AssertEvent(SimEventKind.Moved, 'A');
        }

        [TestCase("A#.")]
        [TestCase("Ao.")]
        [TestCase("AK.")]
        public void Move_IntoPillarCrateOrKeg_IsCancelled(string row)
        {
            Scenario.From(row)
                .Plan('A', Act.Move(Dir.E))
                .ResolveTick()
                .AssertBoard(row)
                .AssertHp('A', 3)
                .AssertEvent(SimEventKind.MoveBlocked, 'A');
        }

        [Test]
        public void SameTile_TwoUnits_BothBonkAndStay()
        {
            Scenario.From("A.B")
                .Plan('A', Act.Move(Dir.E))
                .Plan('B', Act.Move(Dir.W))
                .ResolveTick()
                .AssertBoard("A.B")
                .AssertHp('A', 2)
                .AssertHp('B', 2)
                .AssertEventCount(SimEventKind.Bonk, 2);
        }

        [Test]
        public void SameTile_ThreeUnits_AllBonkAndStay()
        {
            Scenario.From(@"
                .C.
                A.B")
                .Plan('A', Act.Move(Dir.E))
                .Plan('B', Act.Move(Dir.W))
                .Plan('C', Act.Move(Dir.S))
                .ResolveTick()
                .AssertBoard(@"
                .C.
                A.B")
                .AssertHp('A', 2)
                .AssertHp('B', 2)
                .AssertHp('C', 2);
        }

        [Test]
        public void Swap_BothBonkAndStay()
        {
            Scenario.From("AB")
                .Plan('A', Act.Move(Dir.E))
                .Plan('B', Act.Move(Dir.W))
                .ResolveTick()
                .AssertBoard("AB")
                .AssertHp('A', 2)
                .AssertHp('B', 2);
        }

        [Test]
        public void Bonk_GivesNoKillCredit()
        {
            // DECISIONS: movement bonks are mutual accidents, so they never set LastAttacker.
            Scenario.From("A.B")
                .WithHp('A', 1)
                .Plan('A', Act.Move(Dir.E))
                .Plan('B', Act.Move(Dir.W))
                .ResolveTick()
                .AssertDead('A')
                .AssertSelfKill('A');
        }

        [Test]
        public void BlockedChain_StationaryFront_BlocksEveryoneWithoutDamage()
        {
            Scenario.From("ABC.")
                .Plan('A', Act.Move(Dir.E))
                .Plan('B', Act.Move(Dir.E))
                .ResolveTick()
                .AssertBoard("ABC.")
                .AssertHp('A', 3)
                .AssertHp('B', 3)
                .AssertHp('C', 3)
                .AssertEventCount(SimEventKind.MoveBlocked, 2)
                .AssertNoEvent(SimEventKind.Bonk);
        }

        [Test]
        public void Blocked_ByUnitThatBonked()
        {
            // A and B bonk over x=2, so A stays and C (walking into A's tile) is blocked.
            Scenario.From("CA.B")
                .Plan('A', Act.Move(Dir.E))
                .Plan('B', Act.Move(Dir.W))
                .Plan('C', Act.Move(Dir.E))
                .ResolveTick()
                .AssertBoard("CA.B")
                .AssertHp('C', 3)
                .AssertEvent(SimEventKind.MoveBlocked, 'C');
        }

        [Test]
        public void FollowTheLeader_MovesIntoTileBeingLeft()
        {
            Scenario.From("AB.")
                .Plan('A', Act.Move(Dir.E))
                .Plan('B', Act.Move(Dir.E))
                .ResolveTick()
                .AssertBoard(".AB")
                .AssertHp('A', 3)
                .AssertHp('B', 3);
        }

        [Test]
        public void Rotation_FourUnits_AllSucceed()
        {
            // The smallest possible rotation: a 3-cycle can't exist on a 4-direction grid (DECISIONS).
            Scenario.From(@"
                AB
                DC")
                .Plan('A', Act.Move(Dir.E))
                .Plan('B', Act.Move(Dir.S))
                .Plan('C', Act.Move(Dir.W))
                .Plan('D', Act.Move(Dir.N))
                .ResolveTick()
                .AssertBoard(@"
                DA
                CB")
                .AssertNoEvent(SimEventKind.Bonk)
                .AssertNoEvent(SimEventKind.MoveBlocked);
        }

        [Test]
        public void Rotation_OneMemberBlocked_WholeLoopStays()
        {
            // D stays, so C is blocked by D, B by C, A by B: the chain unwinds, nothing moves, no damage.
            Scenario.From(@"
                AB
                DC")
                .Plan('A', Act.Move(Dir.E))
                .Plan('B', Act.Move(Dir.S))
                .Plan('C', Act.Move(Dir.W))
                .ResolveTick()
                .AssertBoard(@"
                AB
                DC")
                .AssertEventCount(SimEventKind.MoveBlocked, 3)
                .AssertNoEvent(SimEventKind.Damaged);
        }

        [Test]
        public void WalkIntoPit_FallsInstantly_AsSelfKill()
        {
            Scenario.From("A~..B")
                .Plan('A', Act.Move(Dir.E))
                .ResolveTick()
                .AssertDead('A')
                .AssertEvent(SimEventKind.Fell, 'A')
                .AssertSelfKill('A');
        }

        [Test]
        public void WalkOffBoard_CountsAsPit()
        {
            Scenario.From("A.B")
                .Plan('A', Act.Move(Dir.W))
                .ResolveTick()
                .AssertDead('A')
                .AssertEvent(SimEventKind.Fell, 'A');
        }

        [Test]
        public void TwoUnitsIntoSamePit_BonkInsteadOfFalling()
        {
            Scenario.From("A~B")
                .Plan('A', Act.Move(Dir.E))
                .Plan('B', Act.Move(Dir.W))
                .ResolveTick()
                .AssertAlive('A')
                .AssertAlive('B')
                .AssertBoard("A~B")
                .AssertHp('A', 2);
        }

        [Test]
        public void FallIsInstant_DiesInMovementPhase()
        {
            var s = Scenario.From("A~..B")
                .Plan('A', Act.Move(Dir.E))
                .ResolveTick();
            var died = s.Find(SimEventKind.Died, 'A');
            Assert.That(died, Is.Not.Null);
            Assert.That(died.Value.Phase, Is.EqualTo(SimPhase.Movement));
            Assert.That(died.Value.Reason, Is.EqualTo((byte)DeathCause.Fell));
        }
    }
}
