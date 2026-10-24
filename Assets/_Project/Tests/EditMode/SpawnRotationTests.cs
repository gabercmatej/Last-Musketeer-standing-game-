using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    public class SpawnRotationTests
    {
        static readonly int[,] Schedule = { { 0,1,2,3 }, { 1,3,0,2 }, { 0,2,3,1 }, { 3,0,1,2 } };
        static readonly int[] Clockwise = { 0,1,3,2 };

        [TestCase(BuiltInMaps.Courtyard)]
        [TestCase(BuiltInMaps.Bridge)]
        public void ActualRoundStartsFollowExactScheduleAndRepeat(string ascii)
        {
            var cfg = new GameConfig { PointsToWin = 10000 };
            var m = MatchRunner.NewMatch(MapParser.Parse(ascii), new Ability[4], cfg, 17);
            var replica = m.Clone();
            for (int r = 0; r < 8; r++)
            {
                for (int position = 0; position < 4; position++)
                {
                    int player = Clockwise[Schedule[r % 4, position]];
                    Assert.That(m.Round.Units[player].Pos, Is.EqualTo(m.Map.Spawns[Clockwise[position]]), $"round {r+1}, player {player}");
                }
                Assert.That(m.ComputeHash(), Is.EqualTo(replica.ComputeHash()));
                EndRound(m); EndRound(replica);
            }
        }

        static void EndRound(MatchState m)
        {
            for (int i = 1; i < m.PlayerCount; i++) m.Round.Units[i].Alive = false;
            var plans = new TurnPlan[m.PlayerCount];
            for (int i = 0; i < plans.Length; i++) plans[i] = new TurnPlan(i);
            MatchRunner.PlayTurn(m, plans);
        }

        [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void UniqueExistingSpawnsAndNoConsecutiveStationaryPlayer(int players)
        {
            var m = MatchRunner.NewMatch(MapParser.Parse(BuiltInMaps.Courtyard), new Ability[players], new GameConfig { PointsToWin = 10000 }, 1);
            for (int r = 0; r < 8; r++)
            {
                var old = m.Round.Clone();
                EndRound(m);
                for (int i = 0; i < players; i++)
                {
                    Assert.That(m.Map.Spawns, Does.Contain(m.Round.Units[i].Pos));
                    Assert.That(m.Round.Units[i].Pos, Is.Not.EqualTo(old.Units[i].Pos));
                    for (int j = 0; j < i; j++) Assert.That(m.Round.Units[i].Pos, Is.Not.EqualTo(m.Round.Units[j].Pos));
                }
            }
        }

        [Test]
        public void EveryPairIsAdjacentAtLeastTwice()
        {
            var m = MatchRunner.NewMatch(MapParser.Parse(BuiltInMaps.Courtyard), new Ability[4], new GameConfig { PointsToWin = 10000 }, 1);
            var counts = new int[4,4];
            for (int r = 0; r < 4; r++)
            {
                for (int a = 0; a < 4; a++) for (int b = a+1; b < 4; b++)
                {
                    var x = m.Round.Units[a].Pos; var y = m.Round.Units[b].Pos;
                    if (x.X == y.X || x.Y == y.Y) counts[a,b]++;
                }
                EndRound(m);
            }
            for (int a = 0; a < 4; a++) for (int b = a+1; b < 4; b++) Assert.That(counts[a,b], Is.GreaterThanOrEqualTo(2));
        }
    }
}
