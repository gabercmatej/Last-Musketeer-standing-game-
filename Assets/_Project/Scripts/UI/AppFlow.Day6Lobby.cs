using Musketeers.Game;
using Musketeers.Net;
using UnityEngine;

namespace Musketeers.UI
{
    public sealed partial class AppFlow
    {
        NameDialog _nameDialog;
        bool LobbyTyping => _nameDialog != null && _nameDialog.Open;

        void InitDay6Lobby()
        {
            _nameDialog = new NameDialog(_root);
            _lobby.ChangeNameClicked += () => _nameDialog.Show(_model.Name);
            _nameDialog.Saved += name =>
            {
                if (!_model.Rename(name))
                    return;
                _menu.SetName(name);
                if (!_model.Online)
                    RefreshStage();
                _dialogClosedFrame = Time.frameCount;
            };
        }

        void DisposeDay6Lobby() { }
        void CloseLobbyDialogs() => _nameDialog?.Hide();
        void ResetLobbyChat() { }
    }
}
