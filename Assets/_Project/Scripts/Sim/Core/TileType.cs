namespace Musketeers.Sim
{
    // §3 tile table. Spawns are Floor; their positions live in MapData.Spawns.
    public enum TileType : byte { Floor, Pillar, Crate, Pit }

    public static class TileTypeExt
    {
        // Pillars and crates block both movement and shots. Pits only kill.
        public static bool IsSolid(this TileType t) => t == TileType.Pillar || t == TileType.Crate;

        public static char ToChar(this TileType t)
        {
            switch (t)
            {
                case TileType.Pillar: return '#';
                case TileType.Crate: return 'o';
                case TileType.Pit: return '~';
                default: return '.';
            }
        }
    }
}
