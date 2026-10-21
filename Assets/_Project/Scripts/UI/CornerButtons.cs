using System;
using Musketeers.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace Musketeers.UI
{
    // Matej's square icon buttons (blue tile, gold pictogram) with a small caption under each. The menu shows
    // Lore / Settings / Fullscreen in its top-right corner; the lobby (and later the HUD) reuse the same builders.
    public static class CornerButtons
    {
        // artName: file name under UI/buttons (lore, settings, ...). A missing sprite shows a styled tile with the caption's initial.
        public static Button Add(VisualElement row, string artName, string caption, string tooltip, Action onClick)
        {
            var btn = Make(artName, caption, tooltip, out _, out _);
            if (onClick != null)
                btn.clicked += onClick;
            row.Add(btn);
            return btn;
        }

        // The top-left trio of the lobby, the match HUD and the results screen: Fullscreen, Settings, Leave, same size
        // (FIXES_4E §3.1, §4.1). onLeave null = no Leave button (the results screen has Quit instead).
        public static void AddTopLeftSet(VisualElement row, Action onSettings, Action onLeave, string leaveCaption = "Leave", string leaveTooltip = "Leave")
        {
            AddFullscreen(row);
            Add(row, "settings", "Settings", "Volume and credits", onSettings);
            if (onLeave != null)
                Add(row, "leave-game", leaveCaption, leaveTooltip, onLeave);
        }

        // Fullscreen toggle. It runs on PointerDown, not click: Unity WebGL defers the fullscreen request to the next
        // user-gesture event (the mouseup of this same click), so one click works in the browser. The icon follows
        // Screen.fullScreen, polled because Esc leaves fullscreen in the browser without telling us.
        public static Button AddFullscreen(VisualElement row)
        {
            var btn = Make("fullscreen", "Fullscreen", "Toggle fullscreen", out var img, out var caption);
            bool shown = false;
            bool first = true;
            void Refresh()
            {
                bool fs = WebFullscreen.IsFullscreen;
                if (!first && fs == shown)
                    return;
                first = false;
                shown = fs;
                Fill(img, fs ? "exit-fullscreen" : "fullscreen", fs ? "Windowed" : "Fullscreen");
                caption.text = fs ? "Windowed" : "Fullscreen";
                btn.tooltip = fs ? "Leave fullscreen" : "Enter fullscreen";
            }
            btn.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0)
                    return;
                WebFullscreen.Toggle();
                btn.schedule.Execute(Refresh).ExecuteLater(250);
            }, TrickleDown.TrickleDown);
            btn.schedule.Execute(Refresh).Every(500);
            Refresh();
            row.Add(btn);
            return btn;
        }

        static Button Make(string artName, string captionText, string tooltip, out VisualElement img, out Label caption)
        {
            var btn = new Button { focusable = false, tooltip = tooltip };
            btn.AddToClassList("icon-btn");
            img = new VisualElement { pickingMode = PickingMode.Ignore };
            img.AddToClassList("icon-btn__img");
            caption = new Label(captionText) { pickingMode = PickingMode.Ignore };
            caption.AddToClassList("icon-btn__caption");
            btn.Add(img);
            btn.Add(caption);
            Fill(img, artName, captionText);
            return btn;
        }

        static void Fill(VisualElement img, string artName, string captionText)
        {
            img.Clear();
            bool art = UiArt.Set(img, UiArt.Get(GameArt.UiButton(artName)));
            img.EnableInClassList("icon-btn__img--fallback", !art);
            if (!art)
            {
                var l = new Label(string.IsNullOrEmpty(captionText) ? "?" : captionText.Substring(0, 1)) { pickingMode = PickingMode.Ignore };
                l.AddToClassList("icon-btn__initial");
                img.Add(l);
            }
        }
    }
}
