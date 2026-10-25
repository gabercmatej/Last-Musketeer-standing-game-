using System.Collections.Generic;
using Musketeers.Game;
using Musketeers.UI;
using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    public class ReleaseFixTests
    {
        [Test]
        public void Crates_AllFourRewards_AreSeededCappedAndCreditedOnce()
        {
            var seen = new HashSet<int>();
            for (uint seed = 1; seed <= 64; seed++)
            {
                int expected = new DeterministicRng(seed).Range(0, 4);
                var s = Scenario.From("A.o.B");
                s.State.Rng = new DeterministicRng(seed);
                s.WithGrit('A', 0).WithGrit('B', 0).Plan('A', Act.Fire(Dir.E)).Plan('B', Act.Fire(Dir.W)).ResolveTick();
                s.AssertEventCount(SimEventKind.CrateDestroyed, 1);
                int owner = s.Find(SimEventKind.CrateDestroyed).Value.Unit;
                s.AssertGrit(owner == 0 ? 'A' : 'B', expected).AssertGrit(owner == 0 ? 'B' : 'A', 0);
                Assert.That(s.Find(SimEventKind.CrateDestroyed).Value.Amount, Is.EqualTo(expected));
                seen.Add(expected);
            }
            Assert.That(seen, Is.EquivalentTo(new[] { 0, 1, 2, 3 }));
        }

        [Test]
        public void EnvironmentalPenaltyNeverDescribesSelfKillOrPositiveGain()
        {
            var root = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>("Assets/_Project/UI/Scoreboard.uxml").CloneTree();
            var ui = new ScoreboardUI(root);
            var m = MatchRunner.NewMatch(MapParser.Parse("1.2"), new[] { Ability.Grappin, Ability.BullRush }, new GameConfig(), 1);
            var r = new RoundResult { RoundNumber = 1, Winner = -1, Gains = new List<PointGain>() };
            r.Gains.Add(new PointGain { Player = 0, Victim = 0, Amount = -1, Reason = PointReason.EnvironmentalDeath });
            ui.Show(r, m, new[] { "Matej", "Nik" }, new[] { UnityEngine.Color.red, UnityEngine.Color.blue }, null);
            var labels = UnityEngine.UIElements.UQueryExtensions.Query<UnityEngine.UIElements.Label>(root).ToList();
            Assert.That(labels.Exists(l => l.text.Contains("took down Matej")), Is.False);
            Assert.That(labels.Exists(l => l.text == "-1"), Is.True);
        }

        [Test]
        public void Countdown_FreshFiveSeconds_DuplicateFinishCannotRestart_ClocksAgree()
        {
            var host = new PostReplayCountdown(); var guest = new PostReplayCountdown();
            Assert.That(host.Begin(7, 100, 105.25), Is.True);
            Assert.That(guest.Begin(7, 100.1, 105.25), Is.True);
            Assert.That(host.Remaining(100), Is.EqualTo(5));
            Assert.That(guest.Remaining(100.1), Is.EqualTo(5));
            Assert.That(host.Remaining(102), Is.EqualTo(guest.Remaining(102)));
            Assert.That(host.Begin(7, 104, 109), Is.False);
            Assert.That(host.Remaining(105.25), Is.Zero);
            host.Finish(); Assert.That(host.Begin(7, 110), Is.False);
            Assert.That(host.Begin(8, 200), Is.True);
            Assert.That(host.Remaining(200), Is.EqualTo(5));
            Assert.That(host.Remaining(205), Is.Zero);
            Assert.That(PostReplayCountdown.VictoryDelay, Is.EqualTo(1));
        }

        [TestCase(Ability.Grappin, "A.~.B", "dragged")]
        [TestCase(Ability.BullRush, "A.B..~", "pushed")]
        [TestCase(Ability.TripleCharge, "A.B~..", "knocked")]
        public void EnvironmentalSpecialKill_UsesActualAttackerAndAction(Ability ability, string map, string verb)
        {
            var s = Scenario.From(map).WithAbility('A', ability).WithGrit('A', 5).Plan('A', Act.Sig(Dir.E)).ResolveTick();
            s.AssertDead('B').AssertKilledBy('B', 'A');
            var death = s.Find(SimEventKind.Died).Value;
            string text = EventText.Describe(death, new[] { "Matej", "Nik" }, s.Log, s.State.Board);
            Assert.That(text, Is.EqualTo($"Matej {verb} Nik into the pit!"));
            Assert.That(s.Log.CountOf(SimEventKind.PointsScored), Is.EqualTo(2));
        }

        [Test]
        public void SelfAndShoveFalls_HaveNoSelfCredit_AndMessagesAgreeForEveryClient()
        {
            var self = Scenario.From("A~B").Plan('A', Act.Move(Dir.E)).ResolveTick();
            self.AssertDead('A');
            var death = self.Find(SimEventKind.Died).Value;
            Assert.That(death.Other, Is.EqualTo(-1));
            Assert.That(EventText.Describe(death, new[] { "Matej", "Nik" }, self.Log, self.State.Board), Is.EqualTo("Matej fell into the pit!"));
            Assert.That(self.Log.CountOf(SimEventKind.PointsScored), Is.EqualTo(1));
            var shove = Scenario.From("AB~").WithGrit('A', 5).Plan('A', Act.Shove(Dir.E)).ResolveTick();
            shove.AssertKilledBy('B', 'A');
            death = shove.Find(SimEventKind.Died).Value;
            for (int client = 0; client < 3; client++)
                Assert.That(EventText.Describe(death, new[] { "Matej", "Nik" }, shove.Log, shove.State.Board, true), Is.EqualTo("Matej pushed Nik into the water!"));
        }

        [Test]
        public void DelayedFall_PreservesRecordedCreditWithoutInventingAction()
        {
            var s = Scenario.From("AB~");
            s.State.Units[1].LastAttacker = 0; s.State.Units[1].LastAttackTurn = s.State.Turn;
            s.Plan('B', Act.Move(Dir.E)).ResolveTick().AssertKilledBy('B', 'A');
            Assert.That(EventText.Describe(s.Find(SimEventKind.Died).Value, new[] { "Matej", "Nik" }, s.Log, s.State.Board), Is.EqualTo("Matej sent Nik into the pit!"));
            Assert.That(s.Log.CountOf(SimEventKind.PointsScored), Is.EqualTo(2));
        }

        [Test]
        public void MultipleExplosionCrates_StayDeterministic_AndUnattributedExplosionPaysNobody()
        {
            for (uint seed = 1; seed <= 20; seed++)
            {
                var first = Scenario.From("AoKoB").WithKegOwner(0, 'A').WithKegFuse(0, 1).WithGrit('A', 0);
                var second = Scenario.From("AoKoB").WithKegOwner(0, 'A').WithKegFuse(0, 1).WithGrit('A', 0);
                first.State.Rng = new DeterministicRng(seed); second.State.Rng = new DeterministicRng(seed);
                first.ResolveTick(); second.ResolveTick();
                first.AssertEventCount(SimEventKind.CrateDestroyed, 2);
                Assert.That(first.State.ComputeHash(), Is.EqualTo(second.State.ComputeHash()));
                Assert.That(first.State.Units[0].Grit, Is.LessThanOrEqualTo(new GameConfig().MaxGrit));
            }
            var none = Scenario.From("AoKoB").WithKegFuse(0, 1).WithGrit('A', 0).WithGrit('B', 0);
            none.State.Kegs[0].Owner = -1;
            none.ResolveTick().AssertGrit('A', 0).AssertGrit('B', 0);
        }
    }
}
