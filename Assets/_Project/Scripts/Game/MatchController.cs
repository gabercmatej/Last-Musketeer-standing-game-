using System;
using System.Collections.Generic;
using Musketeers.Sim;

namespace Musketeers.Game
{
    public enum MatchPhase { Idle, Planning, Playback, RoundOver, MatchOver }

    // Owns the MatchState and the turn loop (TECH_DESIGN §3). It only talks to IMatchTransport, so the
    // same code runs offline and online. The presentation subscribes to its events and calls back:
    //   PlanningStarted → (player plans, SubmitPlan) → TurnReady → play it → PlaybackFinished
    //   → RoundEnded → (scoreboard) → ContinueAfterRound → RoundStarted | MatchEnded
    public sealed class MatchController
    {
        public event Action<int, float> PlanningStarted;     // turnSeq, seconds
        public event Action<TurnPlayback> TurnReady;
        public event Action<RoundResult> RoundEnded;
        public event Action<EventLog> RoundStarted;           // the new round's opening (TurnStart) log
        public event Action MatchEnded;
        public event Action<int> LockMaskChanged;

        public MatchSetup Setup { get; }
        public MatchState Match { get; private set; }
        public MatchPhase Phase { get; private set; }
        public int TurnSeq { get; private set; }
        public int LockMask { get; private set; }
        public EventLog Opening { get; private set; }
        public TurnPlayback Current { get; private set; }
        public RoundResult LastRoundResult { get; private set; }
        public float PlanningSecondsLeft => _transport.PlanningSecondsLeft;
        public GameConfig Cfg => Setup.Config;

        readonly IMatchTransport _transport;

        public MatchController(MatchSetup setup, IMatchTransport transport)
        {
            Setup = setup ?? throw new ArgumentNullException(nameof(setup));
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        public void Start()
        {
            Match = MatchRunner.NewMatch(MapParser.Parse(Setup.MapAscii), Setup.Abilities, Setup.Config, Setup.Seed,
                out var opening, Setup.Characters);
            Opening = opening;
            _transport.PlanningStarted += OnPlanningStarted;
            _transport.PlansResolved += OnPlansResolved;
            _transport.LockMaskChanged += OnLockMask;
            RoundStarted?.Invoke(opening);
            _transport.Start(Setup, () => Match);
        }

        public void Stop()
        {
            _transport.PlanningStarted -= OnPlanningStarted;
            _transport.PlansResolved -= OnPlansResolved;
            _transport.LockMaskChanged -= OnLockMask;
            _transport.Stop();
            Phase = MatchPhase.Idle;
        }

        public void Update(float deltaSeconds) => _transport.Tick(deltaSeconds);

        public void SubmitPlan(TurnPlan plan, bool locked)
        {
            if (Phase == MatchPhase.Planning)
                _transport.SubmitPlan(TurnSeq, plan, locked);
        }

        public bool IsLocked(int slot) => (LockMask & (1 << slot)) != 0;

        // The presentation finished playing Current.
        public void PlaybackFinished()
        {
            if (Phase != MatchPhase.Playback)
                return;
            var o = Current.Outcome;
            if (o.RoundResult != null)
            {
                Phase = MatchPhase.RoundOver;
                LastRoundResult = o.RoundResult;
                RoundEnded?.Invoke(o.RoundResult);
                return;
            }
            _transport.TurnDone(TurnSeq, Match.ComputeHash());
        }

        // The player closed the between-rounds scoreboard (or it timed out).
        public void ContinueAfterRound()
        {
            if (Phase != MatchPhase.RoundOver)
                return;
            if (Match.MatchOver)
            {
                Phase = MatchPhase.MatchOver;
                MatchEnded?.Invoke();
                return;
            }
            RoundStarted?.Invoke(Current.Outcome.NextRoundStart);
            _transport.TurnDone(TurnSeq, Match.ComputeHash());
        }

        void OnPlanningStarted(int turnSeq, float seconds)
        {
            TurnSeq = turnSeq;
            Phase = MatchPhase.Planning;
            PlanningStarted?.Invoke(turnSeq, seconds);
        }

        void OnLockMask(int mask)
        {
            LockMask = mask;
            LockMaskChanged?.Invoke(mask);
        }

        void OnPlansResolved(int turnSeq, TurnPlan[] plans)
        {
            if (turnSeq != TurnSeq || Phase != MatchPhase.Planning)
                return;
            Phase = MatchPhase.Playback;
            Current = Resolve(Match, plans, turnSeq, Setup.Names);
            TurnReady?.Invoke(Current);
        }

        // Plays the turn on the live match through MatchRunner (the one place scoring happens) and, on a
        // clone, tick by tick so playback can show the true state after every tick.
        public static TurnPlayback Resolve(MatchState m, TurnPlan[] plans, int turnSeq, string[] names)
        {
            var before = m.Round.Clone();
            var cfg = m.Cfg;
            var scratch = before.Clone();
            var logs = new List<EventLog>();
            var after = new List<GameState>();
            for (int t = 0; t < cfg.TicksPerTurn && !scratch.RoundOver; t++)
            {
                var log = TurnResolver.ResolveTick(scratch, plans, t, cfg);
                logs.Add(log);
                after.Add(scratch.Clone());
            }

            int roundNumber = m.RoundNumber;
            var pointsBefore = (int[])m.Points.Clone();
            int crownedBefore = m.Crowned;
            var outcome = MatchRunner.PlayTurn(m, plans);
            return new TurnPlayback
            {
                TurnSeq = turnSeq,
                RoundNumber = roundNumber,
                RoundTurn = before.Turn,
                Names = names,
                Before = before,
                Plans = (TurnPlan[])plans.Clone(),
                TickLogs = logs.ToArray(),
                AfterTick = after.ToArray(),
                Outcome = outcome,
                After = (outcome.EndedRound ?? m.Round).Clone(),
                PointsBefore = pointsBefore,
                CrownedBefore = crownedBefore,
            };
        }
    }
}
