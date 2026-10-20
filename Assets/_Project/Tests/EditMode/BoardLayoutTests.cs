using Musketeers.Presentation;
using Musketeers.Sim;
using NUnit.Framework;
using UnityEngine;

namespace Musketeers.Sim.Tests
{
    public class BoardLayoutTests
    {
        [Test]
        public void ArenaRect_LandsOnBoardRect()
        {
            var arena = new Rect(0.15f, 0.11f, 0.70f, 0.747f);
            var sprite = new Vector2(6.53f, 3.67f);   // a 1672x940 sprite at 256 ppu
            var pl = BoardLayout.PlaceBackground(arena, sprite, 15f, 9f, Vector2.zero);

            // World position of the arena's left/right/bottom/top edges on the placed sprite.
            float left = pl.Center.x + (arena.xMin - 0.5f) * sprite.x * pl.Scale;
            float right = pl.Center.x + (arena.xMax - 0.5f) * sprite.x * pl.Scale;
            float bottom = pl.Center.y + (arena.yMin - 0.5f) * sprite.y * pl.Scale;
            float top = pl.Center.y + (arena.yMax - 0.5f) * sprite.y * pl.Scale;
            Assert.AreEqual(-7.5f, left, 1e-3f);
            Assert.AreEqual(7.5f, right, 1e-3f);
            Assert.AreEqual(0f, (bottom + top) * 0.5f, 1e-3f);   // centred on the board
            Assert.AreEqual(9f, top - bottom, 0.1f);             // height within a hair of the board's
        }

        [Test]
        public void CenteredArena_PlacesSpriteAtBoardCenter()
        {
            var pl = BoardLayout.PlaceBackground(new Rect(0.2f, 0.2f, 0.6f, 0.6f), new Vector2(4f, 2f), 6f, 3f, new Vector2(1f, 2f));
            Assert.AreEqual(1f, pl.Center.x, 1e-4f);
            Assert.AreEqual(2f, pl.Center.y, 1e-4f);
            Assert.AreEqual(2.5f, pl.Scale, 1e-4f);
        }

        [Test]
        public void ChessRotation_AlternatesBySquare()
        {
            Assert.AreEqual(0, BoardLayout.FloorRotation(true, 0, 0));
            Assert.AreEqual(90, BoardLayout.FloorRotation(true, 1, 0));
            Assert.AreEqual(90, BoardLayout.FloorRotation(true, 0, 1));
            Assert.AreEqual(0, BoardLayout.FloorRotation(true, 1, 1));
            Assert.AreEqual(0, BoardLayout.FloorRotation(false, 1, 0));
        }

        [Test]
        public void BridgeRotation_PlanksRunAcrossTheWalkingDirection()
        {
            Assert.AreEqual(90, BoardLayout.BridgeRotation(false, true, false, false));   // horizontal run
            Assert.AreEqual(90, BoardLayout.BridgeRotation(true, true, false, false));
            Assert.AreEqual(0, BoardLayout.BridgeRotation(false, false, true, false));    // vertical run
            Assert.AreEqual(90, BoardLayout.BridgeRotation(false, false, false, false));  // lone tile
        }

        [Test]
        public void CheckerShade_DarkensOddSquaresOnly()
        {
            Assert.AreEqual(1f, BoardLayout.CheckerShade(0, 0, 0.07f));
            Assert.AreEqual(0.93f, BoardLayout.CheckerShade(1, 0, 0.07f), 1e-5f);
        }

        [Test]
        public void BridgeMap_BridgeTilesAreHorizontalRuns()
        {
            var data = MapParser.Parse(System.IO.File.ReadAllText("Assets/_Project/Data/Maps/bridge.txt"));
            int bridges = 0;
            for (int y = 0; y < data.Board.Height; y++)
            for (int x = 0; x < data.Board.Width; x++)
            {
                if (!data.Bridges[y * data.Board.Width + x]) continue;
                bridges++;
                bool left = x > 0 && data.Bridges[y * data.Board.Width + x - 1];
                bool right = x < data.Board.Width - 1 && data.Bridges[y * data.Board.Width + x + 1];
                Assert.AreEqual(90, BoardLayout.BridgeRotation(left, right, false, false));
            }
            Assert.AreEqual(12, bridges);
        }
    
        static readonly Rect Arena = new Rect(0.15f, 0.11f, 0.70f, 0.747f);
        static readonly Vector2 Sprite1672 = new Vector2(6.53125f, 3.671875f);   // 1672x940 at 256 ppu

        [Test]
        public void CoverFraming_At16x9_ShowsTheWholeArt()
        {
            BoardLayout.CoverFraming(Arena, Sprite1672, 15f, 9f, 16f / 9f, out float size, out var center, out bool mirror);
            var pl = BoardLayout.PlaceBackground(Arena, Sprite1672, 15f, 9f, Vector2.zero);
            float artW = Sprite1672.x * pl.Scale, artH = Sprite1672.y * pl.Scale;
            Assert.AreEqual(artH * 0.5f, size, 1e-4f);
            Assert.AreEqual(artW, size * 2f * (16f / 9f), 0.02f);   // view width == art width
            Assert.AreEqual(pl.Center.x, center.x, 1e-4f);
            Assert.AreEqual(pl.Center.y, center.y, 1e-4f);
            Assert.IsFalse(mirror);
        }

        [Test]
        public void CoverFraming_Narrower_CropsSides_Wider_NeedsMirror()
        {
            BoardLayout.CoverFraming(Arena, Sprite1672, 15f, 9f, 4f / 3f, out float s43, out _, out bool m43);
            BoardLayout.CoverFraming(Arena, Sprite1672, 15f, 9f, 16f / 10f, out float s1610, out _, out bool m1610);
            BoardLayout.CoverFraming(Arena, Sprite1672, 15f, 9f, 21f / 9f, out float sUw, out _, out bool mUw);
            Assert.AreEqual(s43, s1610, 1e-5f);   // the height always fills the view
            Assert.AreEqual(s43, sUw, 1e-5f);
            Assert.IsFalse(m43);
            Assert.IsFalse(m1610);
            Assert.IsTrue(mUw);
            // At 4:3 the 15-tile board still fits across the view.
            Assert.Greater(s43 * 2f * (4f / 3f), 15f);
        }

        [Test]
        public void ArenaViewport_IsTheBoardInsideTheView()
        {
            BoardLayout.CoverFraming(Arena, Sprite1672, 15f, 9f, 16f / 9f, out float size, out var center, out _);
            var vp = BoardLayout.ArenaViewport(center, size, 16f / 9f, 15f, 9f, Vector2.zero);
            Assert.Greater(vp.x, 0f);
            Assert.Less(vp.xMax, 1f);
            Assert.Greater(vp.y, 0f);
            Assert.Less(vp.yMax, 1f);
            Assert.AreEqual(15f / 9f, vp.width * (16f / 9f) / vp.height, 1e-3f);   // square tiles on screen
            var panel = BoardLayout.ToPanelRect(vp, 1920f, 1080f);
            Assert.AreEqual(vp.width * 1920f, panel.width, 1e-3f);
            Assert.AreEqual((1f - vp.yMax) * 1080f, panel.y, 1e-3f);   // y flipped for UI Toolkit
        }
    }
}
