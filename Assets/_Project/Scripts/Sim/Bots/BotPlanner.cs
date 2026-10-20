using System;
using System.Collections.Generic;

namespace Musketeers.Sim
{
    // A simple heuristic bot (BUILD_PLAN Day 2): varied, not smart, never suicidal on purpose.
    // It plans 3 ticks against a projection of its own position/Grit/musket and of the enemies it
    // pushes or yanks; everyone else is assumed to stand still. Each bot owns its RNG and memory and
    // never touches GameState.Rng, because only the host plans bots (TECH_DESIGN §5) and the shared
    // state hash must not depend on them.
    public sealed class BotPlanner
    {
        public readonly int Slot;
        readonly DeterministicRng _rng;
        Int2[] _seen;            // everyone's position when we last planned
        int _seenTurn = -1;
        int _seenRound = -1;

        // Per-plan scratch.
        GameState _s;
        GameConfig _cfg;
        Int2 _pos;
        int _grit;
        bool _loaded;
        bool _hasKeg;
        bool _swapped;           // planned a Switcheroo this turn: our position afterwards is unknown, so we stand still
        Int2? _plannedKeg;
        Int2[] _ep;              // projected enemy positions (index = slot)
        bool[] _ea;              // projected enemy alive
        bool[] _stood;           // enemy didn't move last turn
        readonly List<Int2> _candidates = new List<Int2>();
        // Every tile we might really be on: the projection assumes our moves succeed, but a bonk or
        // a block leaves us behind and shifts every later move by a tile. A move is only planned if
        // it's pit-free from all of these.
        readonly List<Int2> _possible = new List<Int2>();
        readonly List<Int2> _scratch = new List<Int2>();

        public BotPlanner(int slot, uint seed)
        {
            Slot = slot;
            _rng = new DeterministicRng(seed);
        }

        BotPlanner(int slot, DeterministicRng rng)
        {
            Slot = slot;
            _rng = rng;
        }

        public BotPlanner Clone()
        {
            var c = new BotPlanner(Slot, _rng.Clone()) { _seenTurn = _seenTurn, _seenRound = _seenRound };
            if (_seen != null)
                c._seen = (Int2[])_seen.Clone();
            return c;
        }

        public uint RngState => _rng.State;

        // roundNumber only tells the bot when its "who stood still" memory is stale.
        public TurnPlan Plan(GameState s, GameConfig cfg, int roundNumber = 0)
        {
            var plan = new TurnPlan(Slot);
            var me = s.Units[Slot];
            if (!me.Alive || s.RoundOver)
                return plan;

            _s = s;
            _cfg = cfg;
            _pos = me.Pos;
            _grit = me.Grit;
            _loaded = me.Loaded;
            _hasKeg = TurnResolver.ActiveKegs(s, Slot) >= cfg.MaxKegsPerPlayer;
            _plannedKeg = null;
            _swapped = false;
            _possible.Clear();
            _possible.Add(_pos);
            int n = s.Units.Length;
            _ep = new Int2[n];
            _ea = new bool[n];
            _stood = new bool[n];
            bool memory = _seen != null && _seenRound == roundNumber && _seenTurn == s.Turn - 1;
            for (int i = 0; i < n; i++)
            {
                _ep[i] = s.Units[i].Pos;
                _ea[i] = i != Slot && s.Units[i].Alive;
                _stood[i] = memory && _seen[i] == s.Units[i].Pos;
            }

            for (int t = 0; t < cfg.TicksPerTurn && t < TurnPlan.Length; t++)
            {
                var a = Decide();
                Project(a);
                plan[t] = a;
            }

            _seen = new Int2[n];
            for (int i = 0; i < n; i++)
                _seen[i] = s.Units[i].Pos;
            _seenTurn = s.Turn;
            _seenRound = roundNumber;
            _s = null;
            return plan;
        }

        // --- decision ---

        PlannedAction Decide()
        {
            if (!AnyEnemy() || _swapped)
                return _grit < _cfg.MaxGrit ? PlannedAction.CatchBreath() : PlannedAction.Idle();

            bool inDanger = Dangerous(_pos) || Warned(_pos);
            if (inDanger)
            {
                if (_grit >= _cfg.SignatureCost && TrySignature(true, out var esc))
                    return esc;
                if (TryEscape(out var step))
                    return step;
                if (_grit >= _cfg.ParryCost && Dangerous(_pos))
                    return PlannedAction.Parry();   // can't leave the blast: parry blocks its damage
            }

            if (_grit >= _cfg.SignatureCost && TrySignature(false, out var sig))
                return sig;
            if (_loaded && TryFire(out var fire))
                return fire;
            if (TryShove(out var shove, false))
                return shove;
            if (_grit >= _cfg.KegCost && !_hasKeg && TryKeg(out var keg))
                return keg;
            if (_grit >= _cfg.ParryCost && LinedUpByLoadedEnemy() && Chance(35))
                return PlannedAction.Parry();
            if (!_loaded && (!LinedUpByLoadedEnemy() || Chance(40)))
                return PlannedAction.Reload();
            if (_grit < 2 && Chance(15))
                return PlannedAction.CatchBreath();
            if (TryApproach(out var move))
                return move;
            if (TryShove(out var any, true))
                return any;
            return _grit < _cfg.MaxGrit ? PlannedAction.CatchBreath() : (_loaded ? PlannedAction.Parry() : PlannedAction.Reload());
        }

        // Walk toward the nearest safe tile, passing through warned or blasted tiles if we must
        // (they only hurt if we're still on them when the turn ends or the keg blows).
        bool TryEscape(out PlannedAction a)
        {
            a = default;
            var step = FirstStep(Walkable, p => Standable(p) && !Dangerous(p), out int dist);
            if (step == null)
                return false;
            Dir d = step.Value;
            Int2 mid = _pos + d.ToInt2();
            Int2 two = mid + d.ToInt2();
            // Far from safety with Grit to spare: dash (it flies over the middle tile, only a pillar stops
            // it) if the landing is safe.
            if (dist >= 2 && _grit >= _cfg.DashCost && DashClear(mid) && Standable(two) && !Dangerous(two) && EnemyAt(two) < 0
                && DashLandingPitFree(d))
                a = PlannedAction.Dash(d);
            else if (PitFreeFromAll(d, 1))
                a = PlannedAction.Move(d);
            else
                return false;
            return true;
        }

        bool TryFire(out PlannedAction a)
        {
            a = default;
            int bestScore = -1;
            foreach (Dir d in DirExt.All)
            {
                int e = FirstEnemyInLine(_pos, d, _cfg.FireRange, out int dist);
                if (e < 0)
                    continue;
                int score = (_stood[e] ? 10 : 0) + (10 - dist) + _rng.Range(0, 3);
                if (score > bestScore)
                {
                    bestScore = score;
                    a = PlannedAction.Fire(d);
                }
            }
            return bestScore >= 0 && Chance(85);
        }

        // Shove an adjacent enemy: always toward a pit or a warned tile, often into a wall, and when
        // `anyTarget` (nothing better to do) just because it's there.
        bool TryShove(out PlannedAction a, bool anyTarget)
        {
            a = default;
            foreach (Dir d in DirExt.All)
            {
                Int2 at = _pos + d.ToInt2();
                if (EnemyAt(at) < 0)
                    continue;
                Int2 beyond = at + d.ToInt2();
                bool deadly = Pit(beyond) || Warned(beyond);
                bool wall = Solid(beyond) || EnemyAt(beyond) >= 0;
                if ((deadly && Chance(90)) || (wall && Chance(50)) || (anyTarget && Chance(60)))
                {
                    a = PlannedAction.Shove(d);
                    return true;
                }
            }
            return false;
        }

        // A keg near 2+ clustered enemies, never on a tile whose blast reaches us. With a single
        // enemy left, now and then lob it straight at them (impact).
        bool TryKeg(out PlannedAction a)
        {
            a = default;
            int best = 1;
            _candidates.Clear();
            int r = _cfg.KegThrowRange;
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Abs(dx) + Math.Abs(dy) > r)
                        continue;
                    var p = new Int2(_pos.X + dx, _pos.Y + dy);
                    // Floor only (a pit or crate target blows up on impact), and never a blast that
                    // could chain into a keg sitting next to us.
                    if (!_s.Board.InBounds(p) || _s.Board.Get(p) != TileType.Floor || NearAnyPossible(p) || ChainsNearUs(p))
                        continue;
                    int count = 0;
                    for (int i = 0; i < _ep.Length; i++)
                        if (_ea[i] && Chebyshev(p, _ep[i]) <= 1) count++;
                    if (count > best)
                    {
                        best = count;
                        _candidates.Clear();
                    }
                    if (count == best && count >= 2)
                        _candidates.Add(p);
                }
            }
            if (_candidates.Count > 0 && Chance(75))
            {
                a = PlannedAction.ThrowKeg(_candidates[_rng.Range(0, _candidates.Count)]);
                return true;
            }
            if (EnemiesAlive() == 1 && Chance(15))
            {
                for (int i = 0; i < _ep.Length; i++)
                {
                    if (_ea[i] && _ep[i].Manhattan(_pos) <= r && !NearAnyPossible(_ep[i]) && !ChainsNearUs(_ep[i]))
                    {
                        a = PlannedAction.ThrowKeg(_ep[i]);
                        return true;
                    }
                }
            }
            return false;
        }

        bool TryApproach(out PlannedAction a)
        {
            a = default;
            var first = FirstStepTowardEnemy(out int dist);
            if (first == null)
                return TryHop(out a);   // no safe route (or already next to an enemy)
            Dir d = first.Value;
            // Sometimes dash when far away and the landing is safe.
            Int2 mid = _pos + d.ToInt2();
            Int2 two = mid + d.ToInt2();
            if (dist >= 3 && _grit >= _cfg.DashCost + 1 && Standable(mid) && Standable(two) && !Dangerous(two)
                && EnemyAt(two) < 0 && DashLandingPitFree(d) && Chance(25))
            {
                a = PlannedAction.Dash(d);
                return true;
            }
            if (!PitFreeFromAll(d, 1))
                return false;
            a = PlannedAction.Move(d);
            return true;
        }

        // Dash over a pit, crate or keg (or a unit) to a safe landing that is closer to an enemy.
        bool TryHop(out PlannedAction a)
        {
            a = default;
            if (_grit < _cfg.DashCost + 1 || !Chance(60))
                return false;
            int here = NearestEnemyDistance(_pos);
            foreach (Dir d in DirExt.All)
            {
                Int2 mid = _pos + d.ToInt2();
                Int2 two = mid + d.ToInt2();
                if (!DashClear(mid) || !Standable(two) || Dangerous(two) || EnemyAt(two) >= 0 || !DashLandingPitFree(d))
                    continue;
                if (NearestEnemyDistance(two) < here - 1)
                {
                    a = PlannedAction.Dash(d);
                    return true;
                }
            }
            return false;
        }

        // escaping: only Switcheroo out of danger counts (the enemy takes our tile).
        bool TrySignature(bool escaping, out PlannedAction a)
        {
            a = default;
            switch (_s.Units[Slot].Ability)
            {
                case Ability.Grappin: return !escaping && TryGrappin(out a);
                case Ability.Switcheroo: return TrySwitcheroo(escaping, out a);
                case Ability.TripleCharge: return !escaping && _loaded && TryTripleCharge(out a);
                case Ability.BullRush: return !escaping && TryBullRush(out a);
            }
            return false;
        }

        bool TryGrappin(out PlannedAction a)
        {
            a = default;
            foreach (Dir d in DirExt.All)
            {
                int e = FirstEnemyInLine(_pos, d, _cfg.GrappleRange, out int dist, passPits: true);
                if (e < 0 || dist < 2)
                    continue;
                Int2 landing = _pos + d.ToInt2();
                if (Pit(landing) && Chance(95))
                {
                    a = PlannedAction.Signature(d);   // yank them into the pit next to us
                    return true;
                }
                if (!Pit(landing) && _loaded && dist >= 3 && Chance(25))
                {
                    a = PlannedAction.Signature(d);   // drag them in for a point-blank shot
                    return true;
                }
            }
            return false;
        }

        // Switcheroo (FIXES §3.2 rule 5): pick a living enemy by slot. It fails on a warned tile, so never
        // cast it from one. Escaping a keg blast: swap with an enemy who stands somewhere safe. Otherwise
        // swap with the nearest enemy when we stand next to a pit (they get that spot and we shove them in
        // next turn), now and then just to reshuffle.
        bool TrySwitcheroo(bool escaping, out PlannedAction a)
        {
            a = default;
            if (Warned(_pos))
                return false;
            int best = -1;
            int bestDist = int.MaxValue;
            for (int e = 0; e < _ep.Length; e++)
            {
                if (!_ea[e] || Dangerous(_ep[e]) || Warned(_ep[e]) || !Standable(_ep[e]))
                    continue;
                int dist = _ep[e].Manhattan(_pos);
                if (dist < 2 || dist >= bestDist)
                    continue;
                best = e;
                bestDist = dist;
            }
            if (best < 0)
                return false;
            if (escaping ? Chance(90) : (NextToPit(_pos) ? Chance(60) : Chance(8)))
            {
                a = PlannedAction.Switcheroo(best);
                return true;
            }
            return false;
        }

        // Triple Charge: 3 shots down a line, each hit pushing the target one tile further. Best when the
        // pushes end in a pit or a warned tile, decent point blank, fine as a barrage otherwise.
        bool TryTripleCharge(out PlannedAction a)
        {
            a = default;
            foreach (Dir d in DirExt.All)
            {
                int e = FirstEnemyInLine(_pos, d, _cfg.FireRange, out _);
                if (e < 0)
                    continue;
                int hits = SimulateTripleCharge(d, false, out bool deadly);
                if ((deadly && Chance(95)) || (hits >= 3 && Chance(80)) || (hits >= 2 && Chance(50)) || Chance(15))
                {
                    a = PlannedAction.Signature(d);
                    return true;
                }
            }
            return false;
        }

        // Plays the charge out on the projected enemies: every shot hits the first enemy in line (within
        // FireRange from us) and slides it one tile. Returns the hits; deadly = someone ended in a pit or
        // on a warned tile. With apply, the projected enemies move.
        int SimulateTripleCharge(Dir d, bool apply, out bool deadly)
        {
            deadly = false;
            Int2 dv = d.ToInt2();
            int hits = 0;
            var pos = (Int2[])_ep.Clone();
            var alive = (bool[])_ea.Clone();
            for (int shot = 0; shot < _cfg.TripleChargeShots; shot++)
            {
                int e = -1;
                Int2 p = _pos;
                for (int step = 1; step <= _cfg.FireRange && e < 0; step++)
                {
                    p += dv;
                    if (!_s.Board.InBounds(p) || Solid(p))
                        break;
                    for (int i = 0; i < pos.Length; i++)
                        if (alive[i] && pos[i] == p) { e = i; break; }
                }
                if (e < 0)
                    break;
                hits++;
                for (int step = 0; step < _cfg.TripleChargePush; step++)
                {
                    Int2 next = pos[e] + dv;
                    if (Pit(next)) { alive[e] = false; deadly = true; break; }
                    bool blocked = Solid(next) || next == _pos;
                    for (int i = 0; i < pos.Length && !blocked; i++)
                        if (alive[i] && i != e && pos[i] == next) blocked = true;
                    if (blocked) break;
                    pos[e] = next;
                }
                if (alive[e] && Warned(pos[e]))
                    deadly = true;
            }
            if (apply)
            {
                _ep = pos;
                _ea = alive;
            }
            return hits;
        }

        bool TryBullRush(out PlannedAction a)
        {
            a = default;
            foreach (Dir d in DirExt.All)
            {
                Int2 dv = d.ToInt2();
                Int2 p = _pos;
                int hit = -1;
                for (int step = 0; step < _cfg.BullRushDistance; step++)
                {
                    Int2 next = p + dv;
                    if (Solid(next) || Pit(next))
                        break;
                    hit = EnemyAt(next);
                    if (hit >= 0)
                        break;
                    p = next;
                }
                if (hit < 0 || Dangerous(p) || Warned(p))
                    continue;
                // Where does a push of BullRushPush send them?
                Int2 q = _ep[hit];
                bool deadly = false, slam = false;
                for (int step = 0; step < _cfg.BullRushPush; step++)
                {
                    Int2 next = q + dv;
                    if (Pit(next) || Warned(next)) { deadly = true; break; }
                    if (Solid(next) || EnemyAt(next) >= 0) { slam = true; break; }
                    q = next;
                }
                if ((deadly && Chance(95)) || (slam && Chance(40)) || Chance(10))
                {
                    a = PlannedAction.Signature(d);
                    return true;
                }
            }
            return false;
        }

        // --- projection ---

        void Project(PlannedAction a)
        {
            switch (a.Type)
            {
                case ActionType.CatchBreath:
                    _grit = Math.Min(_cfg.MaxGrit, _grit + _cfg.CatchBreathGrit);
                    break;
                case ActionType.Reload:
                    _loaded = true;
                    break;
                case ActionType.Fire:
                    _loaded = false;
                    break;
                case ActionType.Parry:
                    _grit -= _cfg.ParryCost;
                    break;
                case ActionType.Move:
                    StepTo(_pos + a.Dir.ToInt2());
                    ExpandPossible(a.Dir, 1);
                    break;
                case ActionType.Dash:
                    _grit -= _cfg.DashCost;
                    StepTo(_pos + a.Dir.ToInt2() * 2);
                    ExpandPossible(a.Dir, 2);
                    break;
                case ActionType.Keg:
                    _grit -= _cfg.KegCost;
                    _hasKeg = true;
                    _plannedKeg = a.Target;
                    break;
                case ActionType.Shove:
                    PushEnemy(EnemyAt(_pos + a.Dir.ToInt2()), a.Dir, 1);
                    break;
                case ActionType.Signature:
                    _grit -= _cfg.SignatureCost;
                    ProjectSignature(a);
                    break;
            }
        }

        void ProjectSignature(PlannedAction act)
        {
            Dir d = act.Dir;
            Int2 dv = d.ToInt2();
            switch (_s.Units[Slot].Ability)
            {
                case Ability.TripleCharge:
                    _loaded = false;
                    SimulateTripleCharge(d, true, out _);
                    break;
                case Ability.Grappin:
                {
                    int e = FirstEnemyInLine(_pos, d, _cfg.GrappleRange, out _, passPits: true);
                    if (e >= 0)
                    {
                        _ep[e] = _pos + dv;
                        if (Pit(_ep[e])) _ea[e] = false;
                    }
                    break;
                }
                case Ability.Switcheroo:
                {
                    int e = act.Unit;
                    if (e < _ep.Length && _ea[e])
                    {
                        Int2 mine = _pos;
                        _pos = _ep[e];
                        AddPossible(_possible, _pos);
                        _ep[e] = mine;
                        _swapped = true;   // where we really end up depends on where they are by then: stand still
                    }
                    break;
                }
                case Ability.BullRush:
                {
                    // Any tile along the charge from any possible start could be where we stop (the
                    // unit we expect to hit may have moved, or we may not be where we think).
                    _scratch.Clear();
                    foreach (var q in _possible)
                    {
                        AddPossible(_scratch, q);
                        RushPathIgnoringUnits(q, dv, _scratch);
                    }
                    RushPathIgnoringUnits(_pos, dv, _scratch);
                    int hit = RushEnd(_pos, dv, out Int2 p);
                    AddPossible(_scratch, p);
                    _possible.Clear();
                    _possible.AddRange(_scratch);
                    _pos = p;
                    if (hit >= 0)
                        PushEnemy(hit, d, _cfg.BullRushPush);
                    break;
                }
            }
        }

        // Moves only fail on solids, kegs and units; warned tiles are fine to cross.
        // Expected Bull Rush end from `from`; returns the enemy that stops it, or -1.
        int RushEnd(Int2 from, Int2 dv, out Int2 end)
        {
            end = from;
            for (int step = 0; step < _cfg.BullRushDistance; step++)
            {
                Int2 next = end + dv;
                if (Solid(next) || Pit(next))
                    return -1;
                int hit = EnemyAt(next);
                if (hit >= 0)
                    return hit;
                end = next;
            }
            return -1;
        }

        // Every tile a rush from `from` could stop on if the units in the way had moved off.
        void RushPathIgnoringUnits(Int2 from, Int2 dv, List<Int2> path)
        {
            Int2 p = from;
            for (int step = 0; step < _cfg.BullRushDistance; step++)
            {
                Int2 next = p + dv;
                if (Solid(next) || Pit(next))
                    return;
                p = next;
                AddPossible(path, p);
            }
        }

        // Each possible tile either moves (by 1, or 2 for a dash) or stays (blocked/bonked/cancelled).
        void ExpandPossible(Dir d, int tiles)
        {
            _scratch.Clear();
            foreach (var p in _possible)
            {
                AddPossible(_scratch, p);
                if (tiles == 1)
                    AddPossible(_scratch, p + d.ToInt2());
                else
                    AddPossible(_scratch, p + d.ToInt2() * 2);   // a dash only ever enters its landing tile
            }
            AddPossible(_scratch, _pos);
            _possible.Clear();
            _possible.AddRange(_scratch);
        }

        void AddPossible(List<Int2> list, Int2 p)
        {
            if (_s.Board.InBounds(p) && _s.Board.Get(p) == TileType.Floor && !list.Contains(p))
                list.Add(p);
        }

        // No possible tile would put us in a pit by moving `tiles` in direction d.
        bool PitFreeFromAll(Dir d, int tiles)
        {
            foreach (var p in _possible)
            {
                for (int k = 1; k <= tiles; k++)
                {
                    if (Pit(p + d.ToInt2() * k))
                        return false;
                }
            }
            return true;
        }

        // A dash flies over everything but a pillar (or a wall) on the middle tile.
        bool DashClear(Int2 mid) => _s.Board.Get(mid) != TileType.Pillar;

        // No possible tile would put the dash landing in a pit.
        bool DashLandingPitFree(Dir d)
        {
            foreach (var p in _possible)
            {
                if (Pit(p + d.ToInt2() * 2))
                    return false;
            }
            return true;
        }

        bool ChainsNearUs(Int2 target)
        {
            foreach (var k in _s.Kegs)
                if (Chebyshev(k.Pos, target) <= 1 && NearAnyPossible(k.Pos)) return true;
            return false;
        }

        bool NearAnyPossible(Int2 target)
        {
            foreach (var p in _possible)
                if (Chebyshev(p, target) <= 1) return true;
            return Chebyshev(_pos, target) <= 1;
        }

        void StepTo(Int2 p)
        {
            if (Walkable(p))
                _pos = p;
        }

        void PushEnemy(int e, Dir d, int strength)
        {
            if (e < 0)
                return;
            for (int step = 0; step < strength; step++)
            {
                Int2 next = _ep[e] + d.ToInt2();
                if (Pit(next))
                {
                    _ea[e] = false;
                    return;
                }
                if (Solid(next) || EnemyAt(next) >= 0 || next == _pos)
                    return;
                _ep[e] = next;
            }
        }

        // --- board queries on the projection ---

        bool Solid(Int2 p) => _s.Board.Get(p).IsSolid() || _s.KegAt(p) != null || (_plannedKeg == p);
        bool Pit(Int2 p) => _s.Board.Get(p) == TileType.Pit;
        bool Warned(Int2 p) => Crumble.IsWarned(_s, p, _cfg);
        bool Standable(Int2 p) => _s.Board.InBounds(p) && _s.Board.Get(p) == TileType.Floor && _s.KegAt(p) == null && _plannedKeg != p && !Warned(p);

        // Inside the blast of any keg on the board, or of the one we plan to throw this turn.
        bool Dangerous(Int2 p)
        {
            foreach (var k in _s.Kegs)
                if (Chebyshev(k.Pos, p) <= 1) return true;
            return _plannedKeg.HasValue && Chebyshev(_plannedKeg.Value, p) <= 1;
        }

        int EnemyAt(Int2 p)
        {
            for (int i = 0; i < _ep.Length; i++)
                if (_ea[i] && _ep[i] == p) return i;
            return -1;
        }

        bool AnyEnemy() => EnemiesAlive() > 0;

        int EnemiesAlive()
        {
            int n = 0;
            for (int i = 0; i < _ea.Length; i++)
                if (_ea[i]) n++;
            return n;
        }

        bool NextToPit(Int2 p)
        {
            foreach (Dir d in DirExt.All)
                if (Pit(p + d.ToInt2())) return true;
            return false;
        }

        // First enemy along a line, stopped by solids and kegs (and by board edge). Pits let a shot,
        // hook or swap through. Returns the slot and its distance, or -1.
        int FirstEnemyInLine(Int2 from, Dir d, int range, out int dist, bool passPits = true)
        {
            Int2 p = from;
            for (dist = 1; dist <= range; dist++)
            {
                p += d.ToInt2();
                if (!_s.Board.InBounds(p) || Solid(p))
                    return -1;
                if (!passPits && Pit(p))
                    return -1;
                int e = EnemyAt(p);
                if (e >= 0)
                    return e;
            }
            return -1;
        }

        bool LinedUpByLoadedEnemy()
        {
            foreach (Dir d in DirExt.All)
            {
                int e = FirstEnemyInLine(_pos, d, _cfg.FireRange, out _);
                if (e >= 0 && _s.Units[e].Loaded)
                    return true;
            }
            return false;
        }

        int NearestEnemyDistance(Int2 p)
        {
            int best = int.MaxValue;
            for (int i = 0; i < _ep.Length; i++)
                if (_ea[i]) best = Math.Min(best, _ep[i].Manhattan(p));
            return best;
        }

        // BFS to the nearest tile adjacent to an enemy over safe, standable tiles.
        Dir? FirstStepTowardEnemy(out int dist)
        {
            var step = FirstStep(p => Standable(p) && !Dangerous(p) && EnemyAt(p) < 0, p => IsNextToEnemy(p), out dist);
            return step;
        }

        bool IsNextToEnemy(Int2 p)
        {
            foreach (Dir d in DirExt.All)
                if (EnemyAt(p + d.ToInt2()) >= 0) return true;
            return false;
        }

        // Floor we can physically walk over this tick (warned and blasted tiles included).
        bool Walkable(Int2 p) => _s.Board.InBounds(p) && _s.Board.Get(p) == TileType.Floor && _s.KegAt(p) == null
            && _plannedKeg != p && EnemyAt(p) < 0;

        // Breadth-first search from our tile; returns the first step toward the nearest goal tile and
        // its distance, or null (also when we already stand on a goal). The neighbour order is rotated
        // per call so equal routes vary.
        Dir? FirstStep(Func<Int2, bool> passable, Func<Int2, bool> goal, out int dist)
        {
            dist = 0;
            var b = _s.Board;
            if (!b.InBounds(_pos) || goal(_pos))
                return null;
            int w = b.Width;
            var firstDir = new int[w * b.Height];
            var depth = new int[w * b.Height];
            var seen = new bool[w * b.Height];
            var queue = new Queue<Int2>();
            seen[_pos.Y * w + _pos.X] = true;
            queue.Enqueue(_pos);
            int rot = _rng.Range(0, 4);
            while (queue.Count > 0)
            {
                Int2 p = queue.Dequeue();
                int pi = p.Y * w + p.X;
                for (int k = 0; k < 4; k++)
                {
                    Dir d = DirExt.All[(k + rot) & 3];
                    Int2 q = p + d.ToInt2();
                    if (!b.InBounds(q))
                        continue;
                    int qi = q.Y * w + q.X;
                    if (seen[qi] || !passable(q))
                        continue;
                    seen[qi] = true;
                    firstDir[qi] = p == _pos ? (int)d : firstDir[pi];
                    depth[qi] = depth[pi] + 1;
                    if (goal(q))
                    {
                        dist = depth[qi];
                        return (Dir)firstDir[qi];
                    }
                    queue.Enqueue(q);
                }
            }
            return null;
        }

        bool Chance(int percent) => _rng.Range(0, 100) < percent;

        static int Chebyshev(Int2 a, Int2 b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    }
}
