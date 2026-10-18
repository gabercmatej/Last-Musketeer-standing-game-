using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    // MatchStats: the §8 award counters, derived from event logs only.
    public class MatchStatsTests
    {
        static MatchStats Record(Scenario s, int crowned = -1)
        {
            var stats = new MatchStats(s.State.Units.Length);
            stats.Record(s.Log, crowned);
            return stats;
        }

        [Test]
        public void ShotHit_ParryAndRiposte_AreCounted()
        {
            var s = Scenario.From("A..B..C")
                .Plan('A', Act.Fire(Dir.E))
                .Plan('B', Act.Parry())
                .ResolveTick();
            var st = Record(s);
            Assert.That(st.ShotsFired[0], Is.EqualTo(1));
            Assert.That(st.ShotHits[0], Is.EqualTo(0));
            Assert.That(st.ShotsParried[1], Is.EqualTo(1));
            Assert.That(st.Ripostes[1], Is.EqualTo(1));
            Assert.That(st.DamageDealt[1], Is.EqualTo(1));

            s = Scenario.From("A..B").Plan('A', Act.Fire(Dir.E)).ResolveTick();
            st = Record(s);
            Assert.That(st.ShotHits[0], Is.EqualTo(1));
            Assert.That(st.DamageDealt[0], Is.EqualTo(1));
            Assert.That(st.DamageDealt[1], Is.EqualTo(0), "Pacifist so far");
        }

        [Test]
        public void Bonks_And_Fizzles()
        {
            var s = Scenario.From("A.B")
                .WithLoaded('B', false)
                .Plan('A', Act.Move(Dir.E))
                .ResolveTick();
            Assert.That(Record(s).Bonks[0], Is.EqualTo(0));

            s = Scenario.From("A.B.C")
                .WithLoaded('C', false)
                .Plan('A', Act.Move(Dir.E))
                .Plan('B', Act.Move(Dir.W))
                .Plan('C', Act.Fire(Dir.W))
                .ResolveTick();
            var st = Record(s);
            Assert.That(st.Bonks[0], Is.EqualTo(1));
            Assert.That(st.Bonks[1], Is.EqualTo(1));
            Assert.That(st.Fizzles[2], Is.EqualTo(1));
        }

        [Test]
        public void SelfRingOut_VersusPushedOut()
        {
            var s = Scenario.From("~AB~").Plan('A', Act.Move(Dir.W)).ResolveTick();
            var st = Record(s);
            Assert.That(st.SelfRingOuts[0], Is.EqualTo(1));
            Assert.That(st.Deaths[0], Is.EqualTo(1));

            s = Scenario.From("~AB.").Plan('B', Act.Shove(Dir.W)).ResolveTick();
            st = Record(s);
            Assert.That(st.SelfRingOuts[0], Is.EqualTo(0));
            Assert.That(st.Kills[1], Is.EqualTo(1));
        }

        [Test]
        public void KegDamage_GoesToTheOwner_ParriedBlastIsNotAShotParried()
        {
            var s = Scenario.From("A..KB.C")
                .WithKegOwner(0, 'A')
                .WithKegFuse(0, 1)
                .Plan('C', Act.Idle())
                .ResolveTick();
            var st = Record(s);
            Assert.That(st.KegDamage[0], Is.EqualTo(1));
            Assert.That(st.DamageDealt[0], Is.GreaterThanOrEqualTo(1));

            s = Scenario.From("A..KB..")
                .WithKegOwner(0, 'A').WithKegFuse(0, 1)
                .Plan('B', Act.Parry())
                .ResolveTick();
            Assert.That(Record(s).ShotsParried[1], Is.EqualTo(0));
        }

        [Test]
        public void CrownKill_IsCountedForTheKiller()
        {
            var s = Scenario.From("A..B").WithHp('B', 1).Plan('A', Act.Fire(Dir.E)).ResolveTick();
            var st = Record(s, crowned: 1);
            Assert.That(st.Kills[0], Is.EqualTo(1));
            Assert.That(st.CrownKills[0], Is.EqualTo(1));
            Assert.That(Record(s, crowned: -1).CrownKills[0], Is.EqualTo(0));
        }
    }
}
