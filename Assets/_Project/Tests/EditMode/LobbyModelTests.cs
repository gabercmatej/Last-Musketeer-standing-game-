using Musketeers.UI;
using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    // The lobby's local Day-5 state: host vs guest, ready flags, taken characters (FIXES_v0.2 §2.4).
    public class LobbyModelTests
    {
        static LobbyModel Make(int players)
        {
            var m = new LobbyModel(null, null, 2) { Players = players };
            return m;
        }

        [Test]
        public void Host_IsReady_AndBotsAreAlwaysReady_SoAllReady()
        {
            var m = Make(4);
            Assert.That(m.IsHost, Is.True);
            for (int i = 0; i < 4; i++)
                Assert.That(m.IsReady(i), Is.True, "slot " + i);
            Assert.That(m.AllReady(), Is.True);
            Assert.That(m.Ready(), Is.EqualTo(new[] { true, true, true, true }));
        }

        [Test]
        public void Guest_StartsNotReady_AndBlocksAllReady_UntilToggled()
        {
            var m = Make(3);
            m.SetHost(false);
            Assert.That(m.IsHost, Is.False);
            Assert.That(m.MyReady, Is.False);
            Assert.That(m.AllReady(), Is.False);
            Assert.That(m.Ready(), Is.EqualTo(new[] { false, true, true }));
            m.ToggleReady();
            Assert.That(m.MyReady, Is.True);
            Assert.That(m.AllReady(), Is.True);
            m.ToggleReady();
            Assert.That(m.AllReady(), Is.False);
        }

        [Test]
        public void HostCannotToggleReady_AndSwitchingRoleResetsIt()
        {
            var m = Make(2);
            m.ToggleReady();
            Assert.That(m.IsReady(0), Is.True);
            m.SetHost(false);
            m.ToggleReady();
            Assert.That(m.MyReady, Is.True);
            m.SetHost(false);
            Assert.That(m.MyReady, Is.False);
            m.SetHost(true);
            Assert.That(m.IsReady(0), Is.True);
        }

        [Test]
        public void DevBotNotReady_BlocksAllReady_OnlyForSlotsInTheMatch()
        {
            var m = Make(3);
            m.DevSetBotReady(2, false);
            Assert.That(m.AllReady(), Is.False);
            m.DevSetBotReady(2, true);
            Assert.That(m.AllReady(), Is.True);
            m.DevSetBotReady(3, false);   // slot 3 does not play with 3 players
            Assert.That(m.AllReady(), Is.True);
            m.DevSetBotReady(0, false);   // you are never a "bot"
            Assert.That(m.IsReady(0), Is.True);
        }

        [Test]
        public void CanPick_IsFalseForATakenCharacter_AndOutOfRange()
        {
            var m = Make(4);
            Assert.That(m.CanPick(1), Is.True);
            m.SetTaken(1, "Nik");
            Assert.That(m.TakenBy(1), Is.EqualTo("Nik"));
            Assert.That(m.CanPick(1), Is.False);
            Assert.That(m.CanPick(0), Is.True);
            Assert.That(m.CanPick(-1), Is.False);
            Assert.That(m.CanPick(4), Is.False);
            m.SetTaken(1, null);
            Assert.That(m.CanPick(1), Is.True);
        }
    }
}
