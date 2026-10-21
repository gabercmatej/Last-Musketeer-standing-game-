using Musketeers.Data;
using Musketeers.Game;
using Musketeers.Presentation;
using Musketeers.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Musketeers.Sim.Tests
{
    // Session 4F small fixes: glyphs, lobby line-up, tips, background imports, per-character wardrobe.
    public class Fix4FTests
    {
        const string Fredoka = "Assets/_Project/Art/Fonts/Fredoka-SemiBold.ttf";

        static void AssertFontHas(Font font, string text, string where)
        {
            foreach (char ch in text)
            {
                if (ch == '\n' || ch == '\r')
                    continue;
                Assert.That(font.HasCharacter(ch), Is.True, $"'{ch}' (U+{(int)ch:X4}) missing from Fredoka in {where}");
            }
        }

        [Test]
        public void SlotLabels_UseOnlyGlyphsTheFontHas()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(Fredoka);
            Assert.That(font, Is.Not.Null);
            foreach (var t in new[] { ActionType.Move, ActionType.Dash, ActionType.Fire, ActionType.Shove, ActionType.Signature })
                foreach (var d in DirExt.All)
                {
                    string label = ActionInfo.SlotLabel(new PlannedAction(t, d), null);
                    AssertFontHas(font, label, label);
                    Assert.That(label.Length, Is.LessThanOrEqualTo(15), label);
                }
            AssertFontHas(font, ActionInfo.SlotLabel(PlannedAction.ThrowKeg(new Int2(3, 4)), null), "keg label");
        }

        [Test]
        public void LoadingTips_AreCurrent_AndRenderable()
        {
            var tips = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Data/Text/tips.txt");
            var font = AssetDatabase.LoadAssetAtPath<Font>(Fredoka);
            Assert.That(tips, Is.Not.Null);
            StringAssert.DoesNotContain("Z, X, C", tips.text, "emotes are the chat buttons now (4F §9)");
            StringAssert.Contains("30 seconds", tips.text);
            StringAssert.Contains("5 Grit", tips.text);
            AssertFontHas(font, tips.text, "tips.txt");
        }

        [Test]
        public void LobbyLineup_StaysLeftOfTheSettingsPanel_EvenlySpaced([Values(2, 3, 4)] int count)
        {
            // The panel's left edge is at stage x 3.58 at any aspect: 1280 - 14 - 340 px of the 1280 px reference.
            const float panelLeft = (1280f - 14f - 340f) / 1280f * StageView.Width - StageView.Width / 2f;
            float gap = -1f;
            for (int i = 0; i < count; i++)
            {
                float x = StageView.Spot(i, count);
                Assert.That(x, Is.GreaterThan(-StageView.Width / 2f + 0.6f), $"{count}: musketeer {i} off the left edge");
                Assert.That(x + 0.6f, Is.LessThan(panelLeft), $"{count}: musketeer {i} under the panel");
                if (i > 0)
                {
                    float g = x - StageView.Spot(i - 1, count);
                    if (gap >= 0f)
                        Assert.That(g, Is.EqualTo(gap).Within(0.001f), "even spacing");
                    gap = g;
                    Assert.That(g, Is.GreaterThanOrEqualTo(2.5f), "room for the names");
                }
            }
        }

        [Test]
        public void FullScreenBackgrounds_ImportSharp()
        {
            foreach (var path in new[]
            {
                "Assets/_Project/Art/UI/backgrounds/menu.jpg", "Assets/_Project/Art/UI/backgrounds/splash.jpg",
                "Assets/_Project/Art/UI/backgrounds/lobby.jpg", "Assets/_Project/Art/Board/courtyard/background.jpg",
            })
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(ti, Is.Not.Null, path);
                Assert.That(ti.crunchedCompression, Is.False, path);
                Assert.That(ti.textureCompression, Is.EqualTo(TextureImporterCompression.CompressedHQ), path);
                Assert.That(ti.mipmapEnabled, Is.False, path);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.That(ti.maxTextureSize, Is.GreaterThanOrEqualTo(Mathf.Max(tex.width, tex.height)), path + ": imported below source size");
            }
        }

        [Test]
        public void Wardrobe_IsRememberedPerCharacter()
        {
            // High character indices so the test never touches a real player's saved outfit.
            const int nik = 90, matic = 91;
            try
            {
                var m = new LobbyModel(null, null, 2) { Character = nik };
                m.Equip(CosmeticSlot.Cape, "cape_red");
                Assert.That(m.MyLoadout.Cape, Is.EqualTo("cape_red"));
                m.Character = matic;
                Assert.That(string.IsNullOrEmpty(m.MyLoadout.Cape), Is.True, "Matic doesn't inherit Nik's cape");
                m.Equip(CosmeticSlot.Hat, "hat_feather");
                // A new session (new model) still has both outfits.
                var later = new LobbyModel(null, null, 2) { Character = nik };
                Assert.That(later.MyLoadout.Cape, Is.EqualTo("cape_red"));
                later.Character = matic;
                Assert.That(later.MyLoadout.Hat, Is.EqualTo("hat_feather"));
                Assert.That(string.IsNullOrEmpty(later.MyLoadout.Cape), Is.True);
            }
            finally
            {
                PlayerPrefs.DeleteKey("Musketeers.Loadout." + nik);
                PlayerPrefs.DeleteKey("Musketeers.Loadout." + matic);
            }
        }

        [Test]
        public void Boots_StayUnequippable()
        {
            Assert.That(Loadout.Selectable(CosmeticSlot.Boots), Is.False);
            Assert.That(Loadout.Selectable(CosmeticSlot.Cape), Is.True);
        }
    }
}
