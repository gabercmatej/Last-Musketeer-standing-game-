using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    // GAME_DESIGN §4 signature abilities, §6 Phase 2b and Phase 4 (Double Charge).
    // Each test gives A the ability under test and the 3 Grit it costs.
    public class AbilityTests
    {
        static Scenario With(string map, Ability a) =>
            Scenario.From(map).WithAbility('A', a).WithGrit('A', 3);

        [Test]
        public void Signature_WithoutGrit_FizzlesFree()
        {
            Scenario.From("A...B")
                .WithAbility('A', Ability.Grappin)
                .WithGrit('A', 2)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertFizzle('A', FizzleReason.NotEnoughGrit)
                .AssertGrit('A', 2)
                .AssertPos('B', 4, 0);
        }

        // --- Grappin ---

        [Test]
        public void Grappin_YanksUnitNextToYou_CostsThree_SetsAttacker()
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

        // --- Switcheroo ---

        [Test]
        public void Switcheroo_SwapsWithUnit_SetsAttacker()
        {
            var s = With("A..B", Ability.Switcheroo)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertBoard("B..A")
                .AssertEvent(SimEventKind.Swapped, 'A');
            Assert.That(s.Unit('B').LastAttacker, Is.EqualTo(0));
        }

        [Test]
        public void Switcheroo_PassesOverPits()
        {
            With("A~~B", Ability.Switcheroo)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertBoard("B~~A");
        }

        [TestCase("A#.B")]
        [TestCase("Ao.B")]
        public void Switcheroo_BlockedByPillarOrCrate_Misses(string row)
        {
            With(row, Ability.Switcheroo)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertPos('A', 0, 0)
                .AssertPos('B', 3, 0)
                .AssertGrit('A', 0);
        }

        [Test]
        public void Switcheroo_WithKeg_SwapsAndTheBlastHitsYourOldNeighbour()
        {
            // A swaps with a keg about to blow: the keg lands next to B, A is out of range.
            var s = Scenario.From(".BA..K.")
                .WithAbility('B', Ability.Grappin)
                .WithAbility('A', Ability.Switcheroo).WithGrit('A', 3)
                .WithKegFuse(0, 1).WithKegOwner(0, 'A')
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertPos('A', 5, 0)
                .AssertHp('A', 3)
                .AssertHp('B', 2)
                .AssertPos('B', 0, 0)
                .AssertNoKegs();
            Assert.That(s.Unit('B').LastAttacker, Is.EqualTo(0));
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
        public void BullRush_StoppedByUnit_PushesItTwo()
        {
            var s = With("A..B...", Ability.BullRush)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertPos('A', 2, 0)
                .AssertPos('B', 5, 0)
                .AssertHp('B', 3);
            Assert.That(s.Unit('B').LastAttacker, Is.EqualTo(0));
        }

        [Test]
        public void BullRush_PushIntoPit_KillCredit()
        {
            With("A.B.~", Ability.BullRush)
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
        public void BullRush_OnParrier_PushesTwo_ParryStillBlocksShots()
        {
            Scenario.From("A.B...C")
                .WithAbility('A', Ability.BullRush).WithGrit('A', 3)
                .Plan('A', Act.Sig(Dir.E))
                .Plan('B', Act.Parry())
                .Plan('C', Act.Fire(Dir.W))
                .ResolveTick()
                .AssertPos('B', 4, 0)
                .AssertHp('B', 3)
                .AssertHp('C', 2)            // riposte: Bull Rush isn't a shove, so no Guard Break
                .AssertNoEvent(SimEventKind.GuardBreak);
        }

        // --- Double Charge ---

        [Test]
        public void DoubleCharge_PiercesTwoUnits_StopsAtTheSecond()
        {
            var s = Scenario.From("A.B.C.D")
                .WithAbility('A', Ability.DoubleCharge).WithGrit('A', 3)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertHp('B', 2)
                .AssertHp('C', 2)
                .AssertHp('D', 3)
                .AssertLoaded('A', false)
                .AssertGrit('A', 0);
            Assert.That(s.Find(SimEventKind.ShotFired, 'A').Value.Reason, Is.EqualTo((byte)ShotKind.DoubleCharge));
        }

        [Test]
        public void DoubleCharge_IgnoresParry_NoRiposte()
        {
            With("A..B", Ability.DoubleCharge)
                .Plan('B', Act.Parry())
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertHp('B', 2)
                .AssertHp('A', 3)
                .AssertNoEvent(SimEventKind.Reflected)
                .AssertNoEvent(SimEventKind.Parried);
        }

        [TestCase("A......B", 2)]    // 7 away: hit
        [TestCase("A.......B", 3)]   // 8 away: out of range
        public void DoubleCharge_RangeSeven(string row, int bHp)
        {
            With(row, Ability.DoubleCharge)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertHp('B', bHp);
        }

        [Test]
        public void DoubleCharge_Unloaded_FizzlesFree()
        {
            With("A..B", Ability.DoubleCharge)
                .WithLoaded('A', false)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertFizzle('A', FizzleReason.NotLoaded)
                .AssertGrit('A', 3)
                .AssertHp('B', 3);
        }

        [Test]
        public void DoubleCharge_DestroysCrate_AndStops()
        {
            With("A.o.B", Ability.DoubleCharge)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertTile(2, 0, TileType.Floor)
                .AssertHp('B', 3);
        }

        [Test]
        public void DoubleCharge_StopsAtPillar()
        {
            With("A.#.B", Ability.DoubleCharge)
                .Plan('A', Act.Sig(Dir.E))
                .ResolveTick()
                .AssertHp('B', 3)
                .AssertTile(2, 0, TileType.Pillar);
        }

        // --- Phase 2b ordering ---

        [TestCase(1, 4, 0)]   // turn 1: B acts first, swaps with A, then A's hook finds nothing
        [TestCase(2, 1, 0)]   // turn 2: A yanks B to x=1 first, then B swaps with the adjacent A
        public void Abilities_ResolveOneAtATime_InInitiativeOrder(int turn, int ax, int bx)
        {
            Scenario.From("A...B")
                .WithTurn(turn)
                .WithAbility('A', Ability.Grappin).WithGrit('A', 3)
                .WithAbility('B', Ability.Switcheroo).WithGrit('B', 3)
                .Plan('A', Act.Sig(Dir.E))
                .Plan('B', Act.Sig(Dir.W))
                .ResolveTick()
                .AssertPos('A', ax, 0)
                .AssertPos('B', bx, 0);
        }
    }
}
