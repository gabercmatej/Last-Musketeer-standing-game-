using System;

namespace Musketeers.Sim
{
    public static partial class TurnResolver
    {
        // §6 Phase 1: Reload and Catch Breath. Grit gained here is spendable from the next tick,
        // because this tick's Phase 0 already ran.
        static void Phase1Self(TickContext c)
        {
            c.Log.Phase = SimPhase.Self;
            var units = c.S.Units;
            for (int i = 0; i < units.Length; i++)
            {
                if (!c.Active[i])
                    continue;
                var u = units[i];
                switch (c.Act[i].Type)
                {
                    case ActionType.Reload:
                        u.Loaded = true;   // reloading while loaded is allowed, just wasted (§5)
                        c.Log.Add(SimEventKind.Reloaded, unit: i, to: u.Pos);
                        break;
                    case ActionType.CatchBreath:
                        int before = u.Grit;
                        u.Grit = Math.Min(c.Cfg.MaxGrit, u.Grit + c.Cfg.CatchBreathGrit);
                        c.Log.Add(SimEventKind.CaughtBreath, unit: i, to: u.Pos, amount: u.Grit - before);
                        break;
                }
            }
        }
    }
}
