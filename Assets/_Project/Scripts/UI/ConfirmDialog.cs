using System;
using UnityEngine.UIElements;

namespace Musketeers.UI
{
    // A reusable modal question: dim backdrop, panel, message, a primary and a secondary button.
    // Esc and a click outside the panel both answer "secondary" (the safe choice). Used by Leave lobby,
    // "Start anyway?" and (chunk 6) Leave match. AppFlow calls Cancel() on Esc.
    public sealed class ConfirmDialog
    {
        readonly VisualElement _root;
        readonly Label _message;
        readonly Button _primary, _secondary;
        Action _onPrimary, _onSecondary;

        public bool Open { get; private set; }

        public ConfirmDialog(VisualElement parent)
        {
            _root = new VisualElement { name = "confirm" };
            _root.AddToClassList("screen");
            _root.AddToClassList("screen--modal");
            _root.style.display = DisplayStyle.None;
            var panel = new VisualElement();
            panel.AddToClassList("panel");
            panel.AddToClassList("confirm-panel");
            _message = new Label();
            _message.AddToClassList("confirm-message");
            var row = new VisualElement();
            row.AddToClassList("confirm-row");
            _primary = new Button(() => Answer(true)) { focusable = false };
            _primary.AddToClassList("btn");
            _primary.AddToClassList("btn--big");
            _primary.AddToClassList("confirm-btn");
            _secondary = new Button(() => Answer(false)) { focusable = false };
            _secondary.AddToClassList("btn");
            _secondary.AddToClassList("btn--big");
            _secondary.AddToClassList("btn--back");
            _secondary.AddToClassList("confirm-btn");
            row.Add(_secondary);
            row.Add(_primary);
            panel.Add(_message);
            panel.Add(row);
            _root.Add(panel);
            _root.RegisterCallback<ClickEvent>(e => { if (e.target == _root) Answer(false); });
            parent.Add(_root);
        }

        public void Show(string message, string primaryText, string secondaryText, Action onPrimary, Action onSecondary = null)
        {
            _message.text = message;
            _primary.text = primaryText;
            _secondary.text = secondaryText;
            _onPrimary = onPrimary;
            _onSecondary = onSecondary;
            Open = true;
            _root.style.display = DisplayStyle.Flex;
            _root.BringToFront();
        }

        // Esc: same as the secondary button.
        public void Cancel()
        {
            if (Open)
                Answer(false);
        }

        public void Hide()
        {
            Open = false;
            _root.style.display = DisplayStyle.None;
        }

        void Answer(bool primary)
        {
            var cb = primary ? _onPrimary : _onSecondary;
            Hide();
            cb?.Invoke();
        }
    }
}
