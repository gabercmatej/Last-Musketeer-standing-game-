using Musketeers.Game;
using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    public class RoundReplayTests
    {
        const string Arena = @"
            ..........
            .1.2......
            .3.4......
            ..........";

        static MatchState Match(string map = Arena, GameConfig cfg = null) => MatchRunner.NewMatch(
            MapParser.Parse(map), new[] { Ability.Grappin, Ability.Switcheroo, Ability.TripleCharge, Ability.BullRush },
            cfg ?? new GameConfig(), 17);

        static TurnPlan[] Plans() => new[] { new TurnPlan(0), new TurnPlan(1), new TurnPlan(2), new TurnPlan(3) };
        static TurnPlayback Resolve(MatchState m, TurnPlan[] plans, int sequence = 1) =>
            MatchController.Resolve(m, plans, sequence, new[] { "A", "B", "C", "D" });

        [Test]
        public void QuietTurn_RetainsNoSnapshots()
        {
            var replay = new RoundReplay();
            replay.Add(Resolve(Match(), Plans()));
            Assert.That(replay.Clips, Is.Empty);
        }

        [Test]
        public void DeathTicks_AppearChronologically_SkipQuietTicks_KeepCauseLeadIn()
        {
            var m = Match();
            m.Round.Units[1].Hp = 1;
            m.Round.Units[3].Hp = 1;
            var plans = Plans();
            plans[0].A0 = Act.Fire(Dir.E);
            plans[2].A2 = Act.Fire(Dir.E);
            var p = Resolve(m, plans);
            var replay = new RoundReplay();
            replay.Add(p);
            Assert.That(replay.Clips.Count, Is.EqualTo(2));
            Assert.That(replay.Clips[0].Log.Count, Is.EqualTo(p.TickLogs[0].Count));
            Assert.That(replay.Clips[1].Log.Count, Is.EqualTo(p.TickLogs[2].Count));
            Assert.That(replay.Clips[0].Log.Contains(SimEventKind.ShotFired), Is.True);
            Assert.That(replay.Clips[0].DeathCount, Is.EqualTo(1));
            Assert.That(replay.Clips[1].DeathCount, Is.EqualTo(1));
            Assert.That(replay.Clips[0].Focus, Is.EqualTo(p.Before.Units[1].Pos));
            Assert.That(replay.Clips[1].Focus, Is.EqualTo(p.Before.Units[3].Pos));
            Assert.That(replay.Clips[0].Before.ComputeHash(), Is.EqualTo(p.Before.ComputeHash()));
            Assert.That(replay.Clips[1].Before.ComputeHash(), Is.EqualTo(p.AfterTick[1].ComputeHash()));
            Assert.That(replay.Clips[1].After.ComputeHash(), Is.EqualTo(p.AfterTick[2].ComputeHash()));
            for (int clip = 0; clip < 2; clip++)
            {
                var source = p.TickLogs[clip == 0 ? 0 : 2];
                var copied = replay.Clips[clip].Log;
                for (int i = 0; i < source.Count; i++)
                    Assert.That(copied[i], Is.EqualTo(source[i]), "preserve the original event tick, phase and payload");
            }
        }

        [Test]
        public void SimultaneousDeaths_AreOneClip_FocusedOnFirstDeathInEventOrder()
        {
            var m = Match();
            m.Round.Units[1].Hp = 1;
            m.Round.Units[3].Hp = 1;
            var plans = Plans();
            plans[0].A0 = Act.Fire(Dir.E);
            plans[2].A0 = Act.Fire(Dir.E);
            var p = Resolve(m, plans);
            var replay = new RoundReplay();
            replay.Add(p);
            Assert.That(replay.Clips.Count, Is.EqualTo(1));
            Assert.That(replay.Clips[0].DeathCount, Is.EqualTo(2));
            foreach (var e in p.TickLogs[0].Events)
                if (e.Kind == SimEventKind.Died)
                {
                    Assert.That(replay.Clips[0].Focus, Is.EqualTo(e.To));
                    break;
                }
        }

        [Test]
        public void CrumbleDeaths_AreAfterTickDeaths_UsePreCrumbleAndFinalRoundSnapshots()
        {
            var m = Match(@"
                2........4
                .1.3......
                ..........", new GameConfig { CrumbleStartTurn = 1 });
            m.Round.Units[2].Hp = 1;
            var plans = Plans();
            plans[0].A0 = Act.Fire(Dir.E);
            var p = Resolve(m, plans);
            Assert.That(p.Outcome.TurnStart.Contains(SimEventKind.Died), Is.True);
            var replay = new RoundReplay();
            replay.Add(p);
            Assert.That(replay.Clips.Count, Is.EqualTo(2));
            var crumble = replay.Clips[1];
            Assert.That(crumble.DeathCount, Is.EqualTo(2));
            Assert.That(crumble.Log.Contains(SimEventKind.TileCrumbled), Is.True);
            Assert.That(crumble.Before.ComputeHash(), Is.EqualTo(p.AfterTick[p.TickCount - 1].ComputeHash()));
            Assert.That(crumble.After.ComputeHash(), Is.EqualTo(p.After.ComputeHash()));
            Assert.That(crumble.Crowned, Is.EqualTo(p.CrownedBefore));
        }

        [Test]
        public void Add_DoesNotMutateLiveMatchOrPlayback_AndRetainsIndependentClones()
        {
            var m = Match();
            m.Round.Units[1].Hp = 1;
            m.Crowned = m.Round.Crowned = 1;
            var plans = Plans();
            plans[0].A0 = Act.Fire(Dir.E);
            var p = Resolve(m, plans);
            uint liveHash = m.ComputeHash(), beforeHash = p.Before.ComputeHash(), afterHash = p.AfterTick[0].ComputeHash();
            var replay = new RoundReplay();
            replay.Add(p);
            var clip = replay.Clips[0];
            Assert.That(m.ComputeHash(), Is.EqualTo(liveHash));
            Assert.That(p.Before.ComputeHash(), Is.EqualTo(beforeHash));
            Assert.That(p.AfterTick[0].ComputeHash(), Is.EqualTo(afterHash));
            Assert.That(clip.Crowned, Is.EqualTo(1));
            int events = clip.Log.Count;
            p.Before.Units[0].Hp = 0;
            p.Before.Board.Set(new Int2(0, 0), TileType.Pillar);
            p.AfterTick[0].Units[0].Hp = 0;
            p.TickLogs[0].Add(SimEventKind.RoundEnded);
            Assert.That(clip.Before.ComputeHash(), Is.EqualTo(beforeHash));
            Assert.That(clip.After.ComputeHash(), Is.EqualTo(afterHash));
            Assert.That(clip.Log.Count, Is.EqualTo(events));
            Assert.That(m.ComputeHash(), Is.EqualTo(liveHash));
        }

        [Test]
        public void AcrossTurns_AccumulatesDeaths_UntilRoundChangesOrClear()
        {
            var m = Match();
            m.Round.Units[1].Hp = 1;
            m.Round.Units[3].Hp = 1;
            var first = Plans();
            first[0].A0 = Act.Fire(Dir.E);
            var replay = new RoundReplay();
            replay.Add(Resolve(m, first));
            var second = Plans();
            second[2].A0 = Act.Fire(Dir.E);
            replay.Add(Resolve(m, second, 2));
            Assert.That(replay.Clips.Count, Is.EqualTo(2));
            var nextRound = Resolve(Match(), Plans());
            nextRound.RoundNumber = 2;
            replay.Add(nextRound);
            Assert.That(replay.Clips, Is.Empty);
            nextRound.RoundNumber = 1;
            replay.Add(Resolve(Match(), first)); // no damage death: retain no snapshots
            replay.Clear();
            Assert.That(replay.Clips, Is.Empty);
        }
    }
}
