using UnityEngine;

namespace Musketeers.Presentation
{
    // Pure maths for placing the HUD around the arena (UI Toolkit panel pixels, origin top-left, y down).
    // Four regions: a top strip above the board, a bottom strip below it and a column on each side. When a side
    // margin is too narrow for its column, the column keeps a minimum width and overlaps the board edge (the
    // panels are semi-transparent) instead of breaking.
    public static class HudLayout
    {
        public const float Pad = 8f, Gap = 6f;
        public const float MinCol = 128f, MaxCol = 200f;
        public const float MinTop = 40f, MaxTop = 70f;
        public const float MinBottom = 60f, MaxBottom = 80f;
        public const float CompactBelow = 160f;

        public struct Regions
        {
            public Rect Top, Bottom, Left, Right;
            public float RoundX;       // left edge of the Round/Turn block in the top strip
            public bool Compact;       // a column is narrow: use smaller text
            public bool Overlap;       // a column reaches onto the board
        }

        public static Regions Compute(Rect arena, float panelW, float panelH)
        {
            float topH = Mathf.Clamp(arena.yMin, MinTop, MaxTop);
            float botH = Mathf.Clamp(panelH - arena.yMax, MinBottom, MaxBottom);
            float leftRoom = arena.xMin - Pad - Gap;
            float rightRoom = panelW - arena.xMax - Pad - Gap;
            float leftW = Mathf.Clamp(leftRoom, MinCol, MaxCol);
            float rightW = Mathf.Clamp(rightRoom, MinCol, MaxCol);
            float colH = Mathf.Max(0f, panelH - botH - Pad * 2f);
            var r = new Regions
            {
                Top = new Rect(0f, 0f, panelW, topH),
                Bottom = new Rect(0f, panelH - botH, panelW, botH),
                Left = new Rect(Pad, Pad, leftW, colH),
                Right = new Rect(panelW - Pad - rightW, Pad, rightW, colH),
                Compact = leftW < CompactBelow || rightW < CompactBelow,
                Overlap = leftRoom < MinCol || rightRoom < MinCol,
            };
            r.RoundX = Mathf.Max(arena.xMin, r.Left.xMax + Gap);
            return r;
        }

        // The announcer banner lives in the top strip, to the right of the timer, and never on the board: its bottom
        // edge stays at or above the arena's top edge (it shrinks to fit when the margin is small, 16:10 and 4:3).
        // r = the regions from Compute; height/width are the wanted size; the timer block keeps TimerClear px each side of centre.
        public const float TimerClear = 78f, BannerMinW = 120f, BannerMinH = 14f;

        public static Rect BannerRect(Rect arena, Regions r, float panelW, float height, float width)
        {
            float top = 2f;
            float room = Mathf.Min(r.Top.height, Mathf.Max(arena.yMin, 0f)) - top - 2f;
            float h = Mathf.Clamp(Mathf.Min(height, room), BannerMinH, Mathf.Max(BannerMinH, height));
            if (top + h > arena.yMin)
                h = Mathf.Max(0f, arena.yMin - top);
            float left = panelW * 0.5f + TimerClear;
            float right = Mathf.Min(r.Right.xMin - Gap, panelW - Pad);
            if (right - left < BannerMinW)
            {
                // No room beside the timer: centre it in the strip (the timer is hidden behind a translucent flash).
                left = Mathf.Max(Pad, panelW * 0.5f - width * 0.5f);
                right = Mathf.Min(panelW - Pad, left + width);
            }
            float w = Mathf.Min(width, right - left);
            return new Rect(left, top, Mathf.Max(0f, w), h);
        }
    }
}
