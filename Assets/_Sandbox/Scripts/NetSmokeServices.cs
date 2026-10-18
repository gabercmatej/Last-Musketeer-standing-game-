using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Musketeers.Sandbox
{
    // Services init + Sessions with Relay, exactly as TECH_DESIGN §5. No threads: everything is
    // async/await on the main thread (TECH_DESIGN §7).
    public static class NetSmokeServices
    {
        public static ISession Session { get; private set; }
        public static string Profile { get; private set; }

        public static async Task InitAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn)
                return;

            Profile = MakeProfileName();
            var init = new InitializationOptions();
            init.SetProfile(Profile);
            await UnityServices.InitializeAsync(init);
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            NetSmokeLog.Add($"Signed in. profile={Profile} playerId={AuthenticationService.Instance.PlayerId}");
            NetSmokeLog.Add($"Platform={Application.platform} RelayProtocol.Default={RelayProtocol.Default}");
        }

        public static async Task<string> HostAsync()
        {
            ConfigureTransport();
            // Multiplayer Services 2.4: the relay protocol is set via WithNetworkOptions (the
            // RelayNetworkOptions(protocol, ...) ctor from TECH_DESIGN §5 is obsolete).
            var opts = new SessionOptions { MaxPlayers = 4, IsPrivate = true }
                .WithRelayNetwork()
                .WithNetworkOptions(new NetworkOptions { RelayProtocol = RelayProtocol.Default });
            Session = await MultiplayerService.Instance.CreateSessionAsync(opts);
            HookSession();
            LogTransport("Host");
            return Session.Code;
        }

        public static async Task JoinAsync(string code)
        {
            ConfigureTransport();
            var opts = new JoinSessionOptions()
                .WithNetworkOptions(new NetworkOptions { RelayProtocol = RelayProtocol.Default });
            Session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant(), opts);
            HookSession();
            LogTransport("Client");
        }

        public static async Task LeaveAsync()
        {
            if (Session == null)
                return;
            var s = Session;
            Session = null;
            await s.LeaveAsync();
            NetSmokeLog.Add("Left session");
        }

        // The session sets the relay server data but not UseWebSockets, and UnityTransport errors if
        // the two disagree. RelayProtocol.Default is WSS only in Web builds, so match it here.
        static void ConfigureTransport()
        {
            var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
            utp.UseWebSockets = RelayProtocol.Default == RelayProtocol.WSS;
            // Hidden browser tabs freeze; give a tab-switch ~60 s before dropping (TECH_DESIGN §7).
            utp.DisconnectTimeoutMS = 60000;
        }

        static void LogTransport(string role)
        {
            var nm = NetworkManager.Singleton;
            var utp = nm.GetComponent<UnityTransport>();
            NetSmokeLog.Add($"{role}: session={Session?.Code} transportProtocol={utp.Protocol} " +
                            $"UseWebSockets={utp.UseWebSockets} relay={RelayProtocol.Default}");
        }

        static void HookSession()
        {
            Session.PlayerJoined += id => NetSmokeLog.Add($"Session: player joined ({Short(id)})");
            Session.PlayerHasLeft += id => NetSmokeLog.Add($"Session: player left ({Short(id)})");
            Session.RemovedFromSession += () => NetSmokeLog.Add("Session: removed from session");
            Session.Deleted += () => NetSmokeLog.Add("Session: deleted (host left)");
        }

        static string Short(string id) => id != null && id.Length > 6 ? id.Substring(0, 6) : id;

        // Web: two tabs of one browser share storage, so each page load needs its own anonymous
        // profile (TECH_DESIGN §5). Editor: each Multiplayer Play Mode player runs from its own
        // project copy (Library/VP/...), so a hash of dataPath gives a stable per-player profile.
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
