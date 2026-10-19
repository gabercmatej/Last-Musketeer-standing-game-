using System;
using System.Collections.Generic;
using Musketeers.Data;
using UnityEditor;
using UnityEngine;

namespace Musketeers.EditorTools.Studio
{
    // The big canvas: draws the composed musketeer, selects by clicking, drags to move, handles to rotate / scale.
    public sealed class StudioCanvas
    {
        public const int MaxSize = 720;
        const string TexKey = "canvas";
        const float HandleHit = 9f;
        enum Grab { None, Move, Rotate, Scale, Anchor }

        readonly StudioState _s;
        readonly StudioTextures _tex;
        readonly List<RigCompositor.ComposedLayer> _layers = new List<RigCompositor.ComposedLayer>();
        int _rev = -1, _size;
        float _k;
        bool _selBack;
        bool _failed;

        Grab _grab;
        ItemFit _startFit;
        Vector2 _startPose, _pivot;
        Anchor _anchor;
        bool _mirrored;
        float _startAngle, _startDist;
        AnchorId _grabAnchor;
        Anchor _startAnchor;

        public StudioCanvas(StudioState s, StudioTextures tex)
        {
            _s = s;
            _tex = tex;
        }

        public bool Dragging => _grab != Grab.None;
        public IReadOnlyList<RigCompositor.ComposedLayer> Layers => _layers;
        public void Invalidate() => _rev = -1;

        public void SelectLayer(int index)
        {
            var l = _layers[index];
            _selBack = l.BackFoot;
            if (l.Key == "musket" || l.Key == "rapier")
            {
                _s.Selection = StudioSelection.Weapon(l.Key == "musket");
                return;
            }
            var item = _s.Catalog.Get(l.Key);
            if (item != null)
                _s.Selection = StudioSelection.Cosmetic(item.Slot, item.Id);
        }

        public void SelectKey(string key, bool back)
        {
            for (int i = 0; i < _layers.Count; i++)
            {
                if (_layers[i].Key == key && _layers[i].BackFoot == back)
                {
                    SelectLayer(i);
                    return;
                }
            }
        }

        Vector2 ToGui(Rect r, Vector2 pose)
        {
            var o = RigCompositor.Origin(_size);
            return new Vector2(r.x + o.x + pose.x * _k, r.yMax - (o.y + pose.y * _k));
        }

        Vector2 ToPose(Rect r, Vector2 gui)
        {
            var o = RigCompositor.Origin(_size);
            return new Vector2((gui.x - r.x - o.x) / _k, (r.yMax - gui.y - o.y) / _k);
        }

        int FindSelected()
        {
            var sel = _s.Selection;
            if (sel.Kind != SelectionKind.Cosmetic && sel.Kind != SelectionKind.Weapon)
                return -1;
            int first = -1;
            for (int i = 0; i < _layers.Count; i++)
            {
                if (_layers[i].Key != sel.ItemId)
                    continue;
                if (_layers[i].BackFoot == _selBack)
                    return i;
                if (first < 0)
                    first = i;
            }
            return first;
        }

        void Recompose(int size)
        {
            _size = size;
            _k = RigCompositor.Ppu * _s.Zoom * size / MaxSize;
            try
            {
                var opt = _s.Options(new Color32(66, 66, 74, 255));
                var img = RigCompositor.Compose(_s.Character, _s.Pose, _s.Outfit(), size, _s.Zoom * size / MaxSize, opt, _layers);
                _tex.Upload(TexKey, img);
                _failed = false;
            }
            catch (Exception ex)
            {
                if (!_failed)
                    Debug.LogException(ex);
                _failed = true;
            }
            _rev = _s.Rev;
        }

        public void Draw(int size)
        {
            var rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
            int id = GUIUtility.GetControlID(FocusType.Passive);
            var e = Event.current;
            _k = RigCompositor.Ppu * _s.Zoom * size / MaxSize;
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (e.button == 0 && rect.Contains(e.mousePosition) && _size == size)
                    {
                        Begin(rect, e);
                        if (_grab != Grab.None)
                            GUIUtility.hotControl = id;
                        e.Use();
                        GUI.changed = true;
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        Drag(rect, e);
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        _grab = Grab.None;
                        _s.Touch();   // strip refreshes now that the drag is over
                        e.Use();
                    }
                    break;
                case EventType.Layout:
                    // Recompose only here: the layer list drives the inspector's controls, which must not change between Layout and Repaint.
                    if (_rev != _s.Rev || _size != size || _tex.Get(TexKey) == null)
                        Recompose(size);
                    break;
                case EventType.Repaint:
                    var t = _tex.Get(TexKey);
                    if (t != null)
                        GUI.DrawTexture(rect, t);
                    Overlay(rect);
                    break;
            }
        }

        // ---------- input ----------

        void Begin(Rect rect, Event e)
        {
            var c = _s.Character;
            var pp = ToPose(rect, e.mousePosition);
            if (_s.AnchorMode)
            {
                BeginAnchor(rect, e, c);
                return;
            }
            int idx = FindSelected();
            if (idx >= 0 && BeginHandle(rect, e, idx, pp))
                return;
            int hit = RigCompositor.HitTest(_layers, pp);
            if (hit < 0)
            {
                _s.Selection = StudioSelection.None;
                return;
            }
            SelectLayer(hit);
            StartGrab(Grab.Move, hit, pp);
        }

        void StartGrab(Grab g, int idx, Vector2 pp)
        {
            var l = _layers[idx];
            var target = StudioTargets.Resolve(_s, l.BackFoot);
            if (!target.Valid || _s.Character == null)
                return;
            _grab = g;
            _startPose = pp;
            _pivot = l.Placement.Position;
            _anchor = _s.Character.FitFor(_s.Pose).Get(target.Anchor);
            _mirrored = StudioTargets.Mirrored(_s.Character, _s.Pose, l.BackFoot);
            _startFit = FitEdit.Resolve(target.Fit, _s.CharId, _s.Pose);
            _startAngle = Mathf.Atan2(pp.y - _pivot.y, pp.x - _pivot.x) * Mathf.Rad2Deg;
            _startDist = Mathf.Max(0.001f, (pp - _pivot).magnitude);
        }

        bool Geometry(int idx, Rect rect, out Vector2[] corners, out Vector2 rotBase, out Vector2 rotKnob)
        {
            var l = _layers[idx];
            corners = null;
            rotBase = rotKnob = default;
            if (l.Sprite == null || !l.Placement.Valid)
                return false;
            var b = l.Sprite.bounds;
            var min = (Vector2)b.min;
            var max = (Vector2)b.max;
            var p = l.Placement;
            corners = new[]
            {
                ToGui(rect, RigCompositor.ToPose(p, new Vector2(min.x, min.y))),
                ToGui(rect, RigCompositor.ToPose(p, new Vector2(max.x, min.y))),
                ToGui(rect, RigCompositor.ToPose(p, new Vector2(max.x, max.y))),
                ToGui(rect, RigCompositor.ToPose(p, new Vector2(min.x, max.y))),
            };
            float cx = (min.x + max.x) * 0.5f;
            rotBase = ToGui(rect, RigCompositor.ToPose(p, new Vector2(cx, max.y)));
            rotKnob = ToGui(rect, RigCompositor.ToPose(p, new Vector2(cx, max.y + 0.14f / Mathf.Max(0.05f, Mathf.Abs(p.Scale.y)))));
            return true;
        }

        bool BeginHandle(Rect rect, Event e, int idx, Vector2 pp)
        {
            if (!Geometry(idx, rect, out var corners, out _, out var knob))
                return false;
            if ((e.mousePosition - knob).magnitude <= HandleHit + 2)
            {
                StartGrab(Grab.Rotate, idx, pp);
                return _grab != Grab.None;
            }
            foreach (var c in corners)
            {
                if ((e.mousePosition - c).magnitude <= HandleHit)
                {
                    StartGrab(Grab.Scale, idx, pp);
                    return _grab != Grab.None;
                }
            }
            return false;
        }

        void BeginAnchor(Rect rect, Event e, CharacterDef c)
        {
            var pf = PoseFitOf(c, false);
            if (pf == null)
                return;
            foreach (AnchorId a in Enum.GetValues(typeof(AnchorId)))
            {
                if ((ToGui(rect, pf.Get(a).Position) - e.mousePosition).magnitude <= HandleHit + 3)
                {
                    _s.Selection = StudioSelection.OfAnchor(a);
                    _grab = Grab.Anchor;
                    _grabAnchor = a;
                    _startAnchor = pf.Get(a);
                    _startPose = ToPose(rect, e.mousePosition);
                    return;
                }
            }
            _s.Selection = StudioSelection.None;
        }

        PoseFit PoseFitOf(CharacterDef c, bool create)
        {
            if (c == null || c.Fit == null)
                return null;
            var k = c.Resolve(_s.Pose);
            return c.Fit.Get(k) ?? (create ? c.Fit.GetOrAdd(k) : null);
        }

        void Drag(Rect rect, Event e)
        {
            var pp = ToPose(rect, e.mousePosition);
            var c = _s.Character;
            if (_grab == Grab.Anchor)
            {
                var pf = PoseFitOf(c, true);
                if (pf == null)
                    return;
                Undo.RecordObject(c.Fit, "Move anchor");
                var a = _startAnchor;
                a.Position += pp - _startPose;
                pf.Set(_grabAnchor, a);
                _s.MarkDirty(c.Fit);
                _s.Touch();
                return;
            }
            var target = StudioTargets.Resolve(_s, _selBack);
            if (!target.Valid)
                return;
            ItemFit fit;
            switch (_grab)
            {
                case Grab.Move:
                    fit = StudioEdit.Move(_startFit, _anchor, pp - _startPose, _mirrored);
                    break;
                case Grab.Rotate:
                    float ang = Mathf.Atan2(pp.y - _pivot.y, pp.x - _pivot.x) * Mathf.Rad2Deg;
                    fit = StudioEdit.Rotate(_startFit, Mathf.DeltaAngle(_startAngle, ang), _mirrored);
                    break;
                default:
                    fit = StudioEdit.ScaleBy(_startFit, Mathf.Max(0.001f, (pp - _pivot).magnitude) / _startDist);
                    break;
            }
            StudioEdit.Write(target.Fit, target.Owner, _s.Pose, _s.OverrideMusketeer ? _s.CharId : null, fit, _s.LinkAllPoses);
            _s.MarkDirty(target.Owner);
            _s.Touch();
        }

        // ---------- overlay ----------

        void Overlay(Rect rect)
        {
            if (_s.AnchorMode)
            {
                DrawAnchors(rect);
                return;
            }
            int idx = FindSelected();
            if (idx < 0 || !Geometry(idx, rect, out var c, out var rotBase, out var knob))
                return;
            var yellow = new Color(1f, 0.9f, 0.2f);
            Handles.color = yellow;
            Handles.DrawAAPolyLine(2f, ToV3(c[0]), ToV3(c[1]), ToV3(c[2]), ToV3(c[3]), ToV3(c[0]));
            Handles.DrawAAPolyLine(2f, ToV3(rotBase), ToV3(knob));
            Handles.DrawSolidDisc(ToV3(knob), Vector3.forward, 7f);
            Handles.color = Color.black;
            Handles.DrawWireDisc(ToV3(knob), Vector3.forward, 7f);
            foreach (var p in c)
            {
                var sq = new Rect(p.x - 5, p.y - 5, 10, 10);
                EditorGUI.DrawRect(new Rect(sq.x - 1, sq.y - 1, 12, 12), Color.black);
                EditorGUI.DrawRect(sq, yellow);
            }
        }

        void DrawAnchors(Rect rect)
        {
            var pf = PoseFitOf(_s.Character, false);
            if (pf == null)
                return;
            var small = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = Color.white } };
            foreach (AnchorId a in Enum.GetValues(typeof(AnchorId)))
            {
                var p = ToGui(rect, pf.Get(a).Position);
                bool sel = _s.Selection.Kind == SelectionKind.Anchor && _s.Selection.Anchor == a;
                Handles.color = sel ? Color.white : Color.black;
                Handles.DrawSolidDisc(ToV3(p), Vector3.forward, sel ? 9f : 7f);
                Handles.color = RigCompositor.AnchorColor(a);
                Handles.DrawSolidDisc(ToV3(p), Vector3.forward, sel ? 7f : 5f);
                GUI.Label(new Rect(p.x + 9, p.y - 8, 80, 16), a.ToString(), small);
            }
        }

        static Vector3 ToV3(Vector2 v) => new Vector3(v.x, v.y, 0f);
    }
}
