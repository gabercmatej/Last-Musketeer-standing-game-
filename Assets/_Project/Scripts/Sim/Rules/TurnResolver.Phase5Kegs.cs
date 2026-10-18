namespace Musketeers.Sim
{
    public static partial class TurnResolver
    {
        // §6 Phase 5: keg landing, fuses and explosions (incl. kegs marked Detonating by shots).
        // Day 2. Until then thrown kegs fizzle in Phase 0 and shot kegs just stay marked.
        static void Phase5Kegs(TickContext c)
        {
            c.Log.Phase = SimPhase.Kegs;
        }
    }
}
