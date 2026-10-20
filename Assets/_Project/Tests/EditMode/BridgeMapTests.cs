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
            Assert.That(map.Board.Get(new Int2(0, 8)), Is.EqualTo(TileType.Pit));
            Assert.That(map.Board.Get(new Int2(7, 5)), Is.EqualTo(TileType.Pillar));
            Assert.That(map.Board.Get(new Int2(2, 5)), Is.EqualTo(TileType.Crate));
            // Bridge tiles: floor in the sim, flagged for the presentation. Rows 2 and 6 hold bbb~bbb.
            int bridges = 0;
            foreach (bool b in map.Bridges) if (b) bridges++;
            Assert.That(bridges, Is.EqualTo(12));
            Assert.That(map.Bridges[6 * 15 + 4], Is.True);
            Assert.That(map.Board.Get(new Int2(4, 6)), Is.EqualTo(TileType.Floor));
            Assert.That(map.Board.Get(new Int2(7, 6)), Is.EqualTo(TileType.Pit), "the 1-tile water gap between the bridges");
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
