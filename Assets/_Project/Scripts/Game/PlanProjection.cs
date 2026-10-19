using Musketeers.Sim;

namespace Musketeers.Game
{
    // Why an action would fizzle (or can't be planned). Mirrors §6 Phase 0 so the planning UI
    // can grey out what the sim would refuse (GAME_DESIGN §5 Invalid actions).
    public enum PlanIssue : byte { None, NoGrit, Unloaded, KegActive, OutOfRange, Dead, PlanFull }

    public struct TickProjection
    {
        public Int2 Pos;
        public int Grit;
        public bool Loaded;
        public int Kegs;        // active kegs owned + kegs thrown earlier in this plan
        public bool Alive;
        public Dir Facing;
    }

    // What a line (shot, hook, swap) meets first. Pits and empty floor let it through.
    public enum LineHit : byte { Nothing, Obstacle, Unit, Keg }

    // Projects one player's own position, Grit, musket and kegs across the 3 ticks of a plan, assuming
    // everyone else stands still. It's a planning aid only; the sim decides what really happens.
    public sealed class PlanProjection
    {
        public readonly TickProjection[] States = new TickProjection[TurnPlan.Length + 1];  // [t] = before tick t, [3] = end
        public readonly PlanIssue[] Issues = new PlanIssue[TurnPlan.Length];

        public static PlanProjection Project(GameState s, int slot, TurnPlan plan, GameConfig cfg)
        {
            var p = new PlanProjection();
            var u = s.Units[slot];
            var cur = new TickProjection
            {
                Pos = u.Pos,
                Grit = u.Grit,
                Loaded = u.Loaded,
                Kegs = TurnResolver.ActiveKegs(s, slot),
                Alive = u.Alive,
                Facing = Dir.S,
            };
            // Fuses of the kegs we already have out (§6 Phase 5: they tick down every tick and blow at 0,
            // which frees the one-keg limit from the next tick). Shot/chain detonations aren't predicted.
            var fuses = new System.Collections.Generic.List<int>();
            foreach (var k in s.Kegs)
            {
                if (k.Owner == slot)
                    fuses.Add(k.Fuse);
            }
            for (int t = 0; t < TurnPlan.Length; t++)
            {
                p.States[t] = cur;
                var a = plan[t];
                var issue = Check(s.Board, cfg, cur, a, u.Ability, true);
                p.Issues[t] = a.Type == ActionType.Idle ? PlanIssue.None : issue;
                if (issue == PlanIssue.None && cur.Alive)
                    Apply(s, slot, cfg, u.Ability, a, ref cur);
                for (int k = fuses.Count - 1; k >= 0; k--)
                {
                    fuses[k]--;
                    if (fuses[k] <= 0)
                    {
                        fuses.RemoveAt(k);
                        cur.Kegs--;
                    }
                }
            }
            p.States[TurnPlan.Length] = cur;
            return p;
        }

        // §6 Phase 0 requirements. withTarget=false skips the keg range (no target chosen yet).
        public static PlanIssue Check(Board board, GameConfig cfg, TickProjection p, PlannedAction a, Ability ability, bool withTarget)
        {
            if (!p.Alive)
                return PlanIssue.Dead;
            switch (a.Type)
            {
                case ActionType.Dash:
                    return p.Grit < cfg.DashCost ? PlanIssue.NoGrit : PlanIssue.None;
                case ActionType.Fire:
                    return p.Loaded ? PlanIssue.None : PlanIssue.Unloaded;
                case ActionType.Parry:
                    return p.Grit < cfg.ParryCost ? PlanIssue.NoGrit : PlanIssue.None;
                case ActionType.Keg:
                    if (p.Grit < cfg.KegCost) return PlanIssue.NoGrit;
                    if (p.Kegs >= cfg.MaxKegsPerPlayer) return PlanIssue.KegActive;
                    if (withTarget && !KegTargetInRange(board, cfg, p.Pos, a.Target)) return PlanIssue.OutOfRange;
                    return PlanIssue.None;
                case ActionType.Signature:
                    if (p.Grit < cfg.SignatureCost) return PlanIssue.NoGrit;
                    if (ability == Ability.DoubleCharge && !p.Loaded) return PlanIssue.Unloaded;
                    return PlanIssue.None;
                default:
                    return PlanIssue.None;
            }
        }

        // §5 Powder Keg: a tile on the board within Manhattan KegThrowRange (the sim fizzles anything else).
        public static bool KegTargetInRange(Board board, GameConfig cfg, Int2 from, Int2 target) =>
            board.InBounds(target) && from.Manhattan(target) <= cfg.KegThrowRange;

        static void Apply(GameState s, int slot, GameConfig cfg, Ability ability, PlannedAction a, ref TickProjection p)
        {
            Int2 d = a.Dir.ToInt2();
            switch (a.Type)
            {
                case ActionType.Move:
                    p.Facing = a.Dir;
                    StepTo(s, slot, ref p, p.Pos + d);
                    break;
                case ActionType.Dash:
                    p.Grit -= cfg.DashCost;
                    p.Facing = a.Dir;
                    Dash(s, slot, ref p, d);
                    break;
                case ActionType.Fire:
                    p.Loaded = false;
                    p.Facing = a.Dir;
                    break;
                case ActionType.Reload:
                    p.Loaded = true;
                    break;
                case ActionType.Parry:
                    p.Grit -= cfg.ParryCost;
                    break;
                case ActionType.Shove:
                    p.Facing = a.Dir;
                    break;
                case ActionType.Keg:
                    p.Grit -= cfg.KegCost;
                    p.Kegs++;
                    break;
                case ActionType.CatchBreath:
                    p.Grit = System.Math.Min(cfg.MaxGrit, p.Grit + cfg.CatchBreathGrit);
                    break;
                case ActionType.Signature:
                    p.Grit -= cfg.SignatureCost;
                    if (ability == Ability.DoubleCharge)
                        p.Loaded = false;   // §4: Double Charge spends the load (Phase 0)
                    p.Facing = a.Dir;
                    Signature(s, slot, cfg, ability, a.Dir, ref p);
                    break;
            }
        }

        // §6 Phase 2.1: a solid tile or a unit cancels the move; a pit is a fall.
        static void StepTo(GameState s, int slot, ref TickProjection p, Int2 to)
        {
            if (IsSolid(s, to) || OtherUnitAt(s, slot, to))
                return;
            Land(s, ref p, to);
        }

        static void Dash(GameState s, int slot, ref TickProjection p, Int2 d)
        {
            Int2 first = p.Pos + d;
            Int2 second = p.Pos + d * 2;
            if (IsSolid(s, first) || OtherUnitAt(s, slot, first))
                return;
            if (IsSolid(s, second))
            {
                Land(s, ref p, first);
                return;
            }
            if (OtherUnitAt(s, slot, second))
                return;
            Land(s, ref p, second);
        }

        static void Signature(GameState s, int slot, GameConfig cfg, Ability ability, Dir dir, ref TickProjection p)
        {
            Int2 d = dir.ToInt2();
            switch (ability)
            {
                case Ability.Grappin:
                {
                    // Only a wall moves you: you fly to the tile before it.
                    if (TraceLine(s, slot, p.Pos, dir, cfg.GrappleRange, true, out Int2 last) == LineHit.Obstacle)
                        Land(s, ref p, last - d);
                    break;
                }
                case Ability.Switcheroo:
                {
                    var hit = TraceLine(s, slot, p.Pos, dir, cfg.SwitcherooRange, true, out Int2 last);
                    if (hit == LineHit.Unit || hit == LineHit.Keg)
                        p.Pos = last;
                    break;
                }
                case Ability.BullRush:
                    p.Pos = BullRushEnd(s, slot, p.Pos, dir, cfg.BullRushDistance);
                    break;
            }
        }

        static void Land(GameState s, ref TickProjection p, Int2 to)
        {
            p.Pos = to;
            if (s.Board.Get(to) == TileType.Pit)
                p.Alive = false;
        }

        // §4 Bull Rush: up to `distance` tiles, stopping before a pillar, crate, keg, unit or pit.
        public static Int2 BullRushEnd(GameState s, int slot, Int2 from, Dir dir, int distance)
        {
            Int2 d = dir.ToInt2();
            Int2 p = from;
            for (int i = 0; i < distance; i++)
            {
                Int2 next = p + d;
                if (IsSolid(s, next) || s.Board.Get(next) == TileType.Pit || OtherUnitAt(s, slot, next))
                    break;
                p = next;
            }
            return p;
        }

        // Walks from origin (exclusive) up to range tiles and stops at the board edge, like the sim's
        // shot and hook traces. `last` is the hit tile, or the last tile reached.
        public static LineHit TraceLine(GameState s, int self, Int2 origin, Dir dir, int range, bool stopAtUnits, out Int2 last)
        {
            Int2 d = dir.ToInt2();
            last = origin;
            for (int step = 1; step <= range; step++)
            {
                Int2 next = last + d;
                if (!s.Board.InBounds(next))
                    break;
                last = next;
                if (s.Board.Get(next).IsSolid())
                    return LineHit.Obstacle;
                if (s.KegAt(next) != null)
                    return LineHit.Keg;
                if (stopAtUnits && OtherUnitAt(s, self, next))
                    return LineHit.Unit;
            }
            return LineHit.Nothing;
        }

        static bool IsSolid(GameState s, Int2 p) => s.Board.Get(p).IsSolid() || s.KegAt(p) != null;

        static bool OtherUnitAt(GameState s, int self, Int2 p)
        {
            var u = s.UnitAt(p);
            return u != null && u.Slot != self;
        }
    }
}
