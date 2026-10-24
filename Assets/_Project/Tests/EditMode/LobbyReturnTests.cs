using Musketeers.Net;
using NUnit.Framework;
namespace Musketeers.Sim.Tests
{
    public class LobbyReturnTests
    {
        [Test] public void IndependentReturnResetsReadyAndWaitsForLatePlayer()
        {
            var s = new LobbyState(4, 2); s.TryAddHuman(0,"Host",out _); s.TryAddHuman(5,"Guest",out _);
            s.TrySetReady(5,true); Assert.IsTrue(s.BeginMatch());
            Assert.IsFalse(s.ReturnToLobby(5)); Assert.IsFalse(s.TrySetReady(5,true));
            s.CompleteMatch(); Assert.IsFalse(s.CanStart);
            Assert.IsTrue(s.ReturnToLobby(0)); Assert.IsTrue(s.Entries[1].InResults);
            Assert.IsFalse(s.CanStart); Assert.IsFalse(s.BeginMatch());
            Assert.IsTrue(s.ReturnToLobby(5)); Assert.IsFalse(s.Entries[1].Ready);
            Assert.IsTrue(s.CanStart); Assert.IsFalse(s.ReturnToLobby(5));
            s.TrySetReady(5,true); s.CompleteMatch(); Assert.IsTrue(s.Entries[1].Ready);
        }
        [Test] public void LeavingResultsDoesNotStrandRemainingLobby()
        {
            var s = new LobbyState(4, 2); s.TryAddHuman(0,"Host",out _); s.TryAddHuman(5,"Guest",out _); s.TryAddBot();
            s.BeginMatch(); s.CompleteMatch(); s.ReturnToLobby(0); s.Remove(5);
            Assert.IsTrue(s.CanStart); Assert.IsFalse(s.WaitingForResults);
        }
        [Test] public void DuplicateAndUnknownReturnsCannotResetReadyOrReturnOtherHumans()
        {
            var s = new LobbyState(4, 2);
            s.TryAddHuman(0, "Host", out _);
            s.TryAddHuman(5, "Guest", out _);
            s.BeginMatch();
            s.CompleteMatch();
            Assert.IsFalse(s.ReturnToLobby(99));
            Assert.IsTrue(s.Entries[0].InResults);
            Assert.IsTrue(s.Entries[1].InResults);
            Assert.IsTrue(s.ReturnToLobby(5));
            Assert.IsTrue(s.TrySetReady(5, true));
            Assert.IsFalse(s.ReturnToLobby(5));
            Assert.IsTrue(s.Entries[1].Ready);
            Assert.IsTrue(s.Entries[0].InResults);
            Assert.IsFalse(s.CanStart);
            Assert.IsTrue(s.ReturnToLobby(0));
            Assert.IsFalse(s.ReturnToLobby(0));
            Assert.IsTrue(s.Entries[0].Ready);
            Assert.IsTrue(s.Entries[1].Ready);
            Assert.IsTrue(s.CanStart);
        }

        [Test] public void HumanReplacingBotPreservesOtherHumanOwnershipAndReusesBotCharacter()
        {
            var s = new LobbyState(4, 2);
            s.TryAddHuman(0, "Host", out _);
            s.TryAddBot();
            s.TryAddHuman(5, "Guest", out _);
            s.TryAddBot();
            s.TrySetReady(5, true);
            s.TrySetLoadout(5, "hat|face|cape|");
            var host = s.Entries[0];
            var guest = s.Entries[2];
            int replacedCharacter = s.Entries[3].Character;
            Assert.AreEqual(JoinResult.Ok, s.TryAddHuman(9, "New", out int index));
            Assert.AreEqual(3, index);
            Assert.AreEqual(replacedCharacter, s.Entries[index].Character);
            Assert.IsFalse(s.Entries[index].IsBot);
            Assert.IsFalse(s.Entries[index].Ready);
            Assert.AreEqual(9UL, s.Entries[index].ClientId);
            Assert.IsTrue(LobbySlot.From(host).Equals(LobbySlot.From(s.Entries[0])));
            Assert.IsTrue(LobbySlot.From(guest).Equals(LobbySlot.From(s.Entries[2])));
            Assert.AreEqual(4, s.Count);
            Assert.AreEqual(3, s.HumanCount);
        }

        [Test] public void RenameRejectsUnknownEmptyAndRunningMatchWithoutChangingSeat()
        {
            var s = new LobbyState(4, 2);
            s.TryAddHuman(0, "Host", out _);
            s.TryAddHuman(5, "Guest", out _);
            var before = LobbySlot.From(s.Entries[1]);
            Assert.IsFalse(s.TrySetName(99, "Other"));
            Assert.IsFalse(s.TrySetName(5, "   <>"));
            Assert.IsTrue(before.Equals(LobbySlot.From(s.Entries[1])));
            Assert.IsTrue(s.TrySetName(5, "  Črt   Šmid  "));
            Assert.AreEqual("Črt Šmid", s.Entries[1].Name);
            s.BeginMatch();
            before = LobbySlot.From(s.Entries[1]);
            Assert.IsFalse(s.TrySetName(5, "Changed during match"));
            Assert.IsTrue(before.Equals(LobbySlot.From(s.Entries[1])));
        }

        [Test] public void ResultsPresenceRoundTripsThroughWireSlotAndParticipatesInEquality()
        {
            var entry = new LobbyEntry { ClientId = 5, Name = "Guest", Character = 1, InResults = true, Loadout = "" };
            var slot = LobbySlot.From(entry);
            Assert.IsTrue(slot.ToEntry().InResults);
            entry.InResults = false;
            Assert.IsFalse(slot.Equals(LobbySlot.From(entry)));
        }

        [Test] public void RenamePreservesIdentityReadinessAndOutfit()
        {
            var s = new LobbyState(4,2); s.TryAddHuman(0,"Host",out _); s.TryAddHuman(7,"Guest",out _);
            s.TrySetReady(7,true); s.TrySetLoadout(7,"hat|||" ); var before = s.Entries[1];
            Assert.IsFalse(s.TrySetName(99,"Imposter")); Assert.IsTrue(s.TrySetName(7,"New Name"));
            var after = s.Entries[1]; Assert.AreEqual(before.ClientId,after.ClientId); Assert.AreEqual(before.Character,after.Character);
            Assert.AreEqual(before.Ready,after.Ready); Assert.AreEqual(before.Loadout,after.Loadout);
        }
    }
}
