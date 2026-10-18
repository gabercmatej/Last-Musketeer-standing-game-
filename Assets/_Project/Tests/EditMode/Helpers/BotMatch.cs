using System;

namespace Musketeers.Sim.Tests
{
    // A headless bot-vs-bot match on the Courtyard: what the determinism, fuzz and balance tests run.
    public sealed class BotMatch
    {
        public MatchState M;
        public BotPlanner[] Bots;
        public int TurnsThisRound;

        public static BotMatch Create(uint seed, int players, GameConfig cfg = null, string map = BuiltInMaps.Courtyard)
        {
            cfg ??= new GameConfig();
            // Abilities: a seeded shuffle of the four, first `players` of them.
            var pick = new DeterministicRng(seed ^ 0xA5A5A5A5u);
            var all = new[] { Ability.Grappin, Ability.Switcheroo, Ability.DoubleCharge, Ability.BullRush };
            for (int i = all.Length - 1; i > 0; i--)
            {
                int j = pick.Range(0, i + 1);
                (all[i], all[j]) = (all[j], all[i]);
            }
            var abilities = new Ability[players];
            Array.Copy(all, abilities, players);

            var bm = new BotMatch
            {
                M = MatchRunner.NewMatch(MapParser.Parse(map), abilities, cfg, seed),
                Bots = new BotPlanner[players],
            };
            for (int i = 0; i < players; i++)
                bm.Bots[i] = new BotPlanner(i, seed * 2654435761u + (uint)i * 40503u + 1u);
            return bm;
        }

        public TurnOutcome Step()
        {
            var plans = new TurnPlan[Bots.Length];
            for (int i = 0; i < Bots.Length; i++)
                plans[i] = Bots[i].Plan(M.Round, M.Cfg, M.RoundNumber);
            var o = MatchRunner.PlayTurn(M, plans);
            TurnsThisRound = o.RoundResult != null ? 0 : TurnsThisRound + 1;
            return o;
        }

        public BotMatch Clone()
        {
            var c = new BotMatch { M = M.Clone(), Bots = new BotPlanner[Bots.Length], TurnsThisRound = TurnsThisRound };
            for (int i = 0; i < Bots.Length; i++)
                c.Bots[i] = Bots[i].Clone();
            return c;
        }

        // Hash of everything that can influence the future: the match plus each bot's RNG.
        public uint Hash()
        {
            uint h = M.ComputeHash();
            foreach (var b in Bots)
                h = unchecked(h * 16777619u ^ b.RngState);
            return h;
        }
    }
}
