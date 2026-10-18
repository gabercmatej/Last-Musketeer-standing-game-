using System;

namespace Musketeers.Sim
{
    // W×H tile grid. (0,0) is bottom-left (GAME_DESIGN §3).
    public sealed class Board
    {
        public readonly int Width;
        public readonly int Height;
        readonly TileType[] _tiles;

        public Board(int width, int height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), $"Board must be at least 1x1, got {width}x{height}.");
            Width = width;
            Height = height;
            _tiles = new TileType[width * height];
        }

        Board(int width, int height, TileType[] tiles)
        {
            Width = width;
            Height = height;
            _tiles = tiles;
        }

        public bool InBounds(Int2 p) => p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;

        // §3: off-board counts as pit.
        public TileType Get(Int2 p) => InBounds(p) ? _tiles[p.Y * Width + p.X] : TileType.Pit;

        public void Set(Int2 p, TileType tile)
        {
            if (!InBounds(p))
                throw new ArgumentOutOfRangeException(nameof(p), $"{p} is off the {Width}x{Height} board.");
            _tiles[p.Y * Width + p.X] = tile;
        }

        // §3 Cardinal's Guard: the outer ring is 0 and the index grows toward the center.
        public int RingIndex(Int2 p) => Math.Min(Math.Min(p.X, p.Y), Math.Min(Width - 1 - p.X, Height - 1 - p.Y));

        public Board Clone() => new Board(Width, Height, (TileType[])_tiles.Clone());

        internal void AddToHash(ref Fnv1a h)
        {
            h.Add(Width);
            h.Add(Height);
            for (int i = 0; i < _tiles.Length; i++)
                h.Add((int)_tiles[i]);
        }
    }
}
