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
            _music.RegisterValueChangedCallback(_ => Apply());
            _sfx.RegisterValueChangedCallback(e => { Apply(); UiArt.Sfx("SFX/ui/ui_click"); });
            _voice.RegisterValueChangedCallback(_ => Apply());
            _voice.RegisterCallback<PointerUpEvent>(_ => AudioService.Instance?.Announce("en_garde"), TrickleDown.TrickleDown);
            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            Open = visible;
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            var a = AudioService.Instance;
            if (visible && a != null)
            {
                _music.SetValueWithoutNotify(a.MusicVolume);
                _sfx.SetValueWithoutNotify(a.SfxVolume);
                _voice.SetValueWithoutNotify(a.VoiceVolume);
            }
        }

        void Apply() => AudioService.Instance?.SetVolumes(_music.value, _sfx.value, _voice.value);
    }
}
