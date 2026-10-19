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

    // sortingOrder bands. Units and kegs also sort by row so lower rows draw on top (3/4 view).
    public static class Layers
    {
        public const int Tile = 0;
        public const int TileOverlay = 5;
        public const int PreviewUnder = 10;
        public const int Keg = 30;
        public const int Unit = 50;        // + row offset (0..20)
        public const int UnitHud = 80;
        public const int PreviewOver = 90;
        public const int Fx = 100;

        public static int Row(int y) => (BoardMath.Height - 1 - y);
    }
}
