namespace Musketeers.Sim
{
    public static partial class TurnResolver
    {
        // §6 Phase 2: Move and Dash, all simultaneous.
        static void Phase2Movement(TickContext c)
        {
            c.Log.Phase = SimPhase.Movement;
            var s = c.S;
            var units = s.Units;
            int n = units.Length;
            var dest = new Int2[n];
            var first = new Int2[n];       // tile entered first
            var hasSecond = new bool[n];   // a dash that enters a second tile (then dest is that tile)
            var moving = new bool[n];
            var dashing = new bool[n];
            var leaping = new bool[n];

            // 2.1 Desired destinations.
            for (int i = 0; i < n; i++)
            {
                var u = units[i];
                var a = c.Act[i];
                if (!c.Active[i] || !u.Alive || (a.Type != ActionType.Move && a.Type != ActionType.Dash))
                    continue;
                Int2 d = a.Dir.ToInt2();
                Int2 step1 = u.Pos + d;
                if (IsSolid(s, step1))
                {
                    c.Log.Add(SimEventKind.MoveBlocked, unit: i, from: u.Pos, to: step1, dir: a.Dir, reason: (byte)BlockReason.Obstacle);
                    continue;
                }
                first[i] = step1;
                if (a.Type == ActionType.Move)
                {
                    dest[i] = step1;
                    moving[i] = true;
                    continue;
                }

                // Dash: 2 tiles. A unit in the middle tile at the start of the tick cancels it, even
                // if that unit moves away this tick (the check uses positions before anyone moves).
                dashing[i] = true;
                if (s.UnitAt(step1) != null)
                {
                    c.Log.Add(SimEventKind.MoveBlocked, unit: i, from: u.Pos, to: step1, dir: a.Dir, reason: (byte)BlockReason.Unit);
                    continue;
                }
                Int2 step2 = step1 + d;
                if (IsSolid(s, step2))
                {
                    dest[i] = step1;   // only the second tile is blocked: stop on the first
                }
                else
                {
                    dest[i] = step2;
                    hasSecond[i] = true;
                    leaping[i] = IsPit(s, step1) && !IsPit(s, step2);
                }
                moving[i] = true;
            }

            // 2.2 Head-on collisions, computed on the desires above. Two movers bonk if they enter a
            // common tile (same destination, or one passes through a tile the other enters) or cross
            // one edge in opposite directions (a swap, or a dash through someone coming the other way).
            // A unit bonks at most once per tick even if it collides with several movers.
            var bonk = new bool[n];
            var partner = new int[n];
            for (int i = 0; i < n; i++)
            {
                partner[i] = SimEvent.None;
                if (!moving[i])
                    continue;
                for (int j = 0; j < n; j++)
                {
                    if (j == i || !moving[j] || !PathsCollide(units, first, dest, hasSecond, i, j))
                        continue;
                    bonk[i] = true;
                    if (partner[i] == SimEvent.None)
                        partner[i] = j;
                }
            }
            for (int i = 0; i < n; i++)
            {
                if (!bonk[i])
                    continue;
                moving[i] = false;
                c.Log.Add(SimEventKind.Bonk, unit: i, other: partner[i], from: units[i].Pos, to: dest[i], dir: c.Act[i].Dir);
                // Movement bonks are mutual accidents: no kill credit to either side.
                Damage(c, i, c.Cfg.BonkDamage, SimEvent.None, DamageKind.Bonk);
            }

            // 2.3 Blocked: a mover whose destination holds a unit that isn't moving stays put (no damage).
            // Repeat to a fixed point so chains resolve. Whatever is still moving afterwards forms
            // follow-the-leader lines or rotations, which all succeed.
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int i = 0; i < n; i++)
                {
                    if (!moving[i])
                        continue;
                    for (int j = 0; j < n; j++)
                    {
                        if (j == i || moving[j] || !units[j].Alive || units[j].Pos != dest[i])
                            continue;
                        moving[i] = false;
                        changed = true;
                        c.Log.Add(SimEventKind.MoveBlocked, unit: i, from: units[i].Pos, to: dest[i], dir: c.Act[i].Dir, reason: (byte)BlockReason.Unit);
                        break;
                    }
                }
            }

            // 2.4 Apply, then falls (after everyone has moved, so a follower can still take a faller's tile).
            for (int i = 0; i < n; i++)
            {
                if (!moving[i])
                    continue;
                Int2 from = units[i].Pos;
                units[i].Pos = dest[i];
                c.Log.Add(dashing[i] ? SimEventKind.Dashed : SimEventKind.Moved, unit: i, from: from, to: dest[i], dir: c.Act[i].Dir);
                if (leaping[i])
                    c.Log.Add(SimEventKind.Leaped, unit: i, from: from, to: dest[i], dir: c.Act[i].Dir);
            }
            for (int i = 0; i < n; i++)
            {
                if (moving[i] && IsPit(s, units[i].Pos))
                    Fall(c, i, units[i].Pos);
            }
        }

        // A mover enters first[i], and dest[i] too for a full dash. Its edges are start→first and,
        // for a full dash, first→dest.
        static bool PathsCollide(Unit[] units, Int2[] first, Int2[] dest, bool[] hasSecond, int i, int j)
        {
            if (Enters(first, dest, hasSecond, i, first[j]) || (hasSecond[j] && Enters(first, dest, hasSecond, i, dest[j])))
                return true;
            if (Traverses(units, first, dest, hasSecond, j, first[i], units[i].Pos))
                return true;
            return hasSecond[i] && Traverses(units, first, dest, hasSecond, j, dest[i], first[i]);
        }

        static bool Enters(Int2[] first, Int2[] dest, bool[] hasSecond, int i, Int2 p) =>
            first[i] == p || (hasSecond[i] && dest[i] == p);

        static bool Traverses(Unit[] units, Int2[] first, Int2[] dest, bool[] hasSecond, int i, Int2 from, Int2 to) =>
            (units[i].Pos == from && first[i] == to) || (hasSecond[i] && first[i] == from && dest[i] == to);
    }
}
