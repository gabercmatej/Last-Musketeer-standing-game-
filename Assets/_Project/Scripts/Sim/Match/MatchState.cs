using System;
using System.Collections.Generic;

namespace Musketeers.Sim
{
    // Everything above one round (GAME_DESIGN §2): points, round wins, the Crown, sudden death and
    // the current round's GameState. Plain data; MatchRunner drives it. Players are slots 0..n-1 and
    // keep their slot (and spawn) every round; a player sitting out a sudden-death round is a dead unit.
    public sealed class MatchState
    {
        public GameConfig Cfg;
        public MapData Map;
        public Ability[] Abilities;     // per player slot
        public int[] Characters;        // CharacterDef index per player slot
        public int[] Points;
        public int[] RoundWins;
        public bool[] Participants;     // who plays the current round
        public int RoundNumber;         // 1-based, 0 before the first round
        public int Crowned = -1;        // §2 Crown for the current round, -1 for nobody
        public bool SuddenDeath;        // the current round is a tiebreak between Participants only
        public bool MatchOver;
        public int MatchWinner = -1;
        public GameState Round;
        public MatchStats Stats;
        public DeterministicRng Rng;    // seeds each round's GameState
        public List<PointGain> RoundGains = new List<PointGain>();   // this round so far; applied at round end

        public int PlayerCount => Abilities.Length;

        public MatchState Clone()
        {
            return new MatchState
            {
                Cfg = Cfg,
                Map = Map,   // never mutated: each round clones its board
                Abilities = (Ability[])Abilities.Clone(),
                Characters = (int[])Characters.Clone(),
                Points = (int[])Points.Clone(),
                RoundWins = (int[])RoundWins.Clone(),
                Participants = (bool[])Participants.Clone(),
                RoundNumber = RoundNumber,
                Crowned = Crowned,
                SuddenDeath = SuddenDeath,
                MatchOver = MatchOver,
                MatchWinner = MatchWinner,
                Round = Round?.Clone(),
                Stats = Stats.Clone(),
                Rng = Rng.Clone(),
                RoundGains = new List<PointGain>(RoundGains),
            };
        }

        public uint ComputeHash()
        {
            var h = Fnv1a.Start();
            h.Add(PlayerCount);
            for (int i = 0; i < PlayerCount; i++)
            {
                h.Add((int)Abilities[i]);
                h.Add(Characters[i]);
                h.Add(Points[i]);
                h.Add(RoundWins[i]);
                h.Add(Participants[i]);
            }
            h.Add(RoundNumber);
            h.Add(Crowned);
            h.Add(SuddenDeath);
            h.Add(MatchOver);
            h.Add(MatchWinner);
            h.Add(Rng.State);
            h.Add(RoundGains.Count);
            foreach (var g in RoundGains)
            {
                h.Add(g.Player);
                h.Add(g.Amount);
                h.Add((int)g.Reason);
                h.Add(g.Victim);
            }
            Stats.AddToHash(ref h);
            h.Add(Round != null ? Round.ComputeHash() : 0u);
            return h.Value;
        }

        internal static MatchState Create(MapData map, Ability[] abilities, int[] characters, GameConfig cfg, uint seed)
        {
            if (abilities == null || abilities.Length < 2)
                throw new ArgumentException("A match needs at least 2 players.", nameof(abilities));
            if (abilities.Length > map.Spawns.Length)
                throw new ArgumentException($"{abilities.Length} players but the map has only {map.Spawns.Length} spawns.");
            if (characters == null)
            {
                characters = new int[abilities.Length];
                for (int i = 0; i < characters.Length; i++)
                    characters[i] = i;
            }
            int n = abilities.Length;
            var participants = new bool[n];
            for (int i = 0; i < n; i++)
                participants[i] = true;
            return new MatchState
            {
                Cfg = cfg,
                Map = map,
                Abilities = (Ability[])abilities.Clone(),
                Characters = (int[])characters.Clone(),
                Points = new int[n],
                RoundWins = new int[n],
                Participants = participants,
                Stats = new MatchStats(n),
                Rng = new DeterministicRng(seed),
            };
        }
    }
}
