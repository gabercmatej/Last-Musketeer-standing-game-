using System;
using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    // Core types: GameState Clone/ComputeHash, DeterministicRng, Board, Dir, PushMath.
    public class CoreTests
    {
        static GameState Sample() => Scenario.From(@"
            A.K
            .oB").State;

        [Test]
        public void Clone_HasIdenticalHash()
        {
            var s = Sample();
            Assert.That(s.Clone().ComputeHash(), Is.EqualTo(s.ComputeHash()));
        }

        [Test]
        public void Clone_IsIndependent()
        {
            var s = Sample();
            uint before = s.ComputeHash();
            var c = s.Clone();
            c.Units[0].Pos = new Int2(1, 1);
            c.Units[1].Hp = 1;
            c.Kegs[0].Fuse = 1;
            c.Board.Set(new Int2(0, 0), TileType.Pit);
            c.Rng.NextUInt();
            Assert.That(s.ComputeHash(), Is.EqualTo(before));
            Assert.That(s.Units[0].Pos, Is.EqualTo(new Int2(0, 1)));
            Assert.That(s.Units[1].Hp, Is.EqualTo(3));
            Assert.That(s.Kegs[0].Fuse, Is.EqualTo(3));
            Assert.That(s.Board.Get(new Int2(0, 0)), Is.EqualTo(TileType.Floor));
            Assert.That(c.ComputeHash(), Is.Not.EqualTo(before));
        }

        [Test]
        public void Hash_IsStableAcrossIdenticalStates()
        {
            Assert.That(Sample().ComputeHash(), Is.EqualTo(Sample().ComputeHash()));
        }

        [Test]
        public void Hash_ChangesWhenAnyFieldChanges()
        {
            var baseState = Sample();
            uint h0 = baseState.ComputeHash();
            var mutations = new (string name, Action<GameState> mutate)[]
            {
                ("Turn", s => s.Turn++),
                ("NextKegId", s => s.NextKegId++),
                ("RoundOver", s => s.RoundOver = true),
                ("RoundWinner", s => s.RoundWinner = 1),
                ("Rng", s => s.Rng.NextUInt()),
                ("Tile", s => s.Board.Set(new Int2(0, 0), TileType.Pit)),
                ("Unit.Slot count", s => s.Units = new[] { s.Units[0] }),
                ("Unit.Character", s => s.Units[0].Character = 3),
                ("Unit.Ability", s => s.Units[0].Ability = Ability.BullRush),
                ("Unit.Pos.X", s => s.Units[0].Pos = new Int2(1, 1)),
                ("Unit.Pos.Y", s => s.Units[0].Pos = new Int2(0, 0)),
                ("Unit.Hp", s => s.Units[1].Hp--),
                ("Unit.Grit", s => s.Units[1].Grit++),
                ("Unit.Loaded", s => s.Units[1].Loaded = false),
                ("Unit.Alive", s => s.Units[1].Alive = false),
                ("Unit.LastAttacker", s => s.Units[1].LastAttacker = 0),
                ("Unit.LastAttackTurn", s => s.Units[1].LastAttackTurn = 7),
                ("Unit.Parrying", s => s.Units[1].Parrying = true),
                ("Keg.Id", s => s.Kegs[0].Id = 9),
                ("Keg.Owner", s => s.Kegs[0].Owner = 1),
                ("Keg.Pos", s => s.Kegs[0].Pos = new Int2(1, 1)),
                ("Keg.Fuse", s => s.Kegs[0].Fuse--),
                ("Keg.Detonating", s => s.Kegs[0].Detonating = true),
                ("Kegs.Count", s => s.Kegs.Clear()),
            };
            foreach (var (name, mutate) in mutations)
            {
                var c = baseState.Clone();
                mutate(c);
                Assert.That(c.ComputeHash(), Is.Not.EqualTo(h0), $"hash ignored {name}");
            }
        }

        [Test]
        public void Rng_SameSeed_SameSequence_AndCloneContinuesIdentically()
        {
            var a = new DeterministicRng(12345);
            var b = new DeterministicRng(12345);
            for (int i = 0; i < 10; i++)
                Assert.That(a.NextUInt(), Is.EqualTo(b.NextUInt()));
            var c = a.Clone();
            for (int i = 0; i < 10; i++)
                Assert.That(c.NextUInt(), Is.EqualTo(a.NextUInt()));
        }

        [Test]
        public void Rng_ZeroSeed_StillProducesValues_AndRangeStaysInBounds()
        {
            var r = new DeterministicRng(0);
            Assert.That(r.NextUInt(), Is.Not.EqualTo(0u));
            for (int i = 0; i < 1000; i++)
            {
                int v = r.Range(-2, 3);
                Assert.That(v, Is.InRange(-2, 2));
            }
        }

        [Test]
        public void Board_RingIndex_AndOffBoardIsPit()
        {
            var b = new Board(9, 9);
            Assert.That(b.RingIndex(new Int2(0, 0)), Is.EqualTo(0));
            Assert.That(b.RingIndex(new Int2(8, 3)), Is.EqualTo(0));
            Assert.That(b.RingIndex(new Int2(1, 7)), Is.EqualTo(1));
            Assert.That(b.RingIndex(new Int2(4, 4)), Is.EqualTo(4));
            Assert.That(b.Get(new Int2(-1, 4)), Is.EqualTo(TileType.Pit));
            Assert.That(b.Get(new Int2(4, 9)), Is.EqualTo(TileType.Pit));
        }

        [Test]
        public void Dir_Helpers()
        {
            Assert.That(Dir.N.ToInt2(), Is.EqualTo(new Int2(0, 1)));
            Assert.That(Dir.W.ToInt2(), Is.EqualTo(new Int2(-1, 0)));
            Assert.That(Dir.E.Opposite(), Is.EqualTo(Dir.W));
            Assert.That(Dir.N.Opposite(), Is.EqualTo(Dir.S));
            Assert.That(Dir.N.IsPerpendicularTo(Dir.E), Is.True);
            Assert.That(Dir.N.IsPerpendicularTo(Dir.S), Is.False);
            Assert.That(DirExt.TryFromDelta(new Int2(0, -1), out var d), Is.True);
            Assert.That(d, Is.EqualTo(Dir.S));
            Assert.That(DirExt.TryFromDelta(new Int2(1, 1), out _), Is.False);
        }

        // §6 Phase 3.3. Same-direction stacking can't come from plain shoves in one tick (only one unit
        // fits on the tile behind a target), so it's pinned here on the combine rule itself.
        [TestCase(0, 2, 0, 0, PushOutcome.Pushed, Dir.E, 2, TestName = "SameDirection_Stacks")]
        [TestCase(0, 4, 0, 0, PushOutcome.Pushed, Dir.E, 3, TestName = "SameDirection_CapsAtThree")]
        [TestCase(0, 1, 0, 1, PushOutcome.Cancelled, Dir.N, 0, TestName = "Opposite_Cancel")]
        [TestCase(0, 2, 0, 1, PushOutcome.Pushed, Dir.E, 1, TestName = "Opposite_UnequalSubtract")]
        [TestCase(1, 1, 0, 0, PushOutcome.Squeezed, Dir.N, 0, TestName = "Perpendicular_Squeeze")]
        [TestCase(1, 1, 1, 0, PushOutcome.Pushed, Dir.E, 1, TestName = "OppositePairCancels_LeavingPerpendicular")]
        [TestCase(0, 0, 0, 0, PushOutcome.None, Dir.N, 0, TestName = "NoPush")]
        public void PushMath_Combine(int n, int e, int s, int w, PushOutcome outcome, Dir dir, int strength)
        {
            var r = PushMath.Combine(n, e, s, w, 3);
            Assert.That(r.Outcome, Is.EqualTo(outcome));
            if (outcome == PushOutcome.Pushed)
            {
                Assert.That(r.Dir, Is.EqualTo(dir));
                Assert.That(r.Strength, Is.EqualTo(strength));
            }
        }
    }
}
