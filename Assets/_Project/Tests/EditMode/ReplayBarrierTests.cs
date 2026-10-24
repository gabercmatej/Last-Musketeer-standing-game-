using NUnit.Framework;
using Musketeers.Net;
namespace Musketeers.Sim.Tests
{
    public class ReplayBarrierTests
    {
        [Test] public void WaitsForAllHumansAndAllCompletions()
        {
            var b = new ReplayBarrier(); b.Begin(3, new ulong[] { 0, 4 });
            Assert.IsFalse(b.Ready(0)); Assert.IsFalse(b.Ready(99)); Assert.IsTrue(b.Ready(4));
            Assert.IsFalse(b.Done(0)); Assert.IsFalse(b.Done(0)); Assert.IsTrue(b.Done(4));
        }
        [Test] public void AnyHumanSkipIsIdempotentAndLateReadyCannotRestart()
        {
            var b = new ReplayBarrier(); b.Begin(3, new ulong[] { 0, 4 });
            Assert.IsFalse(b.Skip(99)); Assert.IsTrue(b.Skip(4)); Assert.IsFalse(b.Skip(0));
            Assert.IsFalse(b.Ready(0)); Assert.IsFalse(b.Ready(4)); Assert.IsTrue(b.Finished);
            b.Begin(4, new ulong[] { 0, 4 }); Assert.IsFalse(b.Finished);
        }
        [Test] public void DisconnectReleasesBothBarriers()
        {
            var b = new ReplayBarrier(); b.Begin(3, new ulong[] { 0, 4, 5 });
            b.Ready(0); b.Ready(4); b.Disconnect(5); Assert.IsTrue(b.TryStart());
            b.Done(0); b.Disconnect(4); Assert.IsTrue(b.TryFinish());
        }
    }
}
