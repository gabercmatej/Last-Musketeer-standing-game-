using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    // GAME_DESIGN §5 Fire / Parry and §6 Phase 4.
    public class ShotTests
    {
        [Test]
        public void Fire_HitsAtExactlyRangeFive()
        {
            Scenario.From("A....B")
                .Plan('A', Act.Fire(Dir.E))
                .ResolveTick()
                .AssertHp('B', 2)
                .AssertLoaded('A', false)
                .AssertEvent(SimEventKind.ShotHit, 'B');
        }

        [Test]
        public void Fire_MissesAtRangeSix()
        {
            var s = Scenario.From("A.....B")
                .Plan('A', Act.Fire(Dir.E))
                .ResolveTick()
                .AssertHp('B', 3);
            Assert.That(s.Find(SimEventKind.ShotFired, 'A').Value.To, Is.EqualTo(new Int2(5, 0)));
        }

        [Test]
        public void Fire_StopsAtFirstUnit()
        {
            Scenario.From("AB.C")
                .Plan('A', Act.Fire(Dir.E))
                .ResolveTick()
                .AssertHp('B', 2)
                .AssertHp('C', 3);
        }

        [Test]
        public void Fire_PassesOverPits()
        {
            Scenario.From("A~~B")
                .Plan('A', Act.Fire(Dir.E))
                .ResolveTick()
                .AssertHp('B', 2);
        }

        [Test]
        public void Pillar_BlocksShot()
        {
            Scenario.From("A.#.B")
                .Plan('A', Act.Fire(Dir.E))
                .ResolveTick()
                .AssertHp('B', 3)
                .AssertBoard("A.#.B");
        }

        [Test]
        public void Crate_IsDestroyed_AndBlocksShot()
        {
            Scenario.From("A.o.B")
                .Plan('A', Act.Fire(Dir.E))
                .ResolveTick()
                .AssertHp('B', 3)
                .AssertBoard("A...B")
                .AssertEvent(SimEventKind.CrateDestroyed);
        }

        [Test]
        public void TwoShotsIntoOneCrate_BothStop_CrateDestroyedOnce()
        {
            // Same snapshot: the crate still blocks the second shot.
            Scenario.From("A.o.B")
                .Plan('A', Act.Fire(Dir.E))
                .Plan('B', Act.Fire(Dir.W))
                .ResolveTick()
                .AssertHp('A', 3)
                .AssertHp('B', 3)
                .AssertBoard("A...B")
                .AssertEventCount(SimEventKind.CrateDestroyed, 1);
        }

        [Test]
        public void Keg_HitByShot_BlocksTheShot_AndExplodesThisTick()
        {
            // B is 2 tiles past the keg: outside the 3×3, so neither the bullet nor the blast reaches.
            var s = Scenario.From("A.K..B")
                .Plan('A', Act.Fire(Dir.E))
                .ResolveTick()
                .AssertHp('B', 3)
                .AssertEvent(SimEventKind.KegExploded);
            Assert.That(s.State.Kegs, Is.Empty);
        }

        [Test]
        public void MutualShots_BothHit()
        {
            Scenario.From("A..B")
                .Plan('A', Act.Fire(Dir.E))
                .Plan('B', Act.Fire(Dir.W))
                .ResolveTick()
                .AssertHp('A', 2)
                .AssertHp('B', 2);
        }

        [Test]
        public void Riposte_ReflectsIntoShooter()
        {
            var s = Scenario.From("A..B")
                .Plan('A', Act.Fire(Dir.E))
                .Plan('B', Act.Parry())
                .ResolveTick()
                .AssertHp('A', 2)
                .AssertHp('B', 3)
                .AssertGrit('B', 1)
                .AssertEvent(SimEventKind.Parried, 'B')
                .AssertEvent(SimEventKind.Reflected, 'B');
            Assert.That(s.Unit('A').LastAttacker, Is.EqualTo(1), "a reflected hit is credited to the parrier");
        }

        [Test]
        public void Riposte_Lethal_IsCreditedToParrier()
        {
            Scenario.From("A..B")
                .WithHp('A', 1)
                .Plan('A', Act.Fire(Dir.E))
                .Plan('B', Act.Parry())
                .ResolveTick()
                .AssertDead('A')
                .AssertKilledBy('A', 'B');
        }

        [Test]
        public void Parry_ReflectsTwoShotsFromBothSides()
        {
            Scenario.From("A..B..C")
                .Plan('A', Act.Fire(Dir.E))
                .Plan('C', Act.Fire(Dir.W))
                .Plan('B', Act.Parry())
                .ResolveTick()
                .AssertHp('A', 2)
                .AssertHp('B', 3)
                .AssertHp('C', 2)
                .AssertEventCount(SimEventKind.Reflected, 2);
        }

        [Test]
        public void UnitSteppingIntoTheLine_TakesTheShotInsteadOfTheParrier()
        {
            // Shots trace on the post-movement snapshot, so C intercepts and no riposte happens.
            Scenario.From(@"
                A...B
                ..C..")
                .Plan('A', Act.Fire(Dir.E))
                .Plan('B', Act.Parry())
                .Plan('C', Act.Move(Dir.N))
                .ResolveTick()
                .AssertHp('C', 2)
                .AssertHp('A', 3)
                .AssertNoEvent(SimEventKind.Parried);
        }

        [Test]
        public void ReflectedShot_HitsFirstThingOnReverseLine()
        {
            // Drives the tracer directly: a reflection starting at B going W meets C before A.
            var s = Scenario.From("A.C.B");
            var log = new EventLog();
            var ctx = new TickContext(s.State, s.Config, log, 0, null);
            var buf = new ShotBuffer();
            TurnResolver.TraceShot(ctx, Scenario.Slot('B'), s.Unit('B').Pos, Dir.W, s.Config.FireRange, false, buf);
            Assert.That(buf.Hits.Count, Is.EqualTo(1));
            Assert.That(buf.Hits[0].Target, Is.EqualTo(Scenario.Slot('C')));
            Assert.That(buf.Hits[0].Kind, Is.EqualTo(DamageKind.ReflectedShot));
        }

        [Test]
        public void ReflectedShot_IsNeverReflectedAgain()
        {
            // A reflection meeting another parrier is blocked (no damage) but does not bounce back.
            var s = Scenario.From("A.B.C");
            s.Unit('C').Parrying = true;
            var log = new EventLog();
            var ctx = new TickContext(s.State, s.Config, log, 0, null);
            var buf = new ShotBuffer();
            TurnResolver.TraceShot(ctx, Scenario.Slot('B'), s.Unit('B').Pos, Dir.E, s.Config.FireRange, false, buf);
            Assert.That(buf.Hits, Is.Empty);
            Assert.That(log.CountOf(SimEventKind.Parried), Is.EqualTo(1));
            Assert.That(log.CountOf(SimEventKind.Reflected), Is.EqualTo(1), "only the reflection itself, no second bounce");
        }

        [Test]
        public void ShotAtUnitWithZeroHpThisTick_StillHits()
        {
            // C slams B into the pillar (lethal) in Phase 3; B is still on the board until Phase 6, so
            // A's shot stops on B, and as the latest attacker A gets the kill.
            Scenario.From(@"
                .C...
                .B..A
                .#...")
                .WithHp('B', 1)
                .Plan('C', Act.Shove(Dir.S))
                .Plan('A', Act.Fire(Dir.W))
                .ResolveTick()
                .AssertEvent(SimEventKind.ShotHit, 'B')
                .AssertDead('B')
                .AssertKilledBy('B', 'A');
        }
    }
}
