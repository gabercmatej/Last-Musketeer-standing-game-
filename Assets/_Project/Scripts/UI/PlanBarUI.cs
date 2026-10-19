using System;
using System.Collections.Generic;
using Musketeers.Data;
using Musketeers.Game;
using Musketeers.Sim;
using UnityEngine;
using UnityEngine.UIElements;

namespace Musketeers.UI
{
    // Bottom bar: the 3 plan slots, Lock In, a hint line and the action buttons (greyed out with a
    // tooltip reason when the projection says they'd fizzle).
    public sealed class PlanBarUI
    {
        public event Action<ActionType> ActionClicked;
        public event Action<int> SlotClicked;
        public event Action LockClicked;

        readonly VisualElement _bar;
        readonly Label _planningFor;
        readonly Label _hint;
        readonly Button _lock;
        readonly Label _tooltip;
        readonly VisualElement _root;
        readonly List<VisualElement> _actions = new List<VisualElement>();
        readonly List<Label> _actionNames = new List<Label>();
        readonly List<Label> _actionCosts = new List<Label>();
        readonly VisualElement[] _slots = new VisualElement[TurnPlan.Length];
        readonly VisualElement[] _slotIcons = new VisualElement[TurnPlan.Length];
        readonly Label[] _slotTexts = new Label[TurnPlan.Length];
        readonly Label[] _slotSubs = new Label[TurnPlan.Length];
        string[] _tooltips = new string[ActionInfo.Bar.Length];

        public PlanBarUI(VisualElement hudRoot)
        {
            _root = hudRoot;
            _bar = hudRoot.Q("plan-bar");
            _planningFor = hudRoot.Q<Label>("planning-for");
            _hint = hudRoot.Q<Label>("hint");
            _lock = hudRoot.Q<Button>("lock");
            _tooltip = hudRoot.Q<Label>("tooltip");
            _lock.focusable = false;
            _lock.clicked += () => LockClicked?.Invoke();

            var slots = hudRoot.Q("slots");
            for (int t = 0; t < TurnPlan.Length; t++)
            {
                int tick = t;
                var slot = new VisualElement();
                slot.AddToClassList("slot");
                var num = new Label((t + 1).ToString());
                num.AddToClassList("slot-num");
                var icon = new VisualElement();
                icon.AddToClassList("slot-icon");
                var col = new VisualElement();
                var text = new Label("");
                text.AddToClassList("slot-text");
                var sub = new Label("");
                sub.AddToClassList("slot-sub");
                col.Add(text);
                col.Add(sub);
                slot.Add(num);
                slot.Add(icon);
                slot.Add(col);
                slot.RegisterCallback<ClickEvent>(_ => SlotClicked?.Invoke(tick));
                slots.Add(slot);
                _slots[t] = slot;
                _slotIcons[t] = icon;
                _slotTexts[t] = text;
                _slotSubs[t] = sub;
            }

            var actions = hudRoot.Q("actions");
            for (int i = 0; i < ActionInfo.Bar.Length; i++)
            {
                int index = i;
                var type = ActionInfo.Bar[i];
                var el = new VisualElement();
                el.AddToClassList("action");
                var top = new VisualElement();
                top.AddToClassList("action-top");
                var key = new Label(ActionInfo.Hotkey(type));
                key.AddToClassList("action-key");
                var icon = new VisualElement();
                icon.AddToClassList("action-icon");
                icon.style.backgroundImage = new StyleBackground(ActionInfo.Icon(type));
                icon.style.unityBackgroundImageTintColor = ActionInfo.IconTint(type);
                top.Add(key);
                top.Add(icon);
                var name = new Label(type.ToString());
                name.AddToClassList("action-name");
                var cost = new Label("");
                cost.AddToClassList("action-cost");
                el.Add(top);
                el.Add(name);
                el.Add(cost);
                el.RegisterCallback<ClickEvent>(_ => ActionClicked?.Invoke(type));
                el.RegisterCallback<PointerEnterEvent>(_ => ShowTooltip(el, _tooltips[index]));
                el.RegisterCallback<PointerLeaveEvent>(_ => HideTooltip());
                actions.Add(el);
                _actions.Add(el);
                _actionNames.Add(name);
                _actionCosts.Add(cost);
            }
        }

        public void SetVisible(bool visible) => _bar.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        public void SetCharacter(CharacterDef c, GameConfig cfg, Ability ability, string who, Color color)
        {
            for (int i = 0; i < ActionInfo.Bar.Length; i++)
            {
                var type = ActionInfo.Bar[i];
                _actionNames[i].text = ActionInfo.Name(type, c);
                _actionCosts[i].text = ActionInfo.Cost(type, cfg, ability);
            }
            _planningFor.text = who;
            _planningFor.style.color = color;
        }

        public void Refresh(PlanBuilder b, GameConfig cfg, CharacterDef c)
        {
            bool locked = b.Locked;
            for (int i = 0; i < ActionInfo.Bar.Length; i++)
            {
                var type = ActionInfo.Bar[i];
                var issue = locked ? PlanIssue.None : b.IssueFor(type);
                bool off = locked || issue != PlanIssue.None;
                _actions[i].EnableInClassList("action--off", off);
                _actions[i].EnableInClassList("action--pending", b.HasPending && b.Pending == type);
                string desc = ActionInfo.Describe(type, cfg, c);
                _tooltips[i] = issue != PlanIssue.None ? $"{ActionInfo.Issue(issue)}.\n{desc}" : desc;
            }

            for (int t = 0; t < TurnPlan.Length; t++)
            {
                var a = b.Plan[t];
                var issue = b.Projection.Issues[t];
                _slots[t].EnableInClassList("slot--cursor", !locked && t == b.Cursor);
                _slots[t].EnableInClassList("slot--invalid", issue != PlanIssue.None);
                _slots[t].EnableInClassList("slot--locked", locked);
                _slotTexts[t].text = a.Type == ActionType.Idle ? (t == b.Cursor && !locked ? "next..." : "idle") : ActionInfo.Short(a, c);
                _slotSubs[t].text = issue != PlanIssue.None ? ShortIssue(issue) : "";
                var icon = ActionInfo.Icon(a.Type);
                _slotIcons[t].style.backgroundImage = icon != null ? new StyleBackground(icon) : new StyleBackground(StyleKeyword.None);
                _slotIcons[t].style.unityBackgroundImageTintColor = ActionInfo.IconTint(a.Type);
                _slotIcons[t].style.rotate = new Rotate(ActionInfo.IconAngle(a));
            }

            _lock.text = locked ? "Locked  (Space to edit)" : "Lock in  (Space)";
            _lock.EnableInClassList("btn--locked", locked);
            _hint.text = Hint(b);
        }

        static string ShortIssue(PlanIssue i)
        {
            switch (i)
            {
                case PlanIssue.NoGrit: return "fizzles: no grit";
                case PlanIssue.Unloaded: return "fizzles: unloaded";
                case PlanIssue.KegActive: return "fizzles: keg out";
                case PlanIssue.OutOfRange: return "fizzles: range";
                case PlanIssue.Dead: return "you're gone";
                default: return "";
            }
        }

        string Hint(PlanBuilder b)
        {
            if (b.Locked)
                return "Locked in. Waiting for the others...";
            var st = b.CursorState;
            string status = $"Tick {b.EditTick + 1}: {st.Grit} grit, {(st.Loaded ? "loaded" : "unloaded")}";
            if (b.HasPending && PlanBuilder.NeedsDirection(b.Pending))
                return $"{status}.  Pick a direction: WASD / arrows, or click a tile in line.  Esc cancels.";
            if (b.HasPending && PlanBuilder.NeedsTarget(b.Pending))
                return $"{status}.  Click a tile within {b.Config.KegThrowRange} to throw the keg.  Esc cancels.";
            if (b.IsFull)
                return "Plan full.  Space locks in, Backspace undoes, click a slot to change it.";
            return $"{status}.  Pick an action (1-8, Q), or press a direction to move.";
        }

        public void SetMessage(string message) => _hint.text = message;

        void ShowTooltip(VisualElement anchor, string text)
        {
            if (string.IsNullOrEmpty(text))
                return;
            _tooltip.text = text;
            _tooltip.style.display = DisplayStyle.Flex;
            var r = anchor.worldBound;
            var local = _root.WorldToLocal(new Vector2(r.xMin, r.yMin));
            _tooltip.style.left = Mathf.Max(4f, local.x - 60f);
            _tooltip.style.top = StyleKeyword.Auto;
            _tooltip.style.bottom = _root.layout.height - local.y + 6f;
        }

        public void HideTooltip() => _tooltip.style.display = DisplayStyle.None;
    }
}
