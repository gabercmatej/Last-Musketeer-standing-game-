using System;
using Musketeers.Sim;

namespace Musketeers.Game
{
    // How MatchController talks to "the host" (TECH_DESIGN §5) without knowing whether it's online.
    // LocalTransport is the host itself (offline + bots); NetTransport (Day 4) sends the same calls as RPCs.
    // turnSeq counts turns across the whole match (1, 2, 3, …), so it never repeats between rounds.
    public interface IMatchTransport
    {
        // BeginPlanningRpc: planning for turnSeq starts now and lasts `seconds`.
        event Action<int, float> PlanningStarted;
        // ResolveTurnRpc: every slot's plan for turnSeq (index = slot), humans and bots.
        event Action<int, TurnPlan[]> PlansResolved;
        // LockStatusRpc: bit i set = slot i locked in.
        event Action<int> LockMaskChanged;

        float PlanningSecondsLeft { get; }

        // currentMatch lets a hosting transport plan bots against the live state.
        void Start(MatchSetup setup, Func<MatchState> currentMatch);
        // SubmitPlanRpc: sent on every plan change so the latest plan is ready when time runs out.
        void SubmitPlan(int turnSeq, TurnPlan plan, bool locked);
        // TurnDoneRpc: this machine finished resolving and playing back turnSeq.
        void TurnDone(int turnSeq, uint stateHash);
        void Tick(float deltaSeconds);
        void Stop();
    }
}
