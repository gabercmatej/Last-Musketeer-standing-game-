using System.Collections.Generic;
using Musketeers.Data;
using Musketeers.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Musketeers.Sim.Tests
{
    // Plan 006 chunk 3: the CPU compositor mirrors the runtime rig (item masks, weapon states, hit testing).
    public class CompositorTests
    {
        const int Size = 512;
        const float Zoom = 0.75f;

        static CharacterDef Matej()
        {
            var roster = AssetDatabase.LoadAssetAtPath<CharacterRoster>("Assets/_Project/Data/CharacterRoster.asset");
            for (int i = 0; i < roster.Count; i++)
                if (roster.Get(i).Id == "matej") return roster.Get(i);
            Assert.Fail("matej not in roster");
            return null;
        }

        static CosmeticDef Helmet() =>
            AssetDatabase.LoadAssetAtPath<CosmeticCatalog>("Assets/_Project/Data/CosmeticCatalog.asset").Get("hat_helmet_pink");

        static int HatPixels(CharacterDef c, CosmeticDef hat)
        {
            RigCompositor.ClearCache();
            var o = new RigCompositor.Options { Weapons = false };
            var with = RigCompositor.Compose(c, PoseKind.Idle, new[] { hat }, Size, Zoom, o);
            var without = RigCompositor.Compose(c, PoseKind.Idle, new CosmeticDef[0], Size, Zoom, o);
            int n = 0;
            for (int i = 0; i < with.Px.Length; i++)
                if (with.Px[i].a > 128 && (without.Px[i].a < 128 || !with.Px[i].Equals(without.Px[i]))) n++;
            return n;
        }

        [Test]
        public void Compositor_ItemMask_CutsTheItem_InvertKeepsOnlyInside()
        {
            var c = Matej();
            var hat = Helmet();
            Assert.That(hat, Is.Not.Null);
            var saved = hat.Masks.Base;
            var savedPerPose = hat.Masks.PerPose;
            try
            {
                hat.Masks.PerPose = new List<PoseMask>();
                hat.Masks.Base = new BezierPath();
                int full = HatPixels(c, hat);
                Assert.That(full, Is.GreaterThan(200));

                var b = hat.Sprite.bounds;
                var top = new[]
                {
                    new Vector2(b.min.x - 1f, b.center.y), new Vector2(b.max.x + 1f, b.center.y),
                    new Vector2(b.max.x + 1f, b.max.y + 1f), new Vector2(b.min.x - 1f, b.max.y + 1f),
                };
                hat.Masks.Base = BezierPath.FromPolygon(top);
                int cut = HatPixels(c, hat);
                Assert.That(cut, Is.LessThan(full));

                hat.Masks.Base.Invert = true;
                int kept = HatPixels(c, hat);
                Assert.That(kept, Is.GreaterThan(0).And.LessThan(full));
                Assert.That(cut + kept, Is.InRange(full - full / 20, full + full / 20));
            }
            finally
            {
                hat.Masks.Base = saved;
                hat.Masks.PerPose = savedPerPose;
                RigCompositor.ClearCache();
            }
        }

        [Test]
        public void Compositor_AimedMusket_DrawsInFrontOfBody_RapierHidden()
        {
            var c = Matej();
            var layers = new List<RigCompositor.ComposedLayer>();
            RigCompositor.Compose(c, PoseKind.Idle, new CosmeticDef[0], Size, Zoom, new RigCompositor.Options { MusketAimed = true }, layers);
            int body = layers.FindIndex(l => l.Key == "body");
            int musket = layers.FindIndex(l => l.Key == "musket");
            Assert.That(body, Is.GreaterThanOrEqualTo(0));
            Assert.That(musket, Is.GreaterThanOrEqualTo(0));
            Assert.That(layers[musket].Placement.Order, Is.GreaterThan(layers[body].Placement.Order));
            Assert.That(layers[musket].Placement.Order, Is.LessThan(CosmeticLayout.Order(RigPart.Hand)));
            Assert.That(layers.Exists(l => l.Key == "rapier"), Is.False);

            RigCompositor.Compose(c, PoseKind.Idle, new CosmeticDef[0], Size, Zoom, new RigCompositor.Options(), layers);
            Assert.That(layers.Find(l => l.Key == "musket").Placement.Order, Is.LessThan(0));
            Assert.That(layers.Exists(l => l.Key == "rapier"), Is.True);
        }

        [Test]
        public void Compositor_HitTest_FindsHatOverBody()
        {
            var c = Matej();
            var hat = Helmet();
            var layers = new List<RigCompositor.ComposedLayer>();
            RigCompositor.Compose(c, PoseKind.Idle, new[] { hat }, Size, Zoom, new RigCompositor.Options { Weapons = false }, layers);
            var fit = c.FitFor(PoseKind.Idle);
            var hatLayer = layers.FindIndex(l => l.Key == hat.Id);
            Assert.That(hatLayer, Is.GreaterThanOrEqualTo(0));

            // Somewhere on the hat: scan its placed sprite for an opaque, unmasked point.
            var p = layers[hatLayer].Placement;
            int found = -1;
            var b = hat.Sprite.bounds;
            for (int iy = 1; iy < 20 && found != hatLayer; iy++)
            for (int ix = 1; ix < 20 && found != hatLayer; ix++)
            {
                var local = new Vector2(Mathf.Lerp(b.min.x, b.max.x, ix / 20f), Mathf.Lerp(b.min.y, b.max.y, iy / 20f));
                found = RigCompositor.HitTest(layers, RigCompositor.ToPose(p, local));
            }
            Assert.That(found, Is.EqualTo(hatLayer));

            // The torso carries no item.
            var torso = fit.Get(AnchorId.HeadTop).Position + new Vector2(0f, -1.2f);
            var torsoHit = RigCompositor.HitTest(layers, new Vector2(fit.Get(AnchorId.HeadTop).Position.x, fit.Get(AnchorId.FootFront).Position.y + 0.9f));
            Assert.That(torsoHit, Is.EqualTo(-1), "torso point " + torso);
        }

        [Test]
        public void Compositor_ToLocalToPose_RoundTrip()
        {
            foreach (var sx in new[] { 0.7f, -0.7f })
            {
                var p = new LayerPlacement { Valid = true, Position = new Vector2(0.3f, 0.9f), Angle = 37f, Scale = new Vector2(sx, 0.7f) };
                var pt = new Vector2(0.11f, 0.52f);
                var back = RigCompositor.ToPose(p, RigCompositor.ToLocal(p, pt));
                Assert.That(Vector2.Distance(back, pt), Is.LessThan(1e-4f));
            }
        }
    }
}
