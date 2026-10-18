using UnityEngine;
#if UNITY_EDITOR
using System;
using System.IO;
using Unity.Multiplayer.PlayMode;
using Unity.Netcode;
#endif

namespace Musketeers.Sandbox
{
    // Editor-only automation so Claude can drive a Multiplayer Play Mode test without clicking:
    // the main editor hosts and writes the join code to a temp file; virtual players join from
    // it, ping once, and mirror their on-screen log to a file. Inert unless the flag file exists.
    public class NetSmokeAutoTest : MonoBehaviour
    {
#if UNITY_EDITOR
        static string Dir => Path.Combine(Path.GetTempPath(), "musketeers-netsmoke");
        static string FlagFile => Path.Combine(Dir, "autotest.on");
        static string CodeFile => Path.Combine(Dir, "code.txt");
        static string CloneLogFile => Path.Combine(Dir, "clone-log.txt");

        bool _isMain;
        bool _joining;
        bool _pinged;
        float _nextPoll;
        DateTime _startUtc;

        void Start()
        {
            if (!File.Exists(FlagFile))
            {
                enabled = false;
                return;
            }

            _isMain = CurrentPlayer.IsMainEditor;
            _startUtc = DateTime.UtcNow;
            NetSmokeLog.Add("AutoTest on (" + (_isMain ? "main editor: will host" : "virtual player: will join") + ")");

            if (_isMain)
            {
                if (File.Exists(CodeFile))
                    File.Delete(CodeFile);
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            }
            else
            {
                File.WriteAllText(CloneLogFile, "");
                NetSmokeLog.LineAdded += line => File.AppendAllText(CloneLogFile, line + "\n");
            }
        }

        void OnDestroy()
        {
            if (_isMain && NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }

        async void Update()
        {
            if (Time.unscaledTime < _nextPoll)
                return;
            _nextPoll = Time.unscaledTime + 1f;

            var nm = NetworkManager.Singleton;
            if (_isMain)
            {
                if (!_joining && NetSmokeServices.Session == null && Authenticated())
                {
                    _joining = true;
                    try
                    {
                        string code = await NetSmokeServices.HostAsync();
                        NetSmokeLog.Add("AutoTest hosting. Code " + code);
                        File.WriteAllText(CodeFile, code);
                    }
                    catch (Exception e)
                    {
                        NetSmokeLog.Error("AutoTest host: " + e.Message);
                    }
                }
                return;
            }

            if (!_joining && Authenticated() && File.Exists(CodeFile) && File.GetLastWriteTimeUtc(CodeFile) > _startUtc.AddSeconds(-30))
            {
                _joining = true;
                string code = File.ReadAllText(CodeFile).Trim();
                try
                {
                    await NetSmokeServices.JoinAsync(code);
                    NetSmokeLog.Add("AutoTest joined " + code);
                }
                catch (Exception e)
                {
                    NetSmokeLog.Error("AutoTest join: " + e.Message);
                }
            }

            if (!_pinged && nm != null && nm.IsConnectedClient && NetSmokePing.Instance != null)
            {
                _pinged = true;
                NetSmokePing.Instance.SendPing("VirtualPlayer");
            }
        }

        void OnClientConnected(ulong clientId)
        {
            if (clientId == NetworkManager.ServerClientId)
                return;
            NetSmokeLog.Add("AutoTest: client " + clientId + " connected");
            if (NetSmokePing.Instance != null)
                NetSmokePing.Instance.SendPing("HostEditor");
        }

        static bool Authenticated() =>
            Unity.Services.Core.UnityServices.State == Unity.Services.Core.ServicesInitializationState.Initialized &&
            Unity.Services.Authentication.AuthenticationService.Instance.IsSignedIn;
#endif
    }
}
