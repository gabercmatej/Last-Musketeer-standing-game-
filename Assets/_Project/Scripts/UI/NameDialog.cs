using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Musketeers.UI
{
    public sealed class NameDialog
    {
        public event Action<string> Saved;
        readonly VisualElement _root;
        readonly TextField _name;
        readonly Label _hint;
        public bool Open { get; private set; }

        public NameDialog(VisualElement parent)
        {
            _root = new VisualElement { name = "name-dialog" };
            _root.AddToClassList("screen");
            _root.AddToClassList("screen--modal");
            _root.style.display = DisplayStyle.None;
            var panel = new VisualElement();
            panel.AddToClassList("panel");
            panel.AddToClassList("join-panel");
            var title = new Label("Change Name");
            title.AddToClassList("confirm-message");
            _name = new TextField { maxLength = NameRules.MaxLength };
            _name.AddToClassList("field");
            _hint = new Label();
            _hint.AddToClassList("join-status");
            _hint.AddToClassList("join-status--error");
            _name.RegisterValueChangedCallback(_ => _hint.text = "");
            _name.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Escape)
                    Hide();
                else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                    Submit();
                else
                    return;
                e.StopPropagation();
            }, TrickleDown.TrickleDown);
            var row = new VisualElement();
            row.AddToClassList("confirm-row");
            var cancel = new Button(Hide) { text = "Cancel", focusable = false };
            cancel.AddToClassList("btn");
            cancel.AddToClassList("btn--back");
            var save = new Button(Submit) { text = "Save", focusable = false };
            save.AddToClassList("btn");
            save.AddToClassList("btn--primary");
            row.Add(cancel);
            row.Add(save);
            panel.Add(title);
            panel.Add(_name);
            panel.Add(_hint);
            panel.Add(row);
            _root.Add(panel);
            parent.Add(_root);
        }

        public void Show(string name)
        {
            _name.SetValueWithoutNotify(name ?? "");
            _hint.text = "";
            Open = true;
            _root.style.display = DisplayStyle.Flex;
            _root.BringToFront();
            _name.schedule.Execute(() => _name.Focus());
        }

        public void Hide()
        {
            _name.Blur();
            Open = false;
            _root.style.display = DisplayStyle.None;
        }

        void Submit()
        {
            string clean = NameRules.Clean(_name.value);
            if (!NameRules.IsValid(clean, out string hint))
            {
                _hint.text = hint;
                UiArt.Sfx("SFX/ui/ui_error");
                return;
            }
            Saved?.Invoke(clean);
            Hide();
        }
    }
}
