using System;
using Musketeers.Data;
using Musketeers.Game;
using Musketeers.Net;
using Musketeers.Presentation;
using Musketeers.Sim;
using UnityEngine;

namespace Musketeers.UI
{
    // Day 5 chunk B: the online match flow (start, chat, resync, return to lobby). The lobby side is in AppFlow.cs.
    // Offline play never reaches any of this: _netTransport stays null.
    public sealed partial class AppFlow
    {
        bool ControlAllOn => _transport != null && _transport.ControlAll;

        // Offline: the dev autoplay switch. Online: only the autotest plans for the human (AppFlow.AutoTest.cs).
        bool AutoHumans => _transport != null ? _transport.AutoPlayHumans : _atMode != AutoTestMode.Off && _netTransport != null;

        // The host pressed Start in the lobby: everyone (the host included) gets MatchStartRpc and builds the same setup.
        void StartOnlineMatch()
        {
            if (_net == null || !_net.IsHost || _lobbyNet == null || MatchNet.Instance == null)
                return;
            if (!_lobbyNet.HostState.CanStart)
            {
                _confirm.ShowNotice("Wait for everyone to return to the lobby before starting.");
                return;
            }
            var entries = _lobbyNet.Entries();
            if (entries.Count < 2)
            {
                _confirm.ShowNotice("A match needs at least two players.");
                return;
            }
            uint seed = _menu.DevSeed ?? (uint)UnityEngine.Random.Range(1, int.MaxValue);
            _lobbyNet.SetMatchInProgress(true);   // new joiners are turned away until the host returns to the lobby
            MatchNet.Instance.HostStartMatch(seed, _lobbyNet.Settings, entries);
        }

        void OnMatchStartInfo(MatchStartInfo info)
        {
            if (!_model.Online || _lobbyNet == null)
                return;
            var setup = BuildOnlineSetup(info);
            if (setup == null)
                return;
            _pendingOnline = info;
            _chat.Blur();
            StartWithLoading(setup);
        }

        // The same setup on every machine; only LocalSlot (your seat in the host's order) differs.
        MatchSetup BuildOnlineSetup(MatchStartInfo info)
        {
            int n = info.Roster.Count;
            int local = info.SlotOf(_lobbyNet.LocalClientId);
            if (n < 2 || local < 0)
                return null;
            var map = _art.Maps != null ? _art.Maps.Get(info.Settings.Map) : null;
            var setup = new MatchSetup
            {
                Seed = info.Seed,
                MapAscii = map != null && map.Layout != null ? map.Layout.text : BuiltInMaps.Courtyard,
                MapId = map != null ? map.Id : "courtyard",
                Abilities = new Ability[n],
                Characters = new int[n],
                Names = new string[n],
                IsBot = new bool[n],
                Loadouts = new string[n],
                LocalSlot = local,
            };
            setup.Config.PointsToWin = info.Settings.PointsToWin;
            setup.Config.MaxHp = info.Settings.Hearts;
            for (int i = 0; i < n; i++)
            {
                var e = info.Roster[i];
                var def = _art.Character(e.Character);
                setup.Characters[i] = e.Character;
                setup.Abilities[i] = def != null ? def.Ability : (Ability)(e.Character % 4);
                setup.IsBot[i] = e.IsBot;
                setup.Names[i] = !e.IsBot && !string.IsNullOrEmpty(e.Name) ? e.Name : _art.CharacterName(e.Character);
                setup.Loadouts[i] = e.IsBot ? "" : (e.Loadout ?? "");
            }
            return setup;
        }

        // Returning is local UI navigation; only the sender's lobby presence/ready flag changes.
        void ReturnToLobbyClicked()
        {
            if (_model.Online)
            {
                if (_lobbyNet == null || _lobbyNet.MatchInProgress)
                {
                    _confirm.ShowNotice("The host is finishing the match. Try Return to Lobby in a moment.");
                    return;
                }
                _lobbyNet.RequestReturnToLobby();
            }
            ShowLobby();
        }

        void OnReturnedToLobby() { } // Legacy RPC no longer forces anyone out of results.

        void OnChatSent(string text)
        {
            if (_netTransport != null)
                MatchNet.Instance?.SendChat(text);
            else
                _chatModel.Add(_setup != null ? _setup.LocalSlot : 0, text, Time.unscaledTime);
        }

        // A chat line or emote the host relayed (this machine's own included): bubble, sound and log line on every machine.
        void OnChatReceived(int slot, string text, int emote)
        {
            if (_mc == null || _setup == null || _screen != Screen.Match || slot < 0 || slot >= _setup.PlayerCount)
                return;
            if (emote < 0)
            {
                _chatModel.Add(slot, text, Time.unscaledTime);
                _audio?.Sfx("SFX/ui/emote_pop", 0.4f);
                return;
            }
            int index = Mathf.Clamp(emote, 0, ChatUI.EmoteLines.Length - 1);
            var u = _board.Unit(slot);
            if (u != null && u.Alive)
            {
                _fx.Emote(u.transform, new Vector3(0.35f, UnitView.FeetY + 1.85f, 0f), UiArt.Get(GameArt.Emotes[index]), 2f);
                _audio?.Sfx("SFX/ui/emote_pop");
                _audio?.Line(u.CharacterId, "taunt", index);
            }
            _chatModel.Add(slot, ChatUI.EmoteLines[index], Time.unscaledTime, true);
        }

        // Host only: a human's connection dropped mid-match.
        void OnClientLeft(ulong clientId)
        {
            MatchNet.Instance?.ReplayDisconnected(clientId);
            if (_netTransport == null || _setup == null)
                return;
            int slot = _netTransport.SlotOf(clientId);
            if (slot >= 0 && slot < _setup.PlayerCount)
                _log.Line($"{_setup.Names[slot]} left the match", "log-line--turn");
            _netTransport.ClientDisconnected(clientId);
        }

        // The host found our state different from its own and sent the real one.
        void OnSnapshot(MatchState state)
        {
            if (_mc == null)
                return;
            Debug.LogWarning("[Net] Out of sync: state replaced by the host's snapshot.");
            _mc.ApplySnapshot(state);
            if (!_director.IsPlaying)
                _director.ShowState(state.Round, state.Crowned);
        }
    }
}
