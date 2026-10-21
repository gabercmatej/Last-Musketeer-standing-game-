using UnityEngine;

namespace Musketeers.Presentation
{
    // Pure maths for FIXES_4E §7 (fullscreen without cropping). The UI Toolkit panel scales so a 1280x720 reference
    // always fits inside the window (scale = min(w/1280, h/720)), so its size in panel units is >= 1280x720 and
    // matches the window's aspect. The board plus its margins is then fitted inside a "safe" rectangle of that panel
    // (the area between the HUD bands), zooming out and re-centring when the window is narrower or shorter than the
    // art's own framing. Nothing is ever cropped: spare space shows the (mirrored) background art.
    public static class ViewFit
    {
        public const float RefW = 1280f, RefH = 720f;
        // HUD space reserved around the board, in panel units: the top strip, the bottom bar and a column each side
        // (HudLayout.Pad + MinCol + Gap).
        public const float TopReserve = 40f, BottomReserve = 56f, SideReserve = 142f;
        // World units kept visible around the board: names and pips each side and below, the top row's heads above.
        public const float SideMargin = 0.45f, BottomMargin = 0.35f, TopMargin = 1.05f;

        // The panel's size in its own units for a window of this size (the 1280x720 reference scaled to fit inside it).
        public static Vector2 PanelSize(float screenW, float screenH)
        {
            if (screenW < 1f || screenH < 1f)
                return new Vector2(RefW, RefH);
            float k = Mathf.Min(screenW / RefW, screenH / RefH);
            return new Vector2(screenW / k, screenH / k);
        }

        // PanelSettings.match for that scale: height when the window is wider than 16:9, width otherwise
        // (log-interpolated match 0 or 1 equals the min() scale above).
        public static float PanelMatch(float screenW, float screenH) =>
            screenH > 0f && screenW / screenH > RefW / RefH ? 1f : 0f;

        public static Rect SafeRect(Vector2 panel) =>
            new Rect(SideReserve, TopReserve, Mathf.Max(1f, panel.x - SideReserve * 2f), Mathf.Max(1f, panel.y - TopReserve - BottomReserve));

        // The world rect (board centred on the origin) that must stay inside the safe rect.
        public static Rect Expanded(float boardW, float boardH) =>
            new Rect(-boardW * 0.5f - SideMargin, -boardH * 0.5f - BottomMargin, boardW + SideMargin * 2f, boardH + BottomMargin + TopMargin);

        // Maps a world rect to panel units (origin top-left, y down) for a camera at `center` with half-height `ortho`.
        public static Rect ToPanel(Rect world, Vector2 center, float ortho, Vector2 panel)
        {
            float p = panel.y / (2f * ortho);
            float x = panel.x * 0.5f + (world.xMin - center.x) * p;
            float yTop = panel.y * 0.5f - (world.yMax - center.y) * p;
            return new Rect(x, yTop, world.width * p, world.height * p);
        }

        public static bool Contains(Rect outer, Rect inner, float eps = 0.5f) =>
            inner.xMin >= outer.xMin - eps && inner.xMax <= outer.xMax + eps && inner.yMin >= outer.yMin - eps && inner.yMax <= outer.yMax + eps;

        // The camera for the arena. (center0, ortho0) is the preferred framing (the art's cover framing, ortho0 <= 0
        // for none): kept as is when the expanded board already sits inside the safe rect, otherwise the camera
        // zooms out (never in) just enough and centres the expanded board in the safe rect.
        public static void Fit(Vector2 panel, float boardW, float boardH, Vector2 center0, float ortho0, out float ortho, out Vector2 center)
        {
            var safe = SafeRect(panel);
            var e = Expanded(boardW, boardH);
            if (ortho0 > 0f && Contains(safe, ToPanel(e, center0, ortho0, panel)))
            {
                ortho = ortho0;
                center = center0;
                return;
            }
            float p = Mathf.Min(safe.width / e.width, safe.height / e.height);   // panel units per world unit
            ortho = panel.y / (2f * p);
            if (ortho0 > 0f && ortho < ortho0)
            {
                ortho = ortho0;
                p = panel.y / (2f * ortho);
            }
            // Put the expanded board's centre on the safe rect's centre.
            center = new Vector2(
                e.center.x - (safe.center.x - panel.x * 0.5f) / p,
                e.center.y + (safe.center.y - panel.y * 0.5f) / p);
        }

        // The stage (lobby / results) is shown whole: the half-height that fits a stageW x stageH rectangle in a view of this aspect.
        public static float StageOrtho(float stageW, float stageH, float aspect) =>
            Mathf.Max(0.5f, Mathf.Max(stageH * 0.5f, stageW / Mathf.Max(0.1f, aspect) * 0.5f));
    }
}
