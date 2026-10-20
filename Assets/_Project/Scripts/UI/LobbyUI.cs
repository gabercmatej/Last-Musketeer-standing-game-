using System;
using Musketeers.Data;
using Musketeers.Presentation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Musketeers.UI
{
    // The lobby overlay on top of the stage (FIXES_v0.2 §2.4): game-master panel top-left (read-only for guests),
    // Menu / Settings / Fullscreen top-right, Wardrobe + Change Musketeer bottom-left, Start (host) or the
    // Ready toggle (guest) bottom-right. The stage itself (StageView) is redrawn by AppFlow when Changed fires.
    public sealed class LobbyUI
    {
        public event Action StartClicked;          // Start pressed and, if needed, confirmed
        public event Action BackClicked;           // Leave lobby confirmed
        public event Action SettingsClicked;
        public event Action ChangeMusketeerClicked;
        public event Action Changed;               // character, players or map changed: rebuild the stage
        public event Action ReadyChanged;          // only the Ready toggle changed: refresh the status lines
        public event Action<bool> WardrobeToggled;

        readonly VisualElement _root, _gm, _bl, _br, _corner;
        readonly Label _mapName, _mapDesc, _count, _points, _hearts, _header;
        readonly Button _add, _remove, _start, _ready;
        readonly LobbyModel _model;
        readonly ArtLibrary _art;
        readonly ConfirmDialog _confirm;

        public WardrobeUI Wardrobe { get; }

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
            _start.clicked += PressStart;
            _ready.clicked += () =>
            {
                _model.ToggleReady();
                UiArt.Sfx("SFX/ui/ready", 0.7f);
                Refresh();
                ReadyChanged?.Invoke();
            };

            // Top-right: Menu is the biggest, Settings and Fullscreen match the main menu.
            var menu = CornerButtons.Add(_corner, "leave-game", "Menu", "Leave the lobby", AskLeave);
            menu.AddToClassList("icon-btn--big");
            menu.AddToClassList("btn--back");
            CornerButtons.Add(_corner, "settings", "Settings", "Volume and credits", () => SettingsClicked?.Invoke());
            CornerButtons.AddFullscreen(_corner);

            // Bottom-left.
            var wardrobe = CornerButtons.Add(_bl, "wardrobe", "Wardrobe", "Hats, faces, capes and boots", () => SetWardrobe(true));
            wardrobe.AddToClassList("icon-btn--big");
            var change = CornerButtons.Add(_bl, "change-player", "Change Musketeer", "Pick another musketeer", () => ChangeMusketeerClicked?.Invoke());
            change.AddToClassList("icon-btn--big");

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
            _ready.text = _model.MyReady ? "Ready" : "Not ready";
            _ready.EnableInClassList("btn--ready-on", _model.MyReady);

            _points.text = _model.PointsToWin.ToString();
            _hearts.text = _model.Hearts.ToString();
            var map = _art.Maps != null ? _art.Maps.Get(_model.Map) : null;
            _mapName.text = map != null ? map.DisplayName : "The Courtyard";
            _mapDesc.text = map != null ? map.Description : "";
            _count.text = _model.Players.ToString();
            _add.SetEnabled(_model.Players < Mathf.Min(4, _model.RosterCount));
            _remove.SetEnabled(_model.Players > 2);
        }
    }
}
