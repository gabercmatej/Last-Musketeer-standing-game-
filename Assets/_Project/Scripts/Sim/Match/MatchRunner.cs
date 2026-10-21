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
            }
            var board = m.Map.Board.Clone();
            // FIXES_4E §5.3: fresh random pits and crates every round, from the match seed and round number.
            RoundSetup.Apply(m.Map, board, cfg, m.Seed, m.RoundNumber);
            m.Round = new GameState(board, units, new DeterministicRng(m.Rng.NextUInt())) { Crowned = m.Crowned };

            var log = TurnStart.Begin(m.Round, cfg);
            Observe(m, log);
            return log;
        }

        static void Observe(MatchState m, EventLog log)
        {
            m.Stats.Record(log, m.Crowned);
            // FIXES_4E §8.6: kill points land the moment they happen (the resolver emits PointsScored right
            // after Died). Each log is observed exactly once, so nothing is counted twice.
            for (int i = 0; i < log.Count; i++)
            {
                var e = log[i];
                if (e.Kind != SimEventKind.PointsScored)
                    continue;
                m.Points[e.Unit] += e.Amount;
                m.RoundGains.Add(new PointGain { Player = e.Unit, Amount = e.Amount, Reason = (PointReason)e.Reason, Victim = e.Other });
            }
        }

        // §2: +3 to the last musketeer standing (not on a draw; kills were scored live), then check
        // for the match end.
        static RoundResult EndRound(MatchState m)
        {
            var cfg = m.Cfg;
            int winner = m.Round.RoundWinner;
            if (winner >= 0)
            {
                m.RoundGains.Add(new PointGain { Player = winner, Amount = cfg.WinPoints, Reason = PointReason.LastStanding, Victim = SimEvent.None });
                m.RoundWins[winner]++;
            }
            // Kill points were added to m.Points live; only the survival bonus is still owed. RoundGains holds
            // everything for the scoreboard and the per-round tiebreak.
            if (winner >= 0)
                m.Points[winner] += cfg.WinPoints;
            var scored = new int[m.PlayerCount];
            foreach (var g in m.RoundGains)
                scored[g.Player] += g.Amount;
            m.RoundPoints.Add(scored);

            var result = new RoundResult
            {
                RoundNumber = m.RoundNumber,
                Winner = winner,
                Crowned = m.Crowned,
                Gains = new List<PointGain>(m.RoundGains),
            };

            if (Scoring.MatchIsOver(m.Points, cfg))
                FinishMatch(m);
            return result;
        }

        // §2: the winner is 1st place; on a full tie the co-winners share it and the lowest slot is MatchWinner.
        static void FinishMatch(MatchState m)
        {
            m.MatchOver = true;
            m.Places = Scoring.Rank(m.Points, m.RoundPoints);
            m.MatchWinner = -1;
            for (int i = 0; i < m.Places.Length && m.MatchWinner < 0; i++)
                if (m.Places[i] == 1)
                    m.MatchWinner = i;
        }
    }
}
