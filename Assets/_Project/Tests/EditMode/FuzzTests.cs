using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    // TECH_DESIGN §6.3: lots of bot matches, checking invariants after every turn, plus a short
    // summary (the Day 6 balance report will build on this).
    public class FuzzTests
    {
        const int Matches = 1000;
        const int MaxTurnsPerMatch = 2000;
        const int MaxTurnsPerRound = 40;   // the crumble ends every 15x9 round by turn 14 at the latest

        [TestCase("Courtyard", BuiltInMaps.Courtyard)]
        [TestCase("Bridge", BuiltInMaps.Bridge)]
        public void ThousandBotMatches_NoExceptions_AllEnd_InvariantsHold(string mapName, string map)
        {
            var cfg = new GameConfig();
            var r = new Report(mapName);
            for (int i = 0; i < Matches; i++)
            {
                uint seed = (uint)(i * 7919 + 13);
                int players = 2 + i % 3;
                var bm = BotMatch.Create(seed, players, cfg, map);
                CheckInvariants(bm.M.Round, cfg, seed, 0);
                int turns = 0;
                while (!bm.M.MatchOver)
                {
                    var o = bm.Step();
                    turns++;
                    Assert.That(turns, Is.LessThan(MaxTurnsPerMatch), $"{mapName} seed {seed}: match never ended");
                    Assert.That(bm.TurnsThisRound, Is.LessThan(MaxTurnsPerRound), $"{mapName} seed {seed}: round never ended");
                    r.Record(o, bm.M);
                    if (o.EndedRound != null)
                        CheckInvariants(o.EndedRound, cfg, seed, turns);
                    CheckInvariants(bm.M.Round, cfg, seed, turns);
                }
                r.EndMatch(bm.M);
            }
            string summary = r.Summary();
            TestContext.Progress.WriteLine(summary);
            // The full report goes to the test output and a file (Library is git-ignored), not the console: the MCP
            // console reader tags logs written during a test run as exceptions, which would hide real errors.
            System.IO.File.WriteAllText($"Library/fuzz-summary-{mapName}.txt", summary);
            // Bots must never walk or dash into a pit on their own, not even after a blocked move.
            // (A bot blasted off course by its own keg can't foresee that; it shows as "no credit".)
            Assert.That(r.OwnStepFalls, Is.EqualTo(0), $"{mapName}: bots walked into pits on their own");
        }

        static void CheckInvariants(GameState s, GameConfig cfg, uint seed, int turn)
        {
            string where = $"seed {seed} after match turn {turn}";
            for (int i = 0; i < s.Units.Length; i++)
            {
                var u = s.Units[i];
                Assert.That(u.Hp, Is.InRange(0, cfg.MaxHp), $"{where}: hp {u}");
                Assert.That(u.Grit, Is.InRange(0, cfg.MaxGrit), $"{where}: grit {u}");
                Assert.That(u.Parrying, Is.False, $"{where}: parry leaked past the tick {u}");
                if (!u.Alive)
                    continue;
                Assert.That(u.Hp, Is.GreaterThan(0), $"{where}: alive at 0 hp {u}");
                Assert.That(s.Board.InBounds(u.Pos), Is.True, $"{where}: off board {u}");
                var tile = s.Board.Get(u.Pos);
                Assert.That(tile, Is.EqualTo(TileType.Floor), $"{where}: unit on {tile} {u}");
                Assert.That(s.KegAt(u.Pos), Is.Null, $"{where}: unit on a keg {u}");
                for (int j = i + 1; j < s.Units.Length; j++)
                    Assert.That(!s.Units[j].Alive || s.Units[j].Pos != u.Pos, Is.True, $"{where}: two units on {u.Pos}");
            }
            for (int k = 0; k < s.Kegs.Count; k++)
            {
                var keg = s.Kegs[k];
                Assert.That(s.Board.Get(keg.Pos), Is.EqualTo(TileType.Floor), $"{where}: keg off floor {keg}");
                Assert.That(keg.Detonating, Is.False, $"{where}: keg survived its detonation {keg}");
                Assert.That(keg.Fuse, Is.InRange(1, cfg.KegFuseTicks), $"{where}: fuse {keg}");
                for (int j = k + 1; j < s.Kegs.Count; j++)
                    Assert.That(s.Kegs[j].Pos != keg.Pos, Is.True, $"{where}: two kegs on {keg.Pos}");
            }
            int owned = 0;
            for (int p = 0; p < s.Units.Length; p++)
            {
                owned = TurnResolver.ActiveKegs(s, p);
                Assert.That(owned, Is.LessThanOrEqualTo(cfg.MaxKegsPerPlayer), $"{where}: slot {p} has {owned} kegs");
            }
        }

        sealed class Report
        {
            int _matches, _rounds, _turns, _draws, _suddenDeaths;
            readonly int[] _byPlayers = new int[5];
            readonly int[] _winsByAbility = new int[4];
            readonly int[] _appearances = new int[4];
            readonly int[] _roundWinsByAbility = new int[4];
            readonly SortedDictionary<string, int> _deaths = new SortedDictionary<string, int>();
            readonly int[] _actions = new int[10];
            int _turnsThisRound;
            public int OwnStepFalls;
            readonly string _map;
            int _maxTurnsInRound;

            public Report(string map) { _map = map; }

            public void Record(TurnOutcome o, MatchState m)
            {
                _turns++;
                _turnsThisRound++;
                Deaths(o.Turn);
                if (o.TurnStart != null) Deaths(o.TurnStart);
                if (o.RoundResult != null)
                {
                    _rounds++;
                    if (o.RoundResult.Winner < 0) _draws++;
                    else _roundWinsByAbility[(int)m.Abilities[o.RoundResult.Winner]]++;
                    if (o.RoundResult.SuddenDeath) _suddenDeaths++;
                    if (_turnsThisRound > _maxTurnsInRound) _maxTurnsInRound = _turnsThisRound;
                    _turnsThisRound = 0;
                }
                foreach (var e in o.Turn.Events)
                {
                    if (e.Phase == SimPhase.Validate && e.Kind == SimEventKind.Fizzled) _actions[9]++;
                }
                CountActions(o.Turn);
            }

            void CountActions(EventLog log)
            {
                var charges = new HashSet<int>();   // (unit, tick) pairs already counted as one Triple Charge
                foreach (var e in log.Events)
                {
                    switch (e.Kind)
                    {
                        case SimEventKind.ShotFired:
                            if (e.Reason != (byte)ShotKind.TripleCharge) _actions[0]++;
                            else if (charges.Add(e.Unit * 16 + e.Tick)) _actions[1]++;   // only the first of its 3 shots
                            break;
                        case SimEventKind.KegThrown: _actions[2]++; break;
                        case SimEventKind.ParryRaised: _actions[3]++; break;
                        case SimEventKind.Grappled: _actions[4]++; break;
                        case SimEventKind.Swapped: if (e.Amount > 0) _actions[5]++; break;   // a failed swap moves nobody
                        case SimEventKind.BullRushed: _actions[6]++; break;
                        case SimEventKind.KegExploded: _actions[7]++; break;
                        case SimEventKind.Shoved: _actions[8]++; break;
                    }
                }
            }

            // Cause of death: the last damage kind that hit the unit earlier in the same log, or how it fell.
            void Deaths(EventLog log)
            {
                var lastKind = new Dictionary<int, DamageKind>();
                var displaced = new HashSet<int>();   // moved by something other than its own plan this turn
                foreach (var e in log.Events)
                {
                    if (e.Kind == SimEventKind.Damaged)
                        lastKind[e.Unit] = (DamageKind)e.Reason;
                    if (e.Kind == SimEventKind.Pushed || (e.Kind == SimEventKind.Grappled && e.Other >= 0) || (e.Kind == SimEventKind.Swapped && e.Amount > 0 && e.Other >= 0))
                        displaced.Add(e.Kind == SimEventKind.Pushed ? e.Unit : e.Other);
                    if (e.Kind == SimEventKind.Swapped && e.Amount > 0 && e.Other >= 0)
                        displaced.Add(e.Unit);
                    if (e.Kind != SimEventKind.Died)
                        continue;
                    string cause;
                    if (e.Phase == SimPhase.TurnStart) cause = "fell: crumble";
                    else if (e.Reason == (byte)DeathCause.Fell && e.Other >= 0) cause = "fell: pushed/yanked";
                    else if (e.Reason == (byte)DeathCause.Fell && e.Phase == SimPhase.Movement && !displaced.Contains(e.Unit)) { cause = "fell: own step"; OwnStepFalls++; }
                    else if (e.Reason == (byte)DeathCause.Fell) cause = "fell: no credit (own keg/expired)";
                    else cause = lastKind.TryGetValue(e.Unit, out var k) ? "damage: " + k : "damage: ?";
                    _deaths.TryGetValue(cause, out int n);
                    _deaths[cause] = n + 1;
                }
            }

            public void EndMatch(MatchState m)
            {
                _matches++;
                _byPlayers[m.PlayerCount]++;
                foreach (var a in m.Abilities) _appearances[(int)a]++;
                _winsByAbility[(int)m.Abilities[m.MatchWinner]]++;
            }

            public string Summary()
            {
                var sb = new StringBuilder();
                sb.AppendLine($"=== Fuzz summary: {_matches} bot matches on the {_map} (2p {_byPlayers[2]}, 3p {_byPlayers[3]}, 4p {_byPlayers[4]}) ===");
                sb.AppendLine($"turns per round      avg {(double)_turns / _rounds:0.00}, max {_maxTurnsInRound}");
                sb.AppendLine($"avg rounds per match {(double)_rounds / _matches:0.00}");
                sb.AppendLine($"drawn rounds         {_draws} of {_rounds} ({100.0 * _draws / _rounds:0.0}%), sudden-death rounds {_suddenDeaths}");
                sb.AppendLine("match wins by ability (share of all wins | win rate when present):");
                for (int a = 0; a < 4; a++)
                {
                    sb.AppendLine($"  {(Ability)a,-13} {100.0 * _winsByAbility[a] / _matches,5:0.0}% | {100.0 * _winsByAbility[a] / System.Math.Max(1, _appearances[a]),5:0.0}% of {_appearances[a]} matches; round wins {_roundWinsByAbility[a]}");
                }
                int deaths = 0;
                foreach (var kv in _deaths) deaths += kv.Value;
                sb.AppendLine($"causes of death ({deaths}):");
                foreach (var kv in _deaths)
                    sb.AppendLine($"  {kv.Key,-24} {kv.Value,6} ({100.0 * kv.Value / deaths:0.0}%)");
                sb.AppendLine($"per round: shots {Per(0)}, triple charges {Per(1)}, kegs thrown {Per(2)}, kegs exploded {Per(7)}, parries {Per(3)}, shoves {Per(8)}, grapples {Per(4)}, swaps {Per(5)}, bull rushes {Per(6)}, fizzles {Per(9)}");
                return sb.ToString();
            }

            string Per(int i) => ((double)_actions[i] / _rounds).ToString("0.00");
        }
    }
}
