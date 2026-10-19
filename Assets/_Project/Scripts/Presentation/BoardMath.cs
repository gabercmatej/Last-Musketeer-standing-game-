using Musketeers.Sim;
using UnityEngine;

namespace Musketeers.Presentation
{
    // Grid ↔ world. The board is centered on the world origin, one tile per world unit, y up (§3).
    public static class BoardMath
    {
        public static int Width = 9;
        public static int Height = 9;

        public static Vector3 ToWorld(Int2 p) => new Vector3(p.X - (Width - 1) * 0.5f, p.Y - (Height - 1) * 0.5f, 0f);

        public static Int2 ToGrid(Vector3 world) =>
            new Int2(Mathf.RoundToInt(world.x + (Width - 1) * 0.5f), Mathf.RoundToInt(world.y + (Height - 1) * 0.5f));

        public static bool InBounds(Int2 p) => p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;

        public static Vector3 DirVector(Dir d)
        {
            var v = d.ToInt2();
            return new Vector3(v.X, v.Y, 0f);
        }

        public static float DirAngle(Dir d)
        {
            switch (d)
            {
                case Dir.N: return 0f;
                case Dir.E: return -90f;
                case Dir.S: return 180f;
                default: return 90f;
            }
        }
    }

    // sortingOrder bands. Tall things (props, kegs, units) share one band and sort by row, three orders
    // per row, so a pillar in front of a musketeer covers him and one behind doesn't (3/4 view).
    public static class Layers
    {
        public const int Background = -100;
        public const int Tile = 0;
        public const int TileDecal = 2;      // crate debris
        public const int TileOverlay = 5;    // warn glow, cracks
        public const int Shadow = 8;
        public const int PreviewUnder = 10;
        public const int Actors = 20;        // + 3 per row (13 rows max → 59)
        public const int PreviewOver = 90;
        public const int Fx = 100;

        public static int Row(int y) => (BoardMath.Height - 1 - y);

        public static int Prop(int y) => Actors + Row(y) * 3;
        public static int Keg(int y) => Actors + Row(y) * 3 + 1;
        public static int Unit(int y) => Actors + Row(y) * 3 + 2;
    }
}
