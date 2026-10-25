using UnityEngine;
using UnityEngine.UIElements;

namespace Musketeers.UI
{
    // Only wheel input is eased; scrollbar dragging, keyboard and programmatic scrolling stay native.
    public static class GentleScroll
    {
        public static void Apply(ScrollView view)
        {
            if (view.ClassListContains("gentle-scroll")) return;
            view.AddToClassList("gentle-scroll");
            const float step = 6f; // UI Toolkit default is 18 pixels per delta unit.
            view.mouseWheelScrollSize = step;
            Vector2 target = Vector2.zero;
            bool moving = false;
            var animation = view.schedule.Execute(() =>
            {
                if (!moving) return;
                var current = view.scrollOffset;
                var next = Vector2.Lerp(current, target, 0.5f);
                if ((next - target).sqrMagnitude < 0.25f) { next = target; moving = false; }
                view.scrollOffset = next;
            }).Every(16);
            view.RegisterCallback<WheelEvent>(e =>
            {
                if (!moving) target = view.scrollOffset;
                float y = Mathf.Clamp(target.y + e.delta.y * step, 0f, Mathf.Max(0f, view.verticalScroller.highValue));
                float x = Mathf.Clamp(target.x + e.delta.x * step, 0f, Mathf.Max(0f, view.horizontalScroller.highValue));
                if (Mathf.Approximately(y, target.y) && Mathf.Approximately(x, target.x)) return;
                target = new Vector2(x, y); moving = true;
                e.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
            view.RegisterCallback<PointerDownEvent>(_ => moving = false, TrickleDown.TrickleDown);
            view.RegisterCallback<KeyDownEvent>(_ => moving = false, TrickleDown.TrickleDown);
            view.RegisterCallback<DetachFromPanelEvent>(_ => { moving = false; animation.Pause(); });
            view.RegisterCallback<AttachToPanelEvent>(_ => animation.Resume());
        }
    }
}
