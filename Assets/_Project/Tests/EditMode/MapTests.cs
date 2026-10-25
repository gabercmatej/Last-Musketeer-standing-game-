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
        public void Courtyard_Parses_15x9_WithSpawnsPitsPillarsAndCrates()
        {
            var map = MapParser.Parse(BuiltInMaps.Courtyard);
            var b = map.Board;
            Assert.That(b.Width, Is.EqualTo(15));
            Assert.That(b.Height, Is.EqualTo(9));
            Assert.That(b.EdgeWalls, Is.True, "the Courtyard is walled");
            Assert.That(map.Spawns, Is.EqualTo(new[] { new Int2(1, 7), new Int2(13, 7), new Int2(1, 1), new Int2(13, 1) }));

            // FIXES_4E §4.6: only the pillars are fixed; pits and crates are random every round.
            int pits = 0, pillars = 0, crates = 0, removed = 0;
            for (int y = 0; y < 9; y++)
            for (int x = 0; x < 15; x++)
            {
                if (b.IsRemoved(new Int2(x, y))) { removed++; continue; }
                switch (b.Get(new Int2(x, y)))
                {
                    case TileType.Pit: pits++; break;
                    case TileType.Pillar: pillars++; break;
                    case TileType.Crate: crates++; break;
                }
            }
            Assert.That(pits, Is.EqualTo(0));
            Assert.That(crates, Is.EqualTo(0));
            Assert.That(pillars, Is.EqualTo(4));
            Assert.That(removed, Is.EqualTo(12));
            Assert.That(map.RandomPits && map.RandomCrates, Is.True);
            foreach (var p in new[] { new Int2(3, 6), new Int2(11, 6), new Int2(3, 2), new Int2(11, 2) })
                Assert.That(b.Get(p), Is.EqualTo(TileType.Pillar), $"pillar at {p}");
            Assert.That(b.Get(new Int2(7, 4)), Is.EqualTo(TileType.Floor));
            foreach (var p in map.Spawns)
                Assert.That(b.Get(p), Is.EqualTo(TileType.Floor), $"spawn {p} is floor");
        }

        [Test]
        public void CourtyardCenter_AllowsMovementShotsAndPushes()
        {
            string ascii = BuiltInMaps.Courtyard.Replace('1', '.').Replace('2', '.').Replace('3', '.').Replace('4', '.');
            // Use the real map row, so restoring the center pillar breaks all three cases.
            var rows = MapParser.SplitRows(ascii);
            string CenterScenario(string row) => string.Join("\n", new[] {
                rows[0], rows[1], rows[2], rows[3], row, rows[5], rows[6], rows[7], rows[8] });
            char center = rows[4][7];
            Scenario.From(CenterScenario("......A" + center + "..B...."))
                .Plan('A', Act.Move(Dir.E)).ResolveTick().AssertPos('A', 7, 4);
            Scenario.From(CenterScenario("......A" + center + "B......"))
                .Plan('A', Act.Fire(Dir.E)).ResolveTick().AssertHp('B', 2);
            Scenario.From(CenterScenario(".....AB" + center + "......."))
                .Plan('A', Act.Shove(Dir.E)).ResolveTick().AssertPos('B', 7, 4).AssertHp('B', 3);
        }

        [Test]
        public void EveryRound_AllFourCharactersStartAtThreeGrit_OnBothMaps_AndSnapshotPreservesIt()
        {
            foreach (string ascii in new[] { BuiltInMaps.Courtyard, BuiltInMaps.Bridge })
            {
                var cfg = new GameConfig { PointsToWin = 100 };
                var m = MatchRunner.NewMatch(MapParser.Parse(ascii),
                    new[] { Ability.Grappin, Ability.Switcheroo, Ability.TripleCharge, Ability.BullRush }, cfg, 42);
                for (int round = 1; round <= 4; round++)
                {
                    Assert.That(m.RoundNumber, Is.EqualTo(round));
                    Assert.That(m.Round.Turn, Is.EqualTo(1));
                    foreach (var unit in m.Round.Units)
                        Assert.That(unit.Grit, Is.EqualTo(3));
                    var restored = MatchStateSerializer.Deserialize(MatchStateSerializer.Serialize(m), m);
                    Assert.That(restored.ComputeHash(), Is.EqualTo(m.ComputeHash()));
                    restored.Round.Units[0].Grit--;
                    Assert.That(restored.ComputeHash(), Is.Not.EqualTo(m.ComputeHash()));

                    // Let the unchanged crumble rule end this round, after spending everyone's Grit.
                    foreach (var unit in m.Round.Units)
                    {
                        unit.Grit = 0;
                        unit.Pos = new Int2(5 + unit.Slot, 4);
                    }
                    m.Round.Turn = Crumble.FallTurn(4, cfg) - 1;
                    MatchRunner.PlayTurn(m, new[] { new TurnPlan(0), new TurnPlan(1), new TurnPlan(2), new TurnPlan(3) });
                }
                Assert.That(cfg.MaxGrit, Is.EqualTo(5));
            }
        }

        [Test]
        public void Directives_WallsSetTheFlag_UnknownThrows_BridgeTilesAreFloorAndRecorded()
        {
            Assert.That(MapParser.Parse("@walls\n1.2").Board.EdgeWalls, Is.True);
            Assert.That(MapParser.Parse("1.2").Board.EdgeWalls, Is.False);
            Assert.Throws<FormatException>(() => MapParser.Parse("@lava\n1.2"));
            var m = MapParser.Parse("1b2");
            Assert.That(m.Board.Get(new Int2(1, 0)), Is.EqualTo(TileType.Floor));
            Assert.That(m.Bridges, Is.EqualTo(new[] { false, true, false }));
        }

        [Test]
        public void WalledBoard_OutsideIsAPillar_UnwalledIsAPit_AndTheFlagSurvivesCloneAndRoundSetup()
        {
            var walled = MapParser.Parse(BuiltInMaps.Courtyard);
            Assert.That(walled.Board.Get(new Int2(-1, 0)), Is.EqualTo(TileType.Pillar));
            Assert.That(walled.Board.Get(new Int2(15, 3)), Is.EqualTo(TileType.Pillar));
            Assert.That(walled.Board.Clone().EdgeWalls, Is.True);
            var s = GameState.Create(walled.Board, walled.Spawns, new[] { Ability.Grappin, Ability.Switcheroo }, new GameConfig(), 1);
            Assert.That(s.Board.EdgeWalls, Is.True);
            Assert.That(s.Clone().Board.EdgeWalls, Is.True);
            var m = MatchRunner.NewMatch(walled, new[] { Ability.Grappin, Ability.Switcheroo }, new GameConfig(), 1);
            Assert.That(m.Round.Board.EdgeWalls, Is.True);
            Assert.That(m.Clone().Round.Board.EdgeWalls, Is.True);

            var open = MapParser.Parse(BuiltInMaps.Bridge);
            Assert.That(open.Board.EdgeWalls, Is.False);
            Assert.That(open.Board.Get(new Int2(-1, 0)), Is.EqualTo(TileType.Pit));
        }

        [Test]
        public void CourtyardFile_MatchesEmbeddedConstant()
        {
            Assert.That(File.Exists(CourtyardPath), Is.True, CourtyardPath);
            string file = string.Join("\n", MapParser.SplitRows(File.ReadAllText(CourtyardPath)));
            string embedded = string.Join("\n", MapParser.SplitRows(BuiltInMaps.Courtyard));
            Assert.That(file, Is.EqualTo(embedded));
            Assert.That(MapParser.Directives(File.ReadAllText(CourtyardPath)), Is.EqualTo(MapParser.Directives(BuiltInMaps.Courtyard)));
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

        [TestCase(".z.", TestName = "Rejects_UnknownSymbol")]
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
