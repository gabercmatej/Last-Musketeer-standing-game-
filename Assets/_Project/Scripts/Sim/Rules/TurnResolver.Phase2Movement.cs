namespace Musketeers.Sim
{
    public static partial class TurnResolver
    {
        // §6 Phase 2: Move and Dash, all simultaneous. Falls (pit, or off an unwalled board) are instant deaths.
        static void Phase2Movement(TickContext c)
        {
            c.Log.Phase = SimPhase.Movement;
            var s = c.S;
            var units = s.Units;
            int n = units.Length;
            var dest = new Int2[n];
            var first = new Int2[n];       // the tile a mover enters (a dash only ever enters its landing tile)
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
                if (a.Type == ActionType.Move)
                {
                    // FIXES_4E §8.1: nobody walks off the map or onto an 'x' tile (pits and water are still walkable).
                    if (s.Board.IsVoid(step1) || IsSolid(s, step1))
                    {
                        c.Log.Add(SimEventKind.MoveBlocked, unit: i, from: u.Pos, to: step1, dir: a.Dir, reason: (byte)BlockReason.Obstacle);
                        continue;
                    }
                    first[i] = dest[i] = step1;
                    moving[i] = true;
                    continue;
                }

                // Dash (FIXES §3.2 rule 1): 2 tiles, flown over whatever is on the middle tile (crate, keg,
                // unit, pit). Only a pillar there (or the wall of a walled board) blocks it. The landing
                // tile must not be solid; otherwise the dash fails and the Grit stays spent (Phase 0).
                Int2 step2 = step1 + d;
                // FIXES_4E §8.1: a dash over or onto the void is blocked too; the Grit stays spent (Phase 0).
                if (s.Board.IsVoid(step1) || s.Board.Get(step1) == TileType.Pillar || s.Board.IsVoid(step2) || IsSolid(s, step2))
                {
                    Int2 hit = s.Board.IsVoid(step1) || s.Board.Get(step1) == TileType.Pillar ? step1 : step2;
                    c.Log.Add(SimEventKind.MoveBlocked, unit: i, from: u.Pos, to: hit, dir: a.Dir, reason: (byte)BlockReason.Obstacle);
                    continue;
                }
                dashing[i] = true;
                first[i] = dest[i] = step2;
                moving[i] = true;
                // Positions are still the start-of-tick ones here, so a unit under the arc counts.
                leaping[i] = s.Board.Get(step1) != TileType.Floor || s.KegAt(step1) != null || s.UnitAt(step1) != null;
            }

            // 2.2 Head-on collisions, computed on the desires above. Two movers bonk if they enter the
            // same tile or swap tiles (each enters the other's start tile). A dash only enters its landing
            // tile, so passing over a unit or crossing another mover's path causes no bonk.
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
                    if (j == i || !moving[j] || !PathsCollide(units, first, i, j))
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

        // §6 Phase 2.2: a mover (walker or dasher) only ever enters first[i]; a dash flies over its middle
        // tile, so walkers stepping into it and units under the arc never collide with it. Two movers
        // collide if they enter the same tile, or swap: each lands on the other's start tile.
        static bool PathsCollide(Unit[] units, Int2[] first, int i, int j) =>
            first[i] == first[j] || (first[i] == units[j].Pos && first[j] == units[i].Pos);
    }
}
