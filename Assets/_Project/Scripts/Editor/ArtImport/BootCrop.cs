using System.Collections.Generic;
using Musketeers.Data;
using UnityEngine;

namespace Musketeers.EditorTools.ArtImport
{
    // FIXES_v0.2 §1.2 "one boot, rendered twice": each footwear sheet item is a pair, so the importer keeps
    // a single boot. Side view: the front boot (the left, complete one), cut along a polygon traced by eye
    // in item-relative coordinates (0–100, y up; the six tall boots share one silhouette). Front view: the
    // two boots stand side by side, so the pair splits at the emptiest column near the middle and the
    // right boot is kept (the rig mirrors it for the other foot).
    public static class BootCrop
    {
        static readonly float[] Tall =
        {
            1, 62, 1, 66, 6, 88, 10, 94, 30, 98, 50, 99, 53, 90, 56, 74, 53, 66, 50.5f, 63, 50.5f, 46, 56, 38, 58, 33,
            66, 29, 75, 24, 82, 19, 85, 14, 84, 8, 78, 5, 60, 2, 35, 1, 12, 2, 12, 20, 14, 26, 14, 56, 10, 60,
        };

        static readonly Dictionary<string, float[]> Side = new Dictionary<string, float[]>
        {
            { "boots_black", Tall }, { "boots_purple", Tall }, { "boots_green", Tall },
            { "boots_blue", Tall }, { "boots_red", Tall }, { "boots_brown", Tall },
            { "boots_flipflops", new float[] { 10, 100, 43, 100, 43, 62, 48, 50, 62, 42, 76, 34, 84, 30, 90, 22, 91, 12, 88, 6, 70, 1, 30, 1, 5, 5, 0, 15, 1, 30, 7, 36, 9, 55 } },
            { "boots_crocs", new float[] { 11, 100, 41, 100, 41, 62, 45, 50, 52, 47, 62, 44, 75, 40, 84, 34, 88, 25, 87, 14, 80, 7, 60, 3, 20, 2, 4, 8, 1, 20, 3, 42, 8, 48, 11, 52 } },
            { "boots_sneakers", new float[] { 10, 100, 39, 100, 39, 62, 42, 52, 50, 47, 62, 40, 74, 32, 84, 26, 88, 18, 86, 8, 78, 2, 20, 1, 4, 6, 2, 30, 5, 48, 10, 52 } },
            { "boots_barefoot", new float[] { 6, 100, 39, 100, 38, 60, 40, 45, 48, 35, 58, 26, 68, 20, 76, 17, 80, 11, 77, 3, 60, 0, 25, 0, 8, 4, 4, 14, 4, 40, 6, 60 } },
        };

        public static bool Has(string id) => Side.ContainsKey(id);

        public static Img SideBoot(Img pair, string id)
        {
            if (!Side.TryGetValue(id, out var pts))
                return pair;
            var poly = new Vector2[pts.Length / 2];
            for (int i = 0; i < poly.Length; i++)
                poly[i] = new Vector2(pts[i * 2] / 100f * pair.W, pts[i * 2 + 1] / 100f * pair.H);
            var o = new Img(pair.W, pair.H);
            for (int y = 0; y < pair.H; y++)
            for (int x = 0; x < pair.W; x++)
            {
                var p = pair.Px[y * pair.W + x];
                if (p.a == 0)
                    continue;
                // 2×2 coverage keeps the cut edge soft.
                int hits = 0;
                for (int s = 0; s < 4; s++)
                {
                    if (PolygonMask.Contains(poly, new Vector2(x + 0.25f + 0.5f * (s & 1), y + 0.25f + 0.5f * (s >> 1))))
                        hits++;
                }
                if (hits > 0)
                    o.Px[y * pair.W + x] = new Color32(p.r, p.g, p.b, (byte)(p.a * hits / 4));
            }
            return o.Trim();
        }

        public static Img FrontBoot(Img pair)
        {
            int best = pair.W / 2, bestCount = int.MaxValue;
            for (int x = pair.W * 35 / 100; x <= pair.W * 65 / 100; x++)
            {
                int count = 0;
                for (int y = 0; y < pair.H; y++)
                {
                    if (pair.A(x, y) >= 128)
                        count++;
                }
                // Prefer the middle on ties, so a straight gap splits evenly.
                if (count < bestCount || (count == bestCount && Mathf.Abs(x - pair.W / 2) < Mathf.Abs(best - pair.W / 2)))
                {
                    bestCount = count;
                    best = x;
                }
            }
            return pair.Crop(new RectInt(best, 0, pair.W - best, pair.H)).Trim();
        }
    }
}
