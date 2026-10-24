using System.Collections.Generic;

namespace Musketeers.Net
{
    // Host-only replay barrier. A skip persists so a late ready message cannot restart a replay.
    public sealed class ReplayBarrier
    {
        readonly List<ulong> _players = new List<ulong>();
        readonly HashSet<ulong> _ready = new HashSet<ulong>(), _done = new HashSet<ulong>();
        public int Turn { get; private set; } = -1;
        public bool IsFinal { get; private set; }
        public bool Started { get; private set; }
        public bool Finished { get; private set; }
        public void Reset() { Turn = -1; IsFinal = false; Started = Finished = false; _players.Clear(); _ready.Clear(); _done.Clear(); }
        public void Begin(int turn, IEnumerable<ulong> players, bool isFinal = false)
        {
            Reset(); IsFinal = isFinal; Turn = turn; _players.AddRange(players);
        }
        public bool Ready(ulong player) { if (!_players.Contains(player) || Finished) return false; _ready.Add(player); return TryStart(); }
        public bool Done(ulong player) { if (!_players.Contains(player) || !Started || Finished) return false; _done.Add(player); return TryFinish(); }
        public bool Skip(ulong player)
        {
            if (IsFinal || !_players.Contains(player) || Finished) return false;
            Finished = true; return true;
        }
        public void Disconnect(ulong player) { _players.Remove(player); _ready.Remove(player); _done.Remove(player); }
        public bool TryStart()
        {
            if (Started || Finished) return false;
            foreach (ulong id in _players) if (!_ready.Contains(id)) return false;
            Started = true; return true;
        }
        public bool TryFinish()
        {
            if (!Started || Finished) return false;
            foreach (ulong id in _players) if (!_done.Contains(id)) return false;
            Finished = true; return true;
        }
    }
}
