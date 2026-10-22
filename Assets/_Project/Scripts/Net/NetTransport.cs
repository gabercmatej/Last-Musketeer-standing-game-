using System;
using Musketeers.Game;
using Musketeers.Sim;

namespace Musketeers.Net
{
    // Online IMatchTransport (TECH_DESIGN §2/§5). The host is the authority for the planning timer, plans the bots
    // exactly like LocalTransport, and sends every machine the same plans to resolve; clients only submit their own plan
    // and report the state hash after each turn. All wire access goes through INetLink, so this class is plain C#.
    //   host:   Start -> wait for every human's Ready -> BeginPlanning -> (plans in, locks out) -> Resolve(all plans)
    //           -> wait for TurnDone from every connected human (or a timeout) -> compare hashes -> next turn
    //   client: Ready -> BeginPlanning (countdown) -> SubmitPlan on every change -> ResolvePlans -> TurnDone(hash)
    public sealed class NetTransport : IMatchTransport
    {
        public const float GraceSeconds = 0.5f;            // host waits this long after the clients' countdown hits zero
        public const float ReadyTimeoutSeconds = 30f;      // loading screens of slow machines
        public const float TurnDoneTimeoutSeconds = 10f;   // after the first report, the host stops waiting
        public const float RoundOverTimeoutSeconds = 30f;  // a round ending shows the scoreboard (up to 10 s) on every machine

        public event Action<int, float> PlanningStarted;
        public event Action<int, TurnPlan[]> PlansResolved;
        public event Action<int> LockMaskChanged;
        /// <summary>Client: the host sent a full state after a hash mismatch. MatchController.ApplySnapshot takes it.</summary>
        public event Action<MatchState> SnapshotReceived;
        /// <summary>Warnings for the log, e.g. "DESYNC turn 7 slot 2".</summary>
        public event Action<string> Warn;

        /// <summary>Host only: 0 = use GameConfig.PlanningSeconds (dev / autotest).</summary>
        public float PlanningSecondsOverride { get; set; }

        public bool IsHost { get; }
        public int LocalSlot { get; private set; }
        public float PlanningSecondsLeft => _planning ? Math.Max(0f, _timeLeft) : 0f;
        public int TurnSeq => _turnSeq;

        readonly INetLink _link;
        readonly ulong[] _slotClients;        // client id per slot (bots: ignored)
        MatchSetup _setup;
        Func<MatchState> _match;
        bool _stopped;
        int _turnSeq;
        bool _planning;
        float _timeLeft;

        // host
        BotPlanner[] _bots;
        TurnPlan[] _plans;
        bool[] _connected;
        bool[] _ready;
        bool[] _done;
        bool[] _resynced;
        uint[] _hashes;
        int _lockMask;
        bool _started;
        bool _awaitingDone;
        bool _anyDone;
        float _elapsed;
        float _duration;
        float _readyWait;
        float _doneWait;
        float _doneTimeout;

        public NetTransport(INetLink link, bool isHost, ulong[] slotClients)
        {
            _link = link ?? throw new ArgumentNullException(nameof(link));
            IsHost = isHost;
            _slotClients = slotClients ?? throw new ArgumentNullException(nameof(slotClients));
        }

        public void Start(MatchSetup setup, Func<MatchState> currentMatch)
        {
            _setup = setup;
            _match = currentMatch;
            LocalSlot = setup.LocalSlot;
            _stopped = false;
            _turnSeq = 0;
            _planning = false;
            if (!IsHost)
            {
                _link.SendReady();
                return;
            }
            int n = setup.PlayerCount;
            // The same seeding as LocalTransport, so a bot plays the same online and offline.
            _bots = new BotPlanner[n];
            for (int i = 0; i < n; i++)
                _bots[i] = new BotPlanner(i, unchecked(setup.Seed * 2654435761u + (uint)i * 40503u + 1u));
            _plans = new TurnPlan[n];
            _connected = new bool[n];
            _ready = new bool[n];
            _done = new bool[n];
            _resynced = new bool[n];
            _hashes = new uint[n];
            for (int i = 0; i < n; i++)
                _connected[i] = !setup.IsBot[i];
            _ready[LocalSlot] = true;
            _started = false;
            _readyWait = 0f;
            if (AllReady())
                BeginPlanning(1);
        }

        public void Stop()
        {
            _stopped = true;
            _planning = false;
            _awaitingDone = false;
        }

        public void Tick(float dt)
        {
            if (_stopped)
                return;
            if (!IsHost)
            {
                if (_planning)
                    _timeLeft -= dt;
                return;
            }
            if (_plans == null)
                return;
            if (!_started)
            {
                _readyWait += dt;
                if (AllReady() || _readyWait >= ReadyTimeoutSeconds)
                    BeginPlanning(1);
                return;
            }
            if (_planning)
            {
                _timeLeft -= dt;
                _elapsed += dt;
                if (_elapsed >= _duration + GraceSeconds || AllHumansLocked())
                    Resolve();
            }
            else if (_awaitingDone)
            {
                if (_anyDone)
                    _doneWait += dt;
                if (AllConnectedDone() || (_anyDone && _doneWait >= _doneTimeout))
                    Advance();
            }
        }

        // ---------- IMatchTransport (local machine) ----------

        public void SubmitPlan(int turnSeq, TurnPlan plan, bool locked)
        {
            if (_stopped || !_planning || turnSeq != _turnSeq)
                return;
            if (IsHost)
                ApplyPlan(LocalSlot, plan, locked);
            else
                _link.SendPlan(turnSeq, plan, locked);
        }

        public void TurnDone(int turnSeq, uint stateHash)
        {
            if (_stopped || turnSeq != _turnSeq || _planning)
                return;
            if (IsHost)
                RecordDone(LocalSlot, turnSeq, stateHash);
            else
                _link.SendTurnDone(turnSeq, stateHash);
        }

        // ---------- inbound: host ----------

        public int SlotOf(ulong clientId)
        {
            for (int i = 0; i < _slotClients.Length; i++)
            {
                if (_setup != null && _setup.IsBot[i])
                    continue;
                if (_slotClients[i] == clientId)
                    return i;
            }
            return -1;
        }

        public void HostOnReady(ulong from)
        {
            if (_stopped || !IsHost || _ready == null)
                return;
            int slot = SlotOf(from);
            if (slot < 0)
                return;
            _ready[slot] = true;
            if (!_started && AllReady())
                BeginPlanning(1);
        }

        public void HostOnPlan(ulong from, int turnSeq, TurnPlan plan, bool locked)
        {
            if (_stopped || !IsHost || !_planning || turnSeq != _turnSeq)
                return;
            int slot = SlotOf(from);
            if (slot < 0 || !_connected[slot])
                return;
            plan.Slot = (byte)slot;   // a client can only plan its own musketeer
            ApplyPlan(slot, plan, locked);
        }

        public void HostOnTurnDone(ulong from, int turnSeq, uint hash)
        {
            if (_stopped || !IsHost || _done == null)
                return;
            int slot = SlotOf(from);
            if (slot >= 0)
                RecordDone(slot, turnSeq, hash);
        }

        /// <summary>Host: a human's connection dropped. They plan Catch Breath from now on and never hold up the turn (§9).</summary>
        public void ClientDisconnected(ulong clientId)
        {
            if (_stopped || !IsHost || _connected == null)
                return;
            int slot = SlotOf(clientId);
            if (slot < 0 || !_connected[slot])
                return;
            _connected[slot] = false;
            Warn?.Invoke($"Slot {slot} disconnected: planning Catch Breath for them");
            if (_planning)
            {
                _plans[slot] = TurnPlan.AllCatchBreath(slot);
                SetLock(slot, true);
            }
            if (!_started && AllReady())
                BeginPlanning(1);
            else if (_awaitingDone && AllConnectedDone())
                Advance();
        }

        // ---------- inbound: client ----------

        public void ClientOnBegin(int turnSeq, int durationMs)
        {
            if (_stopped || IsHost || turnSeq <= _turnSeq)
                return;
            _turnSeq = turnSeq;
            _planning = true;
            _timeLeft = durationMs / 1000f;
            _lockMask = 0;
            LockMaskChanged?.Invoke(0);
            PlanningStarted?.Invoke(turnSeq, _timeLeft);
        }

        public void ClientOnLock(int turnSeq, int mask)
        {
            if (_stopped || IsHost || turnSeq != _turnSeq || mask == _lockMask)
                return;
            _lockMask = mask;
            LockMaskChanged?.Invoke(mask);
        }

        public void ClientOnResolve(int turnSeq, TurnPlan[] plans)
        {
            if (_stopped || IsHost || turnSeq != _turnSeq || !_planning)
                return;
            _planning = false;
            PlansResolved?.Invoke(turnSeq, plans);
        }

        public void ClientOnSnapshot(int turnSeq, byte[] data)
        {
            if (_stopped || IsHost || turnSeq != _turnSeq || _planning)
                return;
            MatchState state;
            try { state = MatchStateSerializer.Deserialize(data, _match()); }
            catch (FormatException e)
            {
                Warn?.Invoke("Bad snapshot: " + e.Message);
                return;
            }
            SnapshotReceived?.Invoke(state);
        }

        // ---------- host internals ----------

        bool IsHumanSlot(int slot) => !_setup.IsBot[slot];

        bool AllReady()
        {
            for (int i = 0; i < _ready.Length; i++)
            {
                if (IsHumanSlot(i) && _connected[i] && !_ready[i])
                    return false;
            }
            return true;
        }

        // Dead humans have nothing to plan, so they count as locked; so do disconnected ones (they plan Catch Breath).
        bool AllHumansLocked()
        {
            var round = _match().Round;
            for (int i = 0; i < _plans.Length; i++)
            {
                if (IsHumanSlot(i) && _connected[i] && round.Units[i].Alive && (_lockMask & (1 << i)) == 0)
                    return false;
            }
            return true;
        }

        bool AllConnectedDone()
        {
            for (int i = 0; i < _done.Length; i++)
            {
                if (IsHumanSlot(i) && _connected[i] && !_done[i])
                    return false;
            }
            return true;
        }

        void ApplyPlan(int slot, TurnPlan plan, bool locked)
        {
            plan.Slot = (byte)slot;
            _plans[slot] = plan;
            SetLock(slot, locked);
        }

        void SetLock(int slot, bool locked)
        {
            int bit = 1 << slot;
            int mask = locked ? _lockMask | bit : _lockMask & ~bit;
            if (mask == _lockMask)
                return;
            _lockMask = mask;
            _link.BroadcastLock(_turnSeq, mask);
            LockMaskChanged?.Invoke(mask);
        }

        void BeginPlanning(int turnSeq)
        {
            _started = true;
            _turnSeq = turnSeq;
            int mask = 0;
            for (int i = 0; i < _plans.Length; i++)
            {
                _plans[i] = new TurnPlan(i);
                if (IsHumanSlot(i) && !_connected[i])
                {
                    _plans[i] = TurnPlan.AllCatchBreath(i);
                    mask |= 1 << i;
                }
                _done[i] = false;
                _resynced[i] = false;
            }
            _lockMask = mask;
            _planning = true;
            _awaitingDone = false;
            _anyDone = false;
            _doneWait = 0f;
            _elapsed = 0f;
            _duration = PlanningSecondsOverride > 0f ? PlanningSecondsOverride : _setup.Config.PlanningSeconds;
            _timeLeft = _duration;
            _link.BroadcastBegin(turnSeq, (int)Math.Round(_duration * 1000f));
            if (mask != 0)
                _link.BroadcastLock(turnSeq, mask);
            LockMaskChanged?.Invoke(mask);
            PlanningStarted?.Invoke(turnSeq, _timeLeft);
        }

        void Resolve()
        {
            _planning = false;
            var m = _match();
            int roundBefore = m.RoundNumber;
            var plans = new TurnPlan[_plans.Length];
            for (int i = 0; i < plans.Length; i++)
            {
                // Bots run every turn (like LocalTransport), so their RNG never depends on who is connected.
                var botPlan = _bots[i].Plan(m.Round, m.Cfg, m.RoundNumber);
                plans[i] = !IsHumanSlot(i) ? botPlan : _connected[i] ? _plans[i] : TurnPlan.AllCatchBreath(i);
                plans[i].Slot = (byte)i;
            }
            _link.BroadcastResolve(_turnSeq, plans);
            PlansResolved?.Invoke(_turnSeq, plans);
            // The resolve ran synchronously: the match may have moved on to a new round or ended.
            var after = _match();
            _awaitingDone = !after.MatchOver;
            _doneTimeout = after.RoundNumber != roundBefore ? RoundOverTimeoutSeconds : TurnDoneTimeoutSeconds;
        }

        void RecordDone(int slot, int turnSeq, uint hash)
        {
            if (turnSeq != _turnSeq || _planning || slot < 0 || slot >= _done.Length || _done[slot])
                return;
            _done[slot] = true;
            _hashes[slot] = hash;
            _anyDone = true;
            CheckHashes();
            if (_awaitingDone && AllConnectedDone())
                Advance();
        }

        // Compares every report with the host's own (once the host has reported). A mismatch gets a full snapshot.
        void CheckHashes()
        {
            if (!_done[LocalSlot])
                return;
            for (int i = 0; i < _done.Length; i++)
            {
                if (i == LocalSlot || !_done[i] || _resynced[i] || _hashes[i] == _hashes[LocalSlot])
                    continue;
                _resynced[i] = true;
                Warn?.Invoke($"DESYNC turn {_turnSeq} slot {i}");
                if (IsHumanSlot(i) && _connected[i])
                    _link.SendSnapshot(_slotClients[i], _turnSeq, MatchStateSerializer.Serialize(_match()));
            }
        }

        void Advance()
        {
            if (!_awaitingDone)
                return;
            CheckHashes();
            _awaitingDone = false;
            if (_match().MatchOver)
                return;
            BeginPlanning(_turnSeq + 1);
        }
    }
}
