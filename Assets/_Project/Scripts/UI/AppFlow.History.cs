using System;
using Musketeers.Game;
using Musketeers.Net;

namespace Musketeers.UI
{
    public sealed partial class AppFlow
    {
        MatchHistoryStore _historyStore;
        MatchHistoryUI _historyUI;
        string _historyMatchId;
        bool[] _historyInitialBots;
        string[] _historyCharacters;
        bool _historyOnline;
        bool HistoryOpen => _historyUI != null && _historyUI.Open;

        void InitHistory()
        {
            _historyStore = new MatchHistoryStore();
            _historyUI = new MatchHistoryUI(_root, _historyStore, _art.Roster);
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
        }

        // Call only from OnMatchEnded, never the synthetic DevShowResults or arbitrary screen opening.
        void CompleteHistory()
        {
            if (_mc == null || _setup == null || _atMode != AutoTestMode.Off || AutoHumans) return;
            var record = MatchHistoryRecord.Create(_historyMatchId, _historyOnline, true, _historyInitialBots,
                _mc.Match, _historyCharacters, _setup.Names, _setup.MapId, DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'"));
            _historyStore?.Record(record);
        }

        void CloseHistory() => _historyUI?.Close();
    }
}
