using System;
using Musketeers.Data;
using Musketeers.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace Musketeers.UI
{
    // The small in-match chat in the left HUD column: a short log whose lines fade ~6 s after arriving (they stay
    // readable while the field has focus), a text field (max 80) and a row of 4 emote buttons. While the field has
    // focus the gameplay hotkeys are off (IsTyping, read by PlanningInput and AppFlow). Offline it is local only.
    public sealed class ChatUI
    {
        public const int VisibleLines = 4;
        const float FadeStart = 5f, FadeEnd = 6f;

        public event Action<string> Sent;
        public event Action<int> EmoteClicked;

        readonly VisualElement _root, _log;
        readonly TextField _field;
        ChatModel _model;
        string[] _names = new string[0];
        Color[] _colors = new Color[0];
        int _focusFrame = -1, _blurFrame = -1;

        public bool Focused { get; private set; }
        // True while typing, and for the rest of the frame in which the field lost focus (Enter / Esc must not leak to hotkeys).
        public bool IsTyping => Focused || Time.frameCount == _blurFrame;

        public ChatUI(VisualElement root)
        {
            _root = root;
            _log = new VisualElement { pickingMode = PickingMode.Ignore };
            _log.AddToClassList("chat-log");
            _root.Add(_log);

            _field = new TextField { maxLength = ChatModel.MaxLength, focusable = true, name = "chat-field" };
            _field.AddToClassList("chat-field");
            _field.textEdition.placeholder = "Say something…";
            _field.textEdition.hidePlaceholderOnFocus = true;
            _field.RegisterCallback<FocusInEvent>(_ => { Focused = true; Refresh(); });
            _field.RegisterCallback<FocusOutEvent>(_ => { Focused = false; _blurFrame = Time.frameCount; Refresh(); });
            _field.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            _root.Add(_field);

            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("chat-emotes");
            for (int i = 0; i < GameArt.Emotes.Length; i++)
            {
                int index = i;
                var b = new Button { focusable = false, tooltip = EmoteNames[i] };
                b.AddToClassList("chat-emote");
                var img = new VisualElement { pickingMode = PickingMode.Ignore };
                img.AddToClassList("chat-emote__img");
                if (!UiArt.Set(img, UiArt.Get(GameArt.Emotes[i])))
                {
                    var l = new Label(EmoteNames[i].Substring(0, 1)) { pickingMode = PickingMode.Ignore };
                    l.AddToClassList("chat-emote__initial");
                    img.Add(l);
                }
                b.Add(img);
                b.clicked += () => EmoteClicked?.Invoke(index);
                row.Add(b);
            }
            _root.Add(row);
            _root.schedule.Execute(Refresh).Every(100);
        }

        public static readonly string[] EmoteNames = { "Laugh", "Angry", "Salute", "Cry" };
        // The small line an emote button posts in the log.
        public static readonly string[] EmoteLines = { "*laughs*", "*fumes*", "*salutes*", "*cries*" };

        public void Bind(ChatModel model, string[] names, Color[] colors)
        {
            if (_model != null)
                _model.Added -= OnAdded;
            _model = model;
            _names = names ?? new string[0];
            _colors = colors ?? new Color[0];
            _model.Added += OnAdded;
            _field.value = "";
            Blur();
            Rebuild();
        }

        public void Focus()
        {
            _focusFrame = Time.frameCount;
            _field.Focus();
        }

        public void Blur()
        {
            if (Focused)
                _field.Blur();
            Focused = false;
        }

        void OnKeyDown(KeyDownEvent e)
        {
            if (e.keyCode == KeyCode.Escape)
            {
                Blur();
                e.StopPropagation();
            }
            else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                // The Enter that opened the field in the first place must not also send.
                if (Time.frameCount == _focusFrame)
                    return;
                string text = _field.value;
                _field.value = "";
                if (!string.IsNullOrWhiteSpace(text))
                    Sent?.Invoke(text);
                Blur();
                e.StopPropagation();
            }
        }

        void OnAdded(ChatModel.Message m) => Rebuild();

        void Rebuild()
        {
            _log.Clear();
            if (_model == null)
                return;
            var list = _model.Messages;
            int from = Mathf.Max(0, list.Count - VisibleLines);
            for (int i = from; i < list.Count; i++)
                _log.Add(Line(list[i]));
            Refresh();
        }

        VisualElement Line(ChatModel.Message m)
        {
            var line = new VisualElement { pickingMode = PickingMode.Ignore, userData = m.Time };
            line.AddToClassList("chat-line");
            string who = m.Slot >= 0 && m.Slot < _names.Length ? _names[m.Slot] : "?";
            var color = m.Slot >= 0 && m.Slot < _colors.Length ? Color.Lerp(_colors[m.Slot], Color.white, 0.5f) : Color.white;
            var name = new Label(who + ":") { pickingMode = PickingMode.Ignore };
            name.AddToClassList("chat-name");
            name.style.color = color;
            var text = new Label(m.Text) { pickingMode = PickingMode.Ignore };
            text.AddToClassList("chat-text");
            if (m.Emote)
                text.AddToClassList("chat-text--emote");
            line.Add(name);
            line.Add(text);
            return line;
        }

        // Fades each line: full for 5 s, gone at 6 s; everything is readable while the field has focus.
        void Refresh()
        {
            float now = Time.unscaledTime;
            foreach (var child in _log.Children())
            {
                float age = now - (child.userData is float t ? t : now);
                float a = Focused ? 1f : 1f - Mathf.Clamp01((age - FadeStart) / (FadeEnd - FadeStart));
                child.style.opacity = a;
                child.style.display = a <= 0.01f ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }
    }
}
