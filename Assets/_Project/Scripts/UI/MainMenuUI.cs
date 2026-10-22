using System;
using Musketeers.Data;
using Musketeers.Game;
using PrimeTween;
using UnityEngine;
using UnityEngine.UIElements;

namespace Musketeers.UI
{
    // Title screen (GAME_DESIGN §9): Matej's menu art on the left, the panel on the right (name, Play vs Bots,
    // Host/Join), Lore / Settings / Fullscreen top-right. Host/Join wait for Day 5's online play.
    public sealed class MainMenuUI
    {
        const string NameKey = "Musketeers.PlayerName";

        public event Action PlayClicked;     // name is valid and saved
        public event Action LoreClicked;
        public event Action SettingsClicked;
        public event Action HostClicked;     // name is valid and saved (Day 5)
        public event Action JoinClicked;     // name is valid and saved

        readonly VisualElement _root;
        readonly VisualElement _shakeTarget;
        readonly TextField _name;
        readonly TextField _seed;
        readonly Label _hint;
        readonly Label _note;
        Tween _shake;

        public string PlayerName => NameRules.Clean(_name.value);

        public uint? DevSeed => DevFlags.Available && uint.TryParse(_seed.value?.Trim(), out uint s) ? s : (uint?)null;

        public MainMenuUI(VisualElement menuRoot)
        {
            _root = menuRoot.Q("menu");
            _name = menuRoot.Q<TextField>("name-field");
            _seed = menuRoot.Q<TextField>("seed-field");
            _hint = menuRoot.Q<Label>("name-hint");
            _shakeTarget = _name;
            _name.maxLength = NameRules.MaxLength;
            _name.value = PlayerPrefs.GetString(NameKey, "");
            _name.RegisterValueChangedCallback(_ => ClearHint());

            // Matej's menu art already has the logo painted in, so the title only shows when the art is missing.
            bool art = UiArt.Set(menuRoot.Q("menu-bg"), UiArt.Get(GameArt.MenuBackground), new Color(0.08f, 0.07f, 0.12f));
            menuRoot.Q("menu-title").style.display = art ? DisplayStyle.None : DisplayStyle.Flex;
            menuRoot.Q<Label>("menu-version").text = "v" + Application.version;

            var corner = menuRoot.Q("menu-corner");
            CornerButtons.Add(corner, "lore", "Lore", "The story and how to play", () => LoreClicked?.Invoke());
            CornerButtons.Add(corner, "settings", "Settings", "Volume and credits", () => SettingsClicked?.Invoke());
            CornerButtons.AddFullscreen(corner);

            menuRoot.Q<Button>("play-bots").clicked += Play;
            var host = menuRoot.Q<Button>("host");
            var join = menuRoot.Q<Button>("join");
            host.clicked += () => { if (CommitName()) HostClicked?.Invoke(); };
            join.clicked += () => { if (CommitName()) JoinClicked?.Invoke(); };
            host.tooltip = "Create a lobby and invite friends";
            join.tooltip = "Join a friend's lobby with their code";
            _note = menuRoot.Q<Label>("online-note");
            menuRoot.Q("dev-row").style.display = DevFlags.Available ? DisplayStyle.Flex : DisplayStyle.None;
            ClearHint();
            foreach (var b in _root.Query<Button>().ToList())
                b.focusable = false;
        }

        public void SetVisible(bool visible)
        {
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible)
                ClearHint();
        }

        public void SetName(string raw) => _name.value = raw;

        // Public so MCP screenshots can trigger the error state.
        public void Play()
        {
            if (CommitName())
                PlayClicked?.Invoke();
        }

        // Validates and saves the name (shared by Play, Host and Join); shows the error state if it is not valid.
        bool CommitName()
        {
            string cleaned = PlayerName;
            if (!NameRules.IsValid(cleaned, out string hint))
            {
                ShowError(hint);
                return false;
            }
            _name.SetValueWithoutNotify(cleaned);
            PlayerPrefs.SetString(NameKey, cleaned);
            PlayerPrefs.Save();
            return true;
        }

        // A line under the buttons: errors from online play ("The host left. The match has ended.") or a progress note.
        public void SetNote(string text)
        {
            if (_note != null)
                _note.text = text ?? "";
        }

        public void SetOnlineBusy(bool busy)
        {
            _root.Q<Button>("host").SetEnabled(!busy);
            _root.Q<Button>("join").SetEnabled(!busy);
            _root.Q<Button>("play-bots").SetEnabled(!busy);
        }

        public bool HasValidName => NameRules.IsValid(PlayerName, out _);

        void ShowError(string hint)
        {
            _hint.text = hint;
            _name.AddToClassList("field--error");
            UiArt.Sfx("SFX/ui/ui_error");
            _shake.Stop();
            _shakeTarget.style.translate = new Translate(0, 0);
            _shake = Tween.Custom(0f, 1f, 0.35f, t =>
            {
                float x = Mathf.Sin(t * 3f * 2f * Mathf.PI) * (1f - t) * 12f;
                _shakeTarget.style.translate = new Translate(x, 0);
            }, Ease.Linear).OnComplete(() => _shakeTarget.style.translate = new Translate(0, 0));
        }

        void ClearHint()
        {
            _hint.text = "";
            _name.RemoveFromClassList("field--error");
        }
    }
}
