using System.Collections.Generic;

namespace Musketeers.Sim
{
    // Append-only. The resolver sets Tick and Phase as it goes and every Add is stamped with them,
    // so playback can group events by tick, then phase.
    public sealed class EventLog
    {
        readonly List<SimEvent> _events = new List<SimEvent>();

        public int Tick { get; set; }
        public SimPhase Phase { get; set; }

        public int Count => _events.Count;
        public SimEvent this[int index] => _events[index];
        public IReadOnlyList<SimEvent> Events => _events;

        public void Add(SimEvent e)
        {
            e.Tick = (byte)Tick;
            e.Phase = Phase;
            _events.Add(e);
        }

        public void Add(SimEventKind kind, int unit = SimEvent.None, int other = SimEvent.None,
            Int2 from = default, Int2 to = default, Dir dir = Dir.N, int amount = 0, byte reason = 0,
            int keg = SimEvent.None)
        {
            Add(new SimEvent
            {
                Kind = kind,
                Unit = (sbyte)unit,
                Other = (sbyte)other,
                Keg = (short)keg,
                From = from,
                To = to,
                Dir = dir,
                Amount = (sbyte)amount,
                Reason = reason,
            });
        }

        public int CountOf(SimEventKind kind)
        {
            int n = 0;
            for (int i = 0; i < _events.Count; i++)
            {
                if (_events[i].Kind == kind)
                    n++;
            }
            return n;
        }

        public bool Contains(SimEventKind kind) => CountOf(kind) > 0;
    }
}
