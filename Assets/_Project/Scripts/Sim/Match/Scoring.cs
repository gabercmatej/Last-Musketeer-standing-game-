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

        // §2 ranking: total points, then the last round's points, then the round before, and so on back.
        // Equal in every round = a shared place (competition ranking: 1, 1, 3). Places are 1-based per slot.
        public static int[] Rank(int[] points, List<int[]> roundPoints)
        {
            int n = points.Length;
            var places = new int[n];
            for (int i = 0; i < n; i++)
            {
                int ahead = 0;
                for (int j = 0; j < n; j++)
                    if (j != i && Compare(j, i, points, roundPoints) > 0)
                        ahead++;
                places[i] = ahead + 1;
            }
            return places;
        }

        // > 0 when a ranks above b.
        static int Compare(int a, int b, int[] points, List<int[]> roundPoints)
        {
            if (points[a] != points[b])
                return points[a] > points[b] ? 1 : -1;
            for (int r = roundPoints.Count - 1; r >= 0; r--)
            {
                int[] rp = roundPoints[r];
                if (rp[a] != rp[b])
                    return rp[a] > rp[b] ? 1 : -1;
            }
            return 0;
        }

        // §2 match end, checked at the end of a round: the match is over once the top total reaches PointsToWin.
        public static bool MatchIsOver(int[] points, GameConfig cfg)
        {
            for (int i = 0; i < points.Length; i++)
                if (points[i] >= cfg.PointsToWin)
                    return true;
            return false;
        }
    }
}
