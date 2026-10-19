using Musketeers.Data;
using Musketeers.EditorTools.Studio;
using NUnit.Framework;
using UnityEngine;

namespace Musketeers.Sim.Tests
{
    public class StudioEditTests
    {
        static CosmeticDef NewItem(string id, bool enabled = true)
        {
            var d = ScriptableObject.CreateInstance<CosmeticDef>();
            d.Id = id;
            d.DisplayName = id;
            d.EnabledInV1 = enabled;
            return d;
        }

        [Test]
        public void Move_UnrotatesAndUnscalesByTheAnchor()
        {
            // Anchor turned 90 degrees and twice as big: pose "up" 0.2 is anchor-space "right" 0.2, halved by the scale.
            var f = StudioEdit.Move(new ItemFit(0f, 0f, 1f), new Anchor(0, 0, 90f, 2f), new Vector2(0f, 0.2f), false);
            Assert.AreEqual(0.1f, f.Offset.x, 1e-5f);
            Assert.AreEqual(0f, f.Offset.y, 1e-5f);
        }

        [Test]
        public void Move_MirroredNegatesX()
        {
            var f = StudioEdit.Move(new ItemFit(0.5f, 0f, 1f), new Anchor(0, 0), new Vector2(0.1f, 0.2f), true);
            Assert.AreEqual(0.4f, f.Offset.x, 1e-5f);
            Assert.AreEqual(0.2f, f.Offset.y, 1e-5f);
        }

        [Test]
        public void ScaleBy_Clamps()
        {
            Assert.AreEqual(10f, StudioEdit.ScaleBy(new ItemFit(0, 0, 5f), 4f).Scale, 1e-5f);
            Assert.AreEqual(0.05f, StudioEdit.ScaleBy(new ItemFit(0, 0, 0.1f), 0.01f).Scale, 1e-5f);
            Assert.AreEqual(1.5f, StudioEdit.ScaleBy(new ItemFit(0, 0, 1f), 1.5f).Scale, 1e-5f);
        }

        [Test]
        public void Write_EveryoneOnIdle_WritesGlobal_OtherPoseMakesCustom()
        {
            var d = NewItem("hat");
            try
            {
                StudioEdit.Write(d, d, PoseKind.Idle, null, new ItemFit(0.1f, 0.2f, 1.5f), false);
                Assert.AreEqual(0.1f, d.Offset.x, 1e-5f);
                Assert.AreEqual(1.5f, d.Scale, 1e-5f);
                Assert.IsTrue(d.Tuned);
                Assert.IsFalse(FitEdit.IsCustom(d, PoseKind.Lunge));
                StudioEdit.Write(d, d, PoseKind.Lunge, null, new ItemFit(0.3f, 0f, 1f), false);
                Assert.IsTrue(FitEdit.IsCustom(d, PoseKind.Lunge));
                Assert.AreEqual(0.1f, d.Offset.x, 1e-5f, "Idle master untouched");
                Assert.AreEqual(0.3f, FitEdit.Resolve(d, "matej", PoseKind.Lunge).Offset.x, 1e-5f);
            }
            finally { Object.DestroyImmediate(d); }
        }

        [Test]
        public void Write_OverrideMusketeer_OnlyThatMusketeer()
        {
            var d = NewItem("hat");
            try
            {
                StudioEdit.Write(d, d, PoseKind.Idle, "matej", new ItemFit(0.4f, 0f, 1f), false);
                Assert.IsTrue(FitEdit.HasMusketeerOverride(d, "matej", PoseKind.Idle));
                Assert.AreEqual(0.4f, FitEdit.Resolve(d, "matej", PoseKind.Idle).Offset.x, 1e-5f);
                Assert.AreEqual(0f, FitEdit.Resolve(d, "nik", PoseKind.Idle).Offset.x, 1e-5f);
                Assert.AreEqual(0f, d.Offset.x, 1e-5f, "global untouched");
            }
            finally { Object.DestroyImmediate(d); }
        }

        [Test]
        public void Write_LinkAll_MovesEveryPose()
        {
            var d = NewItem("hat");
            try
            {
                FitEdit.Set(d, PoseKind.Hurt, null, new ItemFit(1f, 0f, 1f));   // a pose that already differs
                StudioEdit.Write(d, d, PoseKind.Idle, null, new ItemFit(0.5f, 0f, 1f), true);
                Assert.AreEqual(0.5f, FitEdit.Resolve(d, "matej", PoseKind.Idle).Offset.x, 1e-5f);
                Assert.AreEqual(0.5f, FitEdit.Resolve(d, "matej", PoseKind.Lunge).Offset.x, 1e-5f, "inherits the moved master");
                Assert.AreEqual(1.5f, FitEdit.Resolve(d, "matej", PoseKind.Hurt).Offset.x, 1e-5f, "custom pose keeps its difference");
            }
            finally { Object.DestroyImmediate(d); }
        }

        [Test]
        public void Progress_CountsEnabledItemsTimesPosesPlusWeapons()
        {
            var cat = ScriptableObject.CreateInstance<CosmeticCatalog>();
            var a = NewItem("a");
            var b = NewItem("b");
            var off = NewItem("c", false);
            var musket = ScriptableObject.CreateInstance<WeaponDef>();
            var rapier = ScriptableObject.CreateInstance<WeaponDef>();
            try
            {
                cat.Items.AddRange(new[] { a, b, off });
                cat.Musket = musket;
                cat.Rapier = rapier;
                var rows = StudioProgress.Build(cat);
                Assert.AreEqual((2 + 4) * 4, rows.Count);
                Assert.AreEqual(0, StudioProgress.CountChecked(rows));
                FitEdit.SetChecked(rows[0].Target, rows[0].Pose, true);
                Assert.AreEqual(1, StudioProgress.CountChecked(StudioProgress.Build(cat)));
            }
            finally
            {
                Object.DestroyImmediate(a); Object.DestroyImmediate(b); Object.DestroyImmediate(off);
                Object.DestroyImmediate(musket); Object.DestroyImmediate(rapier); Object.DestroyImmediate(cat);
            }
        }
    
        static BezierPath Square(float x = 0f) => BezierPath.FromPolygon(new[] { new Vector2(x, 0), new Vector2(x + 1, 0), new Vector2(x + 1, 1), new Vector2(x, 1) });

        [Test]
        public void Paste_AllPoses_SetsGlobalAndClearsCustoms()
        {
            var d = NewItem("hat");
            try
            {
                FitEdit.Set(d, PoseKind.Lunge, null, new ItemFit(9f, 9f, 2f));
                FitEdit.Set(d, PoseKind.Victory, "matej", new ItemFit(8f, 8f, 2f));
                d.Masks.SetForPose(PoseKind.Hurt, Square(5f));
                var clip = new FitClip { Fit = new ItemFit(0.1f, 0.2f, 1.3f, 15f), HasMask = true, Mask = Square() };
                StudioPaste.AllPoses(clip, d, null);
                Assert.AreEqual(0.1f, d.Global.Offset.x, 1e-5f);
                Assert.AreEqual(1.3f, d.Global.S, 1e-5f);
                Assert.IsFalse(FitEdit.IsCustom(d, PoseKind.Lunge));
                Assert.AreEqual(0.1f, FitEdit.Resolve(d, "aljaz", PoseKind.Lunge).Offset.x, 1e-5f, "every pose inherits the pasted fit");
                Assert.AreEqual(4, d.Masks.Base.Points.Count);
                Assert.IsTrue(d.Masks.Base.Valid);
                Assert.IsFalse(d.Masks.IsCustom(PoseKind.Hurt), "per-pose masks cleared");
            }
            finally { Object.DestroyImmediate(d); }
        }

        [Test]
        public void Paste_AllPoses_WithMusketeerOverride_SetsThatMusketeerOnEveryPose()
        {
            var d = NewItem("hat");
            try
            {
                var clip = new FitClip { Fit = new ItemFit(0.5f, 0f, 1f) };
                StudioPaste.AllPoses(clip, d, "matej");
                foreach (var p in FitEdit.StudioPoses)
                {
                    Assert.IsTrue(FitEdit.HasMusketeerOverride(d, "matej", p));
                    Assert.AreEqual(0.5f, FitEdit.Resolve(d, "matej", p).Offset.x, 1e-5f);
                }
                Assert.AreEqual(0f, d.Global.Offset.x, 1e-5f, "Global untouched");
            }
            finally { Object.DestroyImmediate(d); }
        }

        [Test]
        public void Paste_OtherItems_OnlyCheckedOnes()
        {
            var a = NewItem("a");
            var b = NewItem("b");
            var c = NewItem("c");
            try
            {
                var clip = new FitClip { Fit = new ItemFit(0.7f, 0f, 1f) };
                var list = new System.Collections.Generic.List<IFitTarget> { a, b, c };
                int n = StudioPaste.ToChecked(clip, list, new[] { true, false, true }, null, true, PoseKind.Idle);
                Assert.AreEqual(2, n);
                Assert.AreEqual(0.7f, a.Offset.x, 1e-5f);
                Assert.AreEqual(0f, b.Offset.x, 1e-5f);
                Assert.AreEqual(0.7f, c.Offset.x, 1e-5f);
                // Not all poses: only the current pose.
                n = StudioPaste.ToChecked(new FitClip { Fit = new ItemFit(0.3f, 0f, 1f) }, list, new[] { false, true, false }, null, false, PoseKind.Lunge);
                Assert.AreEqual(1, n);
                Assert.IsTrue(FitEdit.IsCustom(b, PoseKind.Lunge));
                Assert.IsFalse(FitEdit.IsCustom(a, PoseKind.Lunge));
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
                Object.DestroyImmediate(c);
            }
        }

        [Test]
        public void CopyMaskFrom_TransfersToEveryTargetForAllPoses()
        {
            var src = ScriptableObject.CreateInstance<CharacterFit>();
            var t1 = ScriptableObject.CreateInstance<CharacterFit>();
            var t2 = ScriptableObject.CreateInstance<CharacterFit>();
            try
            {
                var targets = new[] { t1, t2 };
                float shift = 0f;
                foreach (var cf in new[] { src, t1, t2 })
                {
                    foreach (var pose in FitEdit.StudioPoses)
                    {
                        var pf = cf.GetOrAdd(pose);
                        pf.FootFront = new Anchor(0.1f + shift, 0.02f * (int)pose, 10f * shift, 1f + shift);
                    }
                    shift += 0.25f;
                }
                foreach (var pose in FitEdit.StudioPoses)
                    src.Get(pose).SetBodyMask(BodyMaskId.BootFront, Square(0.1f * (int)pose));
                StudioPaste.CopyMask(src, targets, FitEdit.StudioPoses, new[] { BodyMaskId.BootFront });
                foreach (var t in targets)
                {
                    foreach (var pose in FitEdit.StudioPoses)
                    {
                        var s = src.Get(pose);
                        var expect = BodyMaskTransfer.Transfer(s.BootPathFront, s.FootFront, t.Get(pose).FootFront);
                        var got = t.Get(pose).BootPathFront;
                        Assert.IsTrue(got.Valid, pose.ToString());
                        Assert.AreEqual(expect.Points[0].P.x, got.Points[0].P.x, 1e-5f);
                        Assert.AreEqual(expect.Points[0].P.y, got.Points[0].P.y, 1e-5f);
                    }
                }
                Assert.AreNotEqual(src.Get(PoseKind.Idle).BootPathFront.Points[0].P, t1.Get(PoseKind.Idle).BootPathFront.Points[0].P);
            }
            finally
            {
                Object.DestroyImmediate(src);
                Object.DestroyImmediate(t1);
                Object.DestroyImmediate(t2);
            }
        }
    }
}
