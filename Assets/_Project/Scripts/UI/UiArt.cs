using Musketeers.Data;
using Musketeers.Presentation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Musketeers.UI
{
    // Puts game art into UI Toolkit elements, with a fallback so a missing file shows a colour, not a hole.
    public static class UiArt
    {
        public static ArtLibrary Art;

        public static Sprite Get(string key) => Art != null && Art.Game != null ? Art.Game.Get(key) : null;

        // Sets the element's background image, or clears it and shows `fallback` as a flat colour.
        public static bool Set(VisualElement el, Sprite sprite, Color? fallback = null)
        {
            if (sprite != null)
            {
                el.style.backgroundImage = new StyleBackground(sprite);
                el.style.unityBackgroundImageTintColor = Color.white;
                return true;
            }
            el.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            if (fallback.HasValue)
                el.style.backgroundColor = fallback.Value;
            return false;
        }

        public static bool Set(VisualElement el, string key, Color? fallback = null) => Set(el, Get(key), fallback);

        public static Sprite Portrait(int character) => Art?.Character(character)?.Portrait;

        public static VisualElement Image(string cssClass, Sprite sprite, Color? fallback = null)
        {
            var v = new VisualElement { pickingMode = PickingMode.Ignore };
            v.AddToClassList(cssClass);
            Set(v, sprite, fallback);
            return v;
        }

        public static void Sfx(string key, float volume = 1f) => AudioService.Instance?.Sfx(key, volume);

        // Every button click plays ui_click (and hovering a button a soft tick), registered once on a root.
        public static void WireButtonSounds(VisualElement root)
        {
            root.RegisterCallback<ClickEvent>(e =>
            {
                if (e.target is Button b && b.enabledInHierarchy)
                    Sfx(b.ClassListContains("btn--back") ? "SFX/ui/ui_back" : "SFX/ui/ui_click");
            }, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerOverEvent>(e =>
            {
                if (e.target is Button b && b.enabledInHierarchy)
                    Sfx("SFX/ui/ui_hover", 0.35f);
            }, TrickleDown.TrickleDown);
        }
    }
}
