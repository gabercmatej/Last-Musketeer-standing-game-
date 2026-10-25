using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    // GAME_DESIGN §5 Powder Keg and §6 Phase 5.
    public class KegTests
    {
        // --- throwing ---

        [Test]
        public void Throw_LandsOnFloor_CostsTwoGrit_FuseThree()
        {
            var s = Scenario.From("A......")
                .Plan('A', Act.Keg(3, 0))
                .ResolveTick()
                .AssertGrit('A', 1)
                .AssertKegAt(3, 0)
                .AssertEvent(SimEventKind.KegThrown, 'A')
                .AssertEvent(SimEventKind.KegLanded, 'A')
                .AssertNoEvent(SimEventKind.KegExploded);
            Assert.That(s.Keg(0).Owner, Is.EqualTo(0));
            Assert.That(s.Keg(0).Fuse, Is.EqualTo(3));
        }

        [Test]
        public void ThrownKeg_ExplodesInTheSameTickSlotNextTurn()
        {
            var s = Scenario.From("A...B..")
                .Plan('A', Act.Keg(3, 0))
                .ResolveTick().ResolveTick().ResolveTick()   // turn 1: lands, then 2 fuse ticks
                .AssertKegAt(3, 0)
                .AssertHp('B', 3);
            Assert.That(s.Keg(0).Fuse, Is.EqualTo(1));
            s.ResolveTick()                                  // turn 2, tick 0
                .AssertNoKegs()
                .AssertEvent(SimEventKind.KegExploded)
                .AssertHp('B', 2)
                .AssertPos('B', 5, 0)                        // east neighbour pushed 1 outward
                .AssertHp('A', 3);
        }

        [Test]
        public void Throw_FurtherThanThree_FizzlesFree()
        {
            Scenario.From("A......")
                .Plan('A', Act.Keg(4, 0))
                .ResolveTick()
                .AssertFizzle('A', FizzleReason.OutOfRange)
                .AssertGrit('A', 3)
                .AssertNoKegs();
        }

        [Test]
        public void Throw_OffBoard_FizzlesFree()
        {
            Scenario.From("A......")
                .Plan('A', Act.Keg(-1, 0))
                .ResolveTick()
                .AssertFizzle('A', FizzleReason.OutOfRange)
                .AssertGrit('A', 3);
        }

        [Test]
        public void Throw_WithoutGrit_Fizzles()
        {
            Scenario.From("A......")
                .WithGrit('A', 1)
                .Plan('A', Act.Keg(2, 0))
                .ResolveTick()
                .AssertFizzle('A', FizzleReason.NotEnoughGrit)
                .AssertGrit('A', 1);
        }

        [Test]
        public void SecondKeg_WhileOneIsActive_FizzlesKegLimit()
        {
            Scenario.From("A.....K")
                .WithKegOwner(0, 'A')
                .WithGrit('A', 5)
                .Plan('A', Act.Keg(2, 0))
                .ResolveTick()
                .AssertFizzle('A', FizzleReason.KegLimit)
                .AssertGrit('A', 5);
        }

        [Test]
        public void ImpactOnUnit_ExplodesAtOnce_CreditToThrower()
        {
            var s = Scenario.From("A.B....")
                .Plan('A', Act.Keg(2, 0))
                .ResolveTick()
                .AssertNoKegs()
                .AssertHp('B', 2)
                .AssertPos('B', 2, 0);   // the center isn't an orthogonal neighbour: no push
            Assert.That(s.Find(SimEventKind.KegLanded).Value.Reason, Is.EqualTo((byte)KegLanding.Impact));
            Assert.That(s.Unit('B').LastAttacker, Is.EqualTo(0));
        }

        [TestCase("A.o....", TileType.Floor)]   // crate: destroyed by its own blast
        [TestCase("A.#....", TileType.Pillar)]
        [TestCase("A.~....", TileType.Pit)]
        public void ImpactOnNonFloor_Explodes(string row, TileType after)
        {
            Scenario.From(row)
                .Plan('A', Act.Keg(2, 0))
                .ResolveTick()
                .AssertEvent(SimEventKind.KegExploded)
                .AssertNoKegs()
                .AssertTile(2, 0, after);
        }

        [Test]
        public void TwoKegsOnOneTile_SecondImpactsFirst_BothExplode()
        {
            Scenario.From("A...B")
                .Plan('A', Act.Keg(2, 0))
                .Plan('B', Act.Keg(2, 0))
                .ResolveTick()
                .AssertEventCount(SimEventKind.KegExploded, 2)
                .AssertNoKegs()
                .AssertHp('A', 3)
                .AssertHp('B', 3);
        }

        // --- explosion ---

        [Test]
        public void Explosion_Hits3x3_PushesOrthogonalOnly()
        {
            Scenario.From(@"
                .....
                A....
                .K.C.
                .B...
                .....")
                .WithKegFuse(0, 1)
                .ResolveTick()
                .AssertNoKegs()
                .AssertHp('A', 2).AssertPos('A', 0, 3)   // diagonal: damage, no push
                .AssertHp('B', 2).AssertPos('B', 1, 0)   // south neighbour: pushed south
                .AssertHp('C', 3).AssertPos('C', 3, 2);  // 2 tiles away: untouched
        }

        [Test]
        public void Explosion_ParryBlocksDamage_ButStillPushes()
        {
            Scenario.From("KA...")
                .WithKegFuse(0, 1)
                .Plan('A', Act.Parry())
                .ResolveTick()
                .AssertHp('A', 3)
                .AssertPos('A', 2, 0)
                .AssertEvent(SimEventKind.Parried, 'A');
        }

        [Test]
        public void TwoBlasts_OneUnit_AtMostOneKegDamage_OppositePushesCancel()
        {
            Scenario.From("KAK")
                .WithKegFuse(0, 1).WithKegFuse(1, 1)
                .ResolveTick()
                .AssertEventCount(SimEventKind.KegExploded, 2)
                .AssertHp('A', 2)
                .AssertPos('A', 1, 0);
        }

        [Test]
        public void ChainReaction_NeighbourKegExplodesToo()
        {
            var s = Scenario.From("KK.A.")
                .WithKegFuse(0, 1)          // only the first keg is due
                .ResolveTick()
                .AssertNoKegs()
                .AssertEventCount(SimEventKind.KegExploded, 2)
                .AssertHp('A', 3);          // A is 2 from the second keg: outside both blasts
            var chained = s.Log.Events;
            int chainedCount = 0;
            foreach (var e in chained)
                if (e.Kind == SimEventKind.KegExploded && e.Amount == 1) chainedCount++;
            Assert.That(chainedCount, Is.EqualTo(1));
        }

        [Test]
        public void ChainReaction_ReachesUnitsOnlyTheSecondKegCovers()
        {
            Scenario.From("K.K.A..")
                .WithKegFuse(0, 1)
                .ResolveTick()
                .AssertHp('A', 3)            // K0 at 0 and K2 at 2 aren't in each other's 3×3
                .AssertKegAt(2, 0);
            Scenario.From("KKA....")
                .WithKegFuse(0, 1)
                .ResolveTick()
                .AssertNoKegs()
                .AssertHp('A', 2)            // reached by the chained keg at 1
                .AssertPos('A', 3, 0);
        }

        [Test]
        public void ExplosionPush_IntoPillar_WallSlam()
        {
            Scenario.From("KA#")
                .WithKegFuse(0, 1)
                .ResolveTick()
                .AssertHp('A', 1)            // 1 blast + 1 wall slam
                .AssertEvent(SimEventKind.WallSlam, 'A');
        }

        [Test]
        public void ExplosionPush_IntoUnit_Bonk()
        {
            Scenario.From("KAB")
                .WithKegFuse(0, 1)
                .ResolveTick()
                .AssertHp('A', 1)            // blast + bonk
                .AssertHp('B', 2)            // bonk only (B is 2 from the keg)
                .AssertPos('A', 1, 0);
        }

        [Test]
        public void ExplosionPush_IntoPit_KillCreditToOwner()
        {
            Scenario.From("A..KB~")
                .WithKegOwner(0, 'A')
                .WithKegFuse(0, 1)
                .ResolveTick()
                .AssertDead('B')
                .AssertKilledBy('B', 'A');
        }

        [Test]
        public void ShotDetonatedKeg_CreditGoesToShooter()
        {
            Scenario.From("A.KBC")
                .WithKegOwner(0, 'C')
                .WithHp('B', 1)
                .Plan('A', Act.Fire(Dir.E))
                .ResolveTick()
                .AssertDead('B')
                .AssertKilledBy('B', 'A');
        }

        [Test]
        public void Explosion_DestroysCratesInArea()
        {
            Scenario.From(@"
                o.o
                .K.
                ..o")
                .WithKegFuse(0, 1)
                .ResolveTick()
                .AssertTile(0, 2, TileType.Floor)
                .AssertTile(2, 2, TileType.Floor)
                .AssertTile(2, 0, TileType.Floor)
                .AssertEventCount(SimEventKind.CrateDestroyed, 3);
        }

        [Test]
        public void KickedKeg_ThenExplodes_WhereItStopped()
        {
            // Without the kick A would be in the blast. The keg slides 3 to x=4, next to B.
            Scenario.From("AK...B..")
                .WithKegFuse(0, 1)
                .Plan('A', Act.Shove(Dir.E))
                .ResolveTick()
                .AssertHp('A', 3)
                .AssertHp('B', 2)
                .AssertPos('B', 6, 0)
                .AssertNoKegs();
        }

        [Test]
        public void FuseCountsDownEveryTick()
        {
            var s = Scenario.From("K..A.B");   // two units, so the round doesn't end after tick 0
            s.ResolveTick();
            Assert.That(s.Keg(0).Fuse, Is.EqualTo(2));
            s.ResolveTick();
            Assert.That(s.Keg(0).Fuse, Is.EqualTo(1));
            s.ResolveTick().AssertNoKegs().AssertEvent(SimEventKind.KegExploded);
        }

        [Test]
        public void FallenThrower_NeverThrows()
        {
            Scenario.From("BA~....")
                .WithGrit('A', 2)
                .Plan('B', Act.Shove(Dir.E))
                .Plan('A', Act.Keg(4, 0))
                .ResolveTick()
                .AssertDead('A')
                .AssertNoKegs()
                .AssertNoEvent(SimEventKind.KegThrown);
        }
    }
}
