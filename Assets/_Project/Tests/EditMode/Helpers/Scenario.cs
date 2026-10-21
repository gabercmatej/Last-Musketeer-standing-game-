using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    // Shorthand for plans in tests: Act.Fire(Dir.E).
    public static class Act
    {
        public static PlannedAction Idle() => PlannedAction.Idle();
        public static PlannedAction CatchBreath() => PlannedAction.CatchBreath();
        public static PlannedAction Move(Dir d) => PlannedAction.Move(d);
        public static PlannedAction Dash(Dir d) => PlannedAction.Dash(d);
        public static PlannedAction Fire(Dir d) => PlannedAction.Fire(d);
        public static PlannedAction Reload() => PlannedAction.Reload();
        public static PlannedAction Parry() => PlannedAction.Parry();
        public static PlannedAction Shove(Dir d) => PlannedAction.Shove(d);
        public static PlannedAction Keg(int x, int y) => PlannedAction.ThrowKeg(new Int2(x, y));
        public static PlannedAction Signature(Dir d) => PlannedAction.Signature(d);
        public static PlannedAction Sig(Dir d) => PlannedAction.Signature(d);
        public static PlannedAction Switcheroo(char target) => PlannedAction.Switcheroo(target - 'A');
    }

    // TECH_DESIGN §6 ASCII scenario helper. A–D are units in slots 0–3 standing on floor, K is a keg
    // (fuse = KegFuseTicks), everything else uses the map legend. The top line is the highest y.
    // Assertions print the board before and after the last resolve plus every event.
    public sealed class Scenario
    {
        public GameState State { get; }
        public GameConfig Config { get; }
        public EventLog Log { get; private set; } = new EventLog();

        readonly TurnPlan[] _plans;
        int _tick;
        string _before;

        Scenario(GameState state, GameConfig cfg)
        {
            State = state;
            Config = cfg;
            _plans = new TurnPlan[state.Units.Length];
            for (int i = 0; i < _plans.Length; i++)
                _plans[i] = new TurnPlan(i);
            _before = Render();
        }

        public static Scenario From(string ascii, GameConfig cfg = null)
        {
            cfg ??= new GameConfig();
            string[] rows = MapParser.SplitRows(ascii);
            int h = rows.Length, w = rows[0].Length;
            var board = new Board(w, h) { EdgeWalls = MapParser.Directives(ascii).Contains("walls") };
            var unitPos = new Int2?[4];
            var kegs = new List<Int2>();

            for (int r = 0; r < h; r++)
            {
                int y = h - 1 - r;
                for (int x = 0; x < w; x++)
                {
                    char c = rows[r][x];
                    var p = new Int2(x, y);
                    if (c >= 'A' && c <= 'D')
                    {
                        if (unitPos[c - 'A'] != null)
                            throw new ArgumentException($"Unit {c} appears twice.");
                        unitPos[c - 'A'] = p;
                    }
                    else if (c == 'K')
                    {
                        kegs.Add(p);
                    }
                    else if (c == 'x')
                    {
                        board.SetRemoved(p);   // FIXES_4E §4.4
                    }
                    else if (MapParser.TryParseTile(c, out TileType tile, out _))
                    {
                        board.Set(p, tile);
                    }
                    else
                    {
                        throw new ArgumentException($"Unknown scenario symbol '{c}' at {p}.");
                    }
                }
            }

            int count = 0;
            while (count < 4 && unitPos[count] != null)
                count++;
            for (int i = count; i < 4; i++)
            {
                if (unitPos[i] != null)
                    throw new ArgumentException($"Units must be A.. without gaps; {(char)('A' + i)} is present but {(char)('A' + count)} is not.");
            }

            var units = new Unit[count];
            for (int i = 0; i < count; i++)
                units[i] = Sim.Unit.Spawn(i, i, (Ability)i, unitPos[i].Value, cfg);
            var state = new GameState(board, units, new DeterministicRng(1));
            foreach (var p in kegs)
                state.Kegs.Add(new Keg { Id = state.NextKegId++, Owner = -1, Pos = p, Fuse = cfg.KegFuseTicks });
            return new Scenario(state, cfg);
        }

        // --- setup ---

        public Unit Unit(char c)
        {
            int i = c - 'A';
            if (i < 0 || i >= State.Units.Length)
                throw new ArgumentException($"No unit {c} in this scenario.");
            return State.Units[i];
        }

        public static int Slot(char c) => c - 'A';

        public Scenario WithHp(char c, int hp) { Unit(c).Hp = hp; return Snapshot(); }
        public Scenario WithGrit(char c, int grit) { Unit(c).Grit = grit; return Snapshot(); }
        public Scenario WithLoaded(char c, bool loaded) { Unit(c).Loaded = loaded; return Snapshot(); }
        public Scenario WithTurn(int turn) { State.Turn = turn; return Snapshot(); }
        public Scenario WithAbility(char c, Ability a) { Unit(c).Ability = a; return Snapshot(); }
        public Scenario Parrying(char c) { Unit(c).Parrying = true; return Snapshot(); }

        // Kegs are numbered in id order, which for 'K' symbols is reading order (top row first).
        public Keg Keg(int index) => State.Kegs[index];
        public Scenario WithKegFuse(int index, int fuse) { State.Kegs[index].Fuse = fuse; return Snapshot(); }
        public Scenario WithKegOwner(int index, char owner) { State.Kegs[index].Owner = Slot(owner); return Snapshot(); }

        // Runs §6 Turn start for the current State.Turn (Grit, crumble, falls).
        public Scenario TurnStart()
        {
            _before = Render();
            Log = Sim.TurnStart.Begin(State, Config);
            return this;
        }

        public Scenario WithLastAttacker(char victim, char attacker, int turn)
        {
            Unit(victim).LastAttacker = Slot(attacker);
            Unit(victim).LastAttackTurn = turn;
            return Snapshot();
        }

        Scenario Snapshot()
        {
            _before = Render();
            return this;
        }

        // --- planning and resolving ---

        // Plans the next tick to be resolved.
        public Scenario Plan(char c, PlannedAction a)
        {
            _plans[Slot(c)][_tick] = a;
            return this;
        }

        public Scenario PlanTurn(char c, PlannedAction a0, PlannedAction a1, PlannedAction a2)
        {
            if (_tick != 0)
                throw new InvalidOperationException("PlanTurn needs to start at tick 0.");
            int i = Slot(c);
            _plans[i][0] = a0;
            _plans[i][1] = a1;
            _plans[i][2] = a2;
            return this;
        }

        // Resolves one tick. After TicksPerTurn ticks the turn advances, like ResolveTurn does.
        public Scenario ResolveTick()
        {
            _before = Render();
            Log = TurnResolver.ResolveTick(State, _plans, _tick, Config);
            for (int i = 0; i < _plans.Length; i++)
                _plans[i][_tick] = default;
            if (++_tick == Config.TicksPerTurn)
            {
                _tick = 0;
                State.Turn++;
            }
            return this;
        }

        public Scenario ResolveTurn()
        {
            if (_tick != 0)
                throw new InvalidOperationException("ResolveTurn needs to start at tick 0.");
            _before = Render();
            Log = TurnResolver.ResolveTurn(State, _plans, Config);
            for (int i = 0; i < _plans.Length; i++)
                _plans[i] = new TurnPlan(i);
            return this;
        }

        // --- assertions ---

        public Scenario AssertPos(char c, int x, int y)
        {
            if (Unit(c).Pos != new Int2(x, y))
                Fail($"{c} expected at ({x},{y}) but is at {Unit(c).Pos}.");
            return this;
        }

        public Scenario AssertHp(char c, int hp)
        {
            if (Unit(c).Hp != hp)
                Fail($"{c} expected hp {hp} but has {Unit(c).Hp}.");
            return this;
        }

        public Scenario AssertGrit(char c, int grit)
        {
            if (Unit(c).Grit != grit)
                Fail($"{c} expected grit {grit} but has {Unit(c).Grit}.");
            return this;
        }

        public Scenario AssertLoaded(char c, bool loaded)
        {
            if (Unit(c).Loaded != loaded)
                Fail($"{c} expected {(loaded ? "loaded" : "unloaded")}.");
            return this;
        }

        public Scenario AssertAlive(char c, bool alive = true)
        {
            if (Unit(c).Alive != alive)
                Fail($"{c} expected {(alive ? "alive" : "dead")}.");
            return this;
        }

        public Scenario AssertDead(char c) => AssertAlive(c, false);

        public Scenario AssertKegAt(int x, int y)
        {
            if (State.KegAt(new Int2(x, y)) == null)
                Fail($"Expected a keg at ({x},{y}).");
            return this;
        }

        public Scenario AssertNoKegs()
        {
            if (State.Kegs.Count != 0)
                Fail($"Expected no kegs, found {State.Kegs.Count}.");
            return this;
        }

        public Scenario AssertTile(int x, int y, TileType t)
        {
            if (State.Board.Get(new Int2(x, y)) != t)
                Fail($"Expected {t} at ({x},{y}) but found {State.Board.Get(new Int2(x, y))}.");
            return this;
        }

        public Scenario AssertFizzle(char c, FizzleReason reason)
        {
            var e = Find(SimEventKind.Fizzled, c);
            if (e == null)
                Fail($"Expected {c} to fizzle ({reason}).");
            else if (e.Value.Reason != (byte)reason)
                Fail($"Expected {c} to fizzle with {reason}, got {(FizzleReason)e.Value.Reason}.");
            return this;
        }

        public Scenario AssertEvent(SimEventKind kind, char unit = '\0')
        {
            if (Find(kind, unit) == null)
                Fail($"Expected a {kind} event{(unit == '\0' ? "" : $" for {unit}")}.");
            return this;
        }

        public Scenario AssertNoEvent(SimEventKind kind, char unit = '\0')
        {
            if (Find(kind, unit) != null)
                Fail($"Expected no {kind} event{(unit == '\0' ? "" : $" for {unit}")}.");
            return this;
        }

        public Scenario AssertEventCount(SimEventKind kind, int count)
        {
            int n = Log.CountOf(kind);
            if (n != count)
                Fail($"Expected {count} {kind} events, got {n}.");
            return this;
        }

        public Scenario AssertKilledBy(char victim, char killer)
        {
            var e = Find(SimEventKind.Died, victim);
            if (e == null)
                Fail($"Expected {victim} to die.");
            else if (e.Value.Other != Slot(killer))
                Fail($"Expected {victim} killed by {killer}, killer slot was {e.Value.Other}.");
            return this;
        }

        public Scenario AssertSelfKill(char victim)
        {
            var e = Find(SimEventKind.Died, victim);
            if (e == null)
                Fail($"Expected {victim} to die.");
            else if (e.Value.Other != SimEvent.None)
                Fail($"Expected {victim} to self-kill, but credit went to slot {e.Value.Other}.");
            return this;
        }

        public Scenario AssertBoard(string expected)
        {
            string want = string.Join("\n", MapParser.SplitRows(expected));
            string got = Render().TrimEnd('\n');
            if (want != got)
                Fail($"Board mismatch.\nExpected:\n{want}");
            return this;
        }

        public SimEvent? Find(SimEventKind kind, char unit = '\0')
        {
            for (int i = 0; i < Log.Count; i++)
            {
                var e = Log[i];
                if (e.Kind == kind && (unit == '\0' || e.Unit == Slot(unit)))
                    return e;
            }
            return null;
        }

        // --- rendering ---

        public string Render()
        {
            var b = State.Board;
            var sb = new StringBuilder();
            for (int y = b.Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < b.Width; x++)
                {
                    var p = new Int2(x, y);
                    var u = State.UnitAt(p);
                    if (u != null) sb.Append((char)('A' + u.Slot));
                    else if (State.KegAt(p) != null) sb.Append('K');
                    else if (b.IsRemoved(p)) sb.Append('x');
                    else sb.Append(b.Get(p).ToChar());
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }

        void Fail(string message)
        {
            var sb = new StringBuilder(message);
            sb.Append("\n\nBefore:\n").Append(_before);
            sb.Append("After:\n").Append(Render());
            sb.Append("\nUnits:\n");
            foreach (var u in State.Units)
                sb.Append("  ").Append(u).Append('\n');
            foreach (var k in State.Kegs)
                sb.Append("  ").Append(k).Append('\n');
            sb.Append("Events:\n");
            for (int i = 0; i < Log.Count; i++)
                sb.Append("  ").Append(Log[i]).Append('\n');
            Assert.Fail(sb.ToString());
        }
    }
}
