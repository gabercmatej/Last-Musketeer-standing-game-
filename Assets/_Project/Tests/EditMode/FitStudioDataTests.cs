using System.Collections.Generic;
using Musketeers.Data;
using NUnit.Framework;
using UnityEngine;

namespace Musketeers.Sim.Tests
{
    // Plan 006 chunk 1: bezier masks, per-pose item fits, weapons and body-mask transfer.
    public class FitStudioDataTests
    {
        static CosmeticDef NewItem()
        {
            var it = ScriptableObject.CreateInstance<CosmeticDef>();
            it.Id = "test";
            it.Slot = CosmeticSlot.Hat;
            return it;
        }

        static BezierPath Square(float size = 1f) =>
            BezierPath.FromPolygon(new[] { new Vector2(0, 0), new Vector2(size, 0), new Vector2(size, size), new Vector2(0, size) });

        [Test]
        public void BezierPath_CornerOnlyFlattensToThePolygon()
        {
            var poly = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            var path = BezierPath.FromPolygon(poly);
            Assert.That(path.Valid, Is.True);
            Assert.That(path.Flatten(), Is.EqualTo(poly));
            Assert.That(BezierPath.FromPolygon(null).Valid, Is.False);
        }

        [Test]
        public void BezierPath_CurvedSegmentBulgesOffTheChord()
        {
            var path = Square();
            var p0 = path.Points[0];
            p0.Out = new Vector2(0.3f, -0.5f);   // bottom edge bows downwards
            path.Points[0] = p0;
            var p1 = path.Points[1];
            p1.In = new Vector2(-0.3f, -0.5f);
            path.Points[1] = p1;
            var flat = path.Flatten(12);
            Assert.That(flat.Length, Is.EqualTo(12 + 3));
            float minY = 0f;
            foreach (var v in flat) minY = Mathf.Min(minY, v.y);
            Assert.That(minY, Is.LessThan(-0.2f));
        }

        [Test]
        public void PoseFit_LegacyPolygonsMigrateToPaths()
        {
            const string json = "{\"Pose\":0,\"BootMaskBack\":[{\"x\":0.0,\"y\":0.0},{\"x\":0.5,\"y\":0.0},{\"x\":0.5,\"y\":0.25}]}";
            var fit = JsonUtility.FromJson<PoseFit>(json);
            Assert.That(fit.BootPathBack.Flatten(), Is.EqualTo(new[] { new Vector2(0, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0.25f) }));
            Assert.That(fit.BootMaskBack, Is.Empty);
            Assert.That(fit.BootPathFront.Valid, Is.False);
        }

        [Test]
        public void ItemMasks_PerPoseBeatsBase_FrontSpritePoseIgnoresBase()
        {
            var m = new ItemMasks { Base = Square(1f) };
            Assert.That(m.For(PoseKind.Hurt, false), Is.SameAs(m.Base));
            Assert.That(m.For(PoseKind.Victory, true), Is.Null, "front sprite is a different picture");
            var own = Square(2f);
            m.SetForPose(PoseKind.Hurt, own);
            m.SetForPose(PoseKind.Victory, Square(3f));
            Assert.That(m.For(PoseKind.Hurt, false), Is.SameAs(own));
            Assert.That(m.For(PoseKind.Victory, true), Is.Not.Null, "its own per-pose path still applies");
            Assert.That(m.IsCustom(PoseKind.Hurt), Is.True);
            m.ResetToBase(PoseKind.Hurt);
            Assert.That(m.IsCustom(PoseKind.Hurt), Is.False);
            Assert.That(m.For(PoseKind.Hurt, false), Is.SameAs(m.Base));
        }

        [Test]
        public void FitEdit_IdleIsMaster_PosesInheritUntilCustom_ResetRestores()
        {
            var it = NewItem();
            FitEdit.Set(it, PoseKind.Idle, null, new ItemFit(0.1f, 0f, 1f));
            Assert.That(it.Overrides, Is.Empty, "Idle never creates an override");
            Assert.That(FitEdit.Resolve(it, "matej", PoseKind.Hurt).Offset.x, Is.EqualTo(0.1f));
            Assert.That(FitEdit.IsCustom(it, PoseKind.Hurt), Is.False);
            Assert.That(FitEdit.IsCustom(it, PoseKind.Idle), Is.False);

            FitEdit.Set(it, PoseKind.Hurt, "", new ItemFit(0.3f, 0f, 1f));
            Assert.That(FitEdit.IsCustom(it, PoseKind.Hurt), Is.True);
            Assert.That(FitEdit.Resolve(it, "matej", PoseKind.Hurt).Offset.x, Is.EqualTo(0.3f));
            Assert.That(FitEdit.Resolve(it, "matej", PoseKind.Lunge).Offset.x, Is.EqualTo(0.1f));

            FitEdit.ResetToIdle(it, PoseKind.Hurt);
            Assert.That(FitEdit.IsCustom(it, PoseKind.Hurt), Is.False);
            Assert.That(FitEdit.Resolve(it, "matej", PoseKind.Hurt).Offset.x, Is.EqualTo(0.1f));
            Object.DestroyImmediate(it);
        }

        [Test]
        public void FitEdit_MusketeerOverrideOnlyAffectsThatMusketeer()
        {
            var it = NewItem();
            FitEdit.Set(it, PoseKind.Idle, "aljaz", new ItemFit(0.5f, 0f, 1f));
            Assert.That(FitEdit.HasMusketeerOverride(it, "aljaz", PoseKind.Idle), Is.True);
            Assert.That(FitEdit.Resolve(it, "aljaz", PoseKind.Idle).Offset.x, Is.EqualTo(0.5f));
            Assert.That(FitEdit.Resolve(it, "matej", PoseKind.Idle).Offset.x, Is.EqualTo(0f));
            Assert.That(FitEdit.HasMusketeerOverride(it, "matej", PoseKind.Idle), Is.False);
            FitEdit.RemoveMusketeerOverride(it, "aljaz", PoseKind.Idle);
            Assert.That(FitEdit.Resolve(it, "aljaz", PoseKind.Idle).Offset.x, Is.EqualTo(0f));
            Object.DestroyImmediate(it);
        }

        [Test]
        public void FitEdit_LinkAll_MovesEveryPose_KeepsCustomDifference()
        {
            var it = NewItem();
            FitEdit.Set(it, PoseKind.Hurt, "", new ItemFit(0.1f, 0f, 1f));   // custom at +0.1 x vs Idle
            var edited = FitEdit.Resolve(it, "matej", PoseKind.Idle);
            edited.Offset.x += 0.05f;
            FitEdit.SetLinked(it, "", PoseKind.Idle, edited, FitEdit.StudioPoses);
            float idle = FitEdit.Resolve(it, "matej", PoseKind.Idle).Offset.x;
            float hurt = FitEdit.Resolve(it, "matej", PoseKind.Hurt).Offset.x;
            float lunge = FitEdit.Resolve(it, "matej", PoseKind.Lunge).Offset.x;
            Assert.That(idle, Is.EqualTo(0.05f).Within(1e-5));
            Assert.That(hurt, Is.EqualTo(0.15f).Within(1e-5));
            Assert.That(hurt - idle, Is.EqualTo(0.1f).Within(1e-5));
            Assert.That(lunge, Is.EqualTo(0.05f).Within(1e-5), "a non-custom pose follows Idle");
            Object.DestroyImmediate(it);
        }

        [Test]
        public void FitEdit_CheckedIsPerPose()
        {
            var it = NewItem();
            FitEdit.SetChecked(it, PoseKind.Hurt, true);
            Assert.That(FitEdit.IsChecked(it, PoseKind.Hurt), Is.True);
            Assert.That(FitEdit.IsChecked(it, PoseKind.Idle), Is.False);
            FitEdit.SetChecked(it, PoseKind.Hurt, false);
            Assert.That(FitEdit.IsChecked(it, PoseKind.Hurt), Is.False);
            Object.DestroyImmediate(it);
        }

        [Test]
        public void FitEdit_PasteToOtherItemAndAllPoses()
        {
            var a = NewItem();
            var b = NewItem();
            FitEdit.Set(a, PoseKind.Idle, null, new ItemFit(0.2f, 0.1f, 1.5f, 10f));
            a.Masks.Base = Square(1f);
            var clip = FitEdit.Copy(a, "matej", PoseKind.Idle, true, false);
            Assert.That(clip.HasMask, Is.True);
            Assert.That(clip.Mask, Is.Not.SameAs(a.Masks.Base), "the clip is a deep copy");

            FitEdit.Paste(clip, b, "", PoseKind.Idle);
            Assert.That(b.Global.Offset, Is.EqualTo(new Vector2(0.2f, 0.1f)));
            Assert.That(b.Masks.Base.Valid, Is.True, "Idle + everyone sets the base mask");
            foreach (var pose in FitEdit.StudioPoses)
                FitEdit.Paste(clip, b, "", pose);
            Assert.That(FitEdit.IsCustom(b, PoseKind.Hurt), Is.True);
            Assert.That(FitEdit.Resolve(b, "x", PoseKind.Hurt).Rotation, Is.EqualTo(10f));
            Assert.That(b.Masks.IsCustom(PoseKind.Hurt), Is.True);
            Object.DestroyImmediate(a); Object.DestroyImmediate(b);
        }

        [Test]
        public void Layout_MusketRestBehindBody_AimedInFrontBelowHand()
        {
            var tex = new Texture2D(4, 4);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 256);
            var fit = new PoseFit { HandBack = new Anchor(-0.2f, 0.4f, -38f, 0.7f), HandFront = new Anchor(0.2f, 0.4f, -60f, 0.7f) };
            var musket = WeaponDef.Temporary(sprite, true);
            var rest = CosmeticLayout.Weapon(fit, musket, "matej", PoseKind.Idle, false);
            var aimed = CosmeticLayout.Weapon(fit, musket, "matej", PoseKind.Idle, true);
            Assert.That(rest.Position, Is.EqualTo(new Vector2(-0.2f, 0.4f)));
            Assert.That(rest.Order, Is.LessThan(CosmeticLayout.Order(RigPart.Body)));
            Assert.That(aimed.Position, Is.EqualTo(new Vector2(0.2f, 0.4f)));
            Assert.That(aimed.Order, Is.GreaterThan(CosmeticLayout.Order(RigPart.Body)));
            Assert.That(aimed.Order, Is.LessThan(CosmeticLayout.Order(RigPart.Hand)));
            Object.DestroyImmediate(musket); Object.DestroyImmediate(tex);
        }

        [Test]
        public void BodyMaskTransfer_MapsThroughAnchors()
        {
            var from = new Anchor(1f, 1f, 0f, 1f);
            var to = new Anchor(5f, 2f, 90f, 2f);
            var src = BezierPath.FromPolygon(new[] { new Vector2(1f, 1f), new Vector2(1.1f, 1f), new Vector2(1f, 1.1f) });
            var res = BodyMaskTransfer.Transfer(src, from, to);
            Assert.That(res.Points[0].P.x, Is.EqualTo(5f).Within(1e-4));
            Assert.That(res.Points[0].P.y, Is.EqualTo(2f).Within(1e-4));
            var expected = to.Position + CosmeticLayout.Rotate(new Vector2(0.2f, 0f), 90f);
            Assert.That(res.Points[1].P.x, Is.EqualTo(expected.x).Within(1e-4));
            Assert.That(res.Points[1].P.y, Is.EqualTo(expected.y).Within(1e-4));

            var b = ScriptableObject.CreateInstance<CharacterFit>();
            var fitA = ScriptableObject.CreateInstance<CharacterFit>();
            var pa = fitA.GetOrAdd(PoseKind.Idle);
            pa.FootFront = from;
            pa.BootPathFront = src;
            b.GetOrAdd(PoseKind.Idle).FootFront = to;
            BodyMaskTransfer.Copy(fitA, b, PoseKind.Idle, BodyMaskId.BootFront);
            Assert.That(b.Get(PoseKind.Idle).BootPathFront.Points[0].P.x, Is.EqualTo(5f).Within(1e-4));
            Object.DestroyImmediate(fitA); Object.DestroyImmediate(b);
        }
    }
}
