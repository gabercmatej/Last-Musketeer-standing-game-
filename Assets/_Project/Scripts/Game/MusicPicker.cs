using Musketeers.Sim;

namespace Musketeers.Game
{
    // Which battle track fits the moment: battle_final once the round is about to be decided.
    public static class MusicPicker
    {
        // Two or fewer musketeers left, or the Cardinal's Guard has eaten all but the innermost ring or two
        // (at most one ring left outside the centre).
        public static bool IsFinal(GameState s, GameConfig cfg)
        {
            int alive = 0;
            foreach (var u in s.Units)
            {
                if (u.Alive)
                    alive++;
            }
            if (alive <= 2)
                return true;
            int maxRing = Crumble.MaxRing(s.Board);
            int fallen = 0;
            for (int r = 0; r <= maxRing; r++)
            {
                if (Crumble.FallTurn(r, cfg) <= s.Turn)
                    fallen++;
            }
            return maxRing + 1 - fallen <= 2;
        }
    }
}
