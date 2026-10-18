namespace Musketeers.Sim
{
    public static partial class TurnResolver
    {
        // §6 Phase 6: HP deaths happen now, at the end of the tick (falls already died instantly), so
        // a unit hit earlier in the tick still got to shoot. Then the round-over check.
        static void Phase6Deaths(TickContext c)
        {
            c.Log.Phase = SimPhase.Deaths;
            var s = c.S;
            var units = s.Units;
            for (int i = 0; i < units.Length; i++)
            {
                if (units[i].Alive && units[i].Hp <= 0)
                    Kill(c, i, DeathCause.Damage);
            }

            // Parry only lasts this tick.
            for (int i = 0; i < units.Length; i++)
                units[i].Parrying = false;

            int alive = 0;
            int last = SimEvent.None;
            for (int i = 0; i < units.Length; i++)
            {
                if (units[i].Alive)
                {
                    alive++;
                    last = i;
                }
            }
            if (alive <= 1)
            {
                s.RoundOver = true;
                s.RoundWinner = alive == 1 ? last : SimEvent.None;   // nobody left: a draw
                c.Log.Add(SimEventKind.RoundEnded, unit: s.RoundWinner);
            }
        }
    }
}
