using System;
using System.Collections.Generic;

namespace Musketeers.Sim
{
    public sealed class MapData
    {
        public Board Board;
        public Int2[] Spawns;   // Spawns[slot]: '1' is slot 0
        public bool[] Bridges;  // y*Width+x: the 'b' tiles. Floor in the sim; presentation-only (FIXES_v0.2 §3.1)
    }

    // ASCII map → Board + spawns, using the §3 legend. The top text line is y = Height - 1.
    // Lines starting with '@' are directives and never part of the grid: '@walls' makes the board edge a wall.
    public static class MapParser
    {
        public const int MaxSpawns = 4;

        public static MapData Parse(string ascii)
        {
            string[] rows = SplitRows(ascii);
            bool walls = false;
            foreach (string d in Directives(ascii))
            {
                if (d == "walls") walls = true;
                else throw new FormatException($"Unknown map directive '@{d}'.");
            }
            int height = rows.Length;
            int width = rows[0].Length;
            var board = new Board(width, height) { EdgeWalls = walls };
            var bridges = new bool[width * height];
            var spawns = new Int2[MaxSpawns];
            var found = new bool[MaxSpawns];

            for (int r = 0; r < height; r++)
            {
                int y = height - 1 - r;
                for (int x = 0; x < width; x++)
                {
                    char c = rows[r][x];
                    if (!TryParseTile(c, out TileType tile, out int spawnSlot))
                        throw new FormatException($"Unknown map symbol '{c}' at ({x},{y}).");
                    board.Set(new Int2(x, y), tile);
                    if (c == 'b')
                        bridges[y * width + x] = true;
                    if (spawnSlot >= 0)
                    {
                        if (found[spawnSlot])
                            throw new FormatException($"Spawn {spawnSlot + 1} appears twice.");
                        found[spawnSlot] = true;
                        spawns[spawnSlot] = new Int2(x, y);
                    }
                }
            }

            int count = 0;
            while (count < MaxSpawns && found[count])
                count++;
            for (int i = count; i < MaxSpawns; i++)
            {
                if (found[i])
                    throw new FormatException($"Spawn {i + 1} exists but spawn {count + 1} is missing; spawns must be numbered 1..n.");
            }

            var result = new Int2[count];
            Array.Copy(spawns, result, count);
            return new MapData { Board = board, Spawns = result, Bridges = bridges };
        }

        // The '@name' lines of a map text, without the '@', in order.
        public static List<string> Directives(string ascii)
        {
            if (ascii == null)
                throw new ArgumentNullException(nameof(ascii));
            var list = new List<string>();
            foreach (string raw in ascii.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length > 0 && line[0] == '@')
                    list.Add(line.Substring(1).Trim());
            }
            return list;
        }

        // Splits on newlines, trims each line (so maps can be indented in code), drops blank lines and
        // '@' directive lines, and checks the grid is rectangular.
        public static string[] SplitRows(string ascii)
        {
            if (ascii == null)
                throw new ArgumentNullException(nameof(ascii));
            var rows = new List<string>();
            foreach (string raw in ascii.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length > 0 && line[0] != '@')
                    rows.Add(line);
            }
            if (rows.Count == 0)
                throw new FormatException("Map is empty.");
            for (int i = 1; i < rows.Count; i++)
            {
                if (rows[i].Length != rows[0].Length)
                    throw new FormatException($"Map row {i} is {rows[i].Length} wide, expected {rows[0].Length}.");
            }
            return rows.ToArray();
        }

        // §3 legend. spawnSlot is 0-based, or -1 when the symbol isn't a spawn.
        public static bool TryParseTile(char c, out TileType tile, out int spawnSlot)
        {
            spawnSlot = -1;
            switch (c)
            {
                case '.': tile = TileType.Floor; return true;
                case '#': tile = TileType.Pillar; return true;
                case 'o': tile = TileType.Crate; return true;
                case '~': tile = TileType.Pit; return true;
                case 'b': tile = TileType.Floor; return true;   // bridge: a floor tile that only looks different
            }
            if (c >= '1' && c < '1' + MaxSpawns)
            {
                tile = TileType.Floor;
                spawnSlot = c - '1';
                return true;
            }
            tile = TileType.Floor;
            return false;
        }
    }
}
