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
    }
}
