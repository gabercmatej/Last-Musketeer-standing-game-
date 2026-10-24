using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Musketeers.Sim;

namespace Musketeers.Net
{
    // Everything the host decides when a match starts; identical on every machine (only LocalSlot differs, derived from Roster).
    public sealed class MatchStartInfo
    {
        public string MatchId;
        public uint Seed;
        public LobbySettingsData Settings;
        public List<LobbyEntry> Roster = new List<LobbyEntry>();

        public int SlotOf(ulong clientId)
        {
            for (int i = 0; i < Roster.Count; i++)
            {
                if (!Roster[i].IsBot && Roster[i].ClientId == clientId)
                    return i;
            }
            return -1;
        }

        // Client id per slot, for NetTransport (bots get ulong.MaxValue).
        public ulong[] SlotClients()
        {
            var ids = new ulong[Roster.Count];
            for (int i = 0; i < ids.Length; i++)
                ids[i] = Roster[i].IsBot ? ulong.MaxValue : Roster[i].ClientId;
            return ids;
        }
    }

    // The one NetworkBehaviour for the running match (TECH_DESIGN §5): a thin RPC layer. Protocol decisions live in
    // NetTransport (plans, timer, hashes) and ChatRelay (chat rules); this class only serializes and forwards.
    // It lives on the same in-scene NetworkObject as LobbyNet (built by MainSceneBuilder).
    public sealed partial class MatchNet : NetworkBehaviour, INetLink
    {
        public static MatchNet Instance { get; private set; }

        /// <summary>Every machine (host included): the host pressed Start.</summary>
        public static event Action<MatchStartInfo> MatchStarted;
        /// <summary>Every machine: the host sent everyone back to the lobby.</summary>
        public static event Action ReturnedToLobby;
        /// <summary>Every machine: slot, cleaned text (empty for an emote), emote index (-1 for a text message).</summary>
        public static event Action<int, string, int> ChatReceived;
        /// <summary>Lobby-only message with the sender identity captured before any later seat changes.</summary>
        public static event Action<string, int, string, int> LobbyChatReceived;

        /// <summary>The running match's protocol object; AppFlow sets it when the match starts and clears it on teardown.</summary>
        public NetTransport Transport { get; set; }

        readonly ChatRelay _chat = new ChatRelay();

        public override void OnNetworkSpawn() => Instance = this;

        public override void OnNetworkDespawn()
        {
            if (Instance == this)
                Instance = null;
            Transport = null;
        }

        // ---------- local API (AppFlow) ----------

        /// <summary>Host only: everyone builds the same MatchSetup from this and starts.</summary>
        public void HostStartMatch(uint seed, LobbySettingsData settings, IReadOnlyList<LobbyEntry> roster)
        {
            var lobby = LobbyNet.Instance;
            // BeginMatch must already have succeeded. Compare the stable roster fields because
            // that transition intentionally resets Ready and marks participants InResults.
            if (!IsServer || lobby == null || lobby.HostState == null || !lobby.HostState.MatchInProgress ||
                roster == null || roster.Count < 2 || roster.Count != lobby.HostState.Count)
                return;
            var authoritative = lobby.HostState.Entries;
            for (int i = 0; i < roster.Count; i++)
            {
                var requested = roster[i];
                var actual = authoritative[i];
                if (requested.ClientId != actual.ClientId || requested.IsBot != actual.IsBot ||
                    requested.Character != actual.Character || requested.Name != actual.Name ||
                    requested.Loadout != actual.Loadout)
                    return;
            }
            var currentSettings = lobby.HostState.Settings;
            if (settings.Map != currentSettings.Map || settings.PointsToWin != currentSettings.PointsToWin ||
                settings.Hearts != currentSettings.Hearts)
                return;
            var slots = new LobbySlot[roster.Count];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = LobbySlot.From(roster[i]);
            _chat.Reset();
            MatchStartRpc(new FixedString64Bytes(Guid.NewGuid().ToString("N")), seed, LobbySettings.From(settings), slots);
        }

        /// <summary>Host only: the results screen's Return to lobby, for everyone.</summary>
        public void HostReturnToLobby()
        {
            if (IsServer)
                ReturnToLobbyRpc();
        }

        public void SendChat(string text) => ChatRpc(new FixedString512Bytes(ChatRelay.Clean(text)), -1);
        public void SendEmote(int index) => ChatRpc(default, index);

        // ---------- INetLink (this machine -> others) ----------

        public void SendReady() => ReadyRpc();
        public void SendPlan(int turnSeq, TurnPlan plan, bool locked) => PlanRpc(turnSeq, PlanCodec.Encode(plan), locked);
        public void SendTurnDone(int turnSeq, uint stateHash) => TurnDoneRpc(turnSeq, stateHash);
        public void BroadcastBegin(int turnSeq, int durationMs) => BeginRpc(turnSeq, durationMs);
        public void BroadcastLock(int turnSeq, int mask) => LockRpc(turnSeq, mask);
        public void BroadcastResolve(int turnSeq, TurnPlan[] plans) => ResolveRpc(turnSeq, PlanCodec.Encode(plans));

        public void SendSnapshot(ulong clientId, int turnSeq, byte[] data) =>
            SnapshotRpc(turnSeq, data, RpcTarget.Single(clientId, RpcTargetUse.Temp));

        // ---------- client -> host ----------

        [Rpc(SendTo.Server)]
        void ReadyRpc(RpcParams rpcParams = default) => Transport?.HostOnReady(rpcParams.Receive.SenderClientId);

        [Rpc(SendTo.Server)]
        void PlanRpc(int turnSeq, byte[] plan, bool locked, RpcParams rpcParams = default)
        {
            if (Transport == null)
                return;
            TurnPlan p;
            try { p = PlanCodec.Decode(plan); }
            catch (FormatException) { return; }
            Transport.HostOnPlan(rpcParams.Receive.SenderClientId, turnSeq, p, locked);
        }

        [Rpc(SendTo.Server)]
        void TurnDoneRpc(int turnSeq, uint hash, RpcParams rpcParams = default) =>
            Transport?.HostOnTurnDone(rpcParams.Receive.SenderClientId, turnSeq, hash);

        // text: cleaned by the sender and again by the host; emote -1 = text message
        [Rpc(SendTo.Server)]
        void ChatRpc(FixedString512Bytes text, int emote, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            var lobby = LobbyNet.Instance;
            int slot = lobby != null ? lobby.IndexOf(sender) : -1;
            if (slot < 0 || !_chat.Accept(sender, text.ToString(), emote, Time.unscaledTime, out string clean))
                return;
            if (lobby.MatchInProgress)
                ChatBroadcastRpc(slot, new FixedString512Bytes(clean), emote);
            else
            {
                var entry = lobby.Entries()[slot];
                LobbyChatBroadcastRpc(new FixedString64Bytes(entry.Name), entry.Character, new FixedString512Bytes(clean), emote);
            }
        }

        // ---------- host -> clients ----------

        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        void BeginRpc(int turnSeq, int durationMs) => Transport?.ClientOnBegin(turnSeq, durationMs);

        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        void LockRpc(int turnSeq, int mask) => Transport?.ClientOnLock(turnSeq, mask);

        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server)]
        void ResolveRpc(int turnSeq, byte[] plans)
        {
            if (Transport == null)
                return;
            TurnPlan[] decoded;
            try { decoded = PlanCodec.DecodeAll(plans); }
            catch (FormatException) { return; }
            Transport.ClientOnResolve(turnSeq, decoded);
        }

        [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
        void SnapshotRpc(int turnSeq, byte[] data, RpcParams rpcParams = default) => Transport?.ClientOnSnapshot(turnSeq, data);

        // ---------- host -> everyone (the host runs them too) ----------

        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        void MatchStartRpc(FixedString64Bytes matchId, uint seed, LobbySettings settings, LobbySlot[] roster)
        {
            ResetReplay();
            var info = new MatchStartInfo { MatchId = matchId.ToString(), Seed = seed, Settings = settings.ToData() };
            foreach (var s in roster)
                info.Roster.Add(s.ToEntry());
            MatchStarted?.Invoke(info);
        }

        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        void ReturnToLobbyRpc() => ReturnedToLobby?.Invoke();

        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        void ChatBroadcastRpc(int slot, FixedString512Bytes text, int emote) => ChatReceived?.Invoke(slot, text.ToString(), emote);

        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        void LobbyChatBroadcastRpc(FixedString64Bytes name, int character, FixedString512Bytes text, int emote) =>
            LobbyChatReceived?.Invoke(name.ToString(), character, text.ToString(), emote);
    }
}
