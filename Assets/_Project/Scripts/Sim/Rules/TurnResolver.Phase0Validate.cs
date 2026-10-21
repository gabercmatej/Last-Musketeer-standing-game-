namespace Musketeers.Sim
{
    public static partial class TurnResolver
    {
        // §6 Phase 0: check cost and requirements, fizzle (free) or pay, and mark parriers.
        static void Phase0Validate(TickContext c)
        {
            c.Log.Phase = SimPhase.Validate;
            var cfg = c.Cfg;
            var units = c.S.Units;
            for (int i = 0; i < units.Length; i++)
            {
                units[i].Parrying = false;
                c.Active[i] = false;
            }

            for (int i = 0; i < units.Length; i++)
            {
                var u = units[i];
                if (!u.Alive)
                    continue;
                var a = c.Act[i];
                var reason = FizzleReason.None;
                switch (a.Type)
                {
                    case ActionType.Dash:
                        if (u.Grit < cfg.DashCost) reason = FizzleReason.NotEnoughGrit;
                        else u.Grit -= cfg.DashCost;
                        break;
                    case ActionType.Fire:
                        // Firing "costs" the load; paid here so Phase 4 only traces.
                        if (!u.Loaded) reason = FizzleReason.NotLoaded;
                        else u.Loaded = false;
                        break;
                    case ActionType.Parry:
                        if (u.Grit < cfg.ParryCost) reason = FizzleReason.NotEnoughGrit;
                        else
                        {
                            u.Grit -= cfg.ParryCost;
                            u.Parrying = true;
                            // Parrying is cleared at tick end, so playback needs this to show the guard.
                            c.Log.Add(SimEventKind.ParryRaised, unit: i, to: u.Pos);
                        }
                        break;
                    case ActionType.Keg:
                        // §5 Powder Keg: 2 Grit, max 1 active per player, target within Manhattan 3.
                        if (u.Grit < cfg.KegCost) reason = FizzleReason.NotEnoughGrit;
                        else if (ActiveKegs(c.S, i) >= cfg.MaxKegsPerPlayer) reason = FizzleReason.KegLimit;
                        else if (c.S.Board.IsVoid(a.Target) || u.Pos.Manhattan(a.Target) > cfg.KegThrowRange) reason = FizzleReason.OutOfRange;
                        else u.Grit -= cfg.KegCost;
                        break;
                    case ActionType.Signature:
                        // §4: every signature costs SignatureCost Grit; Triple Charge also needs (and spends) the load.
                        if (u.Grit < cfg.SignatureCost) reason = FizzleReason.NotEnoughGrit;
                        else if (u.Ability == Ability.TripleCharge && !u.Loaded) reason = FizzleReason.NotLoaded;
                        else
                        {
                            u.Grit -= cfg.SignatureCost;
                            if (u.Ability == Ability.TripleCharge)
                                u.Loaded = false;
                            // FIXES_4E §8.5: lets the announcer call "Signature move!" for every ability.
                            c.Log.Add(SimEventKind.SignatureUsed, unit: i, to: u.Pos, amount: (int)u.Ability);
                        }
                        break;
                }

                if (reason != FizzleReason.None)
                    c.Log.Add(SimEventKind.Fizzled, unit: i, to: u.Pos, amount: (int)a.Type, reason: (byte)reason);
                else
                    c.Active[i] = true;
            }
        }

        public static int ActiveKegs(GameState s, int owner)
        {
            int n = 0;
            for (int k = 0; k < s.Kegs.Count; k++)
            {
                if (s.Kegs[k].Owner == owner)
                    n++;
            }
            return n;
        }
    }
}
