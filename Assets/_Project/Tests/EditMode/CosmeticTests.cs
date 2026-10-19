using Musketeers.Data;
using NUnit.Framework;
using UnityEngine;

namespace Musketeers.Sim.Tests
{
    // Wardrobe data: loadout strings (PlayerPrefs now, LobbySlot later) and the shared placement math.
    public class CosmeticTests
    {
        [Test]
        public void Loadout_RoundTrips_AndBadTextFallsBack()
        {
            var l = new Loadout { Hat = "hat_cowboy", Face = "", Cape = "cape_red", Boots = "boots_crocs" };
            Assert.That(Loadout.Parse(l.Serialize(), default), Is.EqualTo(l));
            var fallback = new Loadout { Hat = "hat_plumed_blue" };
            Assert.That(Loadout.Parse("garbage", fallback), Is.EqualTo(fallback));
            Assert.That(Loadout.Parse("", fallback), Is.EqualTo(fallback));
            Assert.That(l.With(CosmeticSlot.Face, "face_monocle").Get(CosmeticSlot.Face), Is.EqualTo("face_monocle"));
        }

        [Test]
        public void Layout_UsesTheSlotAnchor_FrontValues_AndHeadTilt()
        {
            var tex = new Texture2D(4, 4);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 256);
            var pose = new PoseArt { Sprite = sprite, Anchors = new PoseAnchors { HeadTop = new Vector2(0f, 1.2f), Feet = Vector2.zero, HeadAngle = 90f } };
            var hat = ScriptableObject.CreateInstance<CosmeticDef>();
            hat.Slot = CosmeticSlot.Hat; hat.Sprite = sprite; hat.Offset = new Vector2(0f, 0.1f); hat.Scale = 2f;
            hat.FrontSprite = sprite; hat.FrontOffset = new Vector2(0f, 0.5f); hat.FrontScale = 3f;

            var p = CosmeticLayout.Place(pose, hat);
            Assert.That(p.Valid, Is.True);
            Assert.That(p.Position.x, Is.EqualTo(-0.1f).Within(1e-4), "offset turns with a tilted head");
            Assert.That(p.Position.y, Is.EqualTo(1.2f).Within(1e-4));
            Assert.That(p.Scale, Is.EqualTo(2f));

            pose.Front = true;
            pose.Anchors = new PoseAnchors { HeadTop = new Vector2(0f, 1.2f) };
            p = CosmeticLayout.Place(pose, hat);
            Assert.That(p.Position.y, Is.EqualTo(1.7f).Within(1e-4));
            Assert.That(p.Scale, Is.EqualTo(3f));

            var boots = ScriptableObject.CreateInstance<CosmeticDef>();
            boots.Slot = CosmeticSlot.Boots; boots.Sprite = sprite;
            pose.Anchors = new PoseAnchors { HideBoots = true };
            Assert.That(CosmeticLayout.Place(pose, boots).Valid, Is.False);
            Object.DestroyImmediate(hat); Object.DestroyImmediate(boots); Object.DestroyImmediate(tex);
        }

        [Test]
        public void FloorVariant_IsStableAndInRange()
        {
            for (int y = 0; y < 13; y++)
            for (int x = 0; x < 13; x++)
            {
                int v = TileVariant.Pick(x, y, 3);
                Assert.That(v, Is.InRange(0, 2));
                Assert.That(TileVariant.Pick(x, y, 3), Is.EqualTo(v));
            }
        }
    }
}
