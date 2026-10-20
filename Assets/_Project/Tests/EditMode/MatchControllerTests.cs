using System.Collections.Generic;
using Musketeers.Game;
using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    // MatchController + LocalTransport, driven headless the way the presentation drives them.
    public class MatchControllerTests
    {
        static MatchSetup Setup(int players, uint seed)
        {
            var abilities = new[] { Ability.Grappin, Ability.Switcheroo, Ability.TripleCharge, Ability.BullRush };
            var s = new MatchSetup
            {
                Seed = seed,
                Abilities = new Ability[players],
                Characters = new int[players],
                Names = new string[players],
                IsBot = new bool[players],
            };
            for (int i = 0; i < players; i++)
            {
                s.Abilities[i] = abilities[i];
                s.Characters[i] = i;
                s.Names[i] = "P" + i;
                s.IsBot[i] = i != 0;
            }
            return s;
        }

        [TestCase(2, 3u)]
        [TestCase(4, 5u)]
        public void AutoPlayedMatch_RunsToTheEnd_AndTickSnapshotsMatchTheTurn(int players, uint seed)
        {
            var transport = new LocalTransport { AutoPlayHumans = true };
            var mc = new MatchController(Setup(players, seed), transport);
            var playbacks = new List<TurnPlayback>();
            int roundsEnded = 0, roundsStarted = 0;
            bool ended = false;
            mc.TurnReady += p => playbacks.Add(p);
            mc.RoundEnded += r => roundsEnded++;
            mc.RoundStarted += l => roundsStarted++;
            mc.MatchEnded += () => ended = true;
            mc.Start();

            for (int guard = 0; guard < 5000 && !ended; guard++)
            {
                switch (mc.Phase)
                {
                    case MatchPhase.Planning: mc.Update(0.1f); break;
                    case MatchPhase.Playback: mc.PlaybackFinished(); break;
                    case MatchPhase.RoundOver: mc.ContinueAfterRound(); break;
                }
            }

            Assert.That(ended, "the match should end");
            Assert.That(mc.Match.MatchOver);
            Assert.That(mc.Phase, Is.EqualTo(MatchPhase.MatchOver));
            Assert.That(roundsEnded, Is.EqualTo(mc.Match.RoundNumber));
            Assert.That(roundsStarted, Is.EqualTo(mc.Match.RoundNumber));
            for (int i = 0; i < playbacks.Count; i++)
            {
                var p = playbacks[i];
                Assert.That(p.TurnSeq, Is.EqualTo(i + 1));
                var all = new List<SimEvent>();
                foreach (var log in p.TickLogs)
                    all.AddRange(log.Events);
                Assert.That(all.Count, Is.EqualTo(p.Outcome.Turn.Count), $"turn {p.TurnSeq} event count");
                for (int k = 0; k < all.Count; k++)
                {
                    var a = all[k];
                    var b = p.Outcome.Turn[k];
                    Assert.That((a.Kind, a.Tick, a.Unit, a.Other, a.To), Is.EqualTo((b.Kind, b.Tick, b.Unit, b.Other, b.To)), $"turn {p.TurnSeq} event {k}");
                }
            }
        }

        [Test]
        public void HumanPlan_IsUsed_AndLockingEndsPlanningEarly()
        {
            var transport = new LocalTransport();
            var mc = new MatchController(Setup(2, 9), transport);
            TurnPlayback got = null;
            mc.TurnReady += p => got = p;
            mc.Start();
            Assert.That(mc.Phase, Is.EqualTo(MatchPhase.Planning));
            Assert.That(mc.PlanningSecondsLeft, Is.EqualTo(30f));

            var plan = new TurnPlan(0) { A0 = PlannedAction.Reload(), A1 = PlannedAction.CatchBreath(), A2 = PlannedAction.Reload() };
            mc.SubmitPlan(plan, false);
            mc.Update(1f);
            Assert.That(got, Is.Null, "not locked yet, time left");
            mc.SubmitPlan(plan, true);
            Assert.That(mc.IsLocked(0));
            mc.Update(0.01f);
            Assert.That(got, Is.Not.Null);
            Assert.That(got.Plans[0].A1.Type, Is.EqualTo(ActionType.CatchBreath));
        }

        [Test]
        public void TimerRunningOut_ResolvesWithTheLatestPlan()
        {
            var transport = new LocalTransport();
            var mc = new MatchController(Setup(2, 9), transport);
            TurnPlayback got = null;
            mc.TurnReady += p => got = p;
            mc.Start();
            mc.SubmitPlan(new TurnPlan(0) { A0 = PlannedAction.Parry() }, false);
            mc.Update(29.9f);
            Assert.That(got, Is.Null);
            mc.Update(0.2f);
            Assert.That(got, Is.Not.Null);
            Assert.That(got.Plans[0].A0.Type, Is.EqualTo(ActionType.Parry));
            Assert.That(got.Plans[0].A1.Type, Is.EqualTo(ActionType.Idle));
        }

        [Test]
        public void ControlAll_UsesTheSubmittedPlans_ForBotSlots()
        {
            var transport = new LocalTransport { ControlAll = true };
            var mc = new MatchController(Setup(2, 9), transport);
            TurnPlayback got = null;
            mc.TurnReady += p => got = p;
            mc.Start();
            mc.SubmitPlan(new TurnPlan(0) { A0 = PlannedAction.Reload() }, true);
            mc.Update(0.1f);
            Assert.That(got, Is.Null, "slot 1 is human-controlled now and hasn't locked");
            mc.SubmitPlan(new TurnPlan(1) { A0 = PlannedAction.CatchBreath() }, true);
            mc.Update(0.1f);
            Assert.That(got.Plans[1].A0.Type, Is.EqualTo(ActionType.CatchBreath));
        }
    }
}
