using System;

namespace Musketeers.Sim
{
    // GAME_DESIGN §6. Each tick runs the phases in order. Inside a phase everything is computed on
    // one snapshot and then applied; where order is unavoidable, initiative decides.
    // Deterministic: ints only, arrays walked in slot or initiative order, no hashed collections.
    public static partial class TurnResolver
    {
        // Resolves all ticks of a turn, stops early if the round ends, then advances state.Turn.
        public static EventLog ResolveTurn(GameState s, TurnPlan[] plans, GameConfig cfg)
        {
            if (cfg.TicksPerTurn > TurnPlan.Length)
                throw new ArgumentException($"TicksPerTurn {cfg.TicksPerTurn} exceeds TurnPlan.Length {TurnPlan.Length}.");
            var log = new EventLog();
            for (int tick = 0; tick < cfg.TicksPerTurn && !s.RoundOver; tick++)
                ResolveTick(s, plans, tick, cfg, log);
            s.Turn++;
            return log;
        }

        public static EventLog ResolveTick(GameState s, TurnPlan[] plans, int tick, GameConfig cfg)
        {
            var log = new EventLog();
            ResolveTick(s, plans, tick, cfg, log);
            return log;
        }

        // One tick: plans[i][tick] is the action of plans[i].Slot. Missing slots Idle.
        public static void ResolveTick(GameState s, TurnPlan[] plans, int tick, GameConfig cfg, EventLog log)
        {
            if (s.RoundOver)
                return;
            log.Tick = tick;
            var c = new TickContext(s, cfg, log, tick, plans);
            Phase0Validate(c);
            Phase1Self(c);
            Phase2Movement(c);
            Phase2bAbilities(c);
            Phase3Shoves(c);
            Phase4Shots(c);
            Phase5Kegs(c);
            Phase6Deaths(c);
        }

        // §6: player slot order rotated by the turn number, lowest goes first.
        public static int Initiative(int slot, int turn, int playerCount) => (slot + turn) % playerCount;

        // Slots sorted by initiative (index 0 acts first).
        public static int[] InitiativeOrder(int turn, int playerCount)
        {
            var order = new int[playerCount];
            for (int slot = 0; slot < playerCount; slot++)
                order[Initiative(slot, turn, playerCount)] = slot;
            return order;
        }

        // §6 Kill credit: the last attacker if that was this turn or last turn, otherwise a self-kill (-1).
        public static int Killer(GameState s, Unit u, GameConfig cfg)
        {
            bool recent = u.LastAttacker >= 0 && u.LastAttacker != u.Slot && s.Turn - u.LastAttackTurn <= cfg.KillCreditTurns;
            return recent ? u.LastAttacker : SimEvent.None;
        }

        // --- shared helpers ---

        // §6 Phase 2.1/3.4: pillars, crates and kegs stop movement. Pits don't (you fall in).
        static bool IsSolid(GameState s, Int2 p) => s.Board.Get(p).IsSolid() || s.KegAt(p) != null;

        static bool IsPit(GameState s, Int2 p) => s.Board.Get(p) == TileType.Pit;

        // Lowers HP (never below 0; deaths happen in Phase 6) and records the attacker for kill credit.
        static void Damage(TickContext c, int target, int amount, int attacker, DamageKind kind, bool credit = true)
        {
            var u = c.S.Units[target];
            u.Hp = Math.Max(0, u.Hp - amount);
            c.Log.Add(SimEventKind.Damaged, unit: target, other: attacker, to: u.Pos, amount: amount, reason: (byte)kind);
            if (credit)
                Credit(c, target, attacker);
        }

        static void Credit(TickContext c, int target, int attacker)
        {
            if (attacker < 0 || attacker == target)
                return;
            var u = c.S.Units[target];
            u.LastAttacker = attacker;
            u.LastAttackTurn = c.S.Turn;
        }

        // §3/§6: entering a pit (or leaving the board) is instant death, not end-of-tick.
        static void Fall(TickContext c, int unit, Int2 pit)
        {
            var u = c.S.Units[unit];
            u.Pos = pit;
            c.Log.Add(SimEventKind.Fell, unit: unit, to: pit);
            Kill(c, unit, DeathCause.Fell);
        }

        static void Kill(TickContext c, int unit, DeathCause cause)
        {
            var u = c.S.Units[unit];
            u.Alive = false;
            u.Parrying = false;
            c.Log.Add(SimEventKind.Died, unit: unit, other: Killer(c.S, u, c.Cfg), to: u.Pos, reason: (byte)cause);
        }
    }
}
