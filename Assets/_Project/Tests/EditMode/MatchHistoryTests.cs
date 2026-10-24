using Musketeers.Game;
using Musketeers.UI;
using NUnit.Framework;
using UnityEngine;

namespace Musketeers.Sim.Tests
{
    public class MatchHistoryTests
    {
        const string Key = "Musketeers.Tests.MatchHistory";
        static readonly string[] Ids = { "matej", "matic", "aljaz", "nik" };

        MatchState Finished()
        {
            var m = MatchRunner.NewMatch(MapParser.Parse(BuiltInMaps.Courtyard),
                new[] { Ability.Grappin, Ability.Switcheroo }, new GameConfig(), 42, out _);
            m.MatchOver = true;
            m.Places = new[] { 1, 2 };
            m.Points[0] = 12;
            m.Points[1] = -1;
            m.RoundWins[0] = 3;
            m.Stats.Kills[0] = 4;
            m.Stats.Deaths[1] = 2;
            return m;
        }

        MatchHistoryRecord Record(string id = "match-1", bool online = true, bool completed = true, bool bot = false)
            => MatchHistoryRecord.Create(id, online, completed, new[] { false, bot }, Finished(),
                new[] { "matej", "matic" }, new[] { "One", "Two" }, "courtyard", "2026-10-24 12:00 UTC");

        [TearDown] public void Cleanup() { PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save(); }

        [TestCase(false, true, false)]
        [TestCase(true, false, false)]
        [TestCase(true, true, true)]
        public void OfflineAbortedOrMixedMatch_IsIneligible(bool online, bool completed, bool bot)
            => Assert.That(Record(online: online, completed: completed, bot: bot), Is.Null);

        [Test] public void UnfinishedOrMissingMatchId_IsIneligible()
        {
            var m = Finished(); m.MatchOver = false;
            Assert.That(MatchHistoryRecord.Create("id", true, true, new[] { false, false }, m,
                new[] { "matej", "matic" }, new[] { "A", "B" }, "bridge", "today"), Is.Null);
            Assert.That(Record(id: ""), Is.Null);
        }

        [Test] public void Retry_DoesNotDuplicate_AndNewMatchAppearsFirst()
        {
            var data = new MatchHistoryData();
            Assert.That(data.Add(Record()), Is.True);
            Assert.That(data.Add(Record()), Is.False);
            Assert.That(data.Add(Record("match-2")), Is.True);
            Assert.That(data.Matches.Count, Is.EqualTo(2));
            Assert.That(data.Matches[0].MatchId, Is.EqualTo("match-2"));
            Assert.That(data.Leaderboard(Ids)[0].Wins, Is.EqualTo(2));
        }

        [Test] public void Leaderboard_IsExactlyRoster_DerivedFromRecords_WithStableTiesAndSharedWins()
        {
            var record = Record(); record.Players[1].Place = 1;
            var data = new MatchHistoryData(); data.Add(record);
            var board = data.Leaderboard(Ids);
            Assert.That(board.Count, Is.EqualTo(4));
            Assert.That(board[0].CharacterId, Is.EqualTo("matej"));
            Assert.That(board[1].CharacterId, Is.EqualTo("matic"));
            Assert.That(board[0].Wins, Is.EqualTo(1)); Assert.That(board[1].Wins, Is.EqualTo(1));
            Assert.That(board[2].CharacterId, Is.EqualTo("aljaz"));
            Assert.That(board[3].CharacterId, Is.EqualTo("nik"));
            data.Matches.Clear();
            Assert.That(data.Leaderboard(Ids).TrueForAll(w => w.Wins == 0), Is.True);
        }

        [Test] public void Reload_RetainsNamesStatsBadgesAndIdempotence()
        {
            PlayerPrefs.DeleteKey(Key);
            var store = new MatchHistoryStore(Key);
            Assert.That(store.Record(Record()), Is.True);
            var reloaded = new MatchHistoryStore(Key);
            var player = reloaded.Data.Matches[0].Players[0];
            Assert.That(player.Name, Is.EqualTo("One"));
            Assert.That(player.RoundWins, Is.EqualTo(3));
            Assert.That(player.Kills, Is.EqualTo(4));
            Assert.That(player.Badges.Count, Is.GreaterThan(0));
            Assert.That(reloaded.Data.Matches[0].Players[1].Deaths, Is.EqualTo(2));
            Assert.That(reloaded.Data.Matches[0].Players[1].Points, Is.EqualTo(-1));
            Assert.That(reloaded.Record(Record()), Is.False);
        }

        [Test] public void CorruptStorage_IsPreservedAndNotOverwritten()
        {
            PlayerPrefs.SetString(Key, "not-json");
            var store = new MatchHistoryStore(Key);
            Assert.That(store.Error, Is.Not.Null);
            Assert.That(store.Record(Record()), Is.False);
            Assert.That(PlayerPrefs.GetString(Key), Is.EqualTo("not-json"));
        }
        [Test] public void EqualVictories_UseMostRecentWin_ThenStableCharacterId()
        {
            var old = Record("old"); old.CompletedUtc = "2026-10-24T10:00:00Z";
            var recent = Record("recent"); recent.CompletedUtc = "2026-10-24T11:00:00Z";
            recent.Players[0].Place = 2; recent.Players[1].Place = 1;
            var data = new MatchHistoryData(); data.Add(old); data.Add(recent);
            Assert.That(data.Leaderboard(Ids)[0].CharacterId, Is.EqualTo("matic"));
            recent.CompletedUtc = old.CompletedUtc;
            Assert.That(data.Leaderboard(Ids)[0].CharacterId, Is.EqualTo("matej"));
            Assert.That(data.Leaderboard(Ids)[2].CharacterId, Is.EqualTo("aljaz"));
        }
    }
}
