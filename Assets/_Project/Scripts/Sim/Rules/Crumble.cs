namespace Musketeers.Sim
{
    // §3 The Cardinal's Guard closes in. Ring r is warned at the start of turn
    // CrumbleStartTurn + r * CrumbleEvery and crumbles into pit at the start of the next turn.
    // Derived from the turn number alone, so GameState needs no extra fields.
    public static class Crumble
    {
        public static int MaxRing(Board b) => (System.Math.Min(b.Width, b.Height) - 1) / 2;

        public static int WarnTurn(int ring, GameConfig cfg) => cfg.CrumbleStartTurn + ring * cfg.CrumbleEvery;

        public static int FallTurn(int ring, GameConfig cfg) => WarnTurn(ring, cfg) + 1;

        // The ring glowing red during this turn (it falls at the start of the next one), or -1.
        public static int WarnedRing(int turn, Board b, GameConfig cfg) => RingFor(turn, b, cfg, 0);

        // The ring that crumbles at the start of this turn, or -1.
        public static int FallingRing(int turn, Board b, GameConfig cfg) => RingFor(turn, b, cfg, 1);

        public static bool IsWarned(GameState s, Int2 p, GameConfig cfg)
        {
            int ring = WarnedRing(s.Turn, s.Board, cfg);
            return ring >= 0 && !s.Board.IsVoid(p) && s.Board.RingIndex(p) == ring;
        }

        static int RingFor(int turn, Board b, GameConfig cfg, int offset)
        {
            int t = turn - offset - cfg.CrumbleStartTurn;
            if (t < 0 || cfg.CrumbleEvery <= 0 || t % cfg.CrumbleEvery != 0)
                return -1;
            int ring = t / cfg.CrumbleEvery;
            return ring <= MaxRing(b) ? ring : -1;
        }
    }
}
