using System;

namespace Musketeers.Sim
{
    public enum PushOutcome : byte { None, Cancelled, Squeezed, Pushed }

    public readonly struct PushResult
    {
        public readonly PushOutcome Outcome;
        public readonly Dir Dir;
        public readonly int Strength;

        public PushResult(PushOutcome outcome, Dir dir = Dir.N, int strength = 0)
        {
            Outcome = outcome;
            Dir = dir;
            Strength = strength;
        }
    }

    // §6 Phase 3.3: combine the pushes one target receives. Inputs are the summed strengths per
    // direction. Treated as a vector sum: opposite pushes subtract (equal ones cancel out), same
    // direction adds up to the cap, and anything left on both axes is a Squeeze.
    public static class PushMath
    {
        public static PushResult Combine(int north, int east, int south, int west, int maxStrength)
        {
            int vx = east - west;
            int vy = north - south;
            if (vx == 0 && vy == 0)
                return new PushResult(north + east + south + west > 0 ? PushOutcome.Cancelled : PushOutcome.None);
            if (vx != 0 && vy != 0)
                return new PushResult(PushOutcome.Squeezed);
            Dir dir = vx > 0 ? Dir.E : vx < 0 ? Dir.W : vy > 0 ? Dir.N : Dir.S;
            int strength = Math.Min(Math.Abs(vx) + Math.Abs(vy), maxStrength);
            return new PushResult(PushOutcome.Pushed, dir, strength);
        }
    }
}
