using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    // BotPlanner behaviour spot checks (it's heuristic; the fuzz test covers the rest).
    public class BotTests
    {
        static TurnPlan PlanFor(Scenario s, char c, uint seed = 7) =>
            new BotPlanner(Scenario.Slot(c), seed).Plan(s.State, s.Config);

        static bool Contains(TurnPlan p, ActionType t) => p.A0.Type == t || p.A1.Type == t || p.A2.Type == t;

        [Test]
        public void SameSeed_SamePlan()
        {
            var s = Scenario.From(BuiltInMaps.Courtyard.Replace('1', 'A').Replace('2', 'B').Replace('3', 'C').Replace('4', 'D'));
            for (uint seed = 1; seed < 20; seed++)
            {
                var a = PlanFor(s, 'A', seed);
                var b = PlanFor(s, 'A', seed);
                for (int t = 0; t < 3; t++)
                    Assert.That(a[t].ToString(), Is.EqualTo(b[t].ToString()));
            }
        }

        [Test]
        public void DoesNotPlanAnything_WhenDead()
        {
            var s = Scenario.From("A..B");
            s.Unit('A').Alive = false;
            var p = PlanFor(s, 'A');
            Assert.That(p.A0.Type, Is.EqualTo(ActionType.Idle));
        }

        [Test]
        public void FiresFirst_WhenLoadedAndLinedUp_MostOfTheTime()
        {
            int fired = 0;
            var s = Scenario.From(@"
                .....
                A...B
                .....");
            for (uint seed = 1; seed <= 50; seed++)
            {
                var p = PlanFor(s, 'A', seed);
                if (p.A0.Type == ActionType.Fire && p.A0.Dir == Dir.E) fired++;
            }
            Assert.That(fired, Is.GreaterThan(30));
        }

        [Test]
        public void NeverWalksIntoAPit_OrOffTheBoard()
        {
            // A is boxed in by pits with the only enemy across them.
            var s = Scenario.From(@"
                ~~~~~~
                ~A~..B
                ~~~~~~");
            for (uint seed = 1; seed <= 50; seed++)
            {
                var sim = s.State.Clone();
                var p = new BotPlanner(0, seed).Plan(sim, s.Config);
                TurnResolver.ResolveTurn(sim, new[] { p, new TurnPlan(1) }, s.Config);
                Assert.That(sim.Units[0].Alive, Is.True, $"seed {seed}: {p.A0} / {p.A1} / {p.A2}");
            }
        }

        [Test]
        public void StepsOffAWarnedTile()
        {
            var s = Scenario.From(@"
                .........
                .........
                .........
                .........
                .........
                .........
                .........
                .........
                A.......B").WithTurn(5);
            for (uint seed = 1; seed <= 30; seed++)
            {
                var sim = s.State.Clone();
                var p = new BotPlanner(0, seed).Plan(sim, s.Config);
                TurnResolver.ResolveTurn(sim, new[] { p, new TurnPlan(1) }, s.Config);
                TurnStart.Begin(sim, s.Config);   // turn 6: ring 0 crumbles
                Assert.That(sim.Units[0].Alive, Is.True, $"seed {seed}: {p.A0} / {p.A1} / {p.A2}");
            }
        }

        [Test]
        public void ShovesAnAdjacentEnemyIntoAPit()
        {
            var s = Scenario.From(@"
                .....
                .AB~.
                .....").WithLoaded('A', false);
            int shoved = 0;
            for (uint seed = 1; seed <= 30; seed++)
            {
                var p = PlanFor(s, 'A', seed);
                if (p.A0.Type == ActionType.Shove && p.A0.Dir == Dir.E) shoved++;
            }
            Assert.That(shoved, Is.GreaterThan(20));
        }

        [Test]
        public void ThrowsKegs_AtClusters_NotAtItsOwnFeet()
        {
            var s = Scenario.From(@"
                .......
                .......
                A...BC.
                ....D..
                .......").WithLoaded('A', false).WithGrit('A', 2);
            int thrown = 0;
            for (uint seed = 1; seed <= 40; seed++)
            {
                var sim = s.State.Clone();
                var p = new BotPlanner(0, seed).Plan(sim, s.Config);
                var log = TurnResolver.ResolveTurn(sim, new[] { p, new TurnPlan(1), new TurnPlan(2), new TurnPlan(3) }, s.Config);
                foreach (var e in log.Events)
                {
                    if (e.Kind == SimEventKind.KegThrown) thrown++;
                    if (e.Kind == SimEventKind.Fizzled && e.Unit == 0)
                        Assert.Fail($"seed {seed}: bot fizzled {(ActionType)e.Amount} ({(FizzleReason)e.Reason})");
                }
                // The keg lands and ticks down this turn; the bot must not be standing in its blast.
                foreach (var k in sim.Kegs)
                    Assert.That(System.Math.Max(System.Math.Abs(k.Pos.X - sim.Units[0].Pos.X), System.Math.Abs(k.Pos.Y - sim.Units[0].Pos.Y)), Is.GreaterThanOrEqualTo(2), $"seed {seed}");
                Assert.That(sim.Units[0].Hp, Is.EqualTo(3), $"seed {seed}");
            }
            Assert.That(thrown, Is.GreaterThan(0));
        }

        [Test]
        public void GrappinBot_YanksEnemyIntoAdjacentPit()
        {
            var s = Scenario.From(@"
                .......
                A~...B.
                .......").WithAbility('A', Ability.Grappin).WithGrit('A', 3).WithLoaded('A', false);
            int yanked = 0;
            for (uint seed = 1; seed <= 30; seed++)
            {
                var p = PlanFor(s, 'A', seed);
                if (p.A0.Type == ActionType.Signature && p.A0.Dir == Dir.E) yanked++;
            }
            Assert.That(yanked, Is.GreaterThan(20));
        }
    }
}
