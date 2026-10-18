using System;
using System.Collections.Generic;

namespace Musketeers.Sim
{
    // What one PlayTurn produced, in playback order: the turn's ticks, then the next turn's start
    // (Grit, crumble), then — if the round ended — the result and the next round's opening.
    public sealed class TurnOutcome
    {
        public EventLog Turn;
        public EventLog TurnStart;          // next turn of the same round; null if the round ended in Turn
        public RoundResult RoundResult;     // non-null when the round ended (in Turn or in TurnStart)
        public GameState EndedRound;        // the finished round's final state (MatchState.Round has moved on)
        public EventLog NextRoundStart;     // turn 1 of the next round; null if none started
        public bool MatchOver;
    }

    public sealed class RoundResult
    {
        public int RoundNumber;
        public int Winner;                  // slot, or -1 for a draw
        public int Crowned;
        public bool SuddenDeath;
        public List<PointGain> Gains;       // everything scored this round, LastStanding last
    }

    // Drives Match → Round → Turn with TurnStart + TurnResolver. Headless: tests, bots and the game's
    // MatchController all use this, so scoring lives in one place.
    //   var m = MatchRunner.NewMatch(map, abilities, cfg, seed, out var opening);
    //   while (!m.MatchOver) { var plans = ...; var outcome = MatchRunner.PlayTurn(m, plans); }
    public static class MatchRunner
    {
        public static MatchState NewMatch(MapData map, Ability[] abilities, GameConfig cfg, uint seed, out EventLog opening, int[] characters = null)
        {
            var m = MatchState.Create(map, abilities, characters, cfg, seed);
            opening = StartRound(m);
            return m;
        }

        public static MatchState NewMatch(MapData map, Ability[] abilities, GameConfig cfg, uint seed) =>
            NewMatch(map, abilities, cfg, seed, out _);

        // Resolves the current turn, runs the next turn's start, and handles round and match end.
        public static TurnOutcome PlayTurn(MatchState m, TurnPlan[] plans)
        {
            if (m.MatchOver)
                throw new InvalidOperationException("The match is over.");
            var o = new TurnOutcome();
            var round = m.Round;

            o.Turn = TurnResolver.ResolveTurn(round, plans, m.Cfg);
            Observe(m, o.Turn);
            if (!round.RoundOver)
            {
                o.TurnStart = TurnStart.Begin(round, m.Cfg);
                Observe(m, o.TurnStart);
            }

            if (round.RoundOver)
            {
                o.EndedRound = round;
                o.RoundResult = EndRound(m);
                if (!m.MatchOver)
                    o.NextRoundStart = StartRound(m);
            }
            o.MatchOver = m.MatchOver;
            return o;
        }

        // A fresh board, everyone on their spawn with full HP, StartGrit and a loaded musket, no kegs.
        // §2 Crown is decided now, from the points so far.
        static EventLog StartRound(MatchState m)
        {
            var cfg = m.Cfg;
            int n = m.PlayerCount;
            m.RoundNumber++;
            m.Crowned = Scoring.StrictLeader(m.Points);
            m.RoundGains.Clear();

            var units = new Unit[n];
            for (int i = 0; i < n; i++)
            {
                // §3: spawns in slot order.
                units[i] = Unit.Spawn(i, m.Characters[i], m.Abilities[i], m.Map.Spawns[i], cfg);
                if (!m.Participants[i])
                    units[i].Alive = false;
            }
            m.Round = new GameState(m.Map.Board.Clone(), units, new DeterministicRng(m.Rng.NextUInt()));

            var log = TurnStart.Begin(m.Round, cfg);
            Observe(m, log);
            return log;
        }

        static void Observe(MatchState m, EventLog log)
        {
            m.Stats.Record(log, m.Crowned);
            Scoring.AddKillGains(log, m.Crowned, m.Cfg, m.RoundGains);
        }

        // §2: +3 to the last musketeer standing (not on a draw), apply the round's gains, then check
        // for a match winner or a sudden-death tiebreak.
        static RoundResult EndRound(MatchState m)
        {
            var cfg = m.Cfg;
            int winner = m.Round.RoundWinner;
            if (winner >= 0)
            {
                m.RoundGains.Add(new PointGain { Player = winner, Amount = cfg.WinPoints, Reason = PointReason.LastStanding, Victim = SimEvent.None });
                m.RoundWins[winner]++;
            }
            foreach (var g in m.RoundGains)
                m.Points[g.Player] += g.Amount;

            var result = new RoundResult
            {
                RoundNumber = m.RoundNumber,
                Winner = winner,
                Crowned = m.Crowned,
                SuddenDeath = m.SuddenDeath,
                Gains = new List<PointGain>(m.RoundGains),
            };

            if (m.SuddenDeath)
            {
                // The tiebreak round decides the match outright; a drawn one is replayed by the same players.
                if (winner >= 0)
                    FinishMatch(m, winner);
                return result;
            }

            var tied = new List<int>();
            int w = Scoring.MatchWinner(m.Points, m.RoundWins, cfg, tied);
            if (w >= 0)
            {
                FinishMatch(m, w);
            }
            else if (tied.Count > 1)
            {
                m.SuddenDeath = true;
                for (int i = 0; i < m.PlayerCount; i++)
                    m.Participants[i] = tied.Contains(i);
            }
            return result;
        }

        static void FinishMatch(MatchState m, int winner)
        {
            m.MatchOver = true;
            m.MatchWinner = winner;
        }
    }
}
