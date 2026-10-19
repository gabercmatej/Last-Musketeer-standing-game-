namespace Musketeers.Data
{
    // Picks a floor variant from the tile position alone, so the same map always looks the same on
    // every machine and every round (no RNG, nothing to sync).
    public static class TileVariant
    {
        public static int Pick(int x, int y, int count)
        {
            if (count <= 1)
                return 0;
            unchecked
            {
                uint h = (uint)x * 73856093u ^ (uint)y * 19349663u;
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                h ^= h >> 15;
                return (int)(h % (uint)count);
            }
        }
    }
}
