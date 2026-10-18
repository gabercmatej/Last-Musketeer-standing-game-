namespace Musketeers.Sim
{
    public static partial class TurnResolver
    {
        // §6 Phase 2b: Grappin, Switcheroo, Bull Rush, one at a time in initiative order.
        // Day 2. Until then Phase 0 fizzles every Signature with NotYetImplemented, so nothing reaches here.
        static void Phase2bAbilities(TickContext c)
        {
            c.Log.Phase = SimPhase.MovementAbilities;
        }
    }
}
