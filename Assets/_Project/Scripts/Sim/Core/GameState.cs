using System;
using System.Collections.Generic;

namespace Musketeers.Sim
{
    // Everything the rules need for one round. Plain data; TurnResolver mutates it.
    public sealed class GameState
    {
        public int Turn = 1;                // 1-based; TurnResolver.ResolveTurn advances it
        public Board Board;
        public Unit[] Units;                // indexed by slot
        public List<Keg> Kegs = new List<Keg>();   // kept in Id order
        public DeterministicRng Rng;
        public int NextKegId;
        public bool RoundOver;
        public int RoundWinner = -1;        // slot of the last musketeer standing, -1 for a draw

        public GameState(Board board, Unit[] units, DeterministicRng rng)
        {
            Board = board ?? throw new ArgumentNullException(nameof(board));
            Units = units ?? throw new ArgumentNullException(nameof(units));
            Rng = rng ?? throw new ArgumentNullException(nameof(rng));
            for (int i = 0; i < units.Length; i++)
            {
                if (units[i].Slot != i)
                    throw new ArgumentException($"Units must be indexed by slot: units[{i}].Slot is {units[i].Slot}.", nameof(units));
            }
        }

        // A fresh round: one unit per ability, slot i on spawn i (§3: with fewer players use spawns in slot order).
        public static GameState Create(Board board, Int2[] spawns, Ability[] abilities, GameConfig cfg, uint seed)
        {
            if (abilities.Length > spawns.Length)
                throw new ArgumentException($"{abilities.Length} players but the map has only {spawns.Length} spawns.");
            var units = new Unit[abilities.Length];
            for (int i = 0; i < units.Length; i++)
                units[i] = Unit.Spawn(i, i, abilities[i], spawns[i], cfg);
            return new GameState(board.Clone(), units, new DeterministicRng(seed));
        }

        public int PlayerCount => Units.Length;

        // Living unit on a tile, or null. Dead units are off the board.
        public Unit UnitAt(Int2 p)
        {
            for (int i = 0; i < Units.Length; i++)
            {
                if (Units[i].Alive && Units[i].Pos == p)
                    return Units[i];
            }
            return null;
        }

        public Keg KegAt(Int2 p)
        {
            for (int i = 0; i < Kegs.Count; i++)
            {
                if (Kegs[i].Pos == p)
                    return Kegs[i];
            }
            return null;
        }

        public int AliveCount()
        {
            int n = 0;
            for (int i = 0; i < Units.Length; i++)
            {
                if (Units[i].Alive)
                    n++;
            }
            return n;
        }

        public GameState Clone()
        {
            var units = new Unit[Units.Length];
            for (int i = 0; i < units.Length; i++)
                units[i] = Units[i].Clone();
            var copy = new GameState(Board.Clone(), units, Rng.Clone())
            {
                Turn = Turn,
                NextKegId = NextKegId,
                RoundOver = RoundOver,
                RoundWinner = RoundWinner,
                Kegs = new List<Keg>(Kegs.Count),
            };
            for (int i = 0; i < Kegs.Count; i++)
                copy.Kegs.Add(Kegs[i].Clone());
            return copy;
        }

        // FNV-1a over every field, in a fixed order (TECH_DESIGN §2 desync check).
        public uint ComputeHash()
        {
            var h = Fnv1a.Start();
            h.Add(Turn);
            h.Add(NextKegId);
            h.Add(RoundOver);
            h.Add(RoundWinner);
            h.Add(Rng.State);
            Board.AddToHash(ref h);
            h.Add(Units.Length);
            for (int i = 0; i < Units.Length; i++)
                Units[i].AddToHash(ref h);
            h.Add(Kegs.Count);
            for (int i = 0; i < Kegs.Count; i++)
                Kegs[i].AddToHash(ref h);
            return h.Value;
        }
    }
}
