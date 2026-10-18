namespace Musketeers.Sim
{
    // §3: 4 directions only. The numeric order (N, E, S, W) is clockwise, which the helpers rely on.
    public enum Dir : byte { N, E, S, W }

    public static class DirExt
    {
        public static readonly Dir[] All = { Dir.N, Dir.E, Dir.S, Dir.W };

        public static Int2 ToInt2(this Dir d)
        {
            switch (d)
            {
                case Dir.N: return new Int2(0, 1);
                case Dir.E: return new Int2(1, 0);
                case Dir.S: return new Int2(0, -1);
                default: return new Int2(-1, 0);
            }
        }

        public static Dir Opposite(this Dir d) => (Dir)(((int)d + 2) & 3);
        public static Dir RotateCW(this Dir d) => (Dir)(((int)d + 1) & 3);
        public static Dir RotateCCW(this Dir d) => (Dir)(((int)d + 3) & 3);

        // N/S vs E/W differ in the low bit.
        public static bool IsPerpendicularTo(this Dir a, Dir b) => (((int)a ^ (int)b) & 1) == 1;

        // Only unit steps map to a direction.
        public static bool TryFromDelta(Int2 delta, out Dir dir)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].ToInt2() == delta)
                {
                    dir = All[i];
                    return true;
                }
            }
            dir = Dir.N;
            return false;
        }
    }
}
