using System.Collections.Generic;
using System.Text;
using Musketeers.Net;
using Musketeers.UI;
using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    // Day 5 chunk A: the pure lobby rules (seats, unique musketeers, ready, bots, settings, names), the wire structs
    // and the invite-link helpers. No NetworkManager is started.
    public class LobbyRulesTests
    {
        static LobbyState Make() => new LobbyState(4, 2, 0);

        // ---------- seats ----------

        [Test]
        public void Host_TakesFirstSeat_AndIsReady()
        {
            var s = Make();
            Assert.That(s.TryAddHuman(0, "Matej", out int i), Is.EqualTo(JoinResult.Ok));
            Assert.That(i, Is.EqualTo(0));
            Assert.That(s.Entries[0].Character, Is.EqualTo(0));
            Assert.That(s.Entries[0].Ready, Is.True);
            Assert.That(s.Entries[0].IsBot, Is.False);
        }

        [Test]
        public void Joiners_GetDistinctFreeMusketeers_AndStartNotReady()
        {
            var s = Make();
            s.TryAddHuman(0, "A", out _);
            s.TryAddHuman(7, "B", out _);
            s.TryAddHuman(9, "C", out _);
            Assert.That(new[] { s.Entries[0].Character, s.Entries[1].Character, s.Entries[2].Character }, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(s.Entries[1].Ready, Is.False);
            Assert.That(s.AllReady(), Is.False);
        }

        [Test]
        public void JoiningHumans_ReplaceBots_UntilFourHumansAreSeated()
        {
            var s = Make();
            s.TryAddHuman(0, "A", out _);
            Assert.That(s.TryAddBot(), Is.True);
            Assert.That(s.TryAddBot(), Is.True);
            Assert.That(s.TryAddHuman(5, "B", out _), Is.EqualTo(JoinResult.Ok));
            Assert.That(s.Count, Is.EqualTo(4));
            Assert.That(s.TryAddHuman(6, "C", out _), Is.EqualTo(JoinResult.Ok));
            Assert.That(s.TryAddHuman(7, "D", out _), Is.EqualTo(JoinResult.Ok));
            Assert.That(s.TryAddHuman(8, "E", out _), Is.EqualTo(JoinResult.Full));
            Assert.That(s.TryAddBot(), Is.False);
        }

        [Test]
        public void Join_WhileMatchRunning_IsRefused()
        {
            var s = Make();
            s.TryAddHuman(0, "A", out _);
            s.MatchInProgress = true;
            Assert.That(s.TryAddHuman(3, "B", out _), Is.EqualTo(JoinResult.MatchInProgress));
            Assert.That(s.TryAddBot(), Is.False);
            Assert.That(s.TryRemoveBot(), Is.False);
        }

        [Test]
        public void Join_Twice_IsAlreadyJoined()
        {
            var s = Make();
            s.TryAddHuman(0, "A", out _);
            Assert.That(s.TryAddHuman(0, "A", out int i), Is.EqualTo(JoinResult.AlreadyJoined));
            Assert.That(i, Is.EqualTo(0));
            Assert.That(s.Count, Is.EqualTo(1));
        }

        [Test]
        public void Leaving_FreesTheSeat_AndTheMusketeer()
        {
            var s = Make();
            s.TryAddHuman(0, "A", out _);
            s.TryAddHuman(4, "B", out _);   // gets musketeer 1
            Assert.That(s.Remove(4), Is.True);
            Assert.That(s.Count, Is.EqualTo(1));
            Assert.That(s.TryAddHuman(8, "C", out _), Is.EqualTo(JoinResult.Ok));
            Assert.That(s.Entries[1].Character, Is.EqualTo(1));
            Assert.That(s.Remove(99), Is.False);
        }

        [Test]
        public void EmptyName_FallsBackToPlayer()
        {
            var s = Make();
            s.TryAddHuman(0, "   <>  ", out _);
            Assert.That(s.Entries[0].Name, Is.EqualTo(LobbyRules.FallbackName));
        }

        // ---------- unique character lock ----------

        [Test]
        public void Pick_TakenMusketeer_IsRefused_FreeOneIsGranted()
        {
            var s = Make();
            s.TryAddHuman(0, "A", out _);   // 0
            s.TryAddHuman(4, "B", out _);   // 1
            Assert.That(s.TryPickCharacter(4, 0), Is.False, "A holds musketeer 0");
            Assert.That(s.Entries[1].Character, Is.EqualTo(1));
            Assert.That(s.TryPickCharacter(4, 3), Is.True);
            Assert.That(s.Entries[1].Character, Is.EqualTo(3));
            Assert.That(s.TryPickCharacter(0, 1), Is.True, "1 is free again");
        }

        [Test]
        public void Pick_OwnMusketeer_IsFine_OutOfRangeAndUnknownAreNot()
        {
            var s = Make();
            s.TryAddHuman(0, "A", out _);
            Assert.That(s.TryPickCharacter(0, 0), Is.True);
            Assert.That(s.TryPickCharacter(0, 4), Is.False);
            Assert.That(s.TryPickCharacter(0, -1), Is.False);
            Assert.That(s.TryPickCharacter(42, 2), Is.False);
        }

        [Test]
        public void Bots_TakeFreeMusketeers_AndLockThem()
        {
            var s = Make();
            s.TryAddHuman(0, "A", out _);       // 0
            s.TryAddBot();                      // 1
            s.TryAddBot();                      // 2
            Assert.That(s.Entries[1].IsBot && s.Entries[1].Ready && s.Entries[1].Character == 1, Is.True);
            Assert.That(s.Entries[2].Character, Is.EqualTo(2));
            s.TryAddHuman(5, "B", out _);       // 3
            Assert.That(s.TryPickCharacter(5, 1), Is.False, "a bot holds 1");
            Assert.That(s.HolderOf(1), Is.EqualTo(1));
            Assert.That(s.HolderOf(1, except: 1), Is.EqualTo(-1));
        }

        [Test]
        public void RemoveBot_RemovesTheLastBot_AndFreesItsMusketeer()
        {
            var s = Make();
            s.TryAddHuman(0, "A", out _);
            s.TryAddBot();
            s.TryAddBot();
            Assert.That(s.TryRemoveBot(), Is.True);
            Assert.That(s.Count, Is.EqualTo(2));
            Assert.That(s.HolderOf(2), Is.EqualTo(-1));
            Assert.That(s.TryRemoveBot(), Is.True);
            Assert.That(s.TryRemoveBot(), Is.False);
            Assert.That(s.Count, Is.EqualTo(1));
        }

        // ---------- ready ----------

        [Test]
        public void HostCannotUnready_GuestCan_AllReadyFollows()
        {
            var s = Make();
            s.TryAddHuman(0, "A", out _);
            s.TryAddHuman(4, "B", out _);
            s.TrySetReady(0, false);
            Assert.That(s.Entries[0].Ready, Is.True);
            Assert.That(s.AllReady(), Is.False);
            s.TrySetReady(4, true);
            Assert.That(s.AllReady(), Is.True);
            s.TrySetReady(4, false);
            Assert.That(s.AllReady(), Is.False);
            Assert.That(s.TrySetReady(77, true), Is.False);
        }

        [Test]
        public void CanStart_NeedsTwoSeats()
        {
            var s = Make();
            s.TryAddHuman(0, "A", out _);
            Assert.That(s.CanStart, Is.False);
            s.TryAddBot();
            Assert.That(s.CanStart, Is.True);
        }

        // ---------- settings ----------

        [TestCase(6, 1, 0, true)]
        [TestCase(30, 5, 1, true)]
        [TestCase(12, 3, 0, true)]
        [TestCase(5, 3, 0, false)]
        [TestCase(31, 3, 0, false)]
        [TestCase(12, 0, 0, false)]
        [TestCase(12, 6, 0, false)]
        [TestCase(12, 3, 2, false)]    // only maps 0 and 1 exist
        [TestCase(12, 3, -1, false)]
        public void SettingsValidation(int points, int hearts, int map, bool valid)
        {
            var d = new LobbySettingsData { Map = map, PointsToWin = points, Hearts = hearts };
            Assert.That(LobbyRules.IsValidSettings(d, 2), Is.EqualTo(valid));
            var s = Make();
            Assert.That(s.TrySetSettings(d), Is.EqualTo(valid));
            if (!valid)
                Assert.That(s.Settings.PointsToWin, Is.EqualTo(12), "rejected settings change nothing");
        }

        [Test]
        public void Clamps_AndDefaults()
        {
            Assert.That(LobbyRules.ClampPoints(1), Is.EqualTo(6));
            Assert.That(LobbyRules.ClampPoints(99), Is.EqualTo(30));
            Assert.That(LobbyRules.ClampHearts(0), Is.EqualTo(1));
            Assert.That(LobbyRules.ClampHearts(9), Is.EqualTo(5));
            var s = Make();
            s.TrySetSettings(new LobbySettingsData { Map = 1, PointsToWin = 20, Hearts = 5 });
            s.ResetRules();
            Assert.That(s.Settings.PointsToWin, Is.EqualTo(12));
            Assert.That(s.Settings.Hearts, Is.EqualTo(3));
            Assert.That(s.Settings.Map, Is.EqualTo(1), "Default resets the rules, not the map");
        }

        // ---------- names ----------

        [Test]
        public void SanitizeName_KeepsSlovenianAndCroatianLetters()
        {
            Assert.That(LobbyRules.SanitizeName("Črt Šmid"), Is.EqualTo("Črt Šmid"));
            Assert.That(LobbyRules.SanitizeName("žćđ"), Is.EqualTo("žćđ"));
            Assert.That(LobbyRules.SanitizeName("ČŠŽĆĐ čšžćđ"), Is.EqualTo("ČŠŽĆĐ čšžćđ"));
        }

        [Test]
        public void SanitizeName_CollapsesSpaces_AndDropsSymbols()
        {
            Assert.That(LobbyRules.SanitizeName("  Matej   G  "), Is.EqualTo("Matej G"));
            Assert.That(LobbyRules.SanitizeName("<b>Nik</b>\n"), Is.EqualTo("bNikb"));
            Assert.That(LobbyRules.SanitizeName("Al-jaž O'Neil."), Is.EqualTo("Al-jaž O'Neil."));
            Assert.That(LobbyRules.SanitizeName("a—b"), Is.EqualTo("ab"));
            Assert.That(LobbyRules.SanitizeName(null), Is.EqualTo(""));
        }

        [Test]
        public void SanitizeName_CapsAt20Chars_AndFitsTheWire()
        {
            string longName = new string('Š', 40);
            string clean = LobbyRules.SanitizeName(longName);
            Assert.That(clean.Length, Is.EqualTo(20));
            Assert.That(Encoding.UTF8.GetByteCount(clean), Is.LessThanOrEqualTo(LobbyRules.MaxNameBytes));
            // Three-byte letters (Japanese) still fit the 63-byte field.
            string wide = LobbyRules.SanitizeName(new string('あ', 30));
            Assert.That(Encoding.UTF8.GetByteCount(wide), Is.LessThanOrEqualTo(LobbyRules.MaxNameBytes));
            var slot = LobbySlot.From(new LobbyEntry { Name = wide, Loadout = "" });
            Assert.That(slot.Name.ToString(), Is.EqualTo(wide));
        }

        [Test]
        public void Loadout_MustFitTheWire()
        {
            Assert.That(LobbyRules.IsValidLoadout("hat_a|face_b|cape_c|"), Is.True);
            Assert.That(LobbyRules.IsValidLoadout(new string('x', 200)), Is.False);
            var s = Make();
            s.TryAddHuman(0, "A", out _);
            Assert.That(s.TrySetLoadout(0, new string('x', 200)), Is.False);
            Assert.That(s.TrySetLoadout(0, "hat|face|cape|"), Is.True);
            Assert.That(s.Entries[0].Loadout, Is.EqualTo("hat|face|cape|"));
        }

        // ---------- wire structs ----------

        [Test]
        public void LobbySlot_RoundTripsAnEntry_AndComparesByValue()
        {
            var e = new LobbyEntry { ClientId = 3, Name = "Žiga Čeč", Character = 2, Ready = true, IsBot = false, Loadout = "a|b|c|" };
            var slot = LobbySlot.From(e);
            var back = slot.ToEntry();
            Assert.That(back.Name, Is.EqualTo("Žiga Čeč"));
            Assert.That(back.ClientId, Is.EqualTo(3UL));
            Assert.That(back.Character, Is.EqualTo(2));
            Assert.That(back.Ready, Is.True);
            Assert.That(back.Loadout, Is.EqualTo("a|b|c|"));
            Assert.That(slot.Equals(LobbySlot.From(e)), Is.True);
            e.Ready = false;
            Assert.That(slot.Equals(LobbySlot.From(e)), Is.False);
        }

        [Test]
        public void LobbySettings_RoundTrips()
        {
            var d = new LobbySettingsData { Map = 1, PointsToWin = 18, Hearts = 4 };
            var back = LobbySettings.From(d).ToData();
            Assert.That(back.Map, Is.EqualTo(1));
            Assert.That(back.PointsToWin, Is.EqualTo(18));
            Assert.That(back.Hearts, Is.EqualTo(4));
        }

        // ---------- invite link ----------

        [Test]
        public void JoinCode_IsReadFromTheUrl()
        {
            Assert.That(NetSession.ReadJoinCode("https://last-musketeer.netlify.app/?join=ab12cd"), Is.EqualTo("AB12CD"));
            Assert.That(NetSession.ReadJoinCode("https://x.app/?autoplay=3&join=K7M2QX#frag"), Is.EqualTo("K7M2QX"));
            Assert.That(NetSession.ReadJoinCode("https://x.app/"), Is.EqualTo(""));
            Assert.That(NetSession.ReadJoinCode("https://x.app/?map=bridge"), Is.EqualTo(""));
            Assert.That(NetSession.CleanCode(" a-b c1! "), Is.EqualTo("ABC1"));
        }

        [Test]
        public void InviteLink_DropsTheQuery_AddsJoin()
        {
            Assert.That(NetSession.InviteLink("ABC123", "https://x.app/play/?autoplay=3#t"), Is.EqualTo("https://x.app/play/?join=ABC123"));
            Assert.That(NetSession.InviteLink("ABC123", "https://x.app/"), Is.EqualTo("https://x.app/?join=ABC123"));
        }

        // ---------- the online lobby model ----------

        sealed class FakeBackend : ILobbyBackend
        {
            public readonly List<string> Calls = new List<string>();
            public void Pick(int character) => Calls.Add("pick" + character);
            public void SetName(string name) => Calls.Add("name" + name);
            public void SetReady(bool ready) => Calls.Add("ready" + ready);
            public void SetLoadout(string s) => Calls.Add("loadout");
            public void SetSettings(int map, int p, int h) => Calls.Add($"settings{map},{p},{h}");
            public void DefaultSettings() => Calls.Add("default");
            public void AddBot() => Calls.Add("addbot");
            public void RemoveBot() => Calls.Add("removebot");
        }

        static List<LobbyEntry> Seats() => new List<LobbyEntry>
        {
            new LobbyEntry { ClientId = 0, Name = "Host", Character = 0, Ready = true },
            new LobbyEntry { ClientId = 5, Name = "Me", Character = 2, Ready = false },
            new LobbyEntry { IsBot = true, Character = 3, Ready = true },
        };

        [Test]
        public void OnlineModel_ShowsMeFirst_AndMarksOthersTaken()
        {
            var m = new LobbyModel(null, null, 2);
            var be = new FakeBackend();
            m.EnterOnline(be);
            Assert.That(m.ApplyOnline(Seats(), 5, new LobbySettingsData { Map = 1, PointsToWin = 20, Hearts = 2 }, false), Is.True);
            Assert.That(m.Characters(), Is.EqualTo(new[] { 2, 0, 3 }));
            Assert.That(m.SeatNames(), Is.EqualTo(new[] { "Me", "Host", "Bot" }));
            Assert.That(m.Character, Is.EqualTo(2));
            Assert.That(m.Players, Is.EqualTo(3));
            Assert.That(m.IsHost, Is.False);
            Assert.That(m.PointsToWin, Is.EqualTo(20));
            Assert.That(m.Map, Is.EqualTo(1));
            Assert.That(m.TakenBy(0), Is.EqualTo("Host"));
            Assert.That(m.TakenBy(3), Is.EqualTo("Bot"));
            Assert.That(m.TakenBy(2), Is.Null, "your own musketeer is not 'taken'");
            Assert.That(m.CanPick(1), Is.True);
            Assert.That(m.CanPick(0), Is.False);
            Assert.That(m.Ready(), Is.EqualTo(new[] { false, true, true }));
            Assert.That(m.BotCount, Is.EqualTo(1));
        }

        [Test]
        public void OnlineModel_ActionsGoToTheBackend_NotTheModel()
        {
            var m = new LobbyModel(null, null, 2);
            var be = new FakeBackend();
            m.EnterOnline(be);
            m.ApplyOnline(Seats(), 5, LobbyRules.DefaultSettings(), false);
            m.ToggleReady();
            m.PickCharacter(1);
            m.AddBot();
            m.RemoveBot();
            Assert.That(be.Calls, Does.Contain("readyTrue"));
            Assert.That(be.Calls, Does.Contain("pick1"));
            Assert.That(be.Calls, Does.Contain("addbot"));
            Assert.That(be.Calls, Does.Contain("removebot"));
            Assert.That(m.Character, Is.EqualTo(2), "the character only changes when the host's snapshot says so");
        }

        [Test]
        public void OnlineModel_HostSeesItselfReady_AndExitRestoresOffline()
        {
            var m = new LobbyModel(null, null, 2);
            m.EnterOnline(new FakeBackend());
            Assert.That(m.ApplyOnline(Seats(), 0, LobbyRules.DefaultSettings(), true), Is.True);
            Assert.That(m.IsHost && m.MyReady, Is.True);
            Assert.That(m.ApplyOnline(Seats(), 99, LobbyRules.DefaultSettings(), true), Is.False, "no seat for this client yet");
            m.ExitOnline();
            Assert.That(m.Online, Is.False);
            Assert.That(m.TakenBy(0), Is.Null);
            Assert.That(m.SeatNames(), Is.Null);
            Assert.That(m.IsHost, Is.True);
        }
    }
}
