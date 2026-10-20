using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    // GAME_DESIGN §4 signature abilities, §6 Phase 2b and Phase 4 (Triple Charge).
    // Each test gives A the ability under test and the 5 Grit it costs.
    public class AbilityTests
    {
        static Scenario With(string map, Ability a) =>
            Scenario.From(map).WithAbility('A', a).WithGrit('A', 5);

        [Test]
        public void Signature_WithoutGrit_FizzlesFree()
        {
            Scenario.From("A...B")
                .WithAbility('A', Ability.Grappin)
                .WithGrit('A', 4)   // signatures cost 5
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertFizzle('A', FizzleReason.NotEnoughGrit)
                .AssertGrit('A', 4)
                .AssertPos('B', 4, 0);
        }

        // --- Grappin ---

        [Test]
        public void Grappin_YanksUnitNextToYou_CostsFive_SetsAttacker()
        {
            var s = With("A....B", Ability.Grappin)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertBoard("AB....")
                .AssertGrit('A', 0)
                .AssertEvent(SimEventKind.Grappled, 'A');
            Assert.That(s.Unit('B').LastAttacker, Is.EqualTo(0));
            Assert.That(s.Find(SimEventKind.Grappled).Value.Reason, Is.EqualTo((byte)GrappleResult.YankUnit));
        }

        [Test]
        public void Grappin_YankOverAPit_IsSafe()
        {
            With("A.~.B", Ability.Grappin)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertAlive('B')
                .AssertPos('B', 1, 0);
        }

        [Test]
        public void Grappin_YankOntoAPitNextToYou_Falls_KillCredit()
        {
            With("A~..B", Ability.Grappin)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertDead('B')
                .AssertKilledBy('B', 'A');
        }

        [TestCase("A...#")]
        [TestCase("A...o")]
        public void Grappin_PillarOrCrate_PullsYouToTheTileBefore(string row)
        {
            With(row + "B", Ability.Grappin)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertPos('A', 3, 0)
                .AssertPos('B', 5, 0);
        }

        [Test]
        public void Grappin_CrateBeforeUnit_PullsYouInstead()
        {
            With("A.o.B", Ability.Grappin)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertPos('A', 1, 0)
                .AssertPos('B', 4, 0);
        }

        [Test]
        public void Grappin_Keg_IsPulledNextToYou()
        {
            With("A...K.B", Ability.Grappin)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertKegAt(1, 0)
                .AssertPos('A', 0, 0);
        }

        [Test]
        public void Grappin_Nothing_InRange_MissesButSpendsGrit()
        {
            With("A.....B", Ability.Grappin)   // B is 6 away, range is 5
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertPos('A', 0, 0)
                .AssertPos('B', 6, 0)
                .AssertGrit('A', 0);
        }

        // --- Switcheroo (FIXES §3.2 rule 5) ---

        [Test]
        public void Switcheroo_SwapsWithTheChosenMusketeer_AnywhereOnTheBoard_SetsAttacker()
        {
            var s = With(@"
                A..
                ...
                ..B", Ability.Switcheroo)
                .Plan('A', Act.Switcheroo('B'))
                .ResolveTick()
                .AssertBoard(@"
                B..
                ...
                ..A")
                .AssertGrit('A', 0)
                .AssertEvent(SimEventKind.Swapped, 'A');
            Assert.That(s.Unit('B').LastAttacker, Is.EqualTo(0));
            Assert.That(s.Find(SimEventKind.Swapped).Value.Amount, Is.EqualTo(1));
        }

        [Test]
        public void Switcheroo_IgnoresPillarsCratesAndPits_InTheWay()
        {
            With("A#o~B", Ability.Switcheroo)
                .Plan('A', Act.Switcheroo('B'))
                .ResolveTick()
                .AssertPos('A', 4, 0)
                .AssertPos('B', 0, 0);
        }

        [Test]
        public void Switcheroo_UsesTheTargetsPositionAtThatMoment()
        {
            // B walks east in Phase 2; Phase 2b then swaps A with wherever B is now.
            With("A..B.", Ability.Switcheroo)
                .Plan('A', Act.Switcheroo('B'))
                .Plan('B', Act.Move(Dir.E))
                .ResolveTick()
                .AssertPos('A', 4, 0)
                .AssertPos('B', 0, 0);
        }

        [Test]
        public void Switcheroo_FromAWarnedTile_Fails_GritSpent()
        {
            // Turn 5 warns ring 0, the outer ring; A stands on it.
            var s = With(@"
                A....
                .....
                .....
                .....
                ....B", Ability.Switcheroo)
                .WithTurn(5)
                .Plan('A', Act.Switcheroo('B'))
                .ResolveTick()
                .AssertPos('A', 0, 4)
                .AssertPos('B', 4, 0)
                .AssertGrit('A', 0);
            var e = s.Find(SimEventKind.Swapped, 'A').Value;
            Assert.That(e.Amount, Is.EqualTo(0));
            Assert.That(e.Reason, Is.EqualTo((byte)SwitcherooFail.Warned));
        }

        [Test]
        public void Switcheroo_TargetOnAWarnedTile_StillWorks_WhenMaticIsSafe()
        {
            With(@"
                B....
                .....
                ..A..
                .....
                .....", Ability.Switcheroo)
                .WithTurn(5)
                .Plan('A', Act.Switcheroo('B'))
                .ResolveTick()
                .AssertPos('A', 0, 4)
                .AssertPos('B', 2, 2);
        }

        [Test]
        public void Switcheroo_DeadTarget_Fails_GritSpent()
        {
            var s = With("A..B..C", Ability.Switcheroo);
            s.Unit('B').Alive = false;
            s.Plan('A', Act.Switcheroo('B')).ResolveTick().AssertPos('A', 0, 0).AssertGrit('A', 0);
            Assert.That(s.Find(SimEventKind.Swapped, 'A').Value.Reason, Is.EqualTo((byte)SwitcherooFail.TargetDead));
        }

        [Test]
        public void Switcheroo_TargetingYourself_Fails()
        {
            var s = With("A..B", Ability.Switcheroo)
                .Plan('A', Act.Switcheroo('A'))
                .ResolveTick()
                .AssertBoard("A..B")
                .AssertGrit('A', 0);
            Assert.That(s.Find(SimEventKind.Swapped, 'A').Value.Reason, Is.EqualTo((byte)SwitcherooFail.BadTarget));
        }

        [Test]
        public void Switcheroo_CantSwapWithAKeg_Anymore()
        {
            // The kegs sit where a directional Switcheroo used to grab them; now only musketeers can be picked.
            With("AK.B", Ability.Switcheroo)
                .Plan('A', Act.Switcheroo('B'))
                .ResolveTick()
                .AssertKegAt(1, 0)
                .AssertPos('A', 3, 0);
        }

        // --- Bull Rush ---

        [Test]
        public void BullRush_ChargesThreeTiles()
        {
            With("A.....B", Ability.BullRush)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertPos('A', 3, 0)
                .AssertPos('B', 6, 0);
        }

        [Test]
        public void BullRush_StopsBeforeAPit()
        {
            With("A..~..B", Ability.BullRush)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertAlive('A')
                .AssertPos('A', 2, 0);
        }

        [Test]
        public void BullRush_StoppedByUnit_PushesItFour()
        {
            var s = With("A..B.......", Ability.BullRush)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertPos('A', 2, 0)
                .AssertPos('B', 7, 0)
                .AssertHp('B', 3);
            Assert.That(s.Unit('B').LastAttacker, Is.EqualTo(0));
        }

        [Test]
        public void BullRush_PushIntoPit_KillCredit()
        {
            With("A.B..~", Ability.BullRush)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertPos('A', 1, 0)
                .AssertDead('B')
                .AssertKilledBy('B', 'A');
        }

        [Test]
        public void BullRush_PushIntoPillar_WallSlam()
        {
            With("A.B#", Ability.BullRush)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertPos('B', 2, 0)
                .AssertHp('B', 2)
                .AssertEvent(SimEventKind.WallSlam, 'B');
        }

        [Test]
        public void BullRush_RanOutOfDistanceNextToUnit_NoPush()
        {
            With("A...B..", Ability.BullRush)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertPos('A', 3, 0)
                .AssertPos('B', 4, 0);
        }

        [Test]
        public void BullRush_StoppedByKeg_KicksIt()
        {
            With("A.K.....B", Ability.BullRush)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertPos('A', 1, 0)
                .AssertKegAt(5, 0)
                .AssertEvent(SimEventKind.KegKicked);
        }

        [Test]
        public void BullRush_OnParrier_PushesFour_ParryStillBlocksShots()
        {
            Scenario.From("A.B.......C")
                .WithAbility('A', Ability.BullRush).WithGrit('A', 5)
                .Plan('A', Act.Sig(Dir.E))
                .Plan('B', Act.Parry())
                .Plan('C', Act.Fire(Dir.W))
                .ResolveTick()
                .AssertPos('B', 6, 0)
                .AssertHp('B', 3)
                .AssertHp('C', 2)            // riposte: Bull Rush isn't a shove, so no Guard Break
                .AssertNoEvent(SimEventKind.GuardBreak);
        }

        [Test]
        public void BullRush_PushIsFourTiles_NotMore()
        {
            With("A.B........", Ability.BullRush)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertPos('B', 6, 0)   // A stopped on 1, B was on 2 and slid 4
                .AssertHp('B', 3);
        }

        // --- Triple Charge (FIXES §3.2 rule 7) ---

        [Test]
        public void TripleCharge_ThreeHits_ThreePushes_AndTheShotsFollowTheTarget()
        {
            var s = With("A.B......", Ability.TripleCharge)
                .WithHp('B', 5)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertPos('B', 5, 0)
                .AssertHp('B', 2)
                .AssertLoaded('A', false)
                .AssertGrit('A', 0)
                .AssertEventCount(SimEventKind.ShotFired, 3)
                .AssertEventCount(SimEventKind.ShotHit, 3)
                .AssertEventCount(SimEventKind.Pushed, 3);
            Assert.That(s.Find(SimEventKind.ShotFired, 'A').Value.Reason, Is.EqualTo((byte)ShotKind.TripleCharge));
            Assert.That(s.Unit('B').LastAttacker, Is.EqualTo(0));
        }

        [Test]
        public void TripleCharge_RangeFive_TargetPushedOutOfRange_LaterShotsMiss()
        {
            // B at distance 5 is hit once and slides to distance 6, beyond FireRange.
            With("A.....B...", Ability.TripleCharge)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertPos('B', 6, 0)
                .AssertHp('B', 3);
            With("A....B....", Ability.TripleCharge)
                .WithHp('B', 5)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertPos('B', 6, 0)
                .AssertHp('B', 4);
        }

        [Test]
        public void TripleCharge_WallSlamBetweenShots_EachShotSlamsAgain()
        {
            With("A.B#....", Ability.TripleCharge)
                .WithHp('B', 7)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertPos('B', 2, 0)
                .AssertHp('B', 1)   // 3 shots + 3 wall slams
                .AssertEventCount(SimEventKind.WallSlam, 3);
        }

        [Test]
        public void TripleCharge_PushIntoPit_KillsAndCredits_RemainingShotsFlyOn()
        {
            With("A.B.~...", Ability.TripleCharge)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertDead('B')
                .AssertKilledBy('B', 'A')
                .AssertEventCount(SimEventKind.ShotFired, 3);
        }

        [Test]
        public void TripleCharge_Parried_EachShotIsReflected_NoDamageNoPushOnParrier()
        {
            With("A..B", Ability.TripleCharge)
                .WithHp('A', 5)
                .Plan('B', Act.Parry())
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertPos('B', 3, 0)
                .AssertHp('B', 3)
                .AssertHp('A', 2)   // three ripostes
                .AssertEventCount(SimEventKind.Parried, 3)
                .AssertEventCount(SimEventKind.Reflected, 3)
                .AssertEventCount(SimEventKind.Pushed, 0);
        }

        [Test]
        public void TripleCharge_DestroysCrate_NextShotPasses()
        {
            With("A.o.B...", Ability.TripleCharge)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertTile(2, 0, TileType.Floor)
                .AssertEventCount(SimEventKind.CrateDestroyed, 1)
                .AssertPos('B', 6, 0)   // hit by shots 2 and 3
                .AssertHp('B', 1);
        }

        [Test]
        public void TripleCharge_Keg_IsDetonatedAndStopsTheShots()
        {
            With("A.K.B", Ability.TripleCharge)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertEvent(SimEventKind.KegExploded)
                .AssertNoKegs()
                .AssertPos('B', 4, 0);
        }

        [Test]
        public void TripleCharge_StopsAtPillar()
        {
            With("A.#.B", Ability.TripleCharge)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertHp('B', 3)
                .AssertTile(2, 0, TileType.Pillar);
        }

        [Test]
        public void TripleCharge_MusketShotsLandFirst_OnTheSnapshot()
        {
            // B's musket was aimed before the charge pushes B away; it still hits A.
            With("A.B....", Ability.TripleCharge)
                .WithHp('B', 5)
                .Plan('A', Act.Sig(Dir.E))
                .Plan('B', Act.Fire(Dir.W))
                .ResolveTick()
                .AssertHp('A', 2)
                .AssertPos('B', 5, 0)
                .AssertHp('B', 2);
        }

        [Test]
        public void TripleCharge_Unloaded_FizzlesFree()
        {
            With("A..B", Ability.TripleCharge)
                .WithLoaded('A', false)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertFizzle('A', FizzleReason.NotLoaded)
                .AssertGrit('A', 5)
                .AssertHp('B', 3);
        }

        [Test]
        public void Signature_CostsFive()
        {
            Scenario.From("A..B").WithAbility('A', Ability.TripleCharge).WithGrit('A', 4)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertFizzle('A', FizzleReason.NotEnoughGrit)
                .AssertHp('B', 3);
        }

        // --- Phase 2b ordering ---

        [TestCase(1, 4, 0)]   // turn 1: B acts first, swaps with A, then A's hook finds nothing
        [TestCase(2, 1, 0)]   // turn 2: A yanks B to x=1 first, then B swaps with the adjacent A
        public void Abilities_ResolveOneAtATime_InInitiativeOrder(int turn, int ax, int bx)
        {
            Scenario.From("A...B")
                .WithTurn(turn)
                .WithAbility('A', Ability.Grappin).WithGrit('A', 5)
                .WithAbility('B', Ability.Switcheroo).WithGrit('B', 5)
                .Plan('A', Act.Sig(Dir.E))
                .Plan('B', Act.Switcheroo('A'))
                .ResolveTick()
                .AssertPos('A', ax, 0)
                .AssertPos('B', bx, 0);
        }
    }
}
