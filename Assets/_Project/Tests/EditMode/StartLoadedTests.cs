using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    // GameConfig.StartLoaded (Matej, Day 3): §4 says muskets start loaded; the knob lets him try
    // "everyone starts unloaded" without code changes.
    public class StartLoadedTests
    {
        [Test]
        public void Default_IsTrue()
        {
            Assert.That(new GameConfig().StartLoaded, Is.True);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void EveryRound_SpawnsWithTheConfiguredLoad(bool startLoaded)
        {
            var cfg = new GameConfig { StartLoaded = startLoaded };
            var m = MatchRunner.NewMatch(MapParser.Parse(BuiltInMaps.Courtyard),
                new[] { Ability.Grappin, Ability.Switcheroo, Ability.TripleCharge, Ability.BullRush }, cfg, 7);
            foreach (var u in m.Round.Units)
                Assert.That(u.Loaded, Is.EqualTo(startLoaded), $"round 1 unit {u.Slot}");

            // Play to the next round with bots and check the fresh spawn too.
            var bots = new BotPlanner[4];
            for (int i = 0; i < 4; i++)
                bots[i] = new BotPlanner(i, 11u + (uint)i);
            int round = m.RoundNumber;
            for (int guard = 0; guard < 200 && m.RoundNumber == round && !m.MatchOver; guard++)
            {
                var plans = new TurnPlan[4];
                for (int i = 0; i < 4; i++)
                    plans[i] = bots[i].Plan(m.Round, cfg, m.RoundNumber);
                MatchRunner.PlayTurn(m, plans);
            }
            Assert.That(m.RoundNumber, Is.GreaterThan(round), "the first round should end");
            foreach (var u in m.Round.Units)
                Assert.That(u.Loaded, Is.EqualTo(startLoaded), $"round 2 unit {u.Slot}");
        }

        [Test]
        public void Unloaded_FireFizzles_UntilReload()
        {
            var s = Scenario.From("A...B", new GameConfig { StartLoaded = false });
            s.PlanTurn('A', Act.Fire(Dir.E), Act.Reload(), Act.Fire(Dir.E));
            s.ResolveTurn();
            s.AssertEvent(SimEventKind.Fizzled);
            s.AssertHp('B', 2);
        }
    }
}
