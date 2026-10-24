using System.Linq;
using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    // Matej's Day 6 rule: every environmental death costs its victim one point, even with attacker credit.
    public class EnvironmentalDeathTests
    {
        static SimEvent[] Scores(EventLog log) => log.Events.Where(e => e.Kind == SimEventKind.PointsScored).ToArray();

        [Test]
        public void SelfFall_PenalizesOnce_DeadUnitCannotScoreAgain()
        {
            var s = Scenario.From("A~..B.C")
                .Plan('A', Act.Move(Dir.E)).ResolveTick().AssertDead('A');
            var score = Scores(s.Log);
            Assert.That(score.Length, Is.EqualTo(1));
            Assert.That(score[0].Unit, Is.EqualTo(0));
            Assert.That(score[0].Other, Is.EqualTo(0));
            Assert.That(score[0].Amount, Is.EqualTo(-1));
            Assert.That(score[0].Reason, Is.EqualTo((byte)PointReason.EnvironmentalDeath));
            s.ResolveTick().AssertNoEvent(SimEventKind.PointsScored).AssertNoEvent(SimEventKind.Died);
        }

        [TestCase("AB~", false)]
        [TestCase("AB", false)]
        [TestCase("ABx", false)]
        [TestCase("AB~", true)]
        public void ForcedFall_PenalizesVictim_PreservesNormalOrCrownKillCredit(string board, bool crowned)
        {
            var s = Scenario.From(board);
            s.State.Crowned = crowned ? 1 : -1;
            s.Plan('A', Act.Shove(Dir.E)).ResolveTick().AssertKilledBy('B', 'A');
            var score = Scores(s.Log);
            Assert.That(score.Length, Is.EqualTo(2));
            Assert.That(score.Single(e => e.Unit == 1).Amount, Is.EqualTo(-1));
            Assert.That(score.Single(e => e.Unit == 0).Amount, Is.EqualTo(crowned ? 2 : 1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Crumble_PenalizesOnce_WithOrWithoutRecentAttacker(bool attacked)
        {
            var s = Scenario.From(@"
                A....
                .B.C.
                .....").WithTurn(6);
            if (attacked) s.WithLastAttacker('A', 'B', 5);
            s.TurnStart().AssertDead('A');
            var score = Scores(s.Log);
            Assert.That(score.Length, Is.EqualTo(attacked ? 2 : 1));
            Assert.That(score.Single(e => e.Unit == 0).Amount, Is.EqualTo(-1));
            if (attacked) Assert.That(score.Single(e => e.Unit == 1).Amount, Is.EqualTo(1));
            s.TurnStart().AssertNoEvent(SimEventKind.PointsScored).AssertNoEvent(SimEventKind.Died);
        }

        [Test]
        public void ZeroHpUnitForcedIntoWater_HasOneDeathAndOnePenalty()
        {
            var s = Scenario.From("AB~").WithHp('B', 0)
                .Plan('A', Act.Shove(Dir.E)).ResolveTick();
            s.AssertEventCount(SimEventKind.Died, 1);
            Assert.That(Scores(s.Log).Count(e => e.Unit == 1), Is.EqualTo(1));
            var stats = new MatchStats(2);
            stats.Record(s.Log, -1);
            Assert.That(stats.Deaths, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(stats.Kills, Is.EqualTo(new[] { 1, 0 }));
        }

        [Test]
        public void DamageDeath_HasNoEnvironmentalPenalty()
        {
            var s = Scenario.From("A..B").WithHp('B', 1)
                .Plan('A', Act.Fire(Dir.E)).ResolveTick();
            Assert.That(Scores(s.Log).Select(e => e.Amount), Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void NegativePointsAndDeaths_SurviveSnapshotAndContinueDeterministically()
        {
            var m = MatchRunner.NewMatch(MapParser.Parse(@"
                .........
                .1~.2..3.
                ........."), new[] { Ability.Grappin, Ability.Switcheroo, Ability.BullRush }, new GameConfig(), 42);
            var plans = new[] { new TurnPlan(0), new TurnPlan(1), new TurnPlan(2) };
            plans[0].A0 = Act.Move(Dir.E);
            MatchRunner.PlayTurn(m, plans);
            Assert.That(m.Points, Is.EqualTo(new[] { -1, 0, 0 }));
            Assert.That(m.RoundGains.Single().Amount, Is.EqualTo(-1));
            Assert.That(m.Stats.Deaths, Is.EqualTo(new[] { 1, 0, 0 }));
            var clone = m.Clone();
            var restored = MatchStateSerializer.Deserialize(MatchStateSerializer.Serialize(m), m);
            Assert.That(clone.ComputeHash(), Is.EqualTo(m.ComputeHash()));
            Assert.That(restored.ComputeHash(), Is.EqualTo(m.ComputeHash()));
            Assert.That(restored.Points[0], Is.EqualTo(-1));
            Assert.That(restored.Stats.Deaths[0], Is.EqualTo(1));
            MatchRunner.PlayTurn(m, new[] { new TurnPlan(0), new TurnPlan(1), new TurnPlan(2) });
            MatchRunner.PlayTurn(restored, new[] { new TurnPlan(0), new TurnPlan(1), new TurnPlan(2) });
            Assert.That(restored.ComputeHash(), Is.EqualTo(m.ComputeHash()));
            Assert.That(m.Points[0], Is.EqualTo(-1), "the dead unit is never penalized a second time");
        }

        [Test]
        public void Ranking_NegativeTotalsAndNegativeRoundTiebreaks()
        {
            Assert.That(Scoring.Rank(new[] { -1, 0, -2 }, new System.Collections.Generic.List<int[]>()),
                Is.EqualTo(new[] { 2, 1, 3 }));
            Assert.That(Scoring.Rank(new[] { 0, 0 }, new System.Collections.Generic.List<int[]> { new[] { -1, 0 } }),
                Is.EqualTo(new[] { 2, 1 }));
        }
    }
}
