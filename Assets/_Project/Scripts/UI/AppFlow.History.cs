using System;
using Musketeers.Game;
using Musketeers.Net;

namespace Musketeers.UI
{
    public sealed partial class AppFlow
    {
        MatchHistoryStore _historyStore;
        SharedHistoryClient _sharedHistory;
        HistoryRegistration _historyRegistration;
        MatchHistoryUI _historyUI;
        string _historyMatchId;
        bool[] _historyInitialBots;
        string[] _historyCharacters;
        bool _historyOnline;
        bool HistoryOpen => _historyUI != null && _historyUI.Open;

        void InitHistory()
        {
            _historyStore = new MatchHistoryStore();
            _sharedHistory = new SharedHistoryClient();
            _historyUI = new MatchHistoryUI(_root, _historyStore, _art.Roster, _sharedHistory);
            _historyUI.RefreshRequested += () => StartCoroutine(_sharedHistory.Refresh());
            _menu.MatchLogsClicked += _historyUI.Show;
            _results.MatchLogsClicked += _historyUI.Show;
        }

        // Call in StartMatch after assigning _setup and before clearing _pendingOnline.
        void BeginHistory(MatchSetup setup, MatchStartInfo online)
        {
            _historyOnline = online != null;
            _historyMatchId = online?.MatchId;
            _historyInitialBots = setup.IsBot == null ? null : (bool[])setup.IsBot.Clone();
            _historyCharacters = new string[setup.PlayerCount];
            for (int i = 0; i < setup.PlayerCount; i++)
                _historyCharacters[i] = _art.Character(setup.Characters[i])?.Id;
            _historyRegistration = null;
            bool eligible = online != null && setup.PlayerCount >= 2 && _atMode == AutoTestMode.Off && !AutoHumans;
            for (int i = 0; i < setup.PlayerCount; i++) if (_historyInitialBots == null || _historyInitialBots[i]) eligible = false;
            if (eligible && _net != null && _net.IsHost)
            {
                var registration = new HistoryRegistration { MatchId = online.MatchId, MapId = setup.MapId,
                    PointsToWin = setup.Config.PointsToWin, Hearts = setup.Config.MaxHp, Roster = new HistoryParticipant[setup.PlayerCount] };
                for (int i = 0; i < setup.PlayerCount; i++) registration.Roster[i] = new HistoryParticipant {
                    CharacterId = _historyCharacters[i], Name = setup.Names[i], IsBot = false };
                _historyRegistration = registration;
                StartCoroutine(_sharedHistory.Register(registration));
            }
        }

        // Call only from OnMatchEnded, never the synthetic DevShowResults or arbitrary screen opening.
        void CompleteHistory()
        {
            if (_mc == null || _setup == null || _atMode != AutoTestMode.Off || AutoHumans) return;
            var record = MatchHistoryRecord.Create(_historyMatchId, _historyOnline, true, _historyInitialBots,
                _mc.Match, _historyCharacters, _setup.Names, _setup.MapId, DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'"));
            _historyStore?.Record(record);
            if (record != null && _historyRegistration != null && _net != null && _net.IsHost)
                StartCoroutine(_sharedHistory.Submit(_historyRegistration, record, _mc.Match.RoundNumber, _mc.Match.ComputeHash()));
            else if (record != null) StartCoroutine(_sharedHistory.Refresh());
        }

        void CloseHistory() => _historyUI?.Close();
    }
}
