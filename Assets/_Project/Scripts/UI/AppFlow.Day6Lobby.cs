using Musketeers.Game;
using Musketeers.Net;
using UnityEngine;

namespace Musketeers.UI
{
    public sealed partial class AppFlow
    {
        NameDialog _nameDialog;
        ChatUI _lobbyChat;
        readonly ChatModel _lobbyChatModel = new ChatModel();
        bool LobbyTyping => (_nameDialog != null && _nameDialog.Open) || (_lobbyChat != null && _lobbyChat.IsTyping);

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
            _lobbyChat = new ChatUI(_lobby.ChatRoot);
            _lobbyChat.Bind(_lobbyChatModel, null, null);
            _lobbyChat.Sent += text =>
            {
                if (_model.Online)
                    MatchNet.Instance?.SendChat(text);
            };
            _lobbyChat.EmoteClicked += index =>
            {
                if (_model.Online)
                    MatchNet.Instance?.SendEmote(index);
            };
            MatchNet.LobbyChatReceived += OnLobbyChatReceived;
        }

        void DisposeDay6Lobby() => MatchNet.LobbyChatReceived -= OnLobbyChatReceived;

        void CloseLobbyDialogs()
        {
            _nameDialog?.Hide();
            _lobbyChat?.Blur();
        }

        void ResetLobbyChat() => _lobbyChat?.Clear();

        void OnLobbyChatReceived(string name, int character, string text, int emote)
        {
            if (!_model.Online || _lobbyChat == null)
                return;
            var def = _art.Character(character);
            Color color = def != null ? def.Color : Color.white;
            if (emote >= 0)
                text = ChatUI.EmoteLines[Mathf.Clamp(emote, 0, ChatUI.EmoteLines.Length - 1)];
            _lobbyChat.AddLobbyMessage(name, color, text, emote >= 0);
            if (_screen == Screen.Lobby)
                _audio?.Sfx("SFX/ui/emote_pop", 0.4f);
        }
    }
}
