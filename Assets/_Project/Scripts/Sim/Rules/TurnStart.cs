namespace Musketeers.Sim
{
    // §6 Turn start (before planning): +Grit from turn 2, crumble warn/fall, falls on crumbled tiles.
    // Day 2. Stubbed so callers can already wire it in.
    public static class TurnStart
    {
        public static EventLog Begin(GameState s, GameConfig cfg)
        {
            var log = new EventLog { Phase = SimPhase.TurnStart };
            return log;
        }
    }
}
