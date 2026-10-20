using Musketeers.Presentation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Musketeers.UI
{
    // Volume sliders (Music / SFX / Voice, saved in PlayerPrefs by AudioService) and the credits.
    public sealed class SettingsUI
    {
        readonly VisualElement _root;
        readonly Slider _music, _sfx, _voice;

        public bool Open { get; private set; }

        public SettingsUI(VisualElement root, TextAsset credits)
        {
            _root = root.Q("settings");
            _music = root.Q<Slider>("music-volume");
            _sfx = root.Q<Slider>("sfx-volume");
            _voice = root.Q<Slider>("voice-volume");
            root.Q<Label>("credits").text = credits != null ? credits.text : "";
            var close = root.Q<Button>("settings-close");
            close.AddToClassList("btn--back");
            close.focusable = false;
            close.clicked += () => SetVisible(false);
            // No sound while dragging; one preview when the pointer is released (inside or outside the slider).
            _music.RegisterValueChangedCallback(_ => Apply());
            _sfx.RegisterValueChangedCallback(_ => Apply());
            _voice.RegisterValueChangedCallback(_ => Apply());
            RegisterPreview(_sfx, () => UiArt.Sfx("SFX/ui/ui_click"));
            RegisterPreview(_voice, () => AudioService.Instance?.Announce("en_garde"));
            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            var a = AudioService.Instance;
            if (Open && !visible)
                a?.SaveVolumes();
            Open = visible;
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible && a != null)
            {
                _music.SetValueWithoutNotify(a.MusicVolume);
                _sfx.SetValueWithoutNotify(a.SfxVolume);
                _voice.SetValueWithoutNotify(a.VoiceVolume);
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
