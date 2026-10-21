using System.Collections.Generic;
using PrimeTween;
using UnityEngine;
using UnityEngine.UIElements;

namespace Musketeers.Presentation
{
    // Text over the board (names, fuse numbers, "-1" pops, ghost numbers), drawn by UI Toolkit on a layer
    // under the HUD and moved to its world position every frame. Crisp at any resolution, and it needs
    // no font assets or world-space text shaders in the Web build.
    public sealed class WorldLabels : MonoBehaviour
    {
        VisualElement _layer;
        Camera _camera;
        readonly List<WorldLabel> _labels = new List<WorldLabel>();

        public void Init(VisualElement layer, Camera cam)
        {
            _layer = layer;
            _camera = cam;
        }

        public WorldLabel Create(string text, string cssClass)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("world-label");
            if (!string.IsNullOrEmpty(cssClass))
                label.AddToClassList(cssClass);
            label.style.position = Position.Absolute;
            label.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
            _layer?.Add(label);
            var wl = new WorldLabel(this, label);
            _labels.Add(wl);
            return wl;
        }

        // Rises and fades: damage numbers, "click", "+1 grit".
        public void Popup(string text, Vector3 world, Color color, float duration, string cssClass = "popup")
        {
            var wl = Create(text, cssClass);
            wl.World = world;
            wl.Element.style.color = color;
            Tween.Custom(0f, 1f, Mathf.Max(0.05f, duration), t =>
            {
                wl.Offset = new Vector3(0f, 0.35f + 0.5f * t, 0f);
                wl.Element.style.opacity = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            }, Ease.OutQuad).OnComplete(wl.Destroy);
        }

        internal void Remove(WorldLabel wl)
        {
            _labels.Remove(wl);
            wl.Element.RemoveFromHierarchy();
        }

        public void Clear()
        {
            for (int i = _labels.Count - 1; i >= 0; i--)
                _labels[i].Destroy();
        }

        void LateUpdate()
        {
            if (_layer?.panel == null || _camera == null)
                return;
            for (int i = 0; i < _labels.Count; i++)
            {
                var wl = _labels[i];
                if (!wl.Visible)
                    continue;
                Vector3 world = (wl.Follow != null ? wl.Follow.position : wl.World) + wl.Offset;
                Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(_layer.panel, world, _camera);
                // 4F §5: writing left/top dirties the panel's layout, so skip labels that didn't move.
                if ((p - wl.Shown).sqrMagnitude < 0.01f)
                    continue;
                wl.Shown = p;
                wl.Element.style.left = p.x;
                wl.Element.style.top = p.y;
            }
        }
    }

    public sealed class WorldLabel
    {
        readonly WorldLabels _owner;
        public readonly Label Element;
        public Transform Follow;
        public Vector3 World;
        public Vector3 Offset;
        public bool Destroyed { get; private set; }

        internal WorldLabel(WorldLabels owner, Label element)
        {
            _owner = owner;
            Element = element;
        }

        public string Text
        {
            get => Element.text;
            set => Element.text = value;
        }

        public bool Visible { get; private set; } = true;
        internal Vector2 Shown = new Vector2(float.NaN, float.NaN);

        public void SetVisible(bool visible)
        {
            if (visible && !Visible)
                Shown = new Vector2(float.NaN, float.NaN);   // re-place it on the next LateUpdate
            Visible = visible;
            Element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void Destroy()
        {
            if (Destroyed)
                return;
            Destroyed = true;
            _owner.Remove(this);
        }
    }
}
