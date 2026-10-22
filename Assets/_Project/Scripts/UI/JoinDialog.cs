using System;
using Musketeers.Net;
using UnityEngine.UIElements;

namespace Musketeers.UI
{
    // "Join a lobby": a code field (prefilled from ?join=CODE), Join and Cancel, and one status line for
    // "Joining..." and errors ("Lobby is full", "New version is out. Refresh ..."). Same modal look as ConfirmDialog.
    public sealed class JoinDialog
    {
        public event Action<string> JoinRequested;   // a cleaned, non-empty code
        public event Action Cancelled;

        readonly VisualElement _root;
        readonly TextField _code;
        readonly Label _status;
        readonly Button _join, _cancel;
        bool _busy;

        public bool Open { get; private set; }
        public string Code => NetSession.CleanCode(_code.value);

        public JoinDialog(VisualElement parent)
        {
            _root = new VisualElement { name = "join-dialog" };
            _root.AddToClassList("screen");
            _root.AddToClassList("screen--modal");
            _root.style.display = DisplayStyle.None;
            var panel = new VisualElement();
            panel.AddToClassList("panel");
            panel.AddToClassList("join-panel");
            var title = new Label("Join a lobby");
            title.AddToClassList("confirm-message");
            var caption = new Label("Lobby code");
            caption.AddToClassList("section-label");
            _code = new TextField { maxLength = 8 };
            _code.AddToClassList("field");
            _code.AddToClassList("join-code-field");
            _code.RegisterValueChangedCallback(e =>
            {
                string clean = NetSession.CleanCode(e.newValue);
                if (clean != e.newValue)
                    _code.SetValueWithoutNotify(clean);
                if (!_busy)
                    SetStatus("", false);
            });
            _code.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == UnityEngine.KeyCode.Return || e.keyCode == UnityEngine.KeyCode.KeypadEnter)
                    Submit();
            });
            _status = new Label();
            _status.AddToClassList("join-status");
            var row = new VisualElement();
            row.AddToClassList("confirm-row");
            _cancel = new Button(() => { if (!_busy) { Hide(); Cancelled?.Invoke(); } }) { text = "Cancel", focusable = false };
            _cancel.AddToClassList("btn");
            _cancel.AddToClassList("btn--big");
            _cancel.AddToClassList("btn--back");
            _cancel.AddToClassList("confirm-btn");
            _join = new Button(Submit) { text = "Join", focusable = false };
            _join.AddToClassList("btn");
            _join.AddToClassList("btn--big");
            _join.AddToClassList("btn--primary");
            _join.AddToClassList("confirm-btn");
            row.Add(_cancel);
            row.Add(_join);
            panel.Add(title);
            panel.Add(caption);
            panel.Add(_code);
            panel.Add(_status);
            panel.Add(row);
            _root.Add(panel);
            parent.Add(_root);
        }

        public void Show(string prefill)
        {
            _code.SetValueWithoutNotify(NetSession.CleanCode(prefill));
            SetBusy(false);
            SetStatus("", false);
            Open = true;
            _root.style.display = DisplayStyle.Flex;
            _root.BringToFront();
            _code.schedule.Execute(() => _code.Focus());
        }

        public void Hide()
        {
            Open = false;
            _root.style.display = DisplayStyle.None;
        }

        // Esc: close unless a join is in flight.
        public void Cancel()
        {
            if (Open && !_busy)
            {
                Hide();
                Cancelled?.Invoke();
            }
        }

        public void SetStatus(string text, bool error)
        {
            _status.text = text ?? "";
            _status.EnableInClassList("join-status--error", error);
        }

        public void SetBusy(bool busy)
        {
            _busy = busy;
            _join.SetEnabled(!busy);
            _cancel.SetEnabled(!busy);
            _code.SetEnabled(!busy);
        }

        void Submit()
        {
            if (_busy)
                return;
            string code = Code;
            if (code.Length == 0)
            {
                SetStatus("Type the lobby code first.", true);
                UiArt.Sfx("SFX/ui/ui_error");
                return;
            }
            JoinRequested?.Invoke(code);
        }
    }
}
