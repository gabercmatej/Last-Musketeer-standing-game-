using System.Collections.Generic;
using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    // GAME_DESIGN §2 scoring, Crown, ranking and the round lifecycle, driven through MatchRunner.
    public class ScoringTests
    {
        // Spawn 1 at (1,1), spawn 2 at (5,1): 4 apart on one row, so they can shoot each other.
        const string Duel = @"
            .......
            .1...2.
            .......";

        // Spawn 3 sits off the duel row.
        const string Trio = @"
            ...3...
            .1...2.
            .......";

        static readonly Ability[] Two = { Ability.Grappin, Ability.Switcheroo };
        static readonly Ability[] Three = { Ability.Grappin, Ability.Switcheroo, Ability.BullRush };

        static MatchState Match(string map, Ability[] abilities, GameConfig cfg = null) =>
            MatchRunner.NewMatch(MapParser.Parse(map), abilities, cfg ?? new GameConfig(), 42);

        static TurnPlan[] Plans(MatchState m)
        {
            var p = new TurnPlan[m.PlayerCount];
            for (int i = 0; i < p.Length; i++)
                p[i] = new TurnPlan(i);
            return p;
        }

        // A shoots B (on 1 HP) in tick 0; B does nothing.
        static TurnOutcome AKillsB(MatchState m)
        {
            m.Round.Units[1].Hp = 1;
            var p = Plans(m);
            p[0].A0 = PlannedAction.Fire(Dir.E);
            return MatchRunner.PlayTurn(m, p);
        }

        // A and B (both on 1 HP) shoot each other in tick 0.
        static TurnOutcome MutualKill(MatchState m)
        {
            m.Round.Units[0].Hp = 1;
            m.Round.Units[1].Hp = 1;
            var p = Plans(m);
            p[0].A0 = PlannedAction.Fire(Dir.E);
            p[1].A0 = PlannedAction.Fire(Dir.W);
            return MatchRunner.PlayTurn(m, p);
        }

        [Test]
        public void NewMatch_StartsRoundOne_NobodyCrowned()
        {
            var m = Match(Duel, Two);
            Assert.That(m.RoundNumber, Is.EqualTo(1));
            Assert.That(m.Crowned, Is.EqualTo(-1));
            Assert.That(m.Round.Turn, Is.EqualTo(1));
            Assert.That(m.Round.Units[0].Pos, Is.EqualTo(new Int2(1, 1)));
            Assert.That(m.Round.Units[1].Pos, Is.EqualTo(new Int2(5, 1)));
        }

        [Test]
        public void LastStanding_PlusThree_Kill_PlusOne_LeaderGetsCrown()
        {
            var m = Match(Duel, Two);
            var o = AKillsB(m);
            Assert.That(o.RoundResult, Is.Not.Null);
            Assert.That(o.RoundResult.Winner, Is.EqualTo(0));
            Assert.That(o.TurnStart, Is.Null, "the round ended inside the turn");
            Assert.That(m.Points, Is.EqualTo(new[] { 4, 0 }));
            Assert.That(m.RoundWins, Is.EqualTo(new[] { 1, 0 }));
            Assert.That(o.RoundResult.Gains.Count, Is.EqualTo(2));
            Assert.That(o.RoundResult.Gains[0].Reason, Is.EqualTo(PointReason.Kill));
            Assert.That(o.RoundResult.Gains[1].Reason, Is.EqualTo(PointReason.LastStanding));
            Assert.That(o.NextRoundStart, Is.Not.Null);
            Assert.That(m.RoundNumber, Is.EqualTo(2));
            Assert.That(m.Crowned, Is.EqualTo(0), "strict leader wears the Crown next round");
        }

        [Test]
        public void Draw_NoLastStandingPoints_KillsStillCount_NoCrownOnTie()
        {
            var m = Match(Duel, Two);
            var o = MutualKill(m);
            Assert.That(o.RoundResult.Winner, Is.EqualTo(-1));
            Assert.That(m.Points, Is.EqualTo(new[] { 1, 1 }));
            Assert.That(m.RoundWins, Is.EqualTo(new[] { 0, 0 }));
            Assert.That(m.Crowned, Is.EqualTo(-1));
        }

        [Test]
        public void SelfKill_ScoresNothing()
        {
            var m = Match(@"
                .......
                ~1...2.
                .......", Two);
            var p = Plans(m);
            p[0].A0 = PlannedAction.Move(Dir.W);
            var o = MatchRunner.PlayTurn(m, p);
            Assert.That(o.RoundResult.Winner, Is.EqualTo(1));
            Assert.That(m.Points, Is.EqualTo(new[] { 0, 3 }));
            Assert.That(m.Stats.SelfRingOuts[0], Is.EqualTo(1));
        }

        [Test]
        public void KillOnTheCrowned_PlusTwo()
        {
            var m = Match(Duel, Two);
            m.Crowned = 1;
            AKillsB(m);
            Assert.That(m.Points, Is.EqualTo(new[] { 5, 0 }));
            Assert.That(m.Stats.CrownKills[0], Is.EqualTo(1));
        }

        [Test]
        public void Points_AreAppliedOnlyAtRoundEnd()
        {
            var m = Match(@"
                .......
                .1.3.2.
                .......", Three);
            m.Round.Units[2].Hp = 1;
            var p = Plans(m);
            p[0].A0 = PlannedAction.Fire(Dir.E);   // hits slot 2 standing between A and B
            var o = MatchRunner.PlayTurn(m, p);
            Assert.That(o.RoundResult, Is.Null);
            Assert.That(m.Round.Units[2].Alive, Is.False);
            Assert.That(m.Points, Is.EqualTo(new[] { 0, 0, 0 }));
            Assert.That(m.RoundGains.Count, Is.EqualTo(1));
        }

        [Test]
        public void MatchEnds_AtEndOfRound_WhenSomeoneReachesPointsToWin()
        {
            var m = Match(Duel, Two);
            m.Points[0] = 10;
            var o = AKillsB(m);
            Assert.That(o.MatchOver, Is.True);
            Assert.That(m.MatchOver, Is.True);
            Assert.That(m.MatchWinner, Is.EqualTo(0));
            Assert.That(o.NextRoundStart, Is.Null);
            Assert.Throws<System.InvalidOperationException>(() => MatchRunner.PlayTurn(m, Plans(m)));
        }

        [Test]
        public void HigherPoints_WinsEvenIfBothPassTheTarget()
        {
            var m = Match(Duel, Two);
            m.Points[0] = 12;
            m.Points[1] = 11;
            m.RoundWins[1] = 5;
            MutualKill(m);   // 13 vs 12
            Assert.That(m.MatchWinner, Is.EqualTo(0));
        }

        [Test]
        public void TieOnPoints_DecidedByTheLastRound()
        {
            var m = Match(Duel, Two);
            m.Points[0] = 11; m.Points[1] = 11;
            m.RoundWins[0] = 5; m.RoundWins[1] = 0;   // round wins no longer matter
            m.RoundPoints.Add(new[] { 6, 5 });
            MutualKill(m);   // 12 vs 12, this round 1 vs 1: tied last round, so the round before decides
            Assert.That(m.MatchOver, Is.True);
            Assert.That(m.Places, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(m.MatchWinner, Is.EqualTo(0));
        }

        [Test]
        public void TieOnPoints_AtMatchEnd_GoesToWhoScoredMoreInTheFinalRound()
        {
            var m = Match(Duel, Two);
            m.Points[0] = 8; m.Points[1] = 12;
            m.RoundPoints.Add(new[] { 0, 12 });
            AKillsB(m);   // 12 vs 12; this round A scored 4, B 0
            Assert.That(m.MatchOver, Is.True);
            Assert.That(m.Places, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(m.MatchWinner, Is.EqualTo(0));
        }

        [Test]
        public void Match_DoesNotEndBelowPointsToWin()
        {
            var m = Match(Duel, Two);
            m.Points[0] = 7;
            AKillsB(m);   // 11
            Assert.That(m.MatchOver, Is.False);
            Assert.That(m.Places, Is.Null);
            Assert.That(m.RoundPoints.Count, Is.EqualTo(1));
            Assert.That(m.RoundPoints[0], Is.EqualTo(new[] { 4, 0 }));
        }

        [Test]
        public void FullTieAtTheTop_ShareFirstPlace_LowestSlotIsMatchWinner()
        {
            var m = Match(Duel, Two);
            m.Points[0] = 11; m.Points[1] = 11;
            m.RoundPoints.Add(new[] { 5, 5 });
            MutualKill(m);   // 12 vs 12, equal in every round
            Assert.That(m.MatchOver, Is.True);
            Assert.That(m.Places, Is.EqualTo(new[] { 1, 1 }));
            Assert.That(m.MatchWinner, Is.EqualTo(0));
        }

        [Test]
        public void RoundPoints_IncludeTheLastStandingBonus()
        {
            var m = Match(Duel, Two);
            AKillsB(m);
            Assert.That(m.RoundPoints.Count, Is.EqualTo(1));
            Assert.That(m.RoundPoints[0], Is.EqualTo(new[] { 4, 0 }));
        }

        [Test]
        public void Rank_ByPoints()
        {
            var places = Scoring.Rank(new[] { 5, 9, 2 }, new List<int[]>());
            Assert.That(places, Is.EqualTo(new[] { 2, 1, 3 }));
        }

        [Test]
        public void Rank_TieOnPoints_LastRoundDecides()
        {
            var rounds = new List<int[]> { new[] { 6, 1, 0 }, new[] { 1, 6, 0 } };
            Assert.That(Scoring.Rank(new[] { 7, 7, 0 }, rounds), Is.EqualTo(new[] { 2, 1, 3 }));
        }

        [Test]
        public void Rank_TieOnLastRound_RoundBeforeDecides()
        {
            var rounds = new List<int[]> { new[] { 2, 5, 0 }, new[] { 3, 3, 0 }, new[] { 4, 4, 0 } };
            Assert.That(Scoring.Rank(new[] { 9, 12, 0 }, rounds), Is.EqualTo(new[] { 2, 1, 3 }));   // points differ: sanity
            Assert.That(Scoring.Rank(new[] { 9, 9, 0 }, rounds), Is.EqualTo(new[] { 2, 1, 3 }));
        }

        [Test]
        public void Rank_FullTie_SharesThePlace_AndTheNextPlaceSkips()
        {
            var rounds = new List<int[]> { new[] { 3, 3, 1, 0 }, new[] { 4, 4, 0, 0 } };
            Assert.That(Scoring.Rank(new[] { 7, 7, 1, 0 }, rounds), Is.EqualTo(new[] { 1, 1, 3, 4 }));
            Assert.That(Scoring.Rank(new[] { 7, 5, 5, 5 }, new List<int[]> { new[] { 0, 0, 0, 0 } }), Is.EqualTo(new[] { 1, 2, 2, 2 }));
        }

        [Test]
        public void MatchState_HashCoversRoundPointsAndPlaces()
        {
            var m = Match(Duel, Two);
            uint h0 = m.ComputeHash();
            var c = m.Clone();
            c.RoundPoints.Add(new[] { 1, 0 });
            Assert.That(c.ComputeHash(), Is.Not.EqualTo(h0));
            var d = m.Clone();
            d.Places = new[] { 1, 2 };
            Assert.That(d.ComputeHash(), Is.Not.EqualTo(h0));
            Assert.That(d.Clone().ComputeHash(), Is.EqualTo(d.ComputeHash()));
        }

        [Test]
        public void NewRound_FreshBoard_FullHp_StartGrit_Loaded_NoKegs_AtSpawns()
        {
            var m = Match(@"
                ...o...
                .1...2.
                .......", Two);
            m.Round.Units[1].Hp = 1;
            var p = Plans(m);
            p[0].A0 = PlannedAction.ThrowKeg(new Int2(3, 2));   // impact on the crate: destroys it
            p[0].A1 = PlannedAction.Fire(Dir.E);
            p[1].A0 = PlannedAction.Move(Dir.S);
            p[1].A1 = PlannedAction.Move(Dir.N);
            var o = MatchRunner.PlayTurn(m, p);
            Assert.That(o.EndedRound.Board.Get(new Int2(3, 2)), Is.EqualTo(TileType.Floor));
            Assert.That(o.RoundResult.Winner, Is.EqualTo(0));

            var r = m.Round;
            Assert.That(r, Is.Not.SameAs(o.EndedRound));
            Assert.That(r.Turn, Is.EqualTo(1));
            Assert.That(r.Board.Get(new Int2(3, 2)), Is.EqualTo(TileType.Crate));
            Assert.That(r.Kegs, Is.Empty);
            foreach (var u in r.Units)
            {
                Assert.That(u.Alive, Is.True);
                Assert.That(u.Hp, Is.EqualTo(3));
                Assert.That(u.Grit, Is.EqualTo(2));
                Assert.That(u.Loaded, Is.True);
                Assert.That(u.LastAttacker, Is.EqualTo(-1));
            }
            Assert.That(r.Units[0].Pos, Is.EqualTo(new Int2(1, 1)));
            Assert.That(r.Units[1].Pos, Is.EqualTo(new Int2(5, 1)));
        }

        [Test]
        public void RoundCanEnd_AtTurnStart_WhenTheCrumbleTakesSomeone()
        {
            var cfg = new GameConfig { CrumbleStartTurn = 1 };   // ring 0 warned on turn 1, falls on 2
            var m = Match(@"
                .....2.
                .1.....
                .......", Two, cfg);
            var o = MatchRunner.PlayTurn(m, Plans(m));
            Assert.That(o.TurnStart, Is.Not.Null);
            Assert.That(o.TurnStart.Contains(SimEventKind.Died), Is.True);
            Assert.That(o.RoundResult.Winner, Is.EqualTo(0));
            Assert.That(m.Points, Is.EqualTo(new[] { 3, 0 }));
        }

        [Test]
        public void TurnStart_GivesGritFromTurnTwo_ThroughTheRunner()
        {
            var m = Match(Duel, Two);
            Assert.That(m.Round.Units[0].Grit, Is.EqualTo(2));
            MatchRunner.PlayTurn(m, Plans(m));
            Assert.That(m.Round.Turn, Is.EqualTo(2));
            Assert.That(m.Round.Units[0].Grit, Is.EqualTo(3));
        }

        [TestCase(new[] { 0, 0, 0 }, -1)]
        [TestCase(new[] { 3, 1, 0 }, 0)]
        [TestCase(new[] { 3, 5, 5 }, -1)]
        [TestCase(new[] { 1, 2, 7, 6 }, 2)]
        public void Crown_IsTheStrictLeaderOnly(int[] points, int crowned)
        {
            Assert.That(Scoring.StrictLeader(points), Is.EqualTo(crowned));
        }

        [Test]
        public void MatchState_CloneAndHash()
        {
            var m = Match(Duel, Two);
            var c = m.Clone();
            Assert.That(c.ComputeHash(), Is.EqualTo(m.ComputeHash()));
            c.Points[0] = 1;
            Assert.That(c.ComputeHash(), Is.Not.EqualTo(m.ComputeHash()));
            Assert.That(m.Points[0], Is.EqualTo(0));
        }
    }
}
