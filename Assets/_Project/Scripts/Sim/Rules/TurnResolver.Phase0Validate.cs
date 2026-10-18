namespace Musketeers.Sim
{
    public static partial class TurnResolver
    {
        // §6 Phase 0: check cost and requirements, fizzle (free) or pay, and mark parriers.
        static void Phase0Validate(TickContext c)
        {
            c.Log.Phase = SimPhase.Validate;
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
                        if (u.Grit < c.Cfg.DashCost) reason = FizzleReason.NotEnoughGrit;
                        else u.Grit -= c.Cfg.DashCost;
                        break;
                    case ActionType.Fire:
                        // Firing "costs" the load; paid here so Phase 4 only traces.
                        if (!u.Loaded) reason = FizzleReason.NotLoaded;
                        else u.Loaded = false;
                        break;
                    case ActionType.Parry:
                        if (u.Grit < c.Cfg.ParryCost) reason = FizzleReason.NotEnoughGrit;
                        else
                        {
                            u.Grit -= c.Cfg.ParryCost;
                            u.Parrying = true;
                            // Parrying is cleared at tick end, so playback needs this to show the guard.
                            c.Log.Add(SimEventKind.ParryRaised, unit: i, to: u.Pos);
                        }
                        break;
                    case ActionType.Keg:
                    case ActionType.Signature:
                        // Day 2: kegs (Phase 5) and signature abilities (Phase 2b / Double Charge).
                        reason = FizzleReason.NotYetImplemented;
                        break;
                }

                if (reason != FizzleReason.None)
                    c.Log.Add(SimEventKind.Fizzled, unit: i, to: u.Pos, amount: (int)a.Type, reason: (byte)reason);
                else
                    c.Active[i] = true;
            }
        }
    }
}
