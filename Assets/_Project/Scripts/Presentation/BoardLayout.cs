using UnityEngine;

namespace Musketeers.Presentation
{
    // Pure layout maths for the board art, kept free of scene objects so EditMode tests can cover it.
    public static class BoardLayout
    {
        public struct Placement
        {
            public Vector2 Center;   // world position of the background sprite's centre
            public float Scale;      // uniform scale of the background sprite
        }

        // Places a background sprite (spriteSize = its world size at scale 1) so that `arena` (normalized
        // 0-1 in the sprite's UV space, y up) lands on the board rect: boardW x boardH world units around
        // boardCenter. The scale is uniform and follows the board width; the arena's centre maps to the
        // board's centre, so any tiny height mismatch is split evenly top and bottom.
        public static Placement PlaceBackground(Rect arena, Vector2 spriteSize, float boardW, float boardH, Vector2 boardCenter)
        {
            float k = boardW / (arena.width * spriteSize.x);
            var offset = new Vector2((arena.center.x - 0.5f) * spriteSize.x * k, (arena.center.y - 0.5f) * spriteSize.y * k);
            return new Placement { Scale = k, Center = boardCenter - offset };
        }

        // Cover-fit camera for art pinned to the board: the whole art height fills the view (ortho size = half the
        // art's height), centred on the art. At the art's own aspect (16:9) the whole art shows; narrower views crop
        // its left and right; wider views run out of art sideways, which `NeedsMirror` reports (the rig tiles
        // mirrored copies there).
        public static void CoverFraming(Rect arena, Vector2 spriteSize, float boardW, float boardH, float aspect,
            out float orthoSize, out Vector2 center, out bool needsMirror)
        {
            var pl = PlaceBackground(arena, spriteSize, boardW, boardH, Vector2.zero);
            var art = spriteSize * pl.Scale;
            orthoSize = art.y * 0.5f;
            center = pl.Center;
            needsMirror = orthoSize * 2f * aspect > art.x + 1e-3f;
        }

        // The board's rect inside the view as a normalized viewport rect (0-1, y up), for laying the HUD around it.
        public static Rect ArenaViewport(Vector2 camCenter, float orthoSize, float aspect, float boardW, float boardH, Vector2 boardCenter)
        {
            float viewW = orthoSize * 2f * aspect, viewH = orthoSize * 2f;
            float x = (boardCenter.x - boardW * 0.5f - (camCenter.x - viewW * 0.5f)) / viewW;
            float y = (boardCenter.y - boardH * 0.5f - (camCenter.y - viewH * 0.5f)) / viewH;
            return new Rect(x, y, boardW / viewW, boardH / viewH);
        }

        // The same rect in UI Toolkit panel pixels (origin top-left, y down) for a panel of the given size.
        public static Rect ToPanelRect(Rect viewport, float panelW, float panelH) =>
            new Rect(viewport.x * panelW, (1f - viewport.yMax) * panelH, viewport.width * panelW, viewport.height * panelH);

        // Rotation (degrees) of a walkable tile's art. With `chess`, squares where x+y is odd turn 90 degrees.
        public static int FloorRotation(bool chess, int x, int y) => chess && ((x + y) & 1) == 1 ? 90 : 0;

        // Bridge tiles run their planks across the walking direction. A run that continues left or right
        // is walked horizontally, so its planks go vertical (90); a vertical run keeps the art's own
        // horizontal planks (0). A lone tile counts as horizontal.
        public static int BridgeRotation(bool left, bool right, bool up, bool down)
        {
            bool horizontal = left || right;
            bool vertical = up || down;
            return vertical && !horizontal ? 0 : 90;
        }

        // Checker tint multiplier: every other square a few percent darker.
        public static float CheckerShade(int x, int y, float darken) => ((x + y) & 1) == 1 ? 1f - darken : 1f;
    }
}
