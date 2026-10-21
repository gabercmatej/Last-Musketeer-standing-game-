using System.Collections.Generic;
using System.Text;
using Musketeers.Game;
using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    // Session 4E chunk A (FIXES_4E §4.4-4.6, §5, §8): removed 'x' tiles, no walking off the map, pushes at the edge,
    // crates (grit roll), random per-round setup, live kill points, match point, signature event.
    public class Fix4ETests
    {
        static bool Roll(uint seed) => new DeterministicRng(seed).Range(0, 100) < 50;

        static uint SeedWhere(bool lucky)
        {
            for (uint s = 1; s < 1000; s++)
                if (Roll(s) == lucky) return s;
            throw new System.InvalidOperationException();
        }

        static string Dump(Board b)
        {
            var sb = new StringBuilder();
            for (int y = b.Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < b.Width; x++)
                    sb.Append(b.IsRemoved(new Int2(x, y)) ? 'x' : b.Get(new Int2(x, y)).ToChar());
                sb.Append('\n');
            }
            return sb.ToString();
        }

        static Board RoundBoard(string map, uint seed, int round, GameConfig cfg = null)
        {
            var data = MapParser.Parse(map);
            var board = data.Board.Clone();
            RoundSetup.Apply(data, board, cfg ?? new GameConfig(), seed, round);
            return board;
        }

        // ---- removed tiles ----

        [Test]
        public void RemovedTile_IsOffBoard_WallOnWalledMap_DropOnOpenMap()
        {
            var open = MapParser.Parse("1x2").Board;
            Assert.That(open.IsRemoved(new Int2(1, 0)), Is.True);
            Assert.That(open.IsVoid(new Int2(1, 0)), Is.True);
            Assert.That(open.Get(new Int2(1, 0)), Is.EqualTo(TileType.Pit));
            var walled = MapParser.Parse("@walls\n1x2").Board;
            Assert.That(walled.Get(new Int2(1, 0)), Is.EqualTo(TileType.Pillar));
            Assert.That(walled.Clone().IsRemoved(new Int2(1, 0)), Is.True, "survives Clone");
        }

        [Test]
        public void Removed_ChangesTheBoardHash()
        {
            var a = Scenario.From("A.x").State.ComputeHash();
            var b = Scenario.From("A..").State.ComputeHash();
            Assert.That(a, Is.Not.EqualTo(b));
        }

        [Test]
        public void ShotsStopAtRemovedTiles()
        {
            Scenario.From("A.xB")
                .Plan('A', Act.Fire(Dir.E))
                .ResolveTick()
                .AssertHp('B', 3);
        }

        // ---- 8.1 blocked moves ----

        [Test]
        public void Move_ontoRemovedTile_IsBlocked()
        {
            Scenario.From("Ax")
                .Plan('A', Act.Move(Dir.E))
                .ResolveTick()
                .AssertAlive('A').AssertPos('A', 0, 0).AssertHp('A', 3)
                .AssertEvent(SimEventKind.MoveBlocked, 'A');
            Scenario.From("@walls\nAx")
                .Plan('A', Act.Move(Dir.E))
                .ResolveTick()
                .AssertAlive('A').AssertPos('A', 0, 0)
                .AssertEvent(SimEventKind.MoveBlocked, 'A');
        }

        [Test]
        public void Dash_ontoOrOverRemovedTile_IsBlocked_GritStillSpent()
        {
            Scenario.From("A.x")
                .Plan('A', Act.Dash(Dir.E)).ResolveTick()
                .AssertAlive('A').AssertPos('A', 0, 0).AssertGrit('A', 1).AssertHp('A', 3)
                .AssertEvent(SimEventKind.MoveBlocked, 'A');
            Scenario.From("Ax.")
                .Plan('A', Act.Dash(Dir.E)).ResolveTick()
                .AssertAlive('A').AssertPos('A', 0, 0).AssertGrit('A', 1)
                .AssertEvent(SimEventKind.MoveBlocked, 'A');
        }

        [Test]
        public void PitsAndWater_AreStillWalkable()
        {
            Scenario.From("A~")
                .Plan('A', Act.Move(Dir.E)).ResolveTick()
                .AssertDead('A').AssertEvent(SimEventKind.Fell, 'A');
        }

        [Test]
        public void PlanningHelpers_DoNotOfferTheVoid()
        {
            var s = Scenario.From("Ax");
            var b = s.State.Board;
            Assert.That(PlanProjection.WalksIntoVoid(b, new Int2(0, 0), ActionType.Move, Dir.E), Is.True);
            Assert.That(PlanProjection.WalksIntoVoid(b, new Int2(0, 0), ActionType.Move, Dir.W), Is.True, "off the board");
            Assert.That(PlanProjection.WalksIntoVoid(b, new Int2(0, 0), ActionType.Dash, Dir.E), Is.True);
            Assert.That(PlanProjection.WalksIntoVoid(b, new Int2(0, 0), ActionType.Fire, Dir.E), Is.False);
            var plan = new TurnPlan(0);
            plan[0] = Act.Move(Dir.W);
            var proj = PlanProjection.Project(s.State, 0, plan, s.Config);
            Assert.That(proj.States[1].Pos, Is.EqualTo(new Int2(0, 0)));
            Assert.That(proj.States[1].Alive, Is.True);
            Assert.That(PlanProjection.KegTargetInRange(b, s.Config, new Int2(0, 0), new Int2(1, 0)), Is.False);
        }

        [Test]
        public void Bots_NeverPlanTheFirstStepIntoTheVoid_OnBothMaps()
        {
            // Later ticks are planned from a projection that reality can disturb (bonks, pushes), so only
            // the first tick, planned from the true position, is checked.
            foreach (string map in new[] { BuiltInMaps.Bridge, BuiltInMaps.Courtyard })
            for (uint seed = 1; seed <= 25; seed++)
            {
                var bm = BotMatch.Create(seed, 4, map: map);
                int turns = 0;
                while (!bm.M.MatchOver && turns++ < 400)
                {
                    var round = bm.M.Round;
                    var plans = new TurnPlan[bm.Bots.Length];
                    for (int i = 0; i < plans.Length; i++)
                    {
                        plans[i] = bm.Bots[i].Plan(round, bm.M.Cfg, bm.M.RoundNumber);
                        var u = round.Units[i];
                        var a = plans[i].A0;
                        if (u.Alive)
                            Assert.That(PlanProjection.WalksIntoVoid(round.Board, u.Pos, a.Type, a.Dir), Is.False, $"seed {seed} slot {i} {a.Type} {a.Dir} from {u.Pos}");
                    }
                    MatchRunner.PlayTurn(bm.M, plans);
                }
            }
        }

        // ---- 8.2 pushes ----

        [Test]
        public void Push_Bridge_OffBoardAndIntoRemoved_Falls()
        {
            Scenario.From("AB")
                .Plan('A', Act.Shove(Dir.E)).ResolveTick()
                .AssertDead('B').AssertKilledBy('B', 'A');
            Scenario.From("ABx")
                .Plan('A', Act.Shove(Dir.E)).ResolveTick()
                .AssertDead('B').AssertKilledBy('B', 'A');
        }

        [Test]
        public void Push_Courtyard_EdgeAndRemoved_AreWalls()
        {
            Scenario.From("@walls\nABx")
                .Plan('A', Act.Shove(Dir.E)).ResolveTick()
                .AssertAlive('B').AssertPos('B', 1, 0).AssertHp('B', 2)
                .AssertEvent(SimEventKind.WallSlam, 'B');
            Scenario.From("@walls\nAB")
                .Plan('A', Act.Shove(Dir.E)).ResolveTick()
                .AssertAlive('B').AssertHp('B', 2)
                .AssertEvent(SimEventKind.WallSlam, 'B');
        }

        [Test]
        public void Push_Courtyard_IntoPit_StillFalls()
        {
            Scenario.From("@walls\nAB~")
                .Plan('A', Act.Shove(Dir.E)).ResolveTick()
                .AssertDead('B').AssertKilledBy('B', 'A');
        }

        // ---- 8.3 / 8.4 abilities on the new edges ----

        [Test]
        public void Grappin_DragAcrossWater_DropsTheTarget_CreditToGrappler()
        {
            Scenario.From("A~.B")
                .WithAbility('A', Ability.Grappin).WithGrit('A', 5)
                .Plan('A', Act.Sig(Dir.E)).ResolveTick()
                .AssertDead('B').AssertKilledBy('B', 'A');
        }

        [Test]
        public void BullRush_Bridge_PushedIntoRemovedTile_Falls()
        {
            Scenario.From("A.B.x")
                .WithAbility('A', Ability.BullRush).WithGrit('A', 5)
                .Plan('A', Act.Sig(Dir.E)).ResolveTick()
                .AssertDead('B').AssertKilledBy('B', 'A');
        }

        [Test]
        public void BullRush_Deals1AndKnocksBack5()
        {
            var cfg = new GameConfig();
            Assert.That(cfg.BullRushDamage, Is.EqualTo(1));
            Assert.That(cfg.BullRushPush, Is.EqualTo(5));
            Scenario.From("A.B.........")
                .WithAbility('A', Ability.BullRush).WithGrit('A', 5)
                .Plan('A', Act.Sig(Dir.E)).ResolveTick()
                .AssertHp('B', 2).AssertPos('B', 7, 0);
        }

        // ---- 5.1 / 5.2 crates ----

        [Test]
        public void Crate_ShotBreaks_GritRollIsDeterministic()
        {
            uint lucky = SeedWhere(true), unlucky = SeedWhere(false);
            for (int rep = 0; rep < 2; rep++)
            {
                var s = Scenario.From("A.o");
                s.State.Rng = new DeterministicRng(lucky);
                s.WithGrit('A', 2).Plan('A', Act.Fire(Dir.E)).ResolveTick()
                    .AssertTile(2, 0, TileType.Floor).AssertGrit('A', 3);
                Assert.That(s.Find(SimEventKind.CrateDestroyed).Value.Amount, Is.EqualTo(1));
                var t = Scenario.From("A.o");
                t.State.Rng = new DeterministicRng(unlucky);
                t.WithGrit('A', 2).Plan('A', Act.Fire(Dir.E)).ResolveTick()
                    .AssertTile(2, 0, TileType.Floor).AssertGrit('A', 2);
                Assert.That(t.Find(SimEventKind.CrateDestroyed).Value.Amount, Is.EqualTo(0));
            }
        }

        [Test]
        public void Crate_GritIsCappedAtMax()
        {
            var s = Scenario.From("A.o");
            s.State.Rng = new DeterministicRng(SeedWhere(true));
            s.WithGrit('A', 5).Plan('A', Act.Fire(Dir.E)).ResolveTick().AssertGrit('A', 5);
            Assert.That(s.Find(SimEventKind.CrateDestroyed).Value.Amount, Is.EqualTo(0));
        }

        [Test]
        public void Crate_ShoveBreaksIt_ShoverGetsTheRoll()
        {
            var s = Scenario.From("Ao.");
            s.State.Rng = new DeterministicRng(SeedWhere(true));
            s.WithGrit('A', 1).Plan('A', Act.Shove(Dir.E)).ResolveTick()
                .AssertTile(1, 0, TileType.Floor).AssertGrit('A', 2);
            Assert.That(s.Find(SimEventKind.CrateDestroyed).Value.Unit, Is.EqualTo(0));
        }

        [Test]
        public void Crate_ExplosionBreaksIt_KegOwnerGetsTheRoll()
        {
            var s = Scenario.From("A..oK");
            s.State.Rng = new DeterministicRng(SeedWhere(true));
            s.WithKegOwner(0, 'A').WithKegFuse(0, 1).WithGrit('A', 1)
                .Plan('A', Act.Idle()).ResolveTick()
                .AssertTile(3, 0, TileType.Floor).AssertGrit('A', 2);
        }

        [Test]
        public void Crate_RollsHappenInAFixedOrder_SameSeedSameOutcome()
        {
            uint Run(uint seed)
            {
                var s = Scenario.From("oAo");
                s.State.Rng = new DeterministicRng(seed);
                s.Plan('A', Act.Shove(Dir.E)).ResolveTick();
                return s.State.ComputeHash();
            }
            for (uint seed = 1; seed < 20; seed++)
                Assert.That(Run(seed), Is.EqualTo(Run(seed)));
        }

        // ---- 5.3 random setup ----

        [Test]
        public void RandomSetup_Courtyard_FollowsEveryRule()
        {
            var data = MapParser.Parse(BuiltInMaps.Courtyard);
            var cfg = new GameConfig();
            for (uint seed = 1; seed <= 40; seed++)
            for (int round = 1; round <= 4; round++)
            {
                var b = RoundBoard(BuiltInMaps.Courtyard, seed, round);
                var pits = new List<Int2>();
                int crates = 0;
                for (int y = 0; y < b.Height; y++)
                for (int x = 0; x < b.Width; x++)
                {
                    var p = new Int2(x, y);
                    var t = b.IsRemoved(p) ? TileType.Pillar : b.Get(p);
                    bool wasFloor = data.Board.IsRemoved(p) ? false : data.Board.Get(p) == TileType.Floor;
                    if (t == TileType.Pit || t == TileType.Crate)
                    {
                        string where = $"seed {seed} round {round} {p}";
                        Assert.That(wasFloor, Is.True, "only on plain floor " + where);
                        Assert.That(b.RingIndex(p), Is.GreaterThan(0), "not the outer ring " + where);
                        foreach (var sp in data.Spawns)
                            Assert.That(System.Math.Max(System.Math.Abs(sp.X - x), System.Math.Abs(sp.Y - y)), Is.GreaterThan(1), "not near a spawn " + where);
                        if (t == TileType.Pit) pits.Add(p); else crates++;
                    }
                }
                Assert.That(pits.Count, Is.InRange(cfg.RandomPitsMin, cfg.RandomPitsMax), $"seed {seed} round {round}");
                Assert.That(crates, Is.EqualTo(cfg.RandomCrates));
                for (int i = 0; i < pits.Count; i++)
                for (int j = i + 1; j < pits.Count; j++)
                    Assert.That(System.Math.Max(System.Math.Abs(pits[i].X - pits[j].X), System.Math.Abs(pits[i].Y - pits[j].Y)), Is.GreaterThan(1),
                        $"pits {pits[i]} {pits[j]} touch (seed {seed} round {round})");
                Assert.That(RoundSetup.SpawnsConnected(b, data.Spawns), Is.True, $"spawns cut off (seed {seed} round {round})");
            }
        }

        [Test]
        public void RandomSetup_Bridge_OnlyCrates_NotOnPlanksOrWater()
        {
            var data = MapParser.Parse(BuiltInMaps.Bridge);
            for (uint seed = 1; seed <= 40; seed++)
            for (int round = 1; round <= 3; round++)
            {
                var b = RoundBoard(BuiltInMaps.Bridge, seed, round);
                int crates = 0;
                for (int y = 0; y < b.Height; y++)
                for (int x = 0; x < b.Width; x++)
                {
                    var p = new Int2(x, y);
                    if (b.IsRemoved(p)) continue;
                    if (b.Get(p) == TileType.Crate)
                    {
                        crates++;
                        Assert.That(data.Board.Get(p), Is.EqualTo(TileType.Floor));
                        Assert.That(data.Bridges[y * b.Width + x], Is.False, "not on a plank");
                        Assert.That(b.RingIndex(p), Is.GreaterThan(0));
                    }
                    else
                    {
                        Assert.That(b.Get(p), Is.EqualTo(data.Board.Get(p)), $"no random pits or other changes at {p}");
                    }
                }
                Assert.That(crates, Is.EqualTo(3));
            }
        }

        [Test]
        public void RandomSetup_PitNeverCutsASpawnOff()
        {
            const string corridor = "@randompits\n#########\n#########\n#1.....2#\n#########\n#########";
            var data = MapParser.Parse(corridor);
            Assert.That(RoundSetup.SpawnsConnected(data.Board, data.Spawns), Is.True);
            for (uint seed = 1; seed <= 20; seed++)
            {
                var b = RoundBoard(corridor, seed, 1);
                for (int x = 0; x < b.Width; x++)
                    Assert.That(b.Get(new Int2(x, 2)), Is.Not.EqualTo(TileType.Pit));
            }
            Assert.That(RoundSetup.SpawnsConnected(MapParser.Parse("1~2").Board, MapParser.Parse("1~2").Spawns), Is.False);
        }

        [Test]
        public void RandomSetup_IsDeterministic_AndVariesByRound()
        {
            Assert.That(Dump(RoundBoard(BuiltInMaps.Courtyard, 7, 3)), Is.EqualTo(Dump(RoundBoard(BuiltInMaps.Courtyard, 7, 3))));
            var seen = new HashSet<string>();
            for (int round = 1; round <= 10; round++)
                seen.Add(Dump(RoundBoard(BuiltInMaps.Courtyard, 7, round)));
            Assert.That(seen.Count, Is.GreaterThan(7), "different rounds give different layouts");
            Assert.That(Dump(RoundBoard(BuiltInMaps.Courtyard, 7, 2)), Is.Not.EqualTo(Dump(RoundBoard(BuiltInMaps.Courtyard, 8, 2))));
        }

        [Test]
        public void MatchRunner_ResetsAndRerollsEveryRound()
        {
            var bm = BotMatch.Create(5, 4);
            var m = bm.M;
            string first = Dump(m.Round.Board);
            Assert.That(first, Does.Contain("o"));
            Assert.That(first, Does.Contain("~"));
            int turns = 0;
            while (m.RoundNumber == 1 && turns++ < 500)
                bm.Step();
            Assert.That(m.RoundNumber, Is.EqualTo(2));
            Assert.That(m.Round.Turn, Is.EqualTo(1));
            Assert.That(m.Round.Kegs.Count, Is.EqualTo(0));
            Assert.That(m.Round.AliveCount(), Is.EqualTo(4));
            Assert.That(Dump(m.Round.Board), Is.EqualTo(Dump(RoundBoard(BuiltInMaps.Courtyard, 5, 2))), "round 2 uses (seed, round 2)");
            Assert.That(Dump(m.Round.Board), Is.Not.EqualTo(first));
        }

        [Test]
        public void RandomSetup_WorksWithTheShrinkingArena()
        {
            var cfg = new GameConfig();
            var data = MapParser.Parse(BuiltInMaps.Courtyard);
            var m = MatchRunner.NewMatch(data, new[] { Ability.Grappin, Ability.Switcheroo }, cfg, 11);
            var b = m.Round.Board;
            // A crate sitting on ring 2 is swallowed when that ring crumbles; the rest of the round carries on.
            b.Set(new Int2(2, 5), TileType.Crate);
            m.Round.Units[0].Pos = new Int2(6, 4);
            m.Round.Units[1].Pos = new Int2(8, 4);
            m.Round.Turn = Crumble.FallTurn(2, cfg);
            TurnStart.Begin(m.Round, cfg);
            Assert.That(m.Round.RoundOver, Is.False);
            for (int y = 0; y < b.Height; y++)
            for (int x = 0; x < b.Width; x++)
            {
                var p = new Int2(x, y);
                if (!b.IsRemoved(p) && b.RingIndex(p) == 2)
                    Assert.That(b.Get(p), Is.EqualTo(TileType.Pit), $"ring 2 tile {p}");
            }
        }

        // ---- 8.5 signature event ----

        [Test]
        public void SignatureUsed_IsEmitted_OnlyWhenPaid()
        {
            var s = Scenario.From("A.B")
                .WithAbility('A', Ability.BullRush).WithGrit('A', 5)
                .Plan('A', Act.Sig(Dir.E)).ResolveTick();
            s.AssertEvent(SimEventKind.SignatureUsed, 'A');
            Assert.That(s.Find(SimEventKind.SignatureUsed).Value.Amount, Is.EqualTo((int)Ability.BullRush));
            Scenario.From("A.B")
                .WithAbility('A', Ability.BullRush).WithGrit('A', 2)
                .Plan('A', Act.Sig(Dir.E)).ResolveTick()
                .AssertNoEvent(SimEventKind.SignatureUsed);
        }

        // ---- 8.6 / 8.7 live scoring and match point ----

        const string Trio = "...3...\n.1...2.\n.......";
        static readonly Ability[] Three = { Ability.Grappin, Ability.Switcheroo, Ability.BullRush };

        static TurnPlan[] Plans(MatchState m)
        {
            var p = new TurnPlan[m.PlayerCount];
            for (int i = 0; i < p.Length; i++) p[i] = new TurnPlan(i);
            return p;
        }

        static SimEvent FindEvent(EventLog log, SimEventKind kind)
        {
            for (int i = 0; i < log.Count; i++)
                if (log[i].Kind == kind) return log[i];
            throw new System.InvalidOperationException($"no {kind} event");
        }

        [Test]
        public void KillPoints_LandInTheSameTick_WithAnEventRightAfterDied()
        {
            var m = MatchRunner.NewMatch(MapParser.Parse("...3...\n.1...2.\n.......".Replace("...3...\n.1...2.", ".1.3.2.\n.......")), Three, new GameConfig(), 3);
            m.Round.Units[2].Hp = 1;   // slot 2 stands between 0 and 1
            var p = Plans(m);
            p[0].A0 = PlannedAction.Fire(Dir.E);
            var o = MatchRunner.PlayTurn(m, p);
            Assert.That(m.Points, Is.EqualTo(new[] { 1, 0, 0 }));
            int died = -1, scored = -1;
            for (int i = 0; i < o.Turn.Count; i++)
            {
                if (o.Turn[i].Kind == SimEventKind.Died) died = i;
                if (o.Turn[i].Kind == SimEventKind.PointsScored) scored = i;
            }
            Assert.That(died, Is.GreaterThanOrEqualTo(0));
            Assert.That(scored, Is.EqualTo(died + 1));
            Assert.That(o.Turn[scored].Unit, Is.EqualTo(0));
            Assert.That(o.Turn[scored].Other, Is.EqualTo(2));
            Assert.That(o.Turn[scored].Amount, Is.EqualTo(1));
        }

        [Test]
        public void CrownKill_IsWorthTwoImmediately_AndNothingIsCountedTwice()
        {
            var m = MatchRunner.NewMatch(MapParser.Parse(".1...2."), new[] { Ability.Grappin, Ability.Switcheroo }, new GameConfig(), 3);
            m.Crowned = 1;
            m.Round.Crowned = 1;
            m.Round.Units[1].Hp = 1;
            var p = Plans(m);
            p[0].A0 = PlannedAction.Fire(Dir.E);
            var o = MatchRunner.PlayTurn(m, p);
            Assert.That(FindEvent(o.Turn, SimEventKind.PointsScored).Amount, Is.EqualTo(2));
            Assert.That(m.Points, Is.EqualTo(new[] { 5, 0 }), "+2 crown kill once, +3 survival once");
            Assert.That(m.RoundPoints[0], Is.EqualTo(new[] { 5, 0 }));
        }

        [Test]
        public void MatchPoint_IsAQueryOnCurrentPoints_MatchEndsOnlyAtRoundEnd()
        {
            var cfg = new GameConfig { PointsToWin = 1 };
            var m = MatchRunner.NewMatch(MapParser.Parse(".1.3.2.\n......."), Three, cfg, 3);
            Assert.That(m.IsMatchPoint(0), Is.False);
            m.Round.Units[2].Hp = 1;
            var p = Plans(m);
            p[0].A0 = PlannedAction.Fire(Dir.E);
            MatchRunner.PlayTurn(m, p);
            Assert.That(m.IsMatchPoint(0), Is.True, "kill point already reaches the target");
            Assert.That(m.IsMatchPoint(1), Is.False);
            Assert.That(m.MatchOver, Is.False, "the win check waits for the round end");
            var d = new GameConfig();
            var m2 = MatchRunner.NewMatch(MapParser.Parse(".1...2."), new[] { Ability.Grappin, Ability.Switcheroo }, d, 3);
            m2.Points[0] = d.PointsToWin - 1;
            Assert.That(m2.IsMatchPoint(0), Is.False);
            m2.Points[0] = d.PointsToWin;
            Assert.That(m2.IsMatchPoint(0), Is.True);
        }
    }
}
