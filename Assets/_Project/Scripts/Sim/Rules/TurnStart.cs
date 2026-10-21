using System;

namespace Musketeers.Sim
{
    // §6 Turn start (before planning), run once per turn for the current state.Turn:
    // 1. from turn 2, +GritPerTurn for everyone (capped);
    // 2. the warned ring crumbles into pit and the next ring is warned;
    // 3. units (and kegs) on crumbled tiles fall. Kill credit uses the usual window, so a unit shoved
    //    onto the edge last turn and then swallowed by the crumble counts as a kill.
    // If that leaves 1 or fewer alive, the round ends here (it can't wait for a tick).
    public static class TurnStart
    {
        public static EventLog Begin(GameState s, GameConfig cfg)
        {
            var log = new EventLog { Phase = SimPhase.TurnStart };
            if (s.RoundOver)
                return log;
            var units = s.Units;

            if (s.Turn >= 2)
            {
                for (int i = 0; i < units.Length; i++)
                {
                    if (units[i].Alive)
                        units[i].Grit = Math.Min(cfg.MaxGrit, units[i].Grit + cfg.GritPerTurn);
                }
            }

            var b = s.Board;
            int falling = Crumble.FallingRing(s.Turn, b, cfg);
            if (falling >= 0)
            {
                ForRing(b, falling, p =>
                {
                    b.Set(p, TileType.Pit);
                    log.Add(SimEventKind.TileCrumbled, to: p, amount: falling);
                });
            }
            int warned = Crumble.WarnedRing(s.Turn, b, cfg);
            if (warned >= 0)
                ForRing(b, warned, p => log.Add(SimEventKind.TileWarned, to: p, amount: warned));

            if (falling >= 0)
            {
                for (int i = 0; i < units.Length; i++)
                {
                    var u = units[i];
                    if (!u.Alive || b.Get(u.Pos) != TileType.Pit)
                        continue;
                    u.Alive = false;
                    u.Parrying = false;
                    log.Add(SimEventKind.Fell, unit: i, to: u.Pos);
                    int killer = TurnResolver.Killer(s, u, cfg);
                    log.Add(SimEventKind.Died, unit: i, other: killer, to: u.Pos, reason: (byte)DeathCause.Fell);
                    TurnResolver.ScoreKill(s, log, cfg, i, killer);
                }
                foreach (var keg in s.Kegs.ToArray())
                {
                    if (b.Get(keg.Pos) != TileType.Pit)
                        continue;
                    log.Add(SimEventKind.Fell, keg: keg.Id, to: keg.Pos);
                    s.Kegs.Remove(keg);
                }
                TurnResolver.CheckRoundOver(s, log);
            }
            return log;
        }

        static void ForRing(Board b, int ring, Action<Int2> f)
        {
            for (int y = 0; y < b.Height; y++)
            {
                for (int x = 0; x < b.Width; x++)
                {
                    var p = new Int2(x, y);
                    // §3/§4.4: removed `x` corners are outside the board, so they never warn or crumble.
                    if (b.RingIndex(p) == ring && !b.IsRemoved(p))
                        f(p);
                }
            }
        }
    }
}
