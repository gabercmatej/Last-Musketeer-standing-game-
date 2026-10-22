using System;

namespace Musketeers.Sim
{
    // W×H tile grid. (0,0) is bottom-left (GAME_DESIGN §3).
    public sealed class Board
    {
        public readonly int Width;
        public readonly int Height;
        readonly TileType[] _tiles;
        readonly bool[] _removed;   // 'x' tiles (FIXES_4E §4.4): outside the board, but still inside the grid


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
            _removed = new bool[width * height];
        }

        Board(int width, int height, TileType[] tiles, bool[] removed, bool edgeWalls)
        {
            Width = width;
            Height = height;
            _tiles = tiles;
            _removed = removed;
            EdgeWalls = edgeWalls;
        }

        public bool InBounds(Int2 p) => p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;

        // §3: off-board counts as pit, or as a pillar on a walled board. A removed ('x') tile is off-board too.
        public TileType Get(Int2 p) =>
            InBounds(p) && !_removed[p.Y * Width + p.X] ? _tiles[p.Y * Width + p.X] : (EdgeWalls ? TileType.Pillar : TileType.Pit);

        // A removed ('x') tile: inside the grid but not part of the board (§3, FIXES_4E §4.4).
        public bool IsRemoved(Int2 p) => InBounds(p) && _removed[p.Y * Width + p.X];

        // True off the grid and on removed tiles: nobody may walk here (FIXES_4E §8.1); a push or pull
        // across it is a wall slam on a walled board and a fall otherwise.
        public bool IsVoid(Int2 p) => !InBounds(p) || _removed[p.Y * Width + p.X];

        public void SetRemoved(Int2 p, bool removed = true)
        {
            if (!InBounds(p))
                throw new ArgumentOutOfRangeException(nameof(p), $"{p} is off the {Width}x{Height} board.");
            _removed[p.Y * Width + p.X] = removed;
            if (removed)
                _tiles[p.Y * Width + p.X] = TileType.Floor;
        }

        public void Set(Int2 p, TileType tile)
        {
            if (!InBounds(p))
                throw new ArgumentOutOfRangeException(nameof(p), $"{p} is off the {Width}x{Height} board.");
            _tiles[p.Y * Width + p.X] = tile;
        }

        // §3 Cardinal's Guard: the outer ring is 0 and the index grows toward the center.
        public int RingIndex(Int2 p) => Math.Min(Math.Min(p.X, p.Y), Math.Min(Width - 1 - p.X, Height - 1 - p.Y));

        // Raw tile data for MatchStateSerializer (a removed tile reads as Floor, its flag says it is void).
        internal TileType RawTile(int index) => _tiles[index];
        internal bool RawRemoved(int index) => _removed[index];

        public Board Clone() => new Board(Width, Height, (TileType[])_tiles.Clone(), (bool[])_removed.Clone(), EdgeWalls);

        internal void AddToHash(ref Fnv1a h)
        {
            h.Add(Width);
            h.Add(Height);
            h.Add(EdgeWalls);
            for (int i = 0; i < _tiles.Length; i++)
                h.Add(_removed[i] ? -1 : (int)_tiles[i]);
        }
    }
}
