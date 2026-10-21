using System.Linq;
using Musketeers.Game;
using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    // Post-match badges (GAME_DESIGN §8.5), computed from MatchStats only.
    public class AwardsTests
    {
        [Test]
        public void MostWins_TiesShare_ZeroGivesNothing()
        {
            var st = new MatchStats(3);
            st.ShotHits[0] = 4; st.ShotHits[1] = 4; st.ShotHits[2] = 1;
            st.Bonks[2] = 2;
            st.DamageDealt[0] = 3; st.DamageDealt[1] = 2;
            var awards = Awards.Compute(st);
            Assert.That(awards.Where(a => a.Id == "sharpshooter").Select(a => a.Player), Is.EquivalentTo(new[] { 0, 1 }));
            Assert.That(awards.Where(a => a.Id == "bonk_lord").Select(a => a.Player), Is.EquivalentTo(new[] { 2 }));
            Assert.That(awards.Where(a => a.Id == "pacifist").Select(a => a.Player), Is.EquivalentTo(new[] { 2 }));
            Assert.That(awards.Any(a => a.Id == "regicide"), Is.False);
            Assert.That(awards.Any(a => a.Id == "click_click"), Is.False);
        }

        [Test]
        public void SwanDive_GoesToMostRingOuts_Regicide_GoesToEveryoneWhoDidIt()
        {
            var st = new MatchStats(3);
            st.SelfRingOuts[0] = 2; st.SelfRingOuts[1] = 1;
            st.CrownKills[2] = 1;
            var awards = Awards.Compute(st);
            Assert.That(awards.Where(a => a.Id == "swan_dive").Select(a => a.Player), Is.EquivalentTo(new[] { 0 }));
            Assert.That(awards.Where(a => a.Id == "regicide").Select(a => a.Player), Is.EquivalentTo(new[] { 2 }));
        }
    }
}
