using System;

namespace Musketeers.Game
{
    // Presentation clock only. Server time is supplied online, unscaled local time for individual finals.
    public sealed class PostReplayCountdown
    {
        public const float VictoryDelay = 1f;
        public const float Seconds = 5f;
        int _turn = -1;
        double _end;
        public bool Active { get; private set; }
        public bool Begin(int turn, double now, double sharedEnd = 0)
        {
            if (_turn == turn) return false;
            _turn = turn; _end = sharedEnd > 0 ? sharedEnd : now + Seconds; Active = true;
            return true;
        }
        public float Remaining(double now) => Active ? (float)Math.Max(0, Math.Min(Seconds, _end - now)) : 0f;
        public void Finish() => Active = false;
        public void Reset() { _turn = -1; Active = false; }
    }
}
