using System.Collections.Generic;

namespace Musketeers.Sim
{
    public enum PointReason : byte { LastStanding, Kill, CrownKill }

    // One line on the between-rounds scoreboard: "+2 Matej (crown kill on Nik)".
    public struct PointGain
    {
        public int Player;
        public int Amount;
        public PointReason Reason;
        public int Victim;   // -1 for LastStanding

        public override string ToString() => $"+{Amount} p{Player} {Reason}{(Victim >= 0 ? $" on p{Victim}" : "")}";
    }

    // GAME_DESIGN §2 scoring and Crown. Pure functions over a MatchState.
    public static class Scoring
    {
        // §2 Crown: the player with strictly the most points; any tie for first (incl. 0–0) means nobody.
        public static int StrictLeader(int[] points)
        {
            int best = -1;
            int bestPts = int.MinValue;
            bool tie = false;
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] > bestPts)
                {
                    best = i;
                    bestPts = points[i];
                    tie = false;
                }
                else if (points[i] == bestPts)
                {
                    tie = true;
                }
            }
            return tie ? SimEvent.None : best;
        }

        // §2: +1 per kill, +2 if the victim wore the Crown. Self-kills score nothing.
        public static void AddKillGains(EventLog log, int crowned, GameConfig cfg, List<PointGain> gains)
        {
            for (int i = 0; i < log.Count; i++)
            {
                var e = log[i];
                if (e.Kind != SimEventKind.Died || e.Other < 0 || e.Other == e.Unit)
                    continue;
                bool crown = e.Unit == crowned;
                gains.Add(new PointGain
                {
                    Player = e.Other,
                    Amount = crown ? cfg.CrownKillPoints : cfg.KillPoints,
                    Reason = crown ? PointReason.CrownKill : PointReason.Kill,
                    Victim = e.Unit,
                });
            }
        }

        // §2 match end, checked at the end of a round. Returns the winner, -1 if the match goes on, or
        // fills `tied` with the players who must play a sudden-death round (and returns -1).
        // Top points ≥ PointsToWin decide; a tie on points goes to most round wins; still tied → sudden death.
        public static int MatchWinner(int[] points, int[] roundWins, GameConfig cfg, List<int> tied)
        {
            tied.Clear();
            int max = int.MinValue;
            for (int i = 0; i < points.Length; i++)
                if (points[i] > max) max = points[i];
            if (max < cfg.PointsToWin)
                return SimEvent.None;

            int bestWins = int.MinValue;
            for (int i = 0; i < points.Length; i++)
                if (points[i] == max && roundWins[i] > bestWins) bestWins = roundWins[i];
            for (int i = 0; i < points.Length; i++)
                if (points[i] == max && roundWins[i] == bestWins) tied.Add(i);
            if (tied.Count == 1)
            {
                int w = tied[0];
                tied.Clear();
                return w;
            }
            return SimEvent.None;
        }
    }
}
