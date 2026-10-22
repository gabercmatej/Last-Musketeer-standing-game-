using System;
using Musketeers.Net;
using Musketeers.Sim;
using UnityEngine;
#if UNITY_EDITOR
using System.IO;
#endif

namespace Musketeers.UI
{
    // Dev tooling (docs/plans/010 B7), inert unless switched on:
    //   Editor (Multiplayer Play Mode): create <temp>/musketeers-autotest/autotest.on (content "leave" for the leave variant).
    //   The main editor hosts; virtual players join with the code the host writes to code.txt in the same folder.
    //   Web: ?autotest=1 (or =leave) on every tab; tabs without &join=CODE host, the others join.
    // Each machine auto-names, picks a musketeer, readies and plans its own slot with a BotPlanner. The host starts when
    // 3 humans are ready, then returns to the lobby, switches map and plays a 2nd match. Logs are "AUTOTEST ..." lines.
    public sealed partial class AppFlow
    {
        enum AutoTestMode { Off, On, Leave }

        const int AutoTestHumans = 3;

        AutoTestMode _atMode;
        bool _atHost;
        string _atName = "Tester";
        float _atNext;
        float _atStartAt = -1f;
        float _atResultsAt = -1f;
        int _atMatches;
        int _atJoinTries;
        float _atRetryAt;   // host/join attempts are at least 6 s apart (the Sessions API answers 429 otherwise)
        bool _atCodeLogged;
        bool _atMapSwitched;
        bool _atFinished;
        BotPlanner _atBot;
        float _atStartedAt;

        void AutoTestInit()
        {
            string mode = "";
            string url = Application.absoluteURL;
            int q = string.IsNullOrEmpty(url) ? -1 : url.IndexOf('?');
            if (q >= 0)
            {
                foreach (var pair in url.Substring(q + 1).Split('&'))
                {
                    var kv = pair.Split('=');
                    if (kv.Length == 2 && kv[0] == "autotest")
                        mode = kv[1];
                }
            }
            bool virtualPlayer = false;
#if UNITY_EDITOR
            string flag = Path.Combine(AutoTestDir, "autotest.on");
            if (File.Exists(flag))
            {
                mode = File.ReadAllText(flag).Trim().ToLowerInvariant();
                if (mode.Length == 0)
                    mode = "1";
            }
            virtualPlayer = Application.dataPath.Replace('\\', '/').Contains("/Library/VP/");
#endif
            if (mode.Length == 0 || mode == "0" || _net == null)
                return;
            _atMode = mode == "leave" ? AutoTestMode.Leave : AutoTestMode.On;
            _atStartedAt = Time.unscaledTime;
#if UNITY_EDITOR
            _atHost = !virtualPlayer;
            if (_atHost && File.Exists(AutoTestCodeFile))
                File.Delete(AutoTestCodeFile);
#else
            _atHost = _pendingJoinCode.Length == 0;
#endif
            uint h = 2166136261;
            foreach (char c in Application.dataPath + Application.absoluteURL + UnityEngine.Random.value)
                h = (h ^ c) * 16777619;
            _atName = _atHost ? "Tester1" : "Tester" + (2 + h % 90);
            Debug.Log($"AUTOTEST on mode={_atMode} role={(_atHost ? "host" : "joiner")} name={_atName}");
        }

#if UNITY_EDITOR
        static string AutoTestDir => Path.Combine(Path.GetTempPath(), "musketeers-autotest");
        static string AutoTestCodeFile => Path.Combine(AutoTestDir, "code.txt");
#endif

        string AutoTestCode()
        {
#if UNITY_EDITOR
            try
            {
                if (File.Exists(AutoTestCodeFile) && File.GetLastWriteTimeUtc(AutoTestCodeFile) > DateTime.UtcNow.AddSeconds(-120))
                    return NetSession.CleanCode(File.ReadAllText(AutoTestCodeFile));
            }
            catch (IOException) { }
            return "";
#else
            return _pendingJoinCode;
#endif
        }

        // Called from OnPlanningStarted: the human's own slot plans like a bot and locks in. Only while an online autotest runs.
        void AutoTestPlan()
        {
            if (_atMode == AutoTestMode.Off || _netTransport == null || _mc == null)
                return;
            int slot = _setup.LocalSlot;
            var round = _mc.Match.Round;
            if (!round.Units[slot].Alive)
                return;
            if (_atBot == null || _atBot.Slot != slot)
                _atBot = new BotPlanner(slot, unchecked(_setup.Seed * 31u + (uint)slot * 7919u + 5u));
            var plan = _atBot.Plan(round, _mc.Cfg, _mc.Match.RoundNumber);
            plan.Slot = (byte)slot;
            _mc.SubmitPlan(plan, true);
        }

        void AutoTestMatchOver(MatchState m)
        {
            if (_atMode == AutoTestMode.Off || _netTransport == null)
                return;
            _atMatches++;
            Debug.Log($"AUTOTEST match over map={_setup.MapId} hash={m.ComputeHash():x8}");
            _atResultsAt = Time.unscaledTime + 3f;
            if (_atMatches >= 2)
            {
                Debug.Log("AUTOTEST done");
                _atFinished = true;
            }
        }

        void AutoTestTick()
        {
            if (_atMode == AutoTestMode.Off || _atFinished && !_atHost || Time.unscaledTime < _atNext)
                return;
            _atNext = Time.unscaledTime + 0.5f;
            float now = Time.unscaledTime;

            if (!_model.Online)
            {
                bool idle = _screen == Screen.Menu && !_onlineBusy && !_charSelect.Visible && !_joinDialog.Open && !_confirm.Open;
                if (!idle || _atFinished || now < _atRetryAt)
                    return;
                if (_atHost)
                {
                    if (_atJoinTries++ < 3)
                    {
                        _atRetryAt = now + 6f;
                        _menu.SetName(_atName);
                        HostOnline();
                    }
                }
                else
                {
                    string code = AutoTestCode();
                    if (code.Length > 0 && _atJoinTries++ < 3)
                    {
                        _atRetryAt = now + 6f;
                        _menu.SetName(_atName);
                        JoinOnline(code);
                    }
                }
                return;
            }

            if (_atHost && !_atCodeLogged && _net != null && _net.Code.Length > 0)
            {
                _atCodeLogged = true;
                Debug.Log("AUTOTEST code=" + _net.Code);
#if UNITY_EDITOR
                try
                {
                    Directory.CreateDirectory(AutoTestDir);
                    File.WriteAllText(AutoTestCodeFile, _net.Code);
                }
                catch (IOException e) { Debug.LogWarning("AUTOTEST could not write the code file: " + e.Message); }
#endif
            }

            if (_charSelect.Visible)
            {
                // The host gave us the first free musketeer on joining; confirm it (the picker would otherwise wait for a click).
                _model.PickCharacter(_model.Character);
                _model.Save();
                ShowLobby();
                return;
            }

            // Leave variant: slot 2 quits during the second match.
            if (_atMode == AutoTestMode.Leave && _atMatches == 1 && _screen == Screen.Match && _setup != null &&
                _setup.LocalSlot == 2 && _mc != null && _mc.TurnSeq >= 3)
            {
                Debug.Log("AUTOTEST leaving mid-match (slot 2)");
                _atFinished = true;
                _atMode = AutoTestMode.Off;
                LeaveToMenu();
                return;
            }

            if (_screen == Screen.Lobby)
            {
                if (!_model.IsHost && !_model.MyReady)
                    _model.ToggleReady();
                if (_atHost)
                {
                    if (_atMatches == 1 && !_atMapSwitched)
                    {
                        _atMapSwitched = true;
                        _model.Map = (_model.Map + 1) % Mathf.Max(1, _model.MapCount);
                        _model.PushSettings();
                        _atStartAt = -1f;
                        return;
                    }
                    int humans = _model.Players - _model.BotCount;
                    bool ready = humans >= AutoTestHumans && _model.AllReady() && _atMatches < 2 && (_atMatches == 0 || _atMapSwitched);
                    if (!ready)
                        _atStartAt = -1f;
                    else if (_atStartAt < 0f)
                        _atStartAt = now + 2f;
                    else if (now >= _atStartAt)
                    {
                        _atStartAt = -1f;
                        StartOnlineMatch();
                    }
                }
                return;
            }

            if (_screen == Screen.Results && _atHost && _atMatches == 1 && _atResultsAt > 0f && now >= _atResultsAt)
            {
                _atResultsAt = -1f;
                ReturnToLobbyClicked();
            }
        }
    }
}
