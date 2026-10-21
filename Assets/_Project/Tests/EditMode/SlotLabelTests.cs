using Musketeers.Data;
using Musketeers.Presentation;
using Musketeers.UI;
using NUnit.Framework;
using UnityEngine;

namespace Musketeers.Sim.Tests
{
    // Plan-slot labels (Matej fix 4): short, never wrapping; names are clipped by USS.
    public class SlotLabelTests
    {
        static CharacterDef Def(Ability a, string name)
        {
            var c = ScriptableObject.CreateInstance<CharacterDef>();
            c.Ability = a;
            c.AbilityName = name;
            return c;
        }

        [Test]
        public void Labels_AreShort_ExceptNames()
        {
            var chars = new[]
            {
                Def(Ability.Grappin, "Grappin"), Def(Ability.TripleCharge, "Triple Charge"), Def(Ability.BullRush, "Bull Rush"),
            };
            var names = new[] { "Matej", "Nik", "A very long musketeer name here" };
            foreach (var c in chars)
            {
                foreach (ActionType t in System.Enum.GetValues(typeof(ActionType)))
                {
                    foreach (Dir d in System.Enum.GetValues(typeof(Dir)))
                    {
                        var label = ActionInfo.SlotLabel(new PlannedAction(t, d, new Int2(3, 4)), c, names);
                        Assert.That(label.Length, Is.LessThanOrEqualTo(15), $"{t} {d}: {label}");
                        Assert.That(label, Does.Not.Contain("\n"));
                    }
                }
            }
        }

        [Test]
        public void Examples()
        {
            var g = Def(Ability.Grappin, "Grappin");
            var tc = Def(Ability.TripleCharge, "Triple Charge");
            var sw = Def(Ability.Switcheroo, "Switcheroo");
            Assert.That(ActionInfo.SlotLabel(new PlannedAction(ActionType.Move, Dir.E, default), g), Is.EqualTo("Move →"));
            Assert.That(ActionInfo.SlotLabel(new PlannedAction(ActionType.Dash, Dir.N, default), g), Is.EqualTo("Dash ↑"));
            Assert.That(ActionInfo.SlotLabel(new PlannedAction(ActionType.Fire, Dir.W, default), g), Is.EqualTo("Fire ←"));
            Assert.That(ActionInfo.SlotLabel(new PlannedAction(ActionType.Reload, Dir.N, default), g), Is.EqualTo("Reload"));
            Assert.That(ActionInfo.SlotLabel(new PlannedAction(ActionType.CatchBreath, Dir.N, default), g), Is.EqualTo("Catch Breath"));
            Assert.That(ActionInfo.SlotLabel(new PlannedAction(ActionType.Keg, Dir.N, new Int2(3, 4)), g), Is.EqualTo("Keg (3,4)"));
            Assert.That(ActionInfo.SlotLabel(new PlannedAction(ActionType.Signature, Dir.E, default), tc), Is.EqualTo("Triple Charge →"));
            Assert.That(ActionInfo.SlotLabel(PlannedAction.Switcheroo(1), sw, new[] { "Matej", "Nik" }), Is.EqualTo("Swap: Nik"));
            Assert.That(ActionInfo.SlotLabel(new PlannedAction(ActionType.Idle, Dir.N, default), g), Is.EqualTo("Idle"));
        }
    }
}
