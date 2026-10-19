using Musketeers.Data;
using NUnit.Framework;
using UnityEditor;

namespace Musketeers.Sim.Tests
{
    // Import Art wires art by file name; the game falls back to placeholders silently, so these tests
    // are what notice a renamed or missing file.
    public class ArtWiringTests
    {
        static T Load<T>(string path) where T : UnityEngine.Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(a, Is.Not.Null, path + " (run Tools/Musketeers/Import Art)");
            return a;
        }

        [Test]
        public void EveryMusketeer_HasPosesPortraitAnchorsAndAnOutfit()
        {
            var roster = Load<CharacterRoster>("Assets/_Project/Data/CharacterRoster.asset");
            var catalog = Load<CosmeticCatalog>("Assets/_Project/Data/CosmeticCatalog.asset");
            foreach (var c in roster.Characters)
            {
                Assert.That(c.Portrait, Is.Not.Null, c.Id + " portrait");
                Assert.That(c.Fit, Is.Not.Null, c.Id + " fit asset");
                foreach (var kind in new[] { PoseKind.Idle, PoseKind.Hurt, PoseKind.Victory, PoseKind.Lunge })
                {
                    Assert.That(c.Art(kind).Sprite, Is.Not.Null, $"{c.Id} {kind} pose");
                    var fit = c.Fit.Get(kind);
                    Assert.That(fit, Is.Not.Null, $"{c.Id} {kind} anchors");
                    Assert.That(fit.BootPathBack.Valid && fit.BootPathFront.Valid, Is.True, $"{c.Id} {kind} boot masks");
                    Assert.That(fit.HandPath.Valid, Is.True, $"{c.Id} {kind} hand mask");
                }
                Assert.That(c.Victory.Front, Is.True, "the victory pose faces the camera");
                foreach (var slot in Loadout.Slots)
                {
                    string id = c.DefaultLoadout.Get(slot);
                    if (id != "")
                    {
                        Assert.That(catalog.Get(id), Is.Not.Null, $"{c.Id} default {slot} {id}");
                        Assert.That(catalog.Get(id).EnabledInV1, Is.True, $"{c.Id} default {slot} {id} is enabled in v1");
                    }
                }
            }
        }

        [Test]
        public void EveryCosmetic_HasSideAndFrontSprites()
        {
            var catalog = Load<CosmeticCatalog>("Assets/_Project/Data/CosmeticCatalog.asset");
            Assert.That(catalog.Items.Count, Is.EqualTo(36));
            foreach (var item in catalog.Items)
            {
                Assert.That(item.Sprite, Is.Not.Null, item.Id);
                Assert.That(item.FrontSprite, Is.Not.Null, item.Id + " front");
                Assert.That(item.Scale, Is.GreaterThan(0f), item.Id);
            }
            foreach (var slot in Loadout.Slots)
                Assert.That(catalog.BySlot(slot).Count, Is.GreaterThan(0), slot.ToString());
        }

        [Test]
        public void BothMaps_HaveLayoutAndTileset()
        {
            var maps = Load<MapCatalog>("Assets/_Project/Data/MapCatalog.asset");
            Assert.That(maps.Count, Is.EqualTo(2));
            foreach (var m in maps.Maps)
            {
                Assert.That(m.Layout, Is.Not.Null, m.Id);
                Assert.DoesNotThrow(() => MapParser.Parse(m.Layout.text), m.Id);
                Assert.That(m.Theme.FloorVariants.Length, Is.EqualTo(3), m.Id);
                Assert.That(m.Theme.Pillar, Is.Not.Null, m.Id);
                Assert.That(m.Theme.Crate, Is.Not.Null, m.Id);
                Assert.That(m.Theme.Pit, Is.Not.Null, m.Id);
                Assert.That(m.Theme.BackgroundArt, Is.Not.Null, m.Id);
            }
        }

        [Test]
        public void SharedArt_HasEveryKeyTheCodeUses()
        {
            var art = Load<GameArt>("Assets/_Project/Data/GameArt.asset");
            string[] keys =
            {
                GameArt.Keg, GameArt.Crown, GameArt.Shadow, GameArt.Hook, GameArt.Musket, GameArt.Rapier,
                GameArt.MuzzleFlash, GameArt.Smoke, GameArt.Spark, GameArt.Explosion, GameArt.Dust, GameArt.Splash,
                GameArt.Stars, GameArt.SwapSwirl, GameArt.Logo, GameArt.KeyArt, GameArt.LobbyBackground, GameArt.SplashArt,
                GameArt.Bubble, GameArt.HeartFull, GameArt.HeartEmpty, GameArt.Grit, GameArt.MusketLoaded,
                GameArt.MusketEmpty, GameArt.LockIn, GameArt.CrownSmall,
            };
            foreach (var k in keys)
                Assert.That(art.Get(k), Is.Not.Null, k);
            foreach (var k in GameArt.Emotes)
                Assert.That(art.Get(k), Is.Not.Null, k);
            foreach (var n in new[] { "move", "dash", "fire", "reload", "parry", "shove", "keg", "catch_breath", "grappin", "switcheroo", "double_charge", "bull_rush" })
                Assert.That(art.Get(GameArt.ActionIcon(n)), Is.Not.Null, n);
            foreach (var b in new[] { "sharpshooter", "wall_of_steel", "bonk_lord", "swan_dive", "regicide", "powder_monkey", "pacifist", "click_click" })
                Assert.That(art.Get(GameArt.Badge(b)), Is.Not.Null, b);
        }
    }
}
