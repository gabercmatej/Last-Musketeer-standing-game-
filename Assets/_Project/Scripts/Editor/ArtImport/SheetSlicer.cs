using System;
using System.Collections.Generic;
using UnityEngine;

namespace Musketeers.EditorTools.ArtImport
{
    // Cuts a cosmetic sheet into one image per item by connected alpha regions.
    //  1. Solid pixels (alpha >= 230) form components; components with >= 1% of the solid area are items.
    //  2. Every other visible pixel joins the nearest item it's connected to (multi-source flood, so
    //     soft edges and faint bridges between touching items split down the middle).
    //  3. Detached bits (a plume tip, a monocle chain) join the nearest item if they're close; dust goes.
    // Items come back in reading order: rows top to bottom, then left to right.
    public static class SheetSlicer
    {
        const byte Solid = 230;
        const float ItemShare = 0.01f;
        const float AttachGap = 0.06f;   // fraction of sheet width a detached bit may sit from its item

        public static List<Img> Slice(Img sheet)
        {
            int w = sheet.W, h = sheet.H, n = w * h;
            var solidLabels = sheet.Label(Solid, out var comps, eight: true);
            long solidTotal = 0;
            foreach (var c in comps)
                solidTotal += c.Count;

            // Items = big components, in reading order.
            var items = new List<int>();
            for (int i = 0; i < comps.Count; i++)
            {
                if (comps[i].Count >= solidTotal * ItemShare)
                    items.Add(i);
            }
            items = ReadingOrder(items, comps);
            var itemOf = new int[comps.Count + 1];
            for (int i = 0; i < itemOf.Length; i++)
                itemOf[i] = -1;
            for (int k = 0; k < items.Count; k++)
                itemOf[items[k] + 1] = k;

            // Seed the flood with item pixels.
            var owner = new int[n];
            var queue = new int[n];
            int head = 0, tail = 0;
            for (int i = 0; i < n; i++)
            {
                owner[i] = -1;
                int l = solidLabels[i];
                if (l > 0 && itemOf[l] >= 0)
                {
                    owner[i] = itemOf[l];
                    queue[tail++] = i;
                }
            }
            while (head < tail)
            {
                int i = queue[head++];
                int x = i % w, y = i / w;
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + (d == 0 ? 1 : d == 1 ? -1 : 0);
                    int ny = y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h)
                        continue;
                    int j = ny * w + nx;
                    if (owner[j] < 0 && sheet.Px[j].a > 0)
                    {
                        owner[j] = owner[i];
                        queue[tail++] = j;
                    }
                }
            }

            // Item bounds so far.
            var x0 = new int[items.Count]; var y0 = new int[items.Count]; var x1 = new int[items.Count]; var y1 = new int[items.Count];
            for (int k = 0; k < items.Count; k++) { x0[k] = int.MaxValue; y0[k] = int.MaxValue; x1[k] = -1; y1[k] = -1; }
            for (int i = 0; i < n; i++)
            {
                int k = owner[i];
                if (k < 0) continue;
                int x = i % w, y = i / w;
                if (x < x0[k]) x0[k] = x;
                if (x > x1[k]) x1[k] = x;
                if (y < y0[k]) y0[k] = y;
                if (y > y1[k]) y1[k] = y;
            }

            // Detached bits: attach to the nearest item when close enough.
            var rest = new Img(w, h);
            for (int i = 0; i < n; i++)
            {
                if (owner[i] < 0 && sheet.Px[i].a > 0)
                    rest.Px[i] = sheet.Px[i];
            }
            var restLabels = rest.Label(1, out var bits, eight: true);
            var bitOwner = new int[bits.Count];
            float maxGap = AttachGap * w;
            for (int b = 0; b < bits.Count; b++)
            {
                bitOwner[b] = -1;
                if (bits[b].MaxAlpha < 128 || bits[b].Count < 20)
                    continue;
                float best = float.MaxValue;
                for (int k = 0; k < items.Count; k++)
                {
                    float gx = Math.Max(0, Math.Max(x0[k] - bits[b].X1, bits[b].X0 - x1[k]));
                    float gy = Math.Max(0, Math.Max(y0[k] - bits[b].Y1, bits[b].Y0 - y1[k]));
                    float gap = Mathf.Sqrt(gx * gx + gy * gy);
                    if (gap < best)
                    {
                        best = gap;
                        bitOwner[b] = k;
                    }
                }
                if (best > maxGap)
                    bitOwner[b] = -1;
            }
            for (int i = 0; i < n; i++)
            {
                int l = restLabels[i];
                if (l > 0 && bitOwner[l - 1] >= 0)
                    owner[i] = bitOwner[l - 1];
            }

            // Cut each item out on its own.
            var result = new List<Img>();
            for (int k = 0; k < items.Count; k++)
            {
                var img = new Img(w, h);
                for (int i = 0; i < n; i++)
                {
                    if (owner[i] == k)
                        img.Px[i] = sheet.Px[i];
                }
                result.Add(img.Trim());
            }
            return result;
        }

        // Rows by vertical centre (top first), then left to right.
        static List<int> ReadingOrder(List<int> items, List<Img.Comp> comps)
        {
            var sorted = new List<int>(items);
            sorted.Sort((a, b) => comps[b].CY.CompareTo(comps[a].CY));
            float avgH = 0;
            foreach (var i in sorted)
                avgH += comps[i].Height;
            avgH /= Math.Max(1, sorted.Count);

            var rows = new List<List<int>>();
            foreach (var i in sorted)
            {
                if (rows.Count == 0 || Math.Abs(comps[rows[rows.Count - 1][0]].CY - comps[i].CY) > avgH * 0.5f)
                    rows.Add(new List<int>());
                rows[rows.Count - 1].Add(i);
            }
            var ordered = new List<int>();
            foreach (var row in rows)
            {
                row.Sort((a, b) => comps[a].CX.CompareTo(comps[b].CX));
                ordered.AddRange(row);
            }
            return ordered;
        }
    }
}
