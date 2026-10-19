using Musketeers.Sim;

namespace Musketeers.Game
{
    // One resolved turn, packaged for playback. Every GameState here is a private clone, so the
    // presentation can read them freely without ever touching the live match.
    public sealed class TurnPlayback
    {
        public int TurnSeq;
        public int RoundNumber;
        public int RoundTurn;               // the round's turn number that was resolved (1-based)
        public string[] Names;
        public GameState Before;            // board at the start of the turn (after its TurnStart)
        public TurnPlan[] Plans;
        public EventLog[] TickLogs;         // one per tick actually resolved (fewer if the round ended)
        public GameState[] AfterTick;       // state after each of those ticks, before the next TurnStart
        public TurnOutcome Outcome;         // what MatchRunner produced (TurnStart, RoundResult, …)
        public GameState After;             // after the next TurnStart, or the finished round's final state
        public int[] PointsBefore;          // match points when the turn started (the HUD shows these during playback)
        public int CrownedBefore;           // and who wore the Crown

        public int TickCount => TickLogs.Length;
    }
}
