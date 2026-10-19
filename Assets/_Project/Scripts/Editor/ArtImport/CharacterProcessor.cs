using System;
using UnityEngine;

namespace Musketeers.EditorTools.ArtImport
{
    // Puts a character pose on the ART_GUIDE §3 canvas: 512×512, feet centre at (256, 64) so the sprite
    // pivot (0.5, 0.125) is the feet, every musketeer's idle body the same height. Also estimates the
    // cosmetic anchors from the silhouette as a first guess (Matej nudges them in the Wardrobe Preview).
    public static class CharacterProcessor
    {
        public const int Canvas = 512;
        public const int FeetX = 256;
        public const int FeetY = 64;
        public const int IdleHeight = 330;   // px of an idle body on the canvas (~1.3 tiles at PPU 256)

        public struct Anchors
        {
            public Vector2 HeadTop, Face, Back, Hand;
            public float HeadAngle;
        }

        public sealed class Result
        {
            public Img Image;
            public Anchors Anchors;   // canvas pixels
            public bool Clipped;
        }

        // Height of the cleaned figure in source pixels: the idle body sets each character's scale.
        public static int FigureHeight(Img cleaned) => cleaned.Bounds(128).height;

        public static Result Place(Img cleaned, float scale, bool front, string pose, int idleHeightOnCanvas)
        {
            var fig = cleaned.Trim().Scale(scale);
            var b = fig.Bounds(128);
            // Feet: the bottom 5% of the solid figure; centre between its outermost pixels.
            int band = Math.Max(2, Mathf.RoundToInt(b.height * 0.05f));
            int fx0 = int.MaxValue, fx1 = -1;
            for (int y = b.yMin; y < b.yMin + band; y++)
            for (int x = 0; x < fig.W; x++)
            {
                if (fig.A(x, y) < 128) continue;
                if (x < fx0) fx0 = x;
                if (x > fx1) fx1 = x;
            }
            int feetCx = fx1 >= 0 ? (fx0 + fx1) / 2 : fig.W / 2;
            int dx = FeetX - feetCx, dy = FeetY - b.yMin;
            var canvas = new Img(Canvas, Canvas);
            canvas.Blit(fig, dx, dy);
            bool clipped = dx < 0 || dy < 0 || dx + fig.W > Canvas || dy + fig.H > Canvas;
            return new Result { Image = canvas, Anchors = Estimate(canvas, front, pose, idleHeightOnCanvas), Clipped = clipped };
        }

        // Silhouette heuristics, in canvas pixels. Good enough to start from; tuned by eye afterwards.
        static Anchors Estimate(Img c, bool front, string pose, int idleH)
        {
            var b = c.Bounds(128);
            float H = idleH;
            // Head top: the first row (from the top) wide enough to be the head, which skips a raised fist.
            int minRun = Mathf.RoundToInt(H * 0.18f);
            int headY = b.yMax - 1, headX = (b.xMin + b.xMax) / 2;
            for (int y = b.yMax - 1; y >= b.yMin; y--)
            {
                LongestRun(c, y, out int r0, out int r1);
                if (r1 - r0 + 1 >= minRun)
                {
                    headY = y;
                    headX = (r0 + r1) / 2;
                    break;
                }
            }
            // Head width/centre over the head band (top 35% of the idle height).
            int hb0 = int.MaxValue, hb1 = -1;
            for (int y = headY; y > headY - H * 0.35f && y >= 0; y--)
            {
                LongestRun(c, y, out int r0, out int r1);
                if (r1 < 0) continue;
                if (r0 < hb0) hb0 = r0;
                if (r1 > hb1) hb1 = r1;
            }
            float headCx = hb1 >= 0 ? (hb0 + hb1) * 0.5f : headX;
            float headW = hb1 >= 0 ? hb1 - hb0 : H * 0.4f;

            var a = new Anchors();
            a.HeadTop = new Vector2(headCx, headY - H * 0.06f);
            a.Face = new Vector2(front ? headCx : headCx + headW * 0.18f, headY - H * 0.25f);
            int shoulderY = Mathf.RoundToInt(headY - H * 0.45f);
            LongestRun(c, Mathf.Clamp(shoulderY, 0, c.H - 1), out int s0, out int s1);
            a.Back = front ? new Vector2(headCx, shoulderY) : new Vector2((s0 >= 0 ? s0 : headCx) + H * 0.06f, shoulderY);
            int handY = Mathf.RoundToInt(FeetY + H * 0.36f);
            LongestRun(c, handY, out int h0, out int h1);
            a.Hand = new Vector2(h1 >= 0 ? h1 - H * 0.03f : FeetX + H * 0.15f, handY);
            a.HeadAngle = pose == "hurt" ? -15f : 0f;
            return a;
        }

        static void LongestRun(Img c, int y, out int best0, out int best1)
        {
            best0 = -1;
            best1 = -2;
            int start = -1;
            for (int x = 0; x <= c.W; x++)
            {
                bool on = x < c.W && c.A(x, y) >= 128;
                if (on && start < 0)
                    start = x;
                if (!on && start >= 0)
                {
                    if (x - 1 - start > best1 - best0)
                    {
                        best0 = start;
                        best1 = x - 1;
                    }
                    start = -1;
                }
            }
        }
    }
}
