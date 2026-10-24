using System;
using System.Collections.Generic;
using Musketeers.Sim;

namespace Musketeers.Game
{
    [Serializable]
    public sealed class HistoryPlayer
    {
        public string CharacterId, Name;
        public int Place, Points, RoundWins, Kills, Deaths;
        public List<HistoryBadge> Badges = new List<HistoryBadge>();
    }

    [Serializable]
    public sealed class HistoryBadge
    {
        public string Id, Name, Description;
    }

    [Serializable]
    public sealed class MatchHistoryRecord
    {
        public string MatchId, CompletedUtc, MapId;
        public List<HistoryPlayer> Players = new List<HistoryPlayer>();

        // Called only by the real match-completed flow. The initial roster is immutable for eligibility.
        public static MatchHistoryRecord Create(string matchId, bool online, bool completed, bool[] initialBots,
            MatchState match, string[] characterIds, string[] names, string mapId, string completedUtc)
        {
            if (!online || !completed || match == null || !match.MatchOver || string.IsNullOrEmpty(matchId) ||
                initialBots == null || initialBots.Length < 2 || initialBots.Length != match.PlayerCount ||
                characterIds == null || characterIds.Length != match.PlayerCount || names == null || names.Length != match.PlayerCount)
                return null;
            foreach (bool bot in initialBots) if (bot) return null;
            var places = match.Places ?? Scoring.Rank(match.Points, match.RoundPoints);
            var record = new MatchHistoryRecord { MatchId = matchId, CompletedUtc = completedUtc, MapId = mapId };
            var awards = Awards.Compute(match.Stats);
            for (int i = 0; i < match.PlayerCount; i++)
            {
                if (string.IsNullOrEmpty(characterIds[i])) return null;
                var player = new HistoryPlayer { CharacterId = characterIds[i], Name = names[i], Place = places[i],
                    Points = match.Points[i], RoundWins = match.RoundWins[i], Kills = match.Stats.Kills[i], Deaths = match.Stats.Deaths[i] };
                foreach (var award in awards)
                    if (award.Player == i) player.Badges.Add(new HistoryBadge { Id = award.Id, Name = award.Name, Description = award.Description });
                record.Players.Add(player);
            }
            record.Players.Sort((a, b) => a.Place != b.Place ? a.Place.CompareTo(b.Place) : string.CompareOrdinal(a.CharacterId, b.CharacterId));
            return record;
        }
    }

    [Serializable]
    public sealed class CharacterWins
    {
        public string CharacterId;
        public int Wins;
        public string LastWin;
    }

    [Serializable]
    public sealed class MatchHistoryData
    {
        public List<MatchHistoryRecord> Matches = new List<MatchHistoryRecord>();
        public bool Add(MatchHistoryRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.MatchId) || Matches.Exists(m => m.MatchId == record.MatchId)) return false;
            Matches.Insert(0, record);
            return true;
        }

        public List<CharacterWins> Leaderboard(string[] rosterIds)
        {
            var result = new List<CharacterWins>();
            foreach (var id in rosterIds)
                {
                int wins = 0;
                string lastWin = "";
                foreach (var match in Matches)
                    if (match.Players.Exists(p => p.CharacterId == id && p.Place == 1))
                    {
                        wins++;
                        if (string.CompareOrdinal(match.CompletedUtc, lastWin) > 0) lastWin = match.CompletedUtc;
                    }
                result.Add(new CharacterWins { CharacterId = id, Wins = wins, LastWin = lastWin });
            }
            result.Sort((a, b) => a.Wins != b.Wins ? b.Wins.CompareTo(a.Wins) :
                a.LastWin != b.LastWin ? string.CompareOrdinal(b.LastWin, a.LastWin) : string.CompareOrdinal(a.CharacterId, b.CharacterId));
            return result;
        }
    }
}
