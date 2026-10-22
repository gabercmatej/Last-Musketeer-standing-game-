using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Musketeers.Net
{
    // A join failed in a way the player should read; Message is shown as is.
    public sealed class NetJoinException : Exception
    {
        public NetJoinException(string message) : base(message) { }
    }

    // Online session service (TECH_DESIGN §5/§7): Unity Services init with a fresh anonymous profile, Sessions + Relay
    // (WSS on Web), the NetworkManager transport setup, and the "why did I end up back at the menu" events.
    // Everything runs on the main thread (async/await only, no threads).
    // Chunk B / AppFlow: Host/Join, then LobbyNet.Instance; subscribe to Ended to return to the menu with a message.
    public sealed class NetSession : MonoBehaviour
    {
        public const string MsgHostLeft = "The host left. The match has ended.";
        public const string MsgLost = "Connection lost.";
        public const string MsgNotFound = "No lobby with that code. Check it and try again.";
        public const string MsgConnectFailed = "Could not connect. Try again.";
        const float ConnectTimeout = 20f;

        public static NetSession Instance { get; private set; }

        [SerializeField] private GameObject _netPrefab;   // NetSystems.prefab: NetworkObject + LobbyNet + MatchNet, spawned by the host

        GameObject _netObject;
        public ISession Session { get; private set; }
        public string Profile { get; private set; }
        public bool Online { get; private set; }
        public bool IsHost => Online && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        public string Code => Session != null ? Session.Code : "";

        /// <summary>Raised once when the session ends without the player asking: kicked (reason from the host), host left, connection lost.</summary>
        public event Action<string> Ended;

        bool _busy;            // host/join in progress: end reasons are returned to that call instead of raised
        bool _leaving;
        string _endReason;
        NetworkManager _hooked;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            Unhook();
            if (Instance == this)
                Instance = null;
        }

        // ---------- init / host / join / leave ----------

        public async Task InitAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn)
                return;
            Profile = MakeProfileName();
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                var init = new InitializationOptions();
                init.SetProfile(Profile);
                await UnityServices.InitializeAsync(init);
            }
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        /// <summary>Creates a private lobby (Relay) and seats the host. Returns the join code.</summary>
        public async Task<string> HostAsync(string playerName)
        {
            if (_busy || Online)
                throw new NetJoinException(MsgConnectFailed);
            _busy = true;
            _endReason = null;
            try
            {
                await InitAsync();
                ConfigureTransport();
                var opts = new SessionOptions { MaxPlayers = LobbyRules.MaxPlayers, IsPrivate = true }
                    .WithRelayNetwork()
                    .WithNetworkOptions(new NetworkOptions { RelayProtocol = RelayProtocol.Default });
                Session = await MultiplayerService.Instance.CreateSessionAsync(opts);
                Online = true;
                Hook();
                await WaitFor(() => NetworkManager.Singleton.IsServer);
                // The lobby/match object is a registered prefab the host spawns; clients receive it automatically.
                _netObject = Instantiate(_netPrefab);
                _netObject.GetComponent<NetworkObject>().Spawn();
                await WaitFor(() => LobbyNet.Instance != null && LobbyNet.Instance.IsSpawned);
                LobbyNet.Instance.HostJoin(playerName);
                return Session.Code;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[NetSession] Host failed: " + e);
                await CleanupAsync();
                throw new NetJoinException(e is NetJoinException ? e.Message : MsgConnectFailed);
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>Joins a lobby by code and waits until the host has seated us. Throws NetJoinException with a player-facing message.</summary>
        public async Task JoinAsync(string code, string playerName)
        {
            if (_busy || Online)
                throw new NetJoinException(MsgConnectFailed);
            _busy = true;
            _endReason = null;
            try
            {
                await InitAsync();
                ConfigureTransport();
                var opts = new JoinSessionOptions()
                    .WithNetworkOptions(new NetworkOptions { RelayProtocol = RelayProtocol.Default });
                // Up to 3 tries. "Already a member" (a half-finished earlier join) leaves the stale membership first;
                // it and rate limiting (429) back off 3-6 s before the next try. Anything else fails at once.
                for (int attempt = 1; ; attempt++)
                {
                    try
                    {
                        Session = await MultiplayerService.Instance.JoinSessionByCodeAsync(NormalizeCode(code), opts);
                        break;
                    }
                    catch (SessionException e)
                    {
                        bool conflict = e.Error == SessionError.SessionConflict;
                        bool retry = conflict || e.Error == SessionError.RateLimitExceeded || e.Message.Contains("429");
                        Debug.LogWarning($"[NetSession] Join attempt {attempt} failed: {e.Error} {e.Message}");
                        if (!retry || attempt >= 3)
                            throw new NetJoinException(MapJoinError(e));
                        if (conflict)
                            await LeaveStaleSessionsAsync();
                        await WaitSeconds(3f * attempt);
                    }
                }
                Online = true;
                Hook();
                await WaitFor(() => LobbyNet.Instance != null && LobbyNet.Instance.IsSpawned && NetworkManager.Singleton.IsConnectedClient);
                LobbyNet.Instance.SendJoinInfo(playerName);
                // The host answers by seating us (or by disconnecting us with a reason).
                await WaitFor(() => LobbyNet.Instance != null && LobbyNet.Instance.IndexOf(NetworkManager.Singleton.LocalClientId) >= 0);
            }
            catch (Exception e)
            {
                string reason = _endReason;   // a kick reason beats a generic failure
                if (reason == null)
                    Debug.LogWarning("[NetSession] Join failed: " + e);
                await CleanupAsync();
                throw new NetJoinException(reason ?? (e is NetJoinException ? e.Message : MsgConnectFailed));
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>The player leaves (or the host closes the lobby). No Ended event for this.</summary>
        public async Task LeaveAsync()
        {
            _leaving = true;
            try { await CleanupAsync(); }
            finally { _leaving = false; }
        }

        async Task CleanupAsync()
        {
            var s = Session;
            Session = null;
            bool wasOnline = Online;
            Online = false;
            var nm = NetworkManager.Singleton;
            bool host = nm != null && nm.IsServer;
            if (_netObject != null)
            {
                var no = _netObject.GetComponent<NetworkObject>();
                if (no != null && no.IsSpawned)
                    no.Despawn(true);
                else
                    Destroy(_netObject);
                _netObject = null;
            }
            if (nm != null && nm.IsListening)
                nm.Shutdown();
            Unhook();
            if (s != null)
            {
                try
                {
                    if (host)
                        await s.AsHost().DeleteAsync();
                    else
                        await s.LeaveAsync();
                }
                catch (Exception e)
                {
                    Debug.Log("[NetSession] Leave: " + e.Message);
                }
            }
            if (wasOnline)
                Debug.Log("[NetSession] Left the session");
        }

        // The session sets the relay server data but not UseWebSockets, and UnityTransport errors if the two disagree.
        // RelayProtocol.Default is WSS only in Web builds. Hidden browser tabs freeze, so a tab-switch gets ~60 s (§7).
        static void ConfigureTransport()
        {
            var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
            utp.UseWebSockets = RelayProtocol.Default == RelayProtocol.WSS;
            utp.DisconnectTimeoutMS = 60000;
        }

        async Task WaitFor(Func<bool> condition)
        {
            float start = Time.realtimeSinceStartup;
            while (!condition())
            {
                if (_endReason != null)
                    throw new NetJoinException(_endReason);
                if (Time.realtimeSinceStartup - start > ConnectTimeout)
                    throw new NetJoinException(MsgConnectFailed);
                await Awaitable.NextFrameAsync();
            }
        }

        static async Task LeaveStaleSessionsAsync()
        {
            foreach (var s in new System.Collections.Generic.List<ISession>(MultiplayerService.Instance.Sessions.Values))
            {
                try { await s.LeaveAsync(); }
                catch (Exception e) { Debug.Log("[NetSession] Stale leave: " + e.Message); }
            }
        }

        static async Task WaitSeconds(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
                await Awaitable.NextFrameAsync();
        }

        static string MapJoinError(SessionException e)
        {
            string text = (e.Message + " " + (e.InnerException != null ? e.InnerException.Message : "")).ToLowerInvariant();
            if (text.Contains("full"))
                return LobbyNet.MsgFull;
            if (e.Error == SessionError.SessionNotFound || text.Contains("not found") || text.Contains("404"))
                return MsgNotFound;
            return MsgConnectFailed;
        }

        // ---------- events ----------

        void Hook()
        {
            Unhook();
            _hooked = NetworkManager.Singleton;
            if (_hooked != null)
            {
                _hooked.OnConnectionEvent += OnConnectionEvent;
                _hooked.OnTransportFailure += OnTransportFailure;
            }
            if (Session != null)
            {
                Session.Deleted += OnSessionGone;
                Session.RemovedFromSession += OnSessionGone;
            }
        }

        void Unhook()
        {
            if (_hooked != null)
            {
                _hooked.OnConnectionEvent -= OnConnectionEvent;
                _hooked.OnTransportFailure -= OnTransportFailure;
                _hooked = null;
            }
        }

        // The local connection closed. A kick carries the host's reason; none means the host went away.
        void OnConnectionEvent(NetworkManager nm, ConnectionEventData data)
        {
            if (data.EventType != ConnectionEvent.ClientDisconnected || nm.IsServer || data.ClientId != nm.LocalClientId)
                return;
            string reason = nm.DisconnectReason;
            End(string.IsNullOrEmpty(reason) ? MsgHostLeft : reason);
        }

        void OnTransportFailure() => End(MsgLost);

        void OnSessionGone()
        {
            if (!IsHost)
                End(MsgHostLeft);
        }

        void End(string reason)
        {
            if (_leaving || !Online && !_busy)
                return;
            if (_busy)
            {
                _endReason = _endReason ?? reason;   // returned to the pending Host/Join call
                return;
            }
            Debug.Log("[NetSession] Ended: " + reason);
            _ = EndAsync(reason);
        }

        async Task EndAsync(string reason)
        {
            _leaving = true;
            await Awaitable.NextFrameAsync();   // not inside NGO's own disconnect callback
            try { await CleanupAsync(); }
            finally { _leaving = false; }
            Ended?.Invoke(reason);
        }

        // ---------- helpers ----------

        public static string NormalizeCode(string code) => (code ?? "").Trim().ToUpperInvariant();

        // "?join=ABC123" in the page URL (the invite link), or "" if absent.
        public static string ReadJoinCode(string url = null)
        {
            url = url ?? Application.absoluteURL;
            int q = string.IsNullOrEmpty(url) ? -1 : url.IndexOf('?');
            if (q < 0)
                return "";
            string query = url.Substring(q + 1);
            int hash = query.IndexOf('#');
            if (hash >= 0)
                query = query.Substring(0, hash);
            foreach (var pair in query.Split('&'))
            {
                int eq = pair.IndexOf('=');
                if (eq > 0 && pair.Substring(0, eq) == "join")
                    return CleanCode(pair.Substring(eq + 1));
            }
            return "";
        }

        public static string CleanCode(string raw)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in NormalizeCode(raw))
            {
                if (c >= 'A' && c <= 'Z' || c >= '0' && c <= '9')
                    sb.Append(c);
            }
            return sb.ToString();
        }

        // The page URL without its query / fragment, plus ?join=CODE (Editor: a placeholder host so the format is visible).
        public static string InviteLink(string code, string url = null)
        {
            url = url ?? Application.absoluteURL;
            if (string.IsNullOrEmpty(url) || !url.StartsWith("http"))
                url = "http://localhost/";
            int cut = url.IndexOfAny(new[] { '?', '#' });
            if (cut >= 0)
                url = url.Substring(0, cut);
            return url + "?join=" + code;
        }

        // Web: two tabs of one browser share storage, so each page load needs its own anonymous profile (TECH_DESIGN §5).
        // Editor: each Multiplayer Play Mode player runs from its own project copy, so a hash of dataPath is stable per player.
        static string MakeProfileName()
        {
#if UNITY_EDITOR
            return "ed" + Fnv1a(Application.dataPath).ToString("x8");
#else
            return "web" + Guid.NewGuid().ToString("N").Substring(0, 8);
#endif
        }

        static uint Fnv1a(string s)
        {
            uint h = 2166136261;
            foreach (char c in s)
            {
                h ^= c;
                h *= 16777619;
            }
            return h;
        }
    }
}
