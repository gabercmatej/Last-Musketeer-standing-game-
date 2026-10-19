using Musketeers.Data;
using Musketeers.EditorTools.ArtImport;
using Musketeers.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Musketeers.Sim.Tests
{
    // Plan 006 chunk 2: FitDefaults.json round trip, per-item masks, weapon layering and weapon defaults.
    public class StudioRuntimeTests
    {
        static Sprite TestSprite()
        {
            var tex = new Texture2D(4, 4);
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
        }

        static BezierPath Square(float size = 1f) =>
            BezierPath.FromPolygon(new[] { new Vector2(0, 0), new Vector2(size, 0), new Vector2(size, size), new Vector2(0, size) });

        static CharacterFit TestFit()
        {
            var fit = ScriptableObject.CreateInstance<CharacterFit>();
            var idle = fit.GetOrAdd(PoseKind.Idle);
            idle.HandBack = new Anchor(-0.2f, 0.4f, -38f, 0.72f);
            idle.HandFront = new Anchor(0.15f, 0.4f, -57f, 0.6f);
            var hurt = fit.GetOrAdd(PoseKind.Hurt);
            hurt.HandBack = new Anchor(-0.2f, 0.4f, -20f, 0.7f);
            hurt.HandFront = new Anchor(0.1f, 0.4f, -30f, 0.5f);
            return fit;
        }

        [Test]
        public void Defaults_RoundTrip_KeepsMasksCheckedAndWeapons()
        {
            var mask = Square();
            var pt = mask.Points[2];
            pt.P = new Vector2(0.25f, 0.5f);
            mask.Points[2] = pt;
            mask.Closed = true;
            mask.Invert = true;
            var item = new CosmeticTuning.ItemDefaults { Id = "hat_x", Fit = new ItemFit(0, 0, 1) };
            item.Masks.Base = mask;
            item.Checked = 0b101;
            var active = new FitSet { Name = "active" };
            FitEdit.Set(active, PoseKind.Hurt, "", new ItemFit(0.1f, 0.2f, 1.5f, 33f));
            var d = new CosmeticTuning.Defaults();
            d.Items.Add(item);
            d.Weapons.Add(new CosmeticTuning.WeaponDefaults { Id = "musket", Rest = new FitSet { Name = "rest" }, Active = active });

            string json = CosmeticTuning.ToJson(d);
            Assert.That(CosmeticTuning.ToJson(d), Is.EqualTo(json), "deterministic");
            var back = CosmeticTuning.FromJson(json);
            var bi = back.Items[0];
            Assert.That(bi.Masks.Base.Points[2].P, Is.EqualTo(new Vector2(0.25f, 0.5f)));
            Assert.That(bi.Masks.Base.Invert, Is.True);
            Assert.That(bi.Checked, Is.EqualTo(0b101));
            var bw = back.Weapons[0];
            Assert.That(bw.Id, Is.EqualTo("musket"));
            Assert.That(FitEdit.Resolve(bw.Active, "matej", PoseKind.Hurt).Rotation, Is.EqualTo(33f));
            Assert.That(CosmeticTuning.ToJson(back), Is.EqualTo(json));
        }

        [Test]
        public void Rig_ItemMask_CutsOnlyItsOwnRenderer()
        {
            var go = new GameObject("t");
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 123;
                var mask = new GameObject("m").AddComponent<SpriteMask>();
                mask.transform.SetParent(go.transform, false);

                var path = Square();
                MaskSetup.Apply(mask, sr, path);
                Assert.That(mask.enabled, Is.True);
                Assert.That(mask.isCustomRangeActive, Is.True);
                Assert.That(mask.frontSortingOrder, Is.EqualTo(123));
                Assert.That(mask.backSortingOrder, Is.EqualTo(122));
                Assert.That(sr.maskInteraction, Is.EqualTo(SpriteMaskInteraction.VisibleOutsideMask));

                path.Invert = true;
                MaskSetup.Apply(mask, sr, path);
                Assert.That(sr.maskInteraction, Is.EqualTo(SpriteMaskInteraction.VisibleInsideMask));

                MaskSetup.Apply(mask, sr, null);
                Assert.That(mask.enabled, Is.False);
                Assert.That(sr.maskInteraction, Is.EqualTo(SpriteMaskInteraction.None));

                sr.enabled = false;
                MaskSetup.Apply(mask, sr, path);
                Assert.That(mask.enabled, Is.False);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Rig_AimedMusket_InFrontOfBody_RapierHidden()
        {
            var fit = TestFit().Get(PoseKind.Idle);
            var musket = WeaponDef.Temporary(TestSprite(), true);
            var aimed = CosmeticLayout.Weapon(fit, musket, "matej", PoseKind.Idle, true);
            var rest = CosmeticLayout.Weapon(fit, musket, "matej", PoseKind.Idle, false);
            Assert.That(aimed.Order, Is.GreaterThan(CosmeticLayout.Order(RigPart.Body)));
            Assert.That(aimed.Order, Is.LessThan(CosmeticLayout.Order(RigPart.Hand)));
            Assert.That(rest.Order, Is.LessThan(CosmeticLayout.Order(RigPart.Body)));
            Assert.That(WeaponState.RapierVisible(true, true, true), Is.False);
            Assert.That(WeaponState.RapierVisible(true, false, true), Is.True);
            Assert.That(WeaponState.HandOverlayVisible(false, true, true), Is.True);
            Assert.That(WeaponState.HandOverlayVisible(false, false, true), Is.False);
        }

        [Test]
        public void WireWeapons_DefaultsPointMusketStraightWhenAimed()
        {
            var cf = TestFit();
            var musket = WeaponDef.Temporary(TestSprite(), true);
            var rapier = WeaponDef.Temporary(TestSprite(), false);
            WeaponDefaults.Fill(musket, cf);
            WeaponDefaults.Fill(rapier, cf);
            var idle = cf.Get(PoseKind.Idle);
            Assert.That(musket.Active.Global.Rotation + idle.HandFront.Rotation, Is.EqualTo(0f).Within(1e-4f));
            var placed = CosmeticLayout.Weapon(idle, musket, "matej", PoseKind.Idle, true);
            Assert.That(placed.Angle, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(placed.Scale.x, Is.EqualTo(idle.HandBack.S).Within(1e-4f));
            var hurt = cf.Get(PoseKind.Hurt);
            Assert.That(CosmeticLayout.Weapon(hurt, musket, "matej", PoseKind.Hurt, true).Angle, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(CosmeticLayout.Weapon(idle, rapier, "matej", PoseKind.Idle, true).Angle, Is.EqualTo(WeaponDefaults.RaisedAngle).Within(1e-4f));
            Assert.That(CosmeticLayout.Weapon(idle, rapier, "matej", PoseKind.Idle, false).Angle, Is.EqualTo(idle.HandFront.Rotation).Within(1e-4f));
        }
    }
}
