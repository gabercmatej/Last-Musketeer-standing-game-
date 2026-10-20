using System.Collections.Generic;
using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    // TECH_DESIGN §6.2: the same seed must give the same match, turn for turn, and a mid-match Clone
    // must continue exactly like the original.
    public class DeterminismTests
    {
        const int MaxTurns = 3000;

        static List<uint> Run(BotMatch bm)
        {
            var hashes = new List<uint>();
            while (!bm.M.MatchOver)
            {
                bm.Step();
                hashes.Add(bm.Hash());
                Assert.That(hashes.Count, Is.LessThan(MaxTurns), "match never ended");
            }
            return hashes;
        }

        [TestCase(12345u)]
        [TestCase(777u)]
        [TestCase(2026u)]
        public void FourBotMatch_SameSeed_IdenticalHashEveryTurn(uint seed)
        {
            var a = Run(BotMatch.Create(seed, 4));
            var b = Run(BotMatch.Create(seed, 4));
            Assert.That(a.Count, Is.GreaterThan(5));
            Assert.That(b, Is.EqualTo(a));
        }

        [Test]
        public void BridgeMap_SameSeed_IdenticalHashEveryTurn()
        {
            var a = Run(BotMatch.Create(4242, 4, map: BuiltInMaps.Bridge));
            var b = Run(BotMatch.Create(4242, 4, map: BuiltInMaps.Bridge));
            Assert.That(a.Count, Is.GreaterThan(5));
            Assert.That(b, Is.EqualTo(a));
        }

        [Test]
        public void DifferentSeeds_DifferentMatches()
        {
            Assert.That(Run(BotMatch.Create(1, 4)), Is.Not.EqualTo(Run(BotMatch.Create(2, 4))));
        }

        [TestCase(12345u)]
        [TestCase(31337u)]
        public void ResumeFromMidMatchClone_IsIdentical(uint seed)
        {
            var full = Run(BotMatch.Create(seed, 4));
            int half = full.Count / 2;

            var bm = BotMatch.Create(seed, 4);
            for (int i = 0; i < half; i++)
                bm.Step();
            var clone = bm.Clone();
            Assert.That(clone.Hash(), Is.EqualTo(bm.Hash()));

            var rest = Run(clone);
            Assert.That(rest, Is.EqualTo(full.GetRange(half, full.Count - half)));
            // The original keeps going independently and matches as well.
            Assert.That(Run(bm), Is.EqualTo(rest));
        }
    }
}
