using Musketeers.Data;
using Musketeers.UI;
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

        static Sprite TestSprite(out Texture2D tex)
        {
            tex = new Texture2D(4, 4);
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 256);
        }

        static CosmeticDef Item(CosmeticSlot slot, Sprite sprite)
        {
            var it = ScriptableObject.CreateInstance<CosmeticDef>();
            it.Id = slot.ToString().ToLowerInvariant();
            it.Slot = slot;
            it.Sprite = sprite;
            return it;
        }

        [Test]
        public void Layout_AnchorTurnsAndScalesTheItemFit()
        {
            var sprite = TestSprite(out var tex);
            var pose = new PoseArt { Sprite = sprite };
            var fit = new PoseFit { HeadTop = new Anchor(0f, 1.2f, 90f, 2f) };
            var hat = Item(CosmeticSlot.Hat, sprite);
            hat.Offset = new Vector2(0f, 0.1f); hat.Scale = 1.5f; hat.Rotation = 10f;

            var p = CosmeticLayout.Place(pose, fit, "matej", PoseKind.Idle, hat, false);
            Assert.That(p.Valid, Is.True);
            Assert.That(p.Position.x, Is.EqualTo(-0.2f).Within(1e-4), "offset turns with the head and scales with its size");
            Assert.That(p.Position.y, Is.EqualTo(1.2f).Within(1e-4));
            Assert.That(p.Scale.x, Is.EqualTo(3f).Within(1e-4), "anchor scale × item scale");
            Assert.That(p.Angle, Is.EqualTo(100f).Within(1e-4));
            Assert.That(p.Order, Is.GreaterThan(CosmeticLayout.Order(RigPart.Body)));
            Object.DestroyImmediate(hat); Object.DestroyImmediate(tex);
        }

        [Test]
        public void Overrides_MostSpecificWins()
        {
            var sprite = TestSprite(out var tex);
            var hat = Item(CosmeticSlot.Hat, sprite);
            hat.Scale = 1f;
            hat.Overrides.Add(new FitOverride { Character = "nik", AnyPose = true, Fit = new ItemFit(0, 0, 2f) });
            hat.Overrides.Add(new FitOverride { Character = "", AnyPose = false, Pose = PoseKind.Victory, Fit = new ItemFit(0, 0, 3f) });
            hat.Overrides.Add(new FitOverride { Character = "nik", AnyPose = false, Pose = PoseKind.Hurt, Fit = new ItemFit(0, 0, 4f) });

            Assert.That(hat.FitFor("matej", PoseKind.Idle).Scale, Is.EqualTo(1f), "global");
            Assert.That(hat.FitFor("nik", PoseKind.Idle).Scale, Is.EqualTo(2f), "musketeer");
            Assert.That(hat.FitFor("matej", PoseKind.Victory).Scale, Is.EqualTo(3f), "pose");
            Assert.That(hat.FitFor("nik", PoseKind.Victory).Scale, Is.EqualTo(3f), "pose beats musketeer");
            Assert.That(hat.FitFor("nik", PoseKind.Hurt).Scale, Is.EqualTo(4f), "musketeer + pose beats both");
            Object.DestroyImmediate(hat); Object.DestroyImmediate(tex);
        }

        [Test]
        public void Boots_AreOneBootDrawnAtBothFeet_BackOneDarkerAndBehind()
        {
            var sprite = TestSprite(out var tex);
            var pose = new PoseArt { Sprite = sprite };
            var fit = new PoseFit { FootBack = new Anchor(-0.1f, 0f, -30f), FootFront = new Anchor(0.1f, 0f) };
            var boots = Item(CosmeticSlot.Boots, sprite);
            boots.Offset = new Vector2(0f, 0.1f);

            var back = CosmeticLayout.Place(pose, fit, "matej", PoseKind.Idle, boots, true);
            var front = CosmeticLayout.Place(pose, fit, "matej", PoseKind.Idle, boots, false);
            Assert.That(back.Order, Is.LessThan(CosmeticLayout.Order(RigPart.Body)));
            Assert.That(front.Order, Is.GreaterThan(CosmeticLayout.Order(RigPart.Body)));
            Assert.That(back.Shade, Is.LessThan(1f));
            Assert.That(front.Shade, Is.EqualTo(1f));
            Assert.That(back.Angle, Is.EqualTo(-30f).Within(1e-4), "the boot follows its foot");
            Assert.That(front.Position, Is.EqualTo(new Vector2(0.1f, 0.1f)));

            // Front-facing pose: the far boot is the near one mirrored (left and right foot).
            pose.Front = true;
            back = CosmeticLayout.Place(pose, fit, "matej", PoseKind.Victory, boots, true);
            Assert.That(back.Scale.x, Is.LessThan(0f));
            Object.DestroyImmediate(boots); Object.DestroyImmediate(tex);
        }

        [Test]
        public void Weapons_MusketBehindBodyInTheBackHand_RapierInFront()
        {
            var sprite = TestSprite(out var tex);
            var fit = new PoseFit { HandBack = new Anchor(-0.2f, 0.4f, -38f, 0.7f), HandFront = new Anchor(0.2f, 0.4f, -60f, 0.7f) };
            var musket = WeaponDef.Temporary(sprite, true);
            var rapier = WeaponDef.Temporary(sprite, false);
            var m = CosmeticLayout.Weapon(fit, musket, "matej", PoseKind.Idle, false);
            var r = CosmeticLayout.Weapon(fit, rapier, "matej", PoseKind.Idle, false, 62f - fit.HandFront.Rotation);
            Assert.That(m.Position, Is.EqualTo(new Vector2(-0.2f, 0.4f)));
            Assert.That(m.Order, Is.LessThan(CosmeticLayout.Order(RigPart.Body)));
            Assert.That(r.Order, Is.GreaterThan(CosmeticLayout.Order(RigPart.Body)));
            Assert.That(CosmeticLayout.Order(RigPart.Hand), Is.GreaterThan(r.Order), "the hand overlay wraps the grip");
            Assert.That(r.Angle, Is.EqualTo(62f), "an explicit angle (parry) overrides the rest angle");
            fit.NoMusket = true;
            Assert.That(CosmeticLayout.Weapon(fit, musket, "matej", PoseKind.Idle, false).Valid, Is.False);
            Object.DestroyImmediate(musket); Object.DestroyImmediate(rapier);
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void LayerOrder_MatchesTheSpec()
        {
            // FIXES_v0.2 §1.2: cape → back boot → musket → body → front boot → rapier → front-hand overlay.
            var order = new[] { RigPart.Cape, RigPart.BootBack, RigPart.Musket, RigPart.Body, RigPart.BootFront, RigPart.Rapier, RigPart.Hand };
            for (int i = 1; i < order.Length; i++)
                Assert.That(CosmeticLayout.Order(order[i]), Is.GreaterThan(CosmeticLayout.Order(order[i - 1])), order[i].ToString());
        }

        [Test]
        public void PolygonMask_ContainsAndBuildsASprite()
        {
            var poly = new[] { new Vector2(0f, 0f), new Vector2(0.4f, 0f), new Vector2(0.4f, 0.2f), new Vector2(0.2f, 0.05f), new Vector2(0f, 0.2f) };
            Assert.That(PolygonMask.Contains(poly, new Vector2(0.2f, 0.02f)), Is.True);
            Assert.That(PolygonMask.Contains(poly, new Vector2(0.2f, 0.15f)), Is.False, "concave notch");
            Assert.That(PolygonMask.Contains(poly, new Vector2(0.5f, 0.1f)), Is.False);
            var s = PolygonMask.Sprite(poly, out var origin);
            Assert.That(s, Is.Not.Null);
            Assert.That(origin, Is.EqualTo(Vector2.zero));
            Assert.That(s.bounds.size.x, Is.EqualTo(0.4f).Within(0.02f));
            Assert.That(PolygonMask.Sprite(poly, out _), Is.SameAs(s), "cached per shape");
            Assert.That(PolygonMask.Sprite(new Vector2[2], out _), Is.Null);
        }

        [Test]
        public void Catalog_HidesDisabledItems_AndSanitizeDropsThem()
        {
            var sprite = TestSprite(out var tex);
            var catalog = ScriptableObject.CreateInstance<CosmeticCatalog>();
            var on = Item(CosmeticSlot.Hat, sprite); on.Id = "hat_on";
            var off = Item(CosmeticSlot.Hat, sprite); off.Id = "hat_off"; off.EnabledInV1 = false;
            catalog.Items.Add(on); catalog.Items.Add(off);

            Assert.That(catalog.BySlot(CosmeticSlot.Hat).Count, Is.EqualTo(2), "the Wardrobe Studio sees everything");
            Assert.That(catalog.BySlot(CosmeticSlot.Hat, true), Is.EqualTo(new[] { on }), "the wardrobe sees enabled items");
            Assert.That(catalog.Sanitize(new Loadout { Hat = "hat_off" }).Hat, Is.EqualTo(""));
            Assert.That(catalog.Sanitize(new Loadout { Hat = "hat_on" }).Hat, Is.EqualTo("hat_on"));
            for (int i = 0; i < 20; i++)
                Assert.That(catalog.RandomLoadout().Hat, Is.EqualTo("hat_on"));
            Object.DestroyImmediate(on); Object.DestroyImmediate(off); Object.DestroyImmediate(catalog); Object.DestroyImmediate(tex);
        }

        [Test]
        public void Bots_NeverWearCosmetics()
        {
            var sprite = TestSprite(out var tex);
            var catalog = ScriptableObject.CreateInstance<CosmeticCatalog>();
            var hat = Item(CosmeticSlot.Hat, sprite); hat.Id = "hat_x";
            catalog.Items.Add(hat);
            var model = new LobbyModel(null, catalog, 1);
            for (int slot = 1; slot < 4; slot++)
            {
                model.RerollBot(slot);
                Assert.That(model.LoadoutFor(slot), Is.EqualTo(default(Loadout)), "bot " + slot);
            }
            Object.DestroyImmediate(hat); Object.DestroyImmediate(catalog); Object.DestroyImmediate(tex);
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
