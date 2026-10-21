using Musketeers.Data;
using Musketeers.Presentation;
using Musketeers.UI;
using NUnit.Framework;
using UnityEngine;

namespace Musketeers.Sim.Tests
{
    // FIXES_4E chunk B: fullscreen fit maths (§7), lobby rules range/default (§3.2), boots hidden (§3.3).
    public class Fix4EUiTests
    {
        static readonly Vector2[] Windows =
        {
            new Vector2(1920, 1080), new Vector2(1440, 900), new Vector2(2560, 1664), new Vector2(1280, 1024),
            new Vector2(3440, 1440), new Vector2(1024, 768), new Vector2(1280, 720), new Vector2(900, 1200),
        };

        [Test]
        public void PanelSize_KeepsTheWindowAspect_AndNeverShrinksBelowTheReference()
        {
            foreach (var w in Windows)
            {
                var p = ViewFit.PanelSize(w.x, w.y);
                Assert.That(p.x / p.y, Is.EqualTo(w.x / w.y).Within(1e-3f), w.ToString());
                Assert.That(p.x, Is.GreaterThanOrEqualTo(ViewFit.RefW - 0.01f), w.ToString());
                Assert.That(p.y, Is.GreaterThanOrEqualTo(ViewFit.RefH - 0.01f), w.ToString());
                Assert.That(Mathf.Min(p.x - ViewFit.RefW, p.y - ViewFit.RefH), Is.EqualTo(0f).Within(0.01f), "one axis matches the reference exactly");
            }
        }

        [Test]
        public void PanelMatch_UsesHeightWhenWiderThan16by9_ElseWidth()
        {
            Assert.That(ViewFit.PanelMatch(3440, 1440), Is.EqualTo(1f));
            Assert.That(ViewFit.PanelMatch(1920, 1080), Is.EqualTo(0f));
            Assert.That(ViewFit.PanelMatch(1280, 1024), Is.EqualTo(0f));
        }

        [Test]
        public void ArenaFit_PutsTheWholeBoardInsideTheSafeArea_AtEveryAspect()
        {
            foreach (var w in Windows)
            {
                foreach (var board in new[] { new Vector2(15, 9), new Vector2(9, 9), new Vector2(13, 13) })
                {
                    var panel = ViewFit.PanelSize(w.x, w.y);
                    // A preferred framing that crops (art cover framing at a narrow aspect): the fit must still win.
                    ViewFit.Fit(panel, board.x, board.y, new Vector2(2f, 0.5f), 5.5f, out float ortho, out var c);
                    var e = ViewFit.ToPanel(ViewFit.Expanded(board.x, board.y), c, ortho, panel);
                    Assert.That(ViewFit.Contains(ViewFit.SafeRect(panel), e), Is.True, $"{w} board {board}: {e} not in {ViewFit.SafeRect(panel)}");
                    // The board itself is of course inside the panel (nothing cropped).
                    var b = ViewFit.ToPanel(new Rect(-board.x * 0.5f, -board.y * 0.5f, board.x, board.y), c, ortho, panel);
                    Assert.That(ViewFit.Contains(new Rect(0, 0, panel.x, panel.y), b), Is.True, $"{w} board {board}");
                }
            }
        }

        [Test]
        public void ArenaFit_KeepsThePreferredFraming_WhenItAlreadyFits()
        {
            var panel = ViewFit.PanelSize(1920, 1080);
            ViewFit.Fit(panel, 15, 9, Vector2.zero, 8f, out float ortho, out var c);
            Assert.That(ortho, Is.EqualTo(8f).Within(1e-4f));
            Assert.That(c, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void ArenaFit_NeverZoomsInPastThePreferredFraming()
        {
            var panel = ViewFit.PanelSize(1920, 1080);
            ViewFit.Fit(panel, 15, 9, new Vector2(3f, 0f), 9f, out float ortho, out _);
            Assert.That(ortho, Is.GreaterThanOrEqualTo(9f - 1e-4f));
        }

        [Test]
        public void ArenaFit_ZoomsOutOnNarrowerWindows()
        {
            var wide = ViewFit.PanelSize(1920, 1080);
            var square = ViewFit.PanelSize(1280, 1024);
            ViewFit.Fit(wide, 15, 9, Vector2.zero, 0f, out float sWide, out _);
            ViewFit.Fit(square, 15, 9, Vector2.zero, 0f, out float sSquare, out _);
            Assert.That(sSquare, Is.GreaterThan(sWide), "4:3-ish needs a larger view than 16:9 to keep the board whole");
        }

        [Test]
        public void StageOrtho_ShowsTheWholeStageAtAnyAspect()
        {
            foreach (float aspect in new[] { 21f / 9f, 16f / 9f, 16f / 10f, 4f / 3f, 0.75f })
            {
                float s = ViewFit.StageOrtho(16f, 9f, aspect);
                Assert.That(s * 2f, Is.GreaterThanOrEqualTo(9f - 1e-4f), "height " + aspect);
                Assert.That(s * 2f * aspect, Is.GreaterThanOrEqualTo(16f - 1e-3f), "width " + aspect);
            }
        }

        [Test]
        public void HudRegions_NeverOverlapTheBoard_WhenTheFitPlacesIt()
        {
            foreach (var w in Windows)
            {
                var panel = ViewFit.PanelSize(w.x, w.y);
                ViewFit.Fit(panel, 15, 9, Vector2.zero, 0f, out float ortho, out var c);
                var board = ViewFit.ToPanel(new Rect(-7.5f, -4.5f, 15f, 9f), c, ortho, panel);
                var r = HudLayout.Compute(board, panel.x, panel.y);
                Assert.That(r.Overlap, Is.False, w.ToString());
                Assert.That(r.Left.xMax, Is.LessThanOrEqualTo(board.xMin + 0.5f), "left column " + w);
                Assert.That(r.Right.xMin, Is.GreaterThanOrEqualTo(board.xMax - 0.5f), "right column " + w);
            }
        }

        [Test]
        public void PointsToWin_RangeIs6To30_DefaultIs12()
        {
            Assert.That(LobbyModel.MinPointsToWin, Is.EqualTo(6));
            Assert.That(LobbyModel.MaxPointsToWin, Is.EqualTo(30));
            Assert.That(LobbyModel.DefaultPointsToWin, Is.EqualTo(12));
            var m = new LobbyModel(null, null, 2) { PointsToWin = 1 };
            Assert.That(m.PointsToWin, Is.EqualTo(6));
            m.PointsToWin = 99;
            Assert.That(m.PointsToWin, Is.EqualTo(30));
        }

        [Test]
        public void ResetRules_RestoresPoints12_AndHearts3()
        {
            var m = new LobbyModel(null, null, 2) { PointsToWin = 20, Hearts = 5 };
            m.ResetRules();
            Assert.That(m.PointsToWin, Is.EqualTo(12));
            Assert.That(m.Hearts, Is.EqualTo(3));
        }

        static CosmeticDef Item(CosmeticSlot slot, string id)
        {
            var tex = new Texture2D(4, 4);
            var it = ScriptableObject.CreateInstance<CosmeticDef>();
            it.Id = id;
            it.Slot = slot;
            it.Sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 256);
            return it;
        }

        [Test]
        public void Boots_AreNotSelectable_NotRandomised_AndSanitisedAway()
        {
            Assert.That(Loadout.Selectable(CosmeticSlot.Boots), Is.False);
            Assert.That(Loadout.Selectable(CosmeticSlot.Hat), Is.True);
            var catalog = ScriptableObject.CreateInstance<CosmeticCatalog>();
            catalog.Items.Add(Item(CosmeticSlot.Hat, "hat_a"));
            catalog.Items.Add(Item(CosmeticSlot.Boots, "boots_a"));
            Assert.That(catalog.BySlot(CosmeticSlot.Boots, true).Count, Is.EqualTo(1), "the boot items stay in the catalog");
            for (int i = 0; i < 30; i++)
                Assert.That(catalog.RandomLoadout().Boots ?? "", Is.EqualTo(""), "Surprise skips boots");
            var worn = new Loadout { Hat = "hat_a", Boots = "boots_a" };
            Assert.That(catalog.Sanitize(worn).Boots ?? "", Is.EqualTo(""), "everyone wears the body's own boots");
            Assert.That(catalog.Sanitize(worn).Hat, Is.EqualTo("hat_a"));
            Assert.That(catalog.Sanitize(worn, allowUnselectable: true).Boots, Is.EqualTo("boots_a"), "editor tools can still show boots");
            foreach (var it in catalog.Items)
                Object.DestroyImmediate(it);
            Object.DestroyImmediate(catalog);
        }
    }
}
