using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Musketeers.Net
{
    // The one lobby NetworkBehaviour (TECH_DESIGN §5): the seats (NetworkList<LobbySlot>) and the match rules
    // (NetworkVariable<LobbySettings>) are written only by the host, who runs the real rules in LobbyState.
    // Clients ask through RPCs; the host validates, mutates its LobbyState and mirrors it to the network state.
    // Chunk B: read Entries / Settings / LocalClientId for MatchStartRpc, call SetMatchInProgress(true/false).
    public sealed class LobbyNet : NetworkBehaviour
    {
        public const string MsgVersion = "New version is out. Refresh (Cmd/Ctrl+Shift+R)";
        public const string MsgFull = "Lobby is full";
        public const string MsgInProgress = "Match in progress, try again after";
        public const string MsgTimeout = "Could not join the lobby. Try again.";
        const float JoinInfoTimeout = 10f;

        public static LobbyNet Instance { get; private set; }

        readonly NetworkList<LobbySlot> _slots = new NetworkList<LobbySlot>();
        readonly NetworkVariable<LobbySettings> _settings = new NetworkVariable<LobbySettings>();
        readonly NetworkVariable<bool> _matchInProgress = new NetworkVariable<bool>();

        LobbyState _state;                                           // host only
        readonly Dictionary<ulong, float> _pending = new Dictionary<ulong, float>();   // host: connected, JoinInfo not yet received
        readonly List<ulong> _scratch = new List<ulong>();
        bool _dirty;

        // Set by NetSession / AppFlow before the host starts (number of musketeers and maps the build has).
        public static int CharacterCount = 4;
        public static int MapCount = 2;

        /// <summary>The lobby changed (seats, outfits, ready flags, settings). Raised at most once per frame, on every machine.</summary>
        public event Action Changed;
        /// <summary>Host only: a human's connection dropped (their seat is already removed unless a match is running).</summary>
        public event Action<ulong> ClientLeft;

        public ulong LocalClientId => NetworkManager != null ? NetworkManager.LocalClientId : 0;
        public bool MatchInProgress => _matchInProgress.Value;
        public LobbySettingsData Settings => _settings.Value.ToData();
        public int Count => _slots.Count;

        /// <summary>The seats in host order (the same order on every machine; chunk B builds MatchSetup from it).</summary>
        public List<LobbyEntry> Entries()
        {
            var list = new List<LobbyEntry>(_slots.Count);
            for (int i = 0; i < _slots.Count; i++)
                list.Add(_slots[i].ToEntry());
            return list;
        }

        public int IndexOf(ulong clientId)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (!_slots[i].IsBot && _slots[i].ClientId == clientId)
                    return i;
            }
            return -1;
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            _slots.OnListChanged += OnSlotsChanged;
            _settings.OnValueChanged += OnSettingsChanged;
            _matchInProgress.OnValueChanged += OnFlagChanged;
            if (IsServer)
            {
                _state = new LobbyState(CharacterCount, MapCount, NetworkManager.ServerClientId);
                _settings.Value = LobbySettings.From(_state.Settings);
                NetworkManager.OnConnectionEvent += OnConnectionEvent;
            }
            _dirty = true;
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this)
                Instance = null;
            _slots.OnListChanged -= OnSlotsChanged;
            _settings.OnValueChanged -= OnSettingsChanged;
            _matchInProgress.OnValueChanged -= OnFlagChanged;
            if (NetworkManager != null)
                NetworkManager.OnConnectionEvent -= OnConnectionEvent;
        }

        void OnSlotsChanged(NetworkListEvent<LobbySlot> _) => _dirty = true;
        void OnSettingsChanged(LobbySettings a, LobbySettings b) => _dirty = true;
        void OnFlagChanged(bool a, bool b) => _dirty = true;

        void Update()
        {
            if (_dirty)
            {
                _dirty = false;
                Changed?.Invoke();
            }
            if (IsServer && _pending.Count > 0)
            {
                _scratch.Clear();
                foreach (var kv in _pending)
                {
                    if (Time.unscaledTime - kv.Value > JoinInfoTimeout)
                        _scratch.Add(kv.Key);
                }
                foreach (var id in _scratch)
                {
                    _pending.Remove(id);
                    NetworkManager.DisconnectClient(id, MsgTimeout);
                }
            }
        }

        // ---------- host side ----------

        void OnConnectionEvent(NetworkManager nm, ConnectionEventData data)
        {
            if (data.ClientId == NetworkManager.ServerClientId)
                return;
            if (data.EventType == ConnectionEvent.ClientConnected)
                _pending[data.ClientId] = Time.unscaledTime;
            else if (data.EventType == ConnectionEvent.ClientDisconnected)
            {
                _pending.Remove(data.ClientId);
                // During a match the seat stays (chunk B plays Catch Breath for it); the lobby frees it right away.
                if (!_state.MatchInProgress)
                {
                    _state.Remove(data.ClientId);
                    Sync();
                }
                ClientLeft?.Invoke(data.ClientId);
            }
        }

        /// <summary>Host only: seat the host themselves (called by NetSession right after the session starts).</summary>
        public void HostJoin(string name)
        {
            if (!IsServer)
                return;
            _state.TryAddHuman(NetworkManager.ServerClientId, name, out _);
            Sync();
        }

        /// <summary>Host only (chunk B): a match starts or ends. While true, new joiners are refused.</summary>
        public void SetMatchInProgress(bool running)
        {
            if (!IsServer)
                return;
            _state.MatchInProgress = running;
            _matchInProgress.Value = running;
            if (!running)
                PurgeDisconnected();
        }

        /// <summary>Host only: drop the seats of humans who are no longer connected (after a match, back in the lobby).</summary>
        public void PurgeDisconnected()
        {
            if (!IsServer)
                return;
            var list = _state.Entries;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var e = list[i];
                if (!e.IsBot && e.ClientId != NetworkManager.ServerClientId && !IsConnected(e.ClientId))
                    _state.Remove(e.ClientId);
            }
            Sync();
        }

        bool IsConnected(ulong id)
        {
            var ids = NetworkManager.ConnectedClientsIds;
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == id)
                    return true;
            }
            return false;
        }

        /// <summary>Host only (chunk B): the host's current state, for roster / "everyone ready" checks.</summary>
        public LobbyState HostState => _state;

        // Writes the host's LobbyState to the network state with the fewest changes (set in place, then add/remove the tail).
        void Sync()
        {
            var entries = _state.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                var slot = LobbySlot.From(entries[i]);
                if (i < _slots.Count)
                {
                    if (!_slots[i].Equals(slot))
                        _slots[i] = slot;
                }
                else
                    _slots.Add(slot);
            }
            while (_slots.Count > entries.Count)
                _slots.RemoveAt(_slots.Count - 1);
            var s = LobbySettings.From(_state.Settings);
            if (!_settings.Value.Equals(s))
                _settings.Value = s;
        }

        bool FromHost(RpcParams p) => p.Receive.SenderClientId == NetworkManager.ServerClientId;

        // ---------- client -> host RPCs ----------

        [Rpc(SendTo.Server)]
        void JoinInfoRpc(FixedString64Bytes name, FixedString32Bytes version, RpcParams rpcParams = default)
        {
            ulong id = rpcParams.Receive.SenderClientId;
            _pending.Remove(id);
            if (id != NetworkManager.ServerClientId && version.ToString() != Application.version)
            {
                NetworkManager.DisconnectClient(id, MsgVersion);
                return;
            }
            var result = _state.TryAddHuman(id, name.ToString(), out _);
            if (result == JoinResult.Full)
                NetworkManager.DisconnectClient(id, MsgFull);
            else if (result == JoinResult.MatchInProgress)
                NetworkManager.DisconnectClient(id, MsgInProgress);
            else
                Sync();
        }

        [Rpc(SendTo.Server)]
        void PickCharacterRpc(int character, RpcParams rpcParams = default)
        {
            if (_state.TryPickCharacter(rpcParams.Receive.SenderClientId, character))
                Sync();
        }

        [Rpc(SendTo.Server)]
        void SetReadyRpc(bool ready, RpcParams rpcParams = default)
        {
            if (_state.TrySetReady(rpcParams.Receive.SenderClientId, ready))
                Sync();
        }

        [Rpc(SendTo.Server)]
        void SetCosmeticsRpc(FixedString128Bytes loadout, RpcParams rpcParams = default)
        {
            if (_state.TrySetLoadout(rpcParams.Receive.SenderClientId, loadout.ToString()))
                Sync();
        }

        // Host-only controls: the host's own call arrives with the host's client id; anyone else is ignored.
        [Rpc(SendTo.Server)]
        void SetSettingsRpc(int map, int points, int hearts, RpcParams rpcParams = default)
        {
            if (FromHost(rpcParams) && !_state.MatchInProgress &&
                _state.TrySetSettings(new LobbySettingsData { Map = map, PointsToWin = points, Hearts = hearts }))
                Sync();
        }

        [Rpc(SendTo.Server)]
        void DefaultSettingsRpc(RpcParams rpcParams = default)
        {
            if (FromHost(rpcParams) && !_state.MatchInProgress)
            {
                _state.ResetRules();
                Sync();
            }
        }

        [Rpc(SendTo.Server)]
        void AddBotRpc(RpcParams rpcParams = default)
        {
            if (FromHost(rpcParams) && _state.TryAddBot())
                Sync();
        }

        [Rpc(SendTo.Server)]
        void RemoveBotRpc(RpcParams rpcParams = default)
        {
            if (FromHost(rpcParams) && _state.TryRemoveBot())
                Sync();
        }

        // ---------- local API (UI calls these) ----------

        public void SendJoinInfo(string name) =>
            JoinInfoRpc(new FixedString64Bytes(LobbyRules.SanitizeName(name)), new FixedString32Bytes(Application.version ?? ""));

        public void RequestPick(int character) => PickCharacterRpc(character);
        public void RequestReady(bool ready) => SetReadyRpc(ready);

        public void RequestLoadout(string serialized)
        {
            if (LobbyRules.IsValidLoadout(serialized))
                SetCosmeticsRpc(new FixedString128Bytes(serialized ?? ""));
        }

        public void RequestSettings(LobbySettingsData s) => SetSettingsRpc(s.Map, s.PointsToWin, s.Hearts);
        public void RequestDefaultSettings() => DefaultSettingsRpc();
        public void RequestAddBot() => AddBotRpc();
        public void RequestRemoveBot() => RemoveBotRpc();
    }
}
