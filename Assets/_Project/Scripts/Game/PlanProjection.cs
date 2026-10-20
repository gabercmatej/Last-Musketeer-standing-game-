using Musketeers.Sim;

namespace Musketeers.Game
{
    // Why an action would fizzle (or can't be planned). Mirrors §6 Phase 0 so the planning UI
    // can grey out what the sim would refuse (GAME_DESIGN §5 Invalid actions).
    public enum PlanIssue : byte { None, NoGrit, Unloaded, KegActive, OutOfRange, Dead, PlanFull, AlreadyLoaded, Warned }

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
        // FIXES §3.2 rule 2: tick t is a Move or Dash whose destination holds another musketeer. It is allowed
        // (the sim decides); the projection assumes that unit leaves, and the preview marks the tile contested.
        public readonly bool[] Contested = new bool[TurnPlan.Length];

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
                var issue = Check(s.Board, cfg, cur, a, u.Ability, true, Crumble.IsWarned(s, cur.Pos, cfg));
                p.Issues[t] = a.Type == ActionType.Idle ? PlanIssue.None : issue;
                if (issue == PlanIssue.None && cur.Alive)
                {
                    Apply(s, slot, cfg, u.Ability, a, ref cur);
                    p.Contested[t] = (a.Type == ActionType.Move || a.Type == ActionType.Dash) && OtherUnitAt(s, slot, cur.Pos);
                }
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

        // §6 Phase 0 requirements (onWarned: the projected tile is on the ring glowing this turn). withTarget=false skips the keg range (no target chosen yet).
        public static PlanIssue Check(Board board, GameConfig cfg, TickProjection p, PlannedAction a, Ability ability, bool withTarget, bool onWarned = false)
        {
            if (!p.Alive)
                return PlanIssue.Dead;
            switch (a.Type)
            {
                case ActionType.Dash:
                    return p.Grit < cfg.DashCost ? PlanIssue.NoGrit : PlanIssue.None;
                case ActionType.Fire:
                    return p.Loaded ? PlanIssue.None : PlanIssue.Unloaded;
                case ActionType.Reload:
                    return p.Loaded ? PlanIssue.AlreadyLoaded : PlanIssue.None;   // FIXES §3.3: Reload is greyed while loaded
                case ActionType.Parry:
                    return p.Grit < cfg.ParryCost ? PlanIssue.NoGrit : PlanIssue.None;
                case ActionType.Keg:
                    if (p.Grit < cfg.KegCost) return PlanIssue.NoGrit;
                    if (p.Kegs >= cfg.MaxKegsPerPlayer) return PlanIssue.KegActive;
                    if (withTarget && !KegTargetInRange(board, cfg, p.Pos, a.Target)) return PlanIssue.OutOfRange;
                    return PlanIssue.None;
                case ActionType.Signature:
                    if (p.Grit < cfg.SignatureCost) return PlanIssue.NoGrit;
                    if (ability == Ability.TripleCharge && !p.Loaded) return PlanIssue.Unloaded;
                    // §6 Phase 2b: Switcheroo fails from a warned tile (this turn's ring), so it can't be planned there.
                    if (ability == Ability.Switcheroo && onWarned) return PlanIssue.Warned;
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
                    if (ability == Ability.TripleCharge)
                        p.Loaded = false;   // §4: Triple Charge spends the load (Phase 0)
                    p.Facing = a.Dir;
                    Signature(s, slot, cfg, ability, a, ref p);
                    break;
            }
        }

        // §6 Phase 2.1: a solid tile cancels the move; a pit is a fall. Another unit on the tile doesn't refuse
        // the plan (FIXES §3.2 rule 2): the sim decides, and the projection assumes it steps away.
        static void StepTo(GameState s, int slot, ref TickProjection p, Int2 to)
        {
            if (IsSolid(s, to))
                return;
            Land(s, ref p, to);
        }

        // FIXES §3.2 rule 1: a dash flies over everything on the middle tile except a pillar (or the wall
        // of a walled board); the landing tile must be free or the dash fails and you stay put.
        static void Dash(GameState s, int slot, ref TickProjection p, Int2 d)
        {
            Int2 first = p.Pos + d;
            Int2 second = p.Pos + d * 2;
            if (s.Board.Get(first) == TileType.Pillar || IsSolid(s, second))
                return;
            Land(s, ref p, second);
        }

        static void Signature(GameState s, int slot, GameConfig cfg, Ability ability, PlannedAction a, ref TickProjection p)
        {
            Dir dir = a.Dir;
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
                    // FIXES §3.2 rule 5: you end up where the chosen musketeer stands now.
                    if (a.Unit != slot && a.Unit < s.Units.Length && s.Units[a.Unit].Alive)
                        p.Pos = s.Units[a.Unit].Pos;
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

        // The tiles a musket shot from `from` can reach along `dir`, in order: up to `range` tiles, stopping at the
        // first pillar (not included, nothing to hit), crate, keg or unit (included, it takes the shot), exactly like
        // the sim's trace (§6 Phase 4). Used for the Fire preview highlight.
        public static System.Collections.Generic.List<Int2> ShotTiles(GameState s, int self, Int2 from, Dir dir, int range, bool stopAtUnits = true)
        {
            var tiles = new System.Collections.Generic.List<Int2>(range);
            Int2 d = dir.ToInt2();
            Int2 p = from;
            for (int step = 1; step <= range; step++)
            {
                p += d;
                if (!s.Board.InBounds(p) || s.Board.Get(p) == TileType.Pillar)
                    break;
                tiles.Add(p);
                if (s.Board.Get(p).IsSolid() || s.KegAt(p) != null || (stopAtUnits && OtherUnitAt(s, self, p)))
                    break;
            }
            return tiles;
        }

        // Preview of a Bull Rush (§4): where the charge ends, what stopped it, and where the stopped unit would
        // land after being pushed `push` tiles. Mirrors the sim's charge; the push ignores chain reactions.
        public struct RushPreview
        {
            public Int2 End;          // the rusher's final tile
            public bool HasImpact;    // something stopped the charge before the distance ran out
            public Int2 ImpactTile;   // the unit or keg that stopped it
            public bool HitUnit;      // a unit (gets pushed) rather than a keg (gets kicked)
            public Int2 Landing;      // where the pushed unit ends up (== ImpactTile when it can't move)
            public bool Slam;         // the push ended against a wall, crate, keg or unit
            public bool Falls;        // the pushed unit ends in a pit
        }

        public static RushPreview BullRush(GameState s, int slot, Int2 from, Dir dir, int distance, int push)
        {
            var r = new RushPreview { End = from, Landing = from };
            Int2 d = dir.ToInt2();
            Int2 p = from;
            for (int i = 0; i < distance; i++)
            {
                Int2 next = p + d;
                if (s.Board.Get(next).IsSolid() || s.Board.Get(next) == TileType.Pit)
                    break;
                if (s.KegAt(next) != null || OtherUnitAt(s, slot, next))
                {
                    r.HasImpact = true;
                    r.ImpactTile = next;
                    r.HitUnit = s.KegAt(next) == null;
                    break;
                }
                p = next;
            }
            r.End = p;
            r.Landing = r.ImpactTile;
            if (r.HasImpact && r.HitUnit)
                r.Landing = SlideEnd(s, r.ImpactTile, dir, push, out r.Slam, out r.Falls);
            return r;
        }

        // Where something slid `tiles` tiles along `dir` ends up: it stops before a solid tile, keg or unit
        // (a slam) and drops into a pit.
        public static Int2 SlideEnd(GameState s, Int2 from, Dir dir, int tiles, out bool slam, out bool falls)
        {
            slam = false;
            falls = false;
            Int2 d = dir.ToInt2();
            Int2 p = from;
            for (int i = 0; i < tiles; i++)
            {
                Int2 next = p + d;
                if (IsSolid(s, next) || s.UnitAt(next) != null)
                {
                    slam = true;
                    break;
                }
                p = next;
                if (s.Board.Get(p) == TileType.Pit)
                {
                    falls = true;
                    break;
                }
            }
            return p;
        }

        // True when a dash from `from` along `dir` passes over something (pit, crate, keg or unit) on its middle tile.
        public static bool DashHops(GameState s, int self, Int2 from, Dir dir)
        {
            Int2 mid = from + dir.ToInt2();
            return s.Board.Get(mid) != TileType.Floor || s.KegAt(mid) != null || OtherUnitAt(s, self, mid);
        }

        static bool IsSolid(GameState s, Int2 p) => s.Board.Get(p).IsSolid() || s.KegAt(p) != null;

        static bool OtherUnitAt(GameState s, int self, Int2 p)
        {
            var u = s.UnitAt(p);
            return u != null && u.Slot != self;
        }
    }
}
