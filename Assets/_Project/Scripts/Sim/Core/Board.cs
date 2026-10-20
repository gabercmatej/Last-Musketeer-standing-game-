using System;

namespace Musketeers.Sim
{
    // W×H tile grid. (0,0) is bottom-left (GAME_DESIGN §3).
    public sealed class Board
    {
        public readonly int Width;
        public readonly int Height;
        readonly TileType[] _tiles;

        // §3: a walled board (Courtyard) treats the outside as a Pillar, so nothing can be pushed or dashed
        // off it. Traces that already stop at InBounds (shots, Grappin, keg throws) are unaffected.
        public bool EdgeWalls;

        public Board(int width, int height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), $"Board must be at least 1x1, got {width}x{height}.");
            Width = width;
            Height = height;
            _tiles = new TileType[width * height];
        }

        Board(int width, int height, TileType[] tiles, bool edgeWalls)
        {
            Width = width;
            Height = height;
            _tiles = tiles;
            EdgeWalls = edgeWalls;
        }

        public bool InBounds(Int2 p) => p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;

        // §3: off-board counts as pit, or as a pillar on a walled board.
        public TileType Get(Int2 p) => InBounds(p) ? _tiles[p.Y * Width + p.X] : (EdgeWalls ? TileType.Pillar : TileType.Pit);

        public void Set(Int2 p, TileType tile)
        {
            if (!InBounds(p))
                throw new ArgumentOutOfRangeException(nameof(p), $"{p} is off the {Width}x{Height} board.");
            _tiles[p.Y * Width + p.X] = tile;
        }

        // §3 Cardinal's Guard: the outer ring is 0 and the index grows toward the center.
        public int RingIndex(Int2 p) => Math.Min(Math.Min(p.X, p.Y), Math.Min(Width - 1 - p.X, Height - 1 - p.Y));

        public Board Clone() => new Board(Width, Height, (TileType[])_tiles.Clone(), EdgeWalls);

        internal void AddToHash(ref Fnv1a h)
        {
            h.Add(Width);
            h.Add(Height);
            h.Add(EdgeWalls);
            for (int i = 0; i < _tiles.Length; i++)
                h.Add((int)_tiles[i]);
        }
    }
}
