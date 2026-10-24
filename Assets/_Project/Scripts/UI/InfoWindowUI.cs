using System.Collections.Generic;
using Musketeers.Presentation;
using Musketeers.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace Musketeers.UI
{
    // One styled window with three tabs: Lore, How to play, Settings (volume sliders and credits).
    // The menu's Lore button opens it on Lore, every Settings button on Settings. Click outside, Close or Esc closes it.
    public sealed class InfoWindowUI
    {
        public enum Tab { Lore, HowToPlay, Settings }

        readonly VisualElement _root;
        readonly Slider _music, _sfx, _voice;
        readonly Button _mute;
        readonly VisualElement[] _pages = new VisualElement[3];
        readonly Button[] _tabs = new Button[3];

        public bool Open { get; private set; }

        public InfoWindowUI(VisualElement root, TextAsset credits, TextAsset lore, TextAsset howToPlay)
        {
            _root = root.Q("settings");
            _music = root.Q<Slider>("music-volume");
            _sfx = root.Q<Slider>("sfx-volume");
            _voice = root.Q<Slider>("voice-volume");
            _mute = root.Q<Button>("audio-mute");
            _mute.clicked += () =>
            {
                var audio = AudioService.Instance;
                if (audio == null) return;
                audio.SetMuted(!audio.Muted);
                _mute.text = audio.Muted ? "Unmute" : "Mute";
            };
            root.Q<Label>("credits").text = credits != null ? credits.text : "";
            root.Q<Label>("lore-text").text = lore != null ? lore.text.Trim() : "";
            root.Q<Label>("howto-text").text = howToPlay != null ? howToPlay.text.Trim() : "";
            BuildActionTable(root.Q("howto-table"));

            _pages[0] = root.Q("page-lore");
            _pages[1] = root.Q("page-howto");
            _pages[2] = root.Q("page-settings");
            _tabs[0] = root.Q<Button>("tab-lore");
            _tabs[1] = root.Q<Button>("tab-howto");
            _tabs[2] = root.Q<Button>("tab-settings");
            for (int i = 0; i < 3; i++)
            {
                int t = i;
                _tabs[i].focusable = false;
                _tabs[i].clicked += () => Select((Tab)t);
            }

            var close = root.Q<Button>("settings-close");
            close.AddToClassList("btn--back");
            close.focusable = false;
            close.clicked += () => Show(false);
            // A click on the dim backdrop (not on the window) closes it.
            _root.RegisterCallback<ClickEvent>(e => { if (e.target == _root) Show(false); });

            // No sound while dragging; one preview when the pointer is released (inside or outside the slider).
            _music.RegisterValueChangedCallback(_ => Apply());
            _sfx.RegisterValueChangedCallback(_ => Apply());
            _voice.RegisterValueChangedCallback(_ => Apply());
            RegisterPreview(_sfx, () => UiArt.Sfx("SFX/ui/ui_click"));
            RegisterPreview(_voice, () => AudioService.Instance?.Announce("en_garde"));
            Show(false);
        }

        public void Show(bool visible)
        {
            var a = AudioService.Instance;
            if (Open && !visible)
                a?.SaveVolumes();
            Open = visible;
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible && a != null)
            {
                _mute.text = a.Muted ? "Unmute" : "Mute";
                _music.SetValueWithoutNotify(a.MusicVolume);
                _sfx.SetValueWithoutNotify(a.SfxVolume);
                _voice.SetValueWithoutNotify(a.VoiceVolume);
            }
        }

        public void Close() => Show(false);

        public void OpenTab(Tab tab)
        {
            Select(tab);
            Show(true);
        }

        void Select(Tab tab)
        {
            for (int i = 0; i < 3; i++)
            {
                bool on = i == (int)tab;
                _pages[i].style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                _tabs[i].EnableInClassList("info-tab--on", on);
            }
        }

        // The compact action table: icon, name, cost and what it does (GAME_DESIGN §5).
        static void BuildActionTable(VisualElement table)
        {
            var cfg = GameConfig.Default;
            var rows = new (ActionType type, string name, string cost, string text)[]
            {
                (ActionType.Move, "Move", "free", "Step 1 tile."),
                (ActionType.Dash, "Dash", $"{cfg.DashCost} Grit", "Leap 2 tiles in a line."),
                (ActionType.Fire, "Fire", "needs loaded", "Instant shot down a line."),
                (ActionType.Reload, "Reload", "free", "Load your musket."),
                (ActionType.Parry, "Parry", $"{cfg.ParryCost} Grit", "Block shots and blasts."),
                (ActionType.Shove, "Shove", "free", "Push what is next to you."),
                (ActionType.Keg, "Powder Keg", $"{cfg.KegCost} Grit", "Lob a keg that explodes soon."),
                (ActionType.CatchBreath, "Catch Breath", "+1 Grit", "Rest and earn Grit."),
                (ActionType.Signature, "Signature", $"{cfg.SignatureCost} Grit", "Your musketeer's special move."),
            };
            foreach (var r in rows)
            {
                var row = new VisualElement { pickingMode = PickingMode.Ignore };
                row.AddToClassList("howto-row");
                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("howto-icon");
                icon.style.backgroundImage = new StyleBackground(ActionInfo.Icon(r.type));
                icon.style.unityBackgroundImageTintColor = ActionInfo.IconTint(r.type);
                var name = new Label(r.name) { pickingMode = PickingMode.Ignore };
                name.AddToClassList("howto-name");
                var cost = new Label(r.cost) { pickingMode = PickingMode.Ignore };
                cost.AddToClassList("howto-cost");
                var text = new Label(r.text) { pickingMode = PickingMode.Ignore };
                text.AddToClassList("howto-desc");
                row.Add(icon);
                row.Add(name);
                row.Add(cost);
                row.Add(text);
                table.Add(row);
            }
        }

        // PointerUp fires for a click or a drag released over the slider; PointerCaptureOut covers a release
        // outside it. Both can fire for one gesture, so a short guard keeps it to exactly one preview.
        void RegisterPreview(Slider slider, System.Action preview)
        {
            float last = -1f;
            void Fire()
            {
                float now = Time.unscaledTime;
                if (last >= 0f && now - last < 0.3f)
                    return;
                last = now;
                // Wait out the 0.15 s volume ramp so the preview plays at the new level.
                slider.schedule.Execute(preview).ExecuteLater(170);
            }
            slider.RegisterCallback<PointerUpEvent>(_ => Fire(), TrickleDown.TrickleDown);
            slider.RegisterCallback<PointerCaptureOutEvent>(_ => Fire(), TrickleDown.TrickleDown);
        }

        void Apply() => AudioService.Instance?.SetVolumes(_music.value, _sfx.value, _voice.value, save: false);
    }
}
