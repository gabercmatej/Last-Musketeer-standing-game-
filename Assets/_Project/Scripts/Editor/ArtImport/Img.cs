using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Musketeers.EditorTools.ArtImport
{
    // A plain RGBA image for the art importer (editor only). Rows run bottom-up like Unity textures,
    // so (x, y) = (0, 0) is the bottom-left pixel and "up" in the art is +y.
    public sealed class Img
    {
        public readonly int W, H;
        public readonly Color32[] Px;

        public Img(int w, int h)
        {
            W = w;
            H = h;
            Px = new Color32[w * h];
        }

        Img(int w, int h, Color32[] px)
        {
            W = w;
            H = h;
            Px = px;
        }

        public static Img Load(string path)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!tex.LoadImage(File.ReadAllBytes(path)))
                    throw new IOException("Not an image: " + path);
                return new Img(tex.width, tex.height, tex.GetPixels32());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        public byte[] EncodePng() => Encode(t => t.EncodeToPNG());
        public byte[] EncodeJpg(int quality) => Encode(t => t.EncodeToJPG(quality));

        byte[] Encode(Func<Texture2D, byte[]> enc)
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            try
            {
                tex.SetPixels32(Px);
                tex.Apply(false);
                return enc(tex);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        public byte A(int x, int y) => Px[y * W + x].a;

        // ---------- cleaning ----------

        // Removes generation dust: alpha below `minAlpha` becomes 0, and 8-connected specks that are tiny
        // (< minPixels) or only faint haze (max alpha < 128) are erased. Real detached parts survive.
        public void Clean(byte minAlpha = 16, int minPixels = 150)
        {
            for (int i = 0; i < Px.Length; i++)
            {
                if (Px[i].a < minAlpha)
                    Px[i] = new Color32(0, 0, 0, 0);
            }
            var labels = Label(1, out var comps, eight: true);
            for (int i = 0; i < Px.Length; i++)
            {
                int l = labels[i];
                if (l > 0 && (comps[l - 1].Count < minPixels || comps[l - 1].MaxAlpha < 128))
                    Px[i] = new Color32(0, 0, 0, 0);
            }
        }

        public struct Comp
        {
            public int Count;
            public int MaxAlpha;
            public int X0, Y0, X1, Y1;   // inclusive bounds
            public float CX => (X0 + X1) * 0.5f;
            public float CY => (Y0 + Y1) * 0.5f;
            public int Width => X1 - X0 + 1;
            public int Height => Y1 - Y0 + 1;
        }

        // Connected components of pixels with alpha >= threshold. labels[i] is 0 or component index + 1.
        public int[] Label(byte threshold, out List<Comp> comps, bool eight)
        {
            var labels = new int[Px.Length];
            comps = new List<Comp>();
            var stack = new int[Px.Length];
            for (int start = 0; start < Px.Length; start++)
            {
                if (labels[start] != 0 || Px[start].a < threshold)
                    continue;
                int id = comps.Count + 1;
                var c = new Comp { X0 = int.MaxValue, Y0 = int.MaxValue, X1 = -1, Y1 = -1 };
                int sp = 0;
                stack[sp++] = start;
                labels[start] = id;
                while (sp > 0)
                {
                    int i = stack[--sp];
                    int x = i % W, y = i / W;
                    c.Count++;
                    if (Px[i].a > c.MaxAlpha) c.MaxAlpha = Px[i].a;
                    if (x < c.X0) c.X0 = x;
                    if (x > c.X1) c.X1 = x;
                    if (y < c.Y0) c.Y0 = y;
                    if (y > c.Y1) c.Y1 = y;
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if ((dx == 0 && dy == 0) || (!eight && dx != 0 && dy != 0))
                            continue;
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= W || ny >= H)
                            continue;
                        int n = ny * W + nx;
                        if (labels[n] == 0 && Px[n].a >= threshold)
                        {
                            labels[n] = id;
                            stack[sp++] = n;
                        }
                    }
                }
                comps.Add(c);
            }
            return labels;
        }

        // ---------- geometry ----------

        public RectInt Bounds(byte threshold = 1)
        {
            int x0 = W, y0 = H, x1 = -1, y1 = -1;
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                if (Px[y * W + x].a < threshold)
                    continue;
                if (x < x0) x0 = x;
                if (x > x1) x1 = x;
                if (y < y0) y0 = y;
                if (y > y1) y1 = y;
            }
            return x1 < 0 ? new RectInt(0, 0, 0, 0) : new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        }

        public Img Crop(RectInt r)
        {
            var o = new Img(r.width, r.height);
            for (int y = 0; y < r.height; y++)
                Array.Copy(Px, (r.y + y) * W + r.x, o.Px, y * r.width, r.width);
            return o;
        }

        public Img Trim(byte threshold = 1)
        {
            var b = Bounds(threshold);
            return b.width == 0 ? new Img(4, 4) : Crop(b);
        }

        // Copies src into this image at (dx, dy), clipping at the edges (no blending: targets are blank).
        public void Blit(Img src, int dx, int dy)
        {
            for (int y = 0; y < src.H; y++)
            {
                int ty = y + dy;
                if (ty < 0 || ty >= H)
                    continue;
                for (int x = 0; x < src.W; x++)
                {
                    int tx = x + dx;
                    if (tx < 0 || tx >= W)
                        continue;
                    var p = src.Px[y * src.W + x];
                    if (p.a > 0)
                        Px[ty * W + tx] = p;
                }
            }
        }

        // Pads to a size, centring the content (or at the given offset).
        public Img Pad(int w, int h, int dx = int.MinValue, int dy = int.MinValue)
        {
            var o = new Img(w, h);
            o.Blit(this, dx == int.MinValue ? (w - W) / 2 : dx, dy == int.MinValue ? (h - H) / 2 : dy);
            return o;
        }

        // Rounds both sides up to a multiple of 4 so DXT/crunch compression applies (bottom-left anchored).
        public Img PadToMultipleOf4()
        {
            int w = (W + 3) & ~3, h = (H + 3) & ~3;
            return w == W && h == H ? this : Pad(w, h, 0, 0);
        }

        // Area-averaging resize with premultiplied alpha, so edges don't pick up dark or coloured fringes.
        public Img Resize(int nw, int nh)
        {
            nw = Math.Max(1, nw);
            nh = Math.Max(1, nh);
            if (nw == W && nh == H)
                return this;
            // Premultiply into floats.
            var src = new float[W * H * 4];
            for (int i = 0; i < Px.Length; i++)
            {
                var p = Px[i];
                float a = p.a / 255f;
                src[i * 4] = p.r / 255f * a;
                src[i * 4 + 1] = p.g / 255f * a;
                src[i * 4 + 2] = p.b / 255f * a;
                src[i * 4 + 3] = a;
            }
            var tmp = ResampleAxis(src, W, H, nw, true);
            var dst = ResampleAxis(tmp, nw, H, nh, false);
            var o = new Img(nw, nh);
            for (int i = 0; i < o.Px.Length; i++)
            {
                float a = dst[i * 4 + 3];
                if (a <= 0.0005f)
                    continue;
                o.Px[i] = new Color32(
                    ToByte(dst[i * 4] / a), ToByte(dst[i * 4 + 1] / a), ToByte(dst[i * 4 + 2] / a), ToByte(a));
            }
            return o;
        }

        public Img Scale(float factor) => Resize(Mathf.RoundToInt(W * factor), Mathf.RoundToInt(H * factor));

        // Fits inside a box keeping the aspect ratio.
        public Img Fit(int maxW, int maxH)
        {
            float f = Math.Min((float)maxW / W, (float)maxH / H);
            return Scale(f);
        }

        static byte ToByte(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);

        // One-axis box filter with fractional coverage (exact area averaging for downscaling, linear-ish
        // for upscaling). `horizontal` resamples x from sw to dw; otherwise y from sh to dw.
        static float[] ResampleAxis(float[] src, int sw, int sh, int d, bool horizontal)
        {
            int sLen = horizontal ? sw : sh;
            int other = horizontal ? sh : sw;
            int ow = horizontal ? d : sw;
            int oh = horizontal ? sh : d;
            var dst = new float[ow * oh * 4];
            float scale = (float)sLen / d;
            bool up = scale < 1f;
            for (int o = 0; o < other; o++)
            {
                for (int k = 0; k < d; k++)
                {
                    float r = 0, g = 0, b = 0, a = 0, wsum = 0;
                    if (up)
                    {
                        float c = (k + 0.5f) * scale - 0.5f;
                        int i0 = Mathf.FloorToInt(c);
                        float t = c - i0;
                        for (int q = 0; q < 2; q++)
                        {
                            int i = Mathf.Clamp(i0 + q, 0, sLen - 1);
                            float w = q == 0 ? 1f - t : t;
                            int si = horizontal ? (o * sw + i) * 4 : (i * sw + o) * 4;
                            r += src[si] * w; g += src[si + 1] * w; b += src[si + 2] * w; a += src[si + 3] * w;
                            wsum += w;
                        }
                    }
                    else
                    {
                        float s0 = k * scale, s1 = (k + 1) * scale;
                        int i0 = Mathf.FloorToInt(s0), i1 = Mathf.Min(sLen - 1, Mathf.CeilToInt(s1) - 1);
                        for (int i = i0; i <= i1; i++)
                        {
                            float w = Mathf.Min(s1, i + 1) - Mathf.Max(s0, i);
                            if (w <= 0f)
                                continue;
                            int si = horizontal ? (o * sw + i) * 4 : (i * sw + o) * 4;
                            r += src[si] * w; g += src[si + 1] * w; b += src[si + 2] * w; a += src[si + 3] * w;
                            wsum += w;
                        }
                    }
                    int di = horizontal ? (o * ow + k) * 4 : (k * ow + o) * 4;
                    if (wsum > 0f)
                    {
                        dst[di] = r / wsum; dst[di + 1] = g / wsum; dst[di + 2] = b / wsum; dst[di + 3] = a / wsum;
                    }
                }
            }
            return dst;
        }
    }
}
