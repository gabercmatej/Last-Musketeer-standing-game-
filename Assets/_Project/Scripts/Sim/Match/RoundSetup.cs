using System;
using System.Collections.Generic;

namespace Musketeers.Sim
{
    // FIXES_4E §5.3: the random pits and crates scattered at the start of every round. A pure function of
    // (match seed, round number, map), so every client builds the same board with no network traffic.
    public static class RoundSetup
    {
        // Murmur3-style finalizer over seed and round, so neighbouring rounds give unrelated layouts.
        public static uint RoundSeed(uint matchSeed, int round)
        {
            unchecked
            {
                uint h = matchSeed ^ ((uint)round * 0x85EBCA6Bu);
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return h;
            }
        }

        // Scatters pits then crates onto `board` (the round's own clone). Rules, all from FIXES_4E §5.3:
        //  - only plain floor: not the outer ring, not a pillar, water, bridge plank, removed tile, spawn
        //    or any tile within Chebyshev distance 1 of a spawn;
        //  - no two random pits 8-adjacent (decision: "next to each other" includes diagonals);
        //  - a pit that would cut any spawn off from the others (flood fill over non-pit, non-solid tiles)
        //    is rejected and another candidate is drawn.
        // If the board runs out of valid tiles it places fewer; it never loops forever.
        public static void Apply(MapData map, Board board, GameConfig cfg, uint matchSeed, int round)
        {
            if (map == null || (!map.RandomPits && !map.RandomCrates))
                return;
            var rng = new DeterministicRng(RoundSeed(matchSeed, round));
            var candidates = Candidates(map, board);

            var pits = new List<Int2>();
            var rejected = new List<Int2>();   // still fine for a crate
            if (map.RandomPits)
            {
                int want = cfg.RandomPitsMin >= cfg.RandomPitsMax ? cfg.RandomPitsMin : rng.Range(cfg.RandomPitsMin, cfg.RandomPitsMax + 1);
                bool baselineConnected = SpawnsConnected(board, map.Spawns);
                while (pits.Count < want && candidates.Count > 0)
                {
                    int k = rng.Range(0, candidates.Count);
                    Int2 p = candidates[k];
                    candidates.RemoveAt(k);   // a rejected tile stays invalid: pits are never removed again
                    if (TouchesAny(p, pits))
                    {
                        rejected.Add(p);
                        continue;
                    }
                    board.Set(p, TileType.Pit);
                    if (baselineConnected && !SpawnsConnected(board, map.Spawns))
                    {
                        board.Set(p, TileType.Floor);
                        rejected.Add(p);
                        continue;
                    }
                    pits.Add(p);
                }
            }

            if (map.RandomCrates)
            {
                if (rejected.Count > 0)
                {
                    candidates.AddRange(rejected);
                    candidates.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));   // back to row-major
                }
                for (int n = 0; n < cfg.RandomCrates && candidates.Count > 0; n++)
                {
                    int k = rng.Range(0, candidates.Count);
                    board.Set(candidates[k], TileType.Crate);
                    candidates.RemoveAt(k);
                }
            }
        }

        // Row-major list of every tile random objects may use.
        public static List<Int2> Candidates(MapData map, Board board)
        {
            var list = new List<Int2>();
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var p = new Int2(x, y);
                    if (board.IsVoid(p) || board.RingIndex(p) == 0 || board.Get(p) != TileType.Floor)
                        continue;
                    if (map.Bridges != null && map.Bridges[y * board.Width + x])
                        continue;
                    if (NearSpawn(map.Spawns, p))
                        continue;
                    list.Add(p);
                }
            }
            return list;
        }

        static bool NearSpawn(Int2[] spawns, Int2 p)
        {
            foreach (var s in spawns)
            {
                if (Math.Max(Math.Abs(s.X - p.X), Math.Abs(s.Y - p.Y)) <= 1)
                    return true;
            }
            return false;
        }

        static bool TouchesAny(Int2 p, List<Int2> pits)
        {
            foreach (var q in pits)
            {
                if (Math.Max(Math.Abs(q.X - p.X), Math.Abs(q.Y - p.Y)) <= 1)
                    return true;
            }
            return false;
        }

        // True when every spawn can walk to spawn 0 over tiles that are neither pit nor solid.
        public static bool SpawnsConnected(Board board, Int2[] spawns)
        {
            if (spawns.Length < 2)
                return true;
            var seen = new bool[board.Width * board.Height];
            var queue = new Queue<Int2>();
            queue.Enqueue(spawns[0]);
            seen[spawns[0].Y * board.Width + spawns[0].X] = true;
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                foreach (Dir d in DirExt.All)
                {
                    var q = p + d.ToInt2();
                    if (board.IsVoid(q))
                        continue;
                    int i = q.Y * board.Width + q.X;
                    if (seen[i])
                        continue;
                    var t = board.Get(q);
                    if (t == TileType.Pit || t.IsSolid())
                        continue;
                    seen[i] = true;
                    queue.Enqueue(q);
                }
            }
            foreach (var s in spawns)
            {
                if (!seen[s.Y * board.Width + s.X])
                    return false;
            }
            return true;
        }
    }
}
