using System;
using System.Collections.Generic;
using Unity.Netcode;

namespace Musketeers.Net
{
    public sealed partial class MatchNet
    {
        readonly ReplayBarrier _replay = new ReplayBarrier();
        public static event Action<int> ReplayStarted, ReplayFinished;
        public int LastReplayFinished { get; private set; } = -1;
        public double ReviewEndsAt { get; private set; }
        public double ReviewClock => NetworkManager.ServerTime.Time;
        void ResetReplay() { _replay.Reset(); LastReplayFinished = -1; ReviewEndsAt = 0; }
        void EndReplay(int turn) => ReplayEndRpc(turn, ReviewClock + 0.25 + Musketeers.Game.PostReplayCountdown.Seconds);

        // Called after authoritative resolution, before any client requests playback.
        public void PrepareReplay(int turn, bool isFinal = false)
        {
            if (!IsServer || _replay.Turn == turn) return;
            var ids = new List<ulong>();
            foreach (var e in LobbyNet.Instance.Entries())
                if (!e.IsBot && NetworkManager.ConnectedClients.ContainsKey(e.ClientId)) ids.Add(e.ClientId);
            _replay.Begin(turn, ids, isFinal);
        }
        public void ReadyForReplay(int turn) => ReplayReadyRpc(turn);
        public void SkipReplay(int turn) => ReplaySkipRpc(turn);
        public void ReplayDone(int turn) => ReplayDoneRpc(turn);

        [Rpc(SendTo.Server)]
        void ReplayReadyRpc(int turn, RpcParams rpc = default)
        {
            if (turn == _replay.Turn && _replay.Ready(rpc.Receive.SenderClientId)) ReplayBeginRpc(turn);
        }
        [Rpc(SendTo.Server)]
        void ReplaySkipRpc(int turn, RpcParams rpc = default)
        {
            if (turn == _replay.Turn && _replay.Skip(rpc.Receive.SenderClientId)) EndReplay(turn);
        }
        [Rpc(SendTo.Server)]
        void ReplayDoneRpc(int turn, RpcParams rpc = default)
        {
            if (turn == _replay.Turn && _replay.Done(rpc.Receive.SenderClientId)) EndReplay(turn);
        }
        public void ReplayDisconnected(ulong id)
        {
            if (!IsServer || _replay.Turn < 0 || _replay.Finished) return;
            _replay.Disconnect(id);
            if (_replay.TryStart()) ReplayBeginRpc(_replay.Turn);
            if (_replay.TryFinish()) EndReplay(_replay.Turn);
        }
        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        void ReplayBeginRpc(int turn) => ReplayStarted?.Invoke(turn);
        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        void ReplayEndRpc(int turn, double reviewEndsAt) { LastReplayFinished = turn; ReviewEndsAt = reviewEndsAt; ReplayFinished?.Invoke(turn); }
    }
}
