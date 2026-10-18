using System;
using Unity.Netcode;
using UnityEngine;

namespace Musketeers.Sandbox
{
    // Minimal IMGUI for the Day 0 smoke test. Throwaway: the real UI is UI Toolkit (CLAUDE.md rule 8).
    public class NetSmokeUI : MonoBehaviour
    {
        const string NameKey = "netsmoke.name";

        string _name = "";
        string _joinCode = "";
        string _hostCode = "";
        string _status = "Starting...";
        bool _ready;
        bool _busy;

        async void Start()
        {
            _name = PlayerPrefs.GetString(NameKey, "Musketeer");
            _joinCode = ReadJoinCodeFromUrl();
            try
            {
                await NetSmokeServices.InitAsync();
                _ready = true;
                _status = "Ready";
            }
            catch (Exception e)
            {
                _status = "Sign-in failed";
                NetSmokeLog.Error("Init: " + e.Message);
            }
        }

        void OnGUI()
        {
            float scale = Mathf.Max(1f, Screen.height / 540f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale;

            GUILayout.BeginArea(new Rect(12, 12, Mathf.Min(520, w - 24), 520));
            GUILayout.Label("<b>NetSmoke</b>  " + _status + "  v" + Application.version, Rich());

            var nm = NetworkManager.Singleton;
            bool connected = nm != null && (nm.IsHost || nm.IsConnectedClient);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Name", GUILayout.Width(60));
            _name = GUILayout.TextField(_name, 24, GUILayout.Width(200));
            GUILayout.EndHorizontal();

            GUI.enabled = _ready && !_busy && !connected;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Host", GUILayout.Width(120)))
                Host();
            GUILayout.Label(string.IsNullOrEmpty(_hostCode) ? "" : "Code: <b>" + _hostCode + "</b>", Rich());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Code", GUILayout.Width(60));
            _joinCode = GUILayout.TextField(_joinCode, 12, GUILayout.Width(120));
            if (GUILayout.Button("Join", GUILayout.Width(80)))
                Join();
            GUILayout.EndHorizontal();

            GUI.enabled = connected && NetSmokePing.Instance != null;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Ping", GUILayout.Width(120)))
                NetSmokePing.Instance.SendPing(_name);
            GUI.enabled = connected && !_busy;
            if (GUILayout.Button("Leave", GUILayout.Width(80)))
                Leave();
            GUILayout.EndHorizontal();
            GUI.enabled = true;

            if (connected)
                GUILayout.Label($"Connected as {(nm.IsHost ? "HOST" : "CLIENT")}, clients: {(nm.IsHost ? nm.ConnectedClientsIds.Count.ToString() : "?")}");

            GUILayout.Space(8);
            foreach (var line in NetSmokeLog.Lines)
                GUILayout.Label(line);
            GUILayout.EndArea();
        }

        async void Host()
        {
            SaveName();
            _busy = true;
            _status = "Creating session...";
            try
            {
                _hostCode = await NetSmokeServices.HostAsync();
                _status = "Hosting";
                NetSmokeLog.Add("Hosting. Code " + _hostCode);
            }
            catch (Exception e)
            {
                _status = "Host failed";
                NetSmokeLog.Error("Host: " + e.Message);
            }
            _busy = false;
        }

        async void Join()
        {
            SaveName();
            if (string.IsNullOrWhiteSpace(_joinCode))
                return;
            _busy = true;
            _status = "Joining...";
            try
            {
                await NetSmokeServices.JoinAsync(_joinCode);
                _status = "Joined";
                NetSmokeLog.Add("Joined " + _joinCode.ToUpperInvariant());
            }
            catch (Exception e)
            {
                _status = "Join failed";
                NetSmokeLog.Error("Join: " + e.Message);
            }
            _busy = false;
        }

        async void Leave()
        {
            _busy = true;
            try
            {
                await NetSmokeServices.LeaveAsync();
            }
            catch (Exception e)
            {
                NetSmokeLog.Error("Leave: " + e.Message);
            }
            _hostCode = "";
            _status = "Ready";
            _busy = false;
        }

        void SaveName()
        {
            PlayerPrefs.SetString(NameKey, _name);
            PlayerPrefs.Save();
        }

        // Invite links look like https://site/?join=CODE (GAME_DESIGN §9).
        static string ReadJoinCodeFromUrl()
        {
            string url = Application.absoluteURL;
            if (string.IsNullOrEmpty(url))
                return "";
            int q = url.IndexOf('?');
            if (q < 0)
                return "";
            foreach (var part in url.Substring(q + 1).Split('&'))
            {
                if (part.StartsWith("join=", StringComparison.OrdinalIgnoreCase))
                    return Uri.UnescapeDataString(part.Substring(5)).ToUpperInvariant();
            }
            return "";
        }

        static GUIStyle Rich()
        {
            var s = new GUIStyle(GUI.skin.label) { richText = true };
            return s;
        }
    }
}
