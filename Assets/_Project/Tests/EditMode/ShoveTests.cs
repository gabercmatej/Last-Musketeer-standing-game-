using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    // GAME_DESIGN §5 Shove and §6 Phase 3.
    public class ShoveTests
    {
        [Test]
        public void Shove_PushesOneTile_AndSetsAttacker()
        {
            var s = Scenario.From("AB..")
                .Plan('A', Act.Shove(Dir.E))
                .ResolveTick()
                .AssertBoard("A.B.")
                .AssertHp('B', 3)
                .AssertEvent(SimEventKind.Pushed, 'B');
            Assert.That(s.Unit('B').LastAttacker, Is.EqualTo(0));
        }

        [Test]
        public void Shove_UsesPostMovementPositions()
        {
            // B walks into the tile A shoves, so B gets pushed back.
            Scenario.From("A.B")
                .Plan('A', Act.Shove(Dir.E))
                .Plan('B', Act.Move(Dir.W))
                .ResolveTick()
                .AssertBoard("A.B");
        }

        [Test]
        public void ShoveIntoPit_KillsWithCredit()
        {
            Scenario.From("AB~")
                .Plan('A', Act.Shove(Dir.E))
                .ResolveTick()
                .AssertDead('B')
                .AssertKilledBy('B', 'A');
        }

        [Test]
        public void GuardBreak_PushesTwo_CancelsParry_SoLaterShotHits()
        {
            Scenario.From("AB...C")
                .Plan('A', Act.Shove(Dir.E))
                .Plan('B', Act.Parry())
                .Plan('C', Act.Fire(Dir.W))
                .ResolveTick()
                .AssertBoard("A..B.C")
                .AssertHp('B', 2)
                .AssertHp('C', 3)
                .AssertGrit('B', 2)
                .AssertEvent(SimEventKind.GuardBreak, 'B')
                .AssertNoEvent(SimEventKind.Parried)
                .AssertNoEvent(SimEventKind.Reflected);
        }

        [Test]
        public void GuardBreak_IntoPitTwoTilesAway_KillsWithCredit()
        {
            Scenario.From("AB.~")
                .Plan('A', Act.Shove(Dir.E))
                .Plan('B', Act.Parry())
                .ResolveTick()
                .AssertDead('B')
                .AssertKilledBy('B', 'A');
        }

        [Test]
        public void GuardBreak_SlidesThenBonksUnit()
        {
            Scenario.From("AB.C")
                .Plan('A', Act.Shove(Dir.E))
                .Plan('B', Act.Parry())
                .ResolveTick()
                .AssertBoard("A.BC")
                .AssertHp('B', 2)
                .AssertHp('C', 2);
        }

        [Test]
        public void OppositeShoves_Cancel()
        {
            Scenario.From("ABC")
                .Plan('A', Act.Shove(Dir.E))
                .Plan('C', Act.Shove(Dir.W))
                .ResolveTick()
                .AssertBoard("ABC")
                .AssertHp('B', 3)
                .AssertNoEvent(SimEventKind.Pushed)
                .AssertNoEvent(SimEventKind.Squeezed);
        }

        [Test]
        public void PerpendicularShoves_Squeeze_OneDamage_NoMove()
        {
            Scenario.From(@"
                .C.
                AB.")
                .Plan('A', Act.Shove(Dir.E))
                .Plan('C', Act.Shove(Dir.S))
                .ResolveTick()
                .AssertBoard(@"
                .C.
                AB.")
                .AssertHp('B', 2)
                .AssertEvent(SimEventKind.Squeezed, 'B');
        }

        [Test]
        public void Squeeze_OnParrier_StillBreaksGuard()
        {
            Scenario.From(@"
                .C..
                AB.D")
                .Plan('A', Act.Shove(Dir.E))
                .Plan('C', Act.Shove(Dir.S))
                .Plan('B', Act.Parry())
                .Plan('D', Act.Fire(Dir.W))
                .ResolveTick()
                .AssertHp('B', 1)       // squeeze + shot
                .AssertHp('D', 3)
                .AssertEvent(SimEventKind.GuardBreak, 'B');
        }

        [TestCase("AB#")]
        [TestCase("ABo")]
        [TestCase("ABK")]
        public void Push_IntoSolid_WallSlam(string row)
        {
            var s = Scenario.From(row)
                .Plan('A', Act.Shove(Dir.E))
                .ResolveTick()
                .AssertBoard(row)
                .AssertHp('B', 2)
                .AssertEvent(SimEventKind.WallSlam, 'B');
            Assert.That(s.Unit('B').LastAttacker, Is.EqualTo(0));
        }

        [Test]
        public void Push_IntoUnit_BothBonk_NoChain()
        {
            var s = Scenario.From("ABC.")
                .Plan('A', Act.Shove(Dir.E))
                .ResolveTick()
                .AssertBoard("ABC.")
                .AssertHp('B', 2)
                .AssertHp('C', 2)
                .AssertEvent(SimEventKind.Bonk, 'B');
            Assert.That(s.Unit('C').LastAttacker, Is.EqualTo(0), "the shover gets credit for the bonked bystander");
        }

        [Test]
        public void MutualShoves_PushEachOtherApart()
        {
            Scenario.From(".AB.")
                .Plan('A', Act.Shove(Dir.E))
                .Plan('B', Act.Shove(Dir.W))
                .ResolveTick()
                .AssertBoard("A..B");
        }

        [Test]
        public void Shove_EmptyTile_DoesNothing_ShoveOnACrateBreaksIt()
        {
            // FIXES_4E §5.1: B's shove breaks the crate instead of moving it.
            Scenario.From("A.BoC")
                .Plan('A', Act.Shove(Dir.E))
                .Plan('B', Act.Shove(Dir.E))
                .ResolveTick()
                .AssertBoard("A.B.C")
                .AssertEventCount(SimEventKind.Shoved, 2)
                .AssertNoEvent(SimEventKind.Pushed);
        }

        [Test]
        public void KegKick_SlidesUpToThree()
        {
            Scenario.From("AK....B")
                .Plan('A', Act.Shove(Dir.E))
                .ResolveTick()
                .AssertBoard("A...K.B")
                .AssertEvent(SimEventKind.KegKicked);
        }

        [Test]
        public void KegKick_StopsBeforePillar()
        {
            Scenario.From("AK.#B")
                .Plan('A', Act.Shove(Dir.E))
                .ResolveTick()
                .AssertBoard("A.K#B");
        }

        [Test]
        public void KegKick_StopsBeforeUnit()
        {
            Scenario.From("AK.B")
                .Plan('A', Act.Shove(Dir.E))
                .ResolveTick()
                .AssertBoard("A.KB");
        }

        [TestCase(1)]
        [TestCase(2)]
        public void TwoKegKicks_ResolveInKegIdOrder_WhateverTheInitiative(int turn)
        {
            // Keg #0 (left) slides first and gets 3 tiles; keg #1 then stops against it.
            Scenario.From("AK....KB")
                .WithTurn(turn)
                .Plan('A', Act.Shove(Dir.E))
                .Plan('B', Act.Shove(Dir.W))
                .ResolveTick()
                .AssertBoard("A...KK.B");
        }

        [Test]
        public void PushBonk_LogsOneBonkPerParticipant()
        {
            Scenario.From("ABC.")
                .Plan('A', Act.Shove(Dir.E))
                .ResolveTick()
                .AssertEventCount(SimEventKind.Bonk, 2)
                .AssertEvent(SimEventKind.Bonk, 'B')
                .AssertEvent(SimEventKind.Bonk, 'C');
        }

        [Test]
        public void KegKick_IntoPit_KegIsGone()
        {
            var s = Scenario.From("AK.~.B")
                .Plan('A', Act.Shove(Dir.E))
                .ResolveTick()
                .AssertBoard("A..~.B");
            Assert.That(s.State.Kegs.Count, Is.EqualTo(0));
        }
    }
}
