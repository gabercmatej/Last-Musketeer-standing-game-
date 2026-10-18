using System;

namespace Musketeers.Sim
{
    // xorshift32. The only randomness the sim may use (TECH_DESIGN §2), seeded by the host.
    public sealed class DeterministicRng
    {
        // xorshift32 is stuck at 0 forever, so a 0 seed is replaced with a fixed non-zero one.
        const uint ZeroSeedReplacement = 0x9E3779B9u;

        public uint State { get; private set; }

        public DeterministicRng(uint seed)
        {
            State = seed == 0 ? ZeroSeedReplacement : seed;
        }

        public uint NextUInt()
        {
            uint x = State;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            State = x;
            return x;
        }

        // [minInclusive, maxExclusive). Modulo bias is irrelevant for gameplay-sized ranges.
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), $"Empty range [{minInclusive}, {maxExclusive}).");
            uint span = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt() % span);
        }

        public DeterministicRng Clone() => new DeterministicRng(State);
    }
}
