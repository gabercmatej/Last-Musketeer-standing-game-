using System;
using Musketeers.Sim;

namespace Musketeers.Game
{
    // Offline "host": runs the planning timer, plans the bots and resolves the turn when every
    // human is locked in or time runs out (GAME_DESIGN §7). Plans for slots nobody submitted stay Idle.
    public sealed class LocalTransport : IMatchTransport
    {
        public event Action<int, float> PlanningStarted;
        public event Action<int, TurnPlan[]> PlansResolved;
        public event Action<int> LockMaskChanged;

        // Dev: the local player plans every unit (F2). Bots then plan nothing.
        public bool ControlAll { get; set; }
        // Dev: human slots are planned by bots and locked at once, so a match plays itself.
        public bool AutoPlayHumans { get; set; }
        // 0 = use GameConfig.PlanningSeconds.
        public float PlanningSecondsOverride { get; set; }

        public float PlanningSecondsLeft => _planning ? Math.Max(0f, _timeLeft) : 0f;

        MatchSetup _setup;
        Func<MatchState> _match;
        BotPlanner[] _bots;
        TurnPlan[] _plans;
        int _lockMask;
        int _turnSeq;
        bool _planning;
        float _timeLeft;
        bool _stopped;

        public void Start(MatchSetup setup, Func<MatchState> currentMatch)
        {
            _setup = setup;
            _match = currentMatch;
            int n = setup.PlayerCount;
            _bots = new BotPlanner[n];
            for (int i = 0; i < n; i++)
                _bots[i] = new BotPlanner(i, unchecked(setup.Seed * 2654435761u + (uint)i * 40503u + 1u));
            _plans = new TurnPlan[n];
            _stopped = false;
            BeginPlanning(1);
        }

        public void SubmitPlan(int turnSeq, TurnPlan plan, bool locked)
        {
            if (!_planning || turnSeq != _turnSeq || plan.Slot >= _plans.Length)
                return;
            _plans[plan.Slot] = plan;
            int bit = 1 << plan.Slot;
            int mask = locked ? _lockMask | bit : _lockMask & ~bit;
            if (mask != _lockMask)
            {
                _lockMask = mask;
                LockMaskChanged?.Invoke(_lockMask);
            }
        }

        public void TurnDone(int turnSeq, uint stateHash)
        {
            if (_stopped || turnSeq != _turnSeq || _planning)
                return;
            var m = _match();
            if (m.MatchOver)
                return;
            BeginPlanning(turnSeq + 1);
        }

        public void Tick(float deltaSeconds)
        {
            if (!_planning || _stopped)
                return;
            _timeLeft -= deltaSeconds;
            if (_timeLeft <= 0f || AllHumansLocked())
                Resolve();
        }

        public void Stop()
        {
            _stopped = true;
            _planning = false;
        }

        bool IsHuman(int slot) => ControlAll || !_setup.IsBot[slot];

        // Dead humans have nothing to plan, so they count as locked (a spectator never holds up the turn).
        bool AllHumansLocked()
        {
            if (AutoPlayHumans)
                return true;
            var round = _match().Round;
            for (int i = 0; i < _plans.Length; i++)
            {
                if (IsHuman(i) && round.Units[i].Alive && (_lockMask & (1 << i)) == 0)
                    return false;
            }
            return true;
        }

        void BeginPlanning(int turnSeq)
        {
            _turnSeq = turnSeq;
            for (int i = 0; i < _plans.Length; i++)
                _plans[i] = new TurnPlan(i);
            _lockMask = 0;
            _planning = true;
            _timeLeft = PlanningSecondsOverride > 0f ? PlanningSecondsOverride : _setup.Config.PlanningSeconds;
            LockMaskChanged?.Invoke(0);
            PlanningStarted?.Invoke(turnSeq, _timeLeft);
        }

        void Resolve()
        {
            _planning = false;
            var m = _match();
            var plans = new TurnPlan[_plans.Length];
            for (int i = 0; i < plans.Length; i++)
            {
                bool botPlans = !IsHuman(i) || AutoPlayHumans;
                // Bots run every turn even when they're not used, so their RNG doesn't depend on F2.
                var botPlan = _bots[i].Plan(m.Round, m.Cfg, m.RoundNumber);
                plans[i] = botPlans ? botPlan : _plans[i];
                plans[i].Slot = (byte)i;
            }
            PlansResolved?.Invoke(_turnSeq, plans);
        }
    }
}
