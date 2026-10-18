namespace Musketeers.Sim
{
    // Per-tick scratch shared by the phases.
    internal sealed class TickContext
    {
        public readonly GameState S;
        public readonly GameConfig Cfg;
        public readonly EventLog Log;
        public readonly int Tick;
        public readonly PlannedAction[] Act;   // this tick's action per slot
        public readonly bool[] Active;         // passed Phase 0 (paid, not fizzled)
        public readonly int[] Order;           // slots in initiative order

        public TickContext(GameState s, GameConfig cfg, EventLog log, int tick, TurnPlan[] plans)
        {
            S = s;
            Cfg = cfg;
            Log = log;
            Tick = tick;
            int n = s.Units.Length;
            Act = new PlannedAction[n];
            Active = new bool[n];
            if (plans != null)
            {
                // Keyed by Slot. A duplicate slot is a caller bug (e.g. a default TurnPlan claims
                // slot 0 and would silently replace that player's plan), so fail loudly.
                var seen = new bool[n];
                for (int i = 0; i < plans.Length; i++)
                {
                    int slot = plans[i].Slot;
                    if (slot >= n)
                        continue;
                    if (seen[slot])
                        throw new System.ArgumentException($"Two plans claim slot {slot}. Use new TurnPlan(slot) for empty plans.", nameof(plans));
                    seen[slot] = true;
                    Act[slot] = plans[i][tick];
                }
            }
            Order = TurnResolver.InitiativeOrder(s.Turn, n);
        }
    }
}
