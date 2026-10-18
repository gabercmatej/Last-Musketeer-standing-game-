namespace Musketeers.Sim
{
    // 32-bit FNV-1a over little-endian ints. Used for the per-turn desync check (TECH_DESIGN §2).
    internal struct Fnv1a
    {
        const uint OffsetBasis = 2166136261;
        const uint Prime = 16777619;

        public uint Value;

        public static Fnv1a Start() => new Fnv1a { Value = OffsetBasis };

        public void Add(int v)
        {
            unchecked
            {
                uint u = (uint)v;
                for (int i = 0; i < 4; i++)
                {
                    Value ^= (u >> (8 * i)) & 0xFF;
                    Value *= Prime;
                }
            }
        }

        public void Add(uint v) => Add(unchecked((int)v));
        public void Add(bool b) => Add(b ? 1 : 0);
    }
}
