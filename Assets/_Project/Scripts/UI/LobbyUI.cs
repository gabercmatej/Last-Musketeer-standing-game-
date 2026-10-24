using System;
using Musketeers.Data;
using Musketeers.Net;
using Musketeers.Presentation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Musketeers.UI
{
    // The lobby overlay on top of the stage (FIXES_v0.2 §2.4): game-master panel top-right (read-only for guests),
    // Fullscreen / Settings / Leave top-left, Wardrobe + Change Musketeer bottom-left, Start (host) or the
    // Ready toggle (guest) bottom-right. The stage itself (StageView) is redrawn by AppFlow when Changed fires.
    public sealed class LobbyUI
    {
        public event Action StartClicked;          // Start pressed and, if needed, confirmed
        public event Action BackClicked;           // Leave lobby confirmed
        public event Action SettingsClicked;
        public event Action ChangeMusketeerClicked;
        public event Action ChangeNameClicked;
        public event Action Changed;               // character, players or map changed: rebuild the stage
        public event Action ReadyChanged;          // only the Ready toggle changed: refresh the status lines
        public event Action<bool> WardrobeToggled;

        readonly VisualElement _root, _gm, _bl, _br, _corner;
        readonly Label _mapName, _mapDesc, _count, _points, _hearts, _header;
        readonly Button _add, _remove, _start, _ready;
        readonly LobbyModel _model;
        readonly ArtLibrary _art;
        readonly ConfirmDialog _confirm;

        VisualElement _invite;
        Label _inviteLabel;
        string _inviteCode = "";

        public WardrobeUI Wardrobe { get; }
        public VisualElement ChatRoot { get; private set; }

        // Online lobbies show their code with Copy code / Copy invite link (top centre). "" hides it (offline).
        public void SetInviteCode(string code)
        {
            _inviteCode = code ?? "";
            _inviteLabel.text = _inviteCode;
            _invite.style.display = _inviteCode.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void BuildInvite(VisualElement root)
        {
            _invite = new VisualElement { name = "invite" };
            _invite.AddToClassList("panel");
            _invite.AddToClassList("invite-panel");
            var caption = new Label("Lobby code");
            caption.AddToClassList("invite-caption");
            _inviteLabel = new Label();
            _inviteLabel.AddToClassList("invite-code");
            var row = new VisualElement();
            row.AddToClassList("invite-row");
            row.Add(CopyButton("Copy code", () => _inviteCode));
            row.Add(CopyButton("Copy invite link", () => NetSession.InviteLink(_inviteCode)));
            _invite.Add(caption);
            _invite.Add(_inviteLabel);
            _invite.Add(row);
            _invite.style.display = DisplayStyle.None;
            _root.Add(_invite);
        }

        static Button CopyButton(string text, Func<string> payload)
        {
            Button b = null;
            b = new Button { text = text, focusable = false };
            b.AddToClassList("btn");
            b.AddToClassList("btn--small");
            b.clicked += () =>
            {
                Clipboard.Copy(payload());
                b.text = "Copied";
                b.schedule.Execute(() => b.text = text).StartingIn(1500);
            };
            return b;
        }

        public LobbyUI(VisualElement root, LobbyModel model, ArtLibrary art, ConfirmDialog confirm)
        {
            _model = model;
            _art = art;
            _confirm = confirm;
            _root = root.Q("lobby");
            _gm = root.Q("gm-panel");
            _bl = root.Q("lobby-bl");
            _br = root.Q("lobby-br");
            _corner = root.Q("lobby-corner");
            _header = root.Q<Label>("gm-header");
            _mapName = root.Q<Label>("map-name");
            _mapDesc = root.Q<Label>("map-desc");
            _count = root.Q<Label>("player-count");
            _points = root.Q<Label>("points-value");
            _hearts = root.Q<Label>("hearts-value");
            _add = root.Q<Button>("add-bot");
            _remove = root.Q<Button>("remove-bot");
            _start = root.Q<Button>("start");
            _ready = root.Q<Button>("ready");
            UiArt.Set(root.Q("hearts-icon"), GameArt.HeartFull, new Color(0.9f, 0.2f, 0.25f));

            root.Q<Button>("map-prev").clicked += () => StepMap(-1);
            root.Q<Button>("map-next").clicked += () => StepMap(1);
            root.Q<Button>("points-minus").clicked += () => StepRules(-1, 0);
            root.Q<Button>("points-plus").clicked += () => StepRules(1, 0);
            root.Q<Button>("hearts-minus").clicked += () => StepRules(0, -1);
            root.Q<Button>("hearts-plus").clicked += () => StepRules(0, 1);
            _add.clicked += () => SetPlayers(_model.Players + 1);
            _remove.clicked += () => SetPlayers(_model.Players - 1);
            BuildInvite(root);
            _start.clicked += PressStart;
            _ready.clicked += () =>
            {
                _model.ToggleReady();
                UiArt.Sfx("SFX/ui/ready", 0.7f);
                Refresh();
                ReadyChanged?.Invoke();
            };

            // Top-left: Fullscreen, Settings, Leave (same size, the mirror of the menu).
            CornerButtons.AddTopLeftSet(_corner, () => SettingsClicked?.Invoke(), AskLeave, "Leave", "Leave the lobby");
            root.Q<Button>("rules-default").clicked += () =>
            {
                if (!_model.IsHost)
                    return;
                _model.ResetRules();
                _model.Save();
                UiArt.Sfx("SFX/ui/wardrobe_equip", 0.5f);
                Refresh();
            };

            // Bottom-left.
            var wardrobe = CornerButtons.Add(_bl, "wardrobe", "Wardrobe", "Hats, faces, capes and boots", () => SetWardrobe(true));
            wardrobe.AddToClassList("icon-btn--big");
            var change = CornerButtons.Add(_bl, "change-player", "Change Musketeer", "Pick another musketeer", () => ChangeMusketeerClicked?.Invoke());
            change.AddToClassList("icon-btn--big");
            var rename = CornerButtons.Add(_bl, "change-name", "Change Name", "Edit your player name", () => ChangeNameClicked?.Invoke());
            rename.AddToClassList("icon-btn--big");

            ChatRoot = new VisualElement { name = "lobby-chat" };
            ChatRoot.AddToClassList("panel");
            ChatRoot.style.position = Position.Absolute;
            ChatRoot.style.left = 24;
            ChatRoot.style.top = 125;
            ChatRoot.style.width = 320;
            ChatRoot.style.paddingLeft = 12;
            ChatRoot.style.paddingRight = 12;
            ChatRoot.style.paddingTop = 8;
            ChatRoot.style.paddingBottom = 8;
            _root.Add(ChatRoot);

            Wardrobe = new WardrobeUI(root, model, art);
            Wardrobe.Closed += () => SetWardrobe(false);
            foreach (var b in _root.Query<Button>().ToList())
                b.focusable = false;
            Refresh();
        }

        public void SetVisible(bool visible)
        {
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible)
            {
                // Leaving with the wardrobe open: close it and bring the corner controls back for next time.
                Wardrobe.SetVisible(false);
                SetCornersVisible(true);
            }
        }

        public void SetWardrobe(bool open)
        {
            Wardrobe.SetVisible(open);
            SetCornersVisible(!open);
            WardrobeToggled?.Invoke(open);
        }

        // The wardrobe covers the right half; the GM panel and the bottom controls step aside.
        void SetCornersVisible(bool on)
        {
            var d = on ? DisplayStyle.Flex : DisplayStyle.None;
            _gm.style.display = d;
            _bl.style.display = d;
            _br.style.display = d;
        }

        public void AskLeave() =>
            _confirm.Show("Leave lobby?", "Leave", "Stay", () => BackClicked?.Invoke());

        public void PressStart()
        {
            if (!_model.IsHost)
                return;
            if (_model.Players < 2)
            {
                // Online, the host can be alone in the lobby: a match needs at least two musketeers.
                UiArt.Sfx("SFX/ui/ui_error");
                _confirm.ShowNotice("Add a bot or wait for a friend first.");
                return;
            }
            if (_model.WaitingForResults)
            {
                _confirm.ShowNotice("Waiting for players to return from results.");
                return;
            }
            if (_model.AllReady())
            {
                UiArt.Sfx("SFX/ui/ready");
                StartClicked?.Invoke();
                return;
            }
            _confirm.Show("Not everyone is ready. Start anyway?", "Start anyway", "Wait", () =>
            {
                UiArt.Sfx("SFX/ui/ready");
                StartClicked?.Invoke();
            });
        }

        void SetPlayers(int n)
        {
            if (!_model.IsHost)
                return;
            if (_model.Online)
            {
                // Online the host adds/removes bots and the seats come back in the snapshot (4 seats at most, humans included).
                if (n > _model.Players && _model.Players < Mathf.Min(4, _model.RosterCount))
                    _model.AddBot();
                else if (n < _model.Players && _model.BotCount > 0)
                    _model.RemoveBot();
                else
                    return;
                UiArt.Sfx(n > _model.Players ? "SFX/ui/player_join" : "SFX/ui/player_leave");
                return;
            }
            n = Mathf.Clamp(n, 2, Mathf.Min(4, _model.RosterCount));
            if (n == _model.Players)
                return;
            if (n > _model.Players)
            {
                _model.RerollBot(n - 1);
                UiArt.Sfx("SFX/ui/player_join");
            }
            else
                UiArt.Sfx("SFX/ui/player_leave");
            _model.Players = n;
            _model.Save();
            Refresh();
            Changed?.Invoke();
        }

        public void StepMap(int d)
        {
            if (!_model.IsHost)
                return;
            _model.Map = (_model.Map + d + _model.MapCount) % _model.MapCount;
            _model.Save();
            _model.PushSettings();
            Refresh();
            Changed?.Invoke();
        }

        void StepRules(int points, int hearts)
        {
            if (!_model.IsHost)
                return;
            _model.PointsToWin += points;
            _model.Hearts += hearts;
            _model.Save();
            _model.PushSettings();
            UiArt.Sfx("SFX/ui/wardrobe_equip", 0.5f);
            Refresh();
        }

        // Re-reads everything from the model (also after a role switch: host vs guest).
        public void Refresh()
        {
            bool host = _model.IsHost;
            _gm.EnableInClassList("gm-panel--readonly", !host);
            _header.text = host ? "Match settings" : "Set by the host";
            _start.style.display = host ? DisplayStyle.Flex : DisplayStyle.None;
            _ready.style.display = host ? DisplayStyle.None : DisplayStyle.Flex;
            // The label names the action: "I'm ready" (primary) when you are not, "Not ready" (quiet) when you are.
            _ready.text = _model.MyReady ? "Not ready" : "I'm ready";
            _ready.EnableInClassList("btn--secondary", _model.MyReady);
            _ready.EnableInClassList("btn--back", _model.MyReady);

            _points.text = _model.PointsToWin.ToString();
            _hearts.text = _model.Hearts.ToString();
            var map = _art.Maps != null ? _art.Maps.Get(_model.Map) : null;
            _mapName.text = map != null ? map.DisplayName : "The Courtyard";
            _mapDesc.text = map != null ? map.Description : "";
            _count.text = _model.BotCount.ToString();
            ChatRoot.style.display = _model.Online ? DisplayStyle.Flex : DisplayStyle.None;
            _add.SetEnabled(host && _model.BotCount < _model.MaxBots);
            _remove.SetEnabled(host && _model.BotCount > _model.MinBots);
            _invite.style.display = _inviteCode.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
