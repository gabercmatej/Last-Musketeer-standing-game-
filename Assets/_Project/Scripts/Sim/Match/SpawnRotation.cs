using System;

namespace Musketeers.Sim
{
    // Day 6: identities A-D are the initial clockwise occupants, never character names.
    // Only the assignment changes; map spawn coordinates and unit/player slots stay intact.
    public static class SpawnRotation
    {
        static readonly int[,] Occupants = { { 0, 1, 2, 3 }, { 1, 3, 0, 2 }, { 0, 2, 3, 1 }, { 3, 0, 1, 2 } };

        public static int[] Assign(Int2[] spawns, int roundNumber)
        {
            int n = spawns.Length;
            var assigned = new int[n];
            if (n != 4)
            {
                // Small custom/test maps retain unique placement without inventing spawn tiles.
                for (int i = 0; i < n; i++) assigned[i] = (i + Math.Max(0, roundNumber - 1)) % n;
                return assigned;
            }
            var clockwise = new[] { 0, 1, 2, 3 };
            Array.Sort(clockwise, (a, b) => spawns[b].Y != spawns[a].Y
                ? spawns[b].Y.CompareTo(spawns[a].Y) : spawns[a].X.CompareTo(spawns[b].X));
            // Top pair left->right; bottom pair right->left. Both shipped maps use these corners.
            if (spawns[clockwise[0]].X > spawns[clockwise[1]].X) Swap(clockwise, 0, 1);
            if (spawns[clockwise[2]].X < spawns[clockwise[3]].X) Swap(clockwise, 2, 3);
            int round = Math.Max(0, roundNumber - 1) % 4;
            for (int position = 0; position < 4; position++)
                assigned[clockwise[Occupants[round, position]]] = clockwise[position];
            return assigned;
        }

        static void Swap(int[] values, int a, int b)
        {
            int t = values[a]; values[a] = values[b]; values[b] = t;
        }
    }
}
