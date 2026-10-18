using System;
using System.IO;
using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    // GAME_DESIGN §3 maps and MapParser.
    public class MapTests
    {
        const string CourtyardPath = "Assets/_Project/Data/Maps/courtyard.txt";

        [Test]
        public void Courtyard_Parses_WithSpawnsPitsPillarsAndCrates()
        {
            var map = MapParser.Parse(BuiltInMaps.Courtyard);
            var b = map.Board;
            Assert.That(b.Width, Is.EqualTo(9));
            Assert.That(b.Height, Is.EqualTo(9));
            Assert.That(map.Spawns, Is.EqualTo(new[] { new Int2(1, 7), new Int2(7, 7), new Int2(1, 1), new Int2(7, 1) }));

            int pits = 0, pillars = 0, crates = 0;
            for (int y = 0; y < 9; y++)
            for (int x = 0; x < 9; x++)
            {
                switch (b.Get(new Int2(x, y)))
                {
                    case TileType.Pit: pits++; break;
                    case TileType.Pillar: pillars++; break;
                    case TileType.Crate: crates++; break;
                }
            }
            Assert.That(pits, Is.EqualTo(4));
            Assert.That(pillars, Is.EqualTo(4));
            Assert.That(crates, Is.EqualTo(4));

            foreach (var p in new[] { new Int2(3, 5), new Int2(5, 5), new Int2(3, 3), new Int2(5, 3) })
                Assert.That(b.Get(p), Is.EqualTo(TileType.Pit), $"pit at {p}");
            foreach (var p in new[] { new Int2(2, 6), new Int2(6, 6), new Int2(2, 2), new Int2(6, 2) })
                Assert.That(b.Get(p), Is.EqualTo(TileType.Pillar), $"pillar at {p}");
            foreach (var p in new[] { new Int2(4, 7), new Int2(1, 4), new Int2(7, 4), new Int2(4, 1) })
                Assert.That(b.Get(p), Is.EqualTo(TileType.Crate), $"crate at {p}");
            foreach (var p in map.Spawns)
                Assert.That(b.Get(p), Is.EqualTo(TileType.Floor), $"spawn {p} is floor");
        }

        [Test]
        public void CourtyardFile_MatchesEmbeddedConstant()
        {
            Assert.That(File.Exists(CourtyardPath), Is.True, CourtyardPath);
            string file = string.Join("\n", MapParser.SplitRows(File.ReadAllText(CourtyardPath)));
            string embedded = string.Join("\n", MapParser.SplitRows(BuiltInMaps.Courtyard));
            Assert.That(file, Is.EqualTo(embedded));
        }

        [Test]
        public void GameStateCreate_PlacesPlayersOnSpawnsInSlotOrder()
        {
            var map = MapParser.Parse(BuiltInMaps.Courtyard);
            var cfg = new GameConfig();
            var s = GameState.Create(map.Board, map.Spawns, new[] { Ability.Grappin, Ability.Switcheroo, Ability.BullRush }, cfg, 42);
            Assert.That(s.Units.Length, Is.EqualTo(3));
            Assert.That(s.Units[2].Pos, Is.EqualTo(new Int2(1, 1)));
            Assert.That(s.Units[2].Ability, Is.EqualTo(Ability.BullRush));
            Assert.That(s.Units[0].Hp, Is.EqualTo(cfg.MaxHp));
            Assert.That(s.Units[0].Grit, Is.EqualTo(cfg.StartGrit));
            Assert.That(s.Units[0].Loaded, Is.True);
            Assert.That(s.Turn, Is.EqualTo(1));
            Assert.That(s.Board, Is.Not.SameAs(map.Board), "the round gets its own copy of the board");
        }

        [TestCase(".x.", TestName = "Rejects_UnknownSymbol")]
        [TestCase("...\n..", TestName = "Rejects_RaggedRows")]
        [TestCase(".1.\n.3.", TestName = "Rejects_SpawnGap")]
        [TestCase("1.1", TestName = "Rejects_DuplicateSpawn")]
        [TestCase("  \n ", TestName = "Rejects_Empty")]
        public void Parse_RejectsBadMaps(string ascii)
        {
            Assert.Throws<FormatException>(() => MapParser.Parse(ascii));
        }
    }
}
