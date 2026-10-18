using System;

namespace Musketeers.Sim
{
    // A player's 3 actions for one turn. Fixed-size struct so it's cheap to send and copy;
    // unplanned ticks stay default, which is Idle (§5).
    public struct TurnPlan
    {
        public const int Length = 3;

        public byte Slot;
        public PlannedAction A0;
        public PlannedAction A1;
        public PlannedAction A2;

        public TurnPlan(int slot)
        {
            Slot = (byte)slot;
            A0 = default;
            A1 = default;
            A2 = default;
        }

        // §9: a disconnected player's musketeer Catches Breath every tick (an empty plan would Idle).
        public static TurnPlan AllCatchBreath(int slot)
        {
            var p = new TurnPlan(slot);
            p.A0 = p.A1 = p.A2 = PlannedAction.CatchBreath();
            return p;
        }

        public PlannedAction this[int tick]
        {
            get
            {
                switch (tick)
                {
                    case 0: return A0;
                    case 1: return A1;
                    case 2: return A2;
                    default: throw new ArgumentOutOfRangeException(nameof(tick), $"Tick {tick} is outside 0..{Length - 1}.");
                }
            }
            set
            {
                switch (tick)
                {
                    case 0: A0 = value; break;
                    case 1: A1 = value; break;
                    case 2: A2 = value; break;
                    default: throw new ArgumentOutOfRangeException(nameof(tick), $"Tick {tick} is outside 0..{Length - 1}.");
                }
            }
        }
    }
}
