using System.IO;
using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    // GAME_DESIGN §3 Map 2, "The Bridge" (15x9): the text file, the embedded copy, and bots finishing matches on it.
    public class BridgeMapTests
    {
        const string BridgePath = "Assets/_Project/Data/Maps/bridge.txt";

        [Test]
        public void Bridge_Parses_15x9_WithFourSpawnsOnFloor_AndBridgeTiles()
        {
            var map = MapParser.Parse(BuiltInMaps.Bridge);
            Assert.That(map.Board.Width, Is.EqualTo(15));
            Assert.That(map.Board.Height, Is.EqualTo(9));
            Assert.That(map.Spawns, Is.EqualTo(new[] { new Int2(1, 7), new Int2(13, 7), new Int2(1, 1), new Int2(13, 1) }));
            Assert.That(map.Board.IsRemoved(new Int2(0, 8)), Is.True, "corner L of 3 removed (FIXES_4E §4.4)");
            Assert.That(map.Board.Get(new Int2(2, 8)), Is.EqualTo(TileType.Pit));
            Assert.That(map.Board.Get(new Int2(7, 4)), Is.EqualTo(TileType.Pillar), "one pillar, dead centre (4F §3)");
            Assert.That(map.Board.Get(new Int2(7, 3)), Is.EqualTo(TileType.Floor));
            Assert.That(map.Board.Get(new Int2(7, 5)), Is.EqualTo(TileType.Floor));
            int pillars = 0;
            for (int y = 0; y < map.Board.Height; y++)
                for (int x = 0; x < map.Board.Width; x++)
                    if (map.Board.Get(new Int2(x, y)) == TileType.Pillar) pillars++;
            Assert.That(pillars, Is.EqualTo(1));
            Assert.That(map.RandomCrates && !map.RandomPits, Is.True);
            int bridges = 0;
            foreach (bool b in map.Bridges) if (b) bridges++;
            Assert.That(bridges, Is.EqualTo(8), "two horizontal bridges and two vertical ones");
            Assert.That(map.Bridges[4 * 15 + 4], Is.True);
            Assert.That(map.Board.Get(new Int2(4, 4)), Is.EqualTo(TileType.Floor));
            Assert.That(map.Board.Get(new Int2(7, 6)), Is.EqualTo(TileType.Floor), "vertical bridge plank");
        }

        [Test]
        public void BridgeFile_MatchesEmbeddedConstant()
        {
            Assert.That(File.Exists(BridgePath), Is.True, BridgePath);
            string file = string.Join("\n", MapParser.SplitRows(File.ReadAllText(BridgePath)));
            Assert.That(file, Is.EqualTo(string.Join("\n", MapParser.SplitRows(BuiltInMaps.Bridge))));
        }

        [Test]
        public void BotMatches_OnTheBridge_AllFinish()
        {
            for (uint seed = 1; seed <= 60; seed++)
            {
                var bm = BotMatch.Create(seed, 2 + (int)(seed % 3), map: BuiltInMaps.Bridge);
                int turns = 0;
                while (!bm.M.MatchOver)
                {
                    bm.Step();
                    Assert.That(++turns, Is.LessThan(2000), $"seed {seed} never ended");
                }
            }
        }
    }
}
