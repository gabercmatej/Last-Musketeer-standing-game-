using System.Collections.Generic;
using Musketeers.Data;
using UnityEngine;

namespace Musketeers.EditorTools.Studio
{
    // The pen tool's rules, with no window in it: clicks and drags in, a BezierPath changed. Everything is in
    // the path's own coordinate space (item-local for item masks, pose space for body masks); the caller
    // converts mouse positions and gives the hit radius in that same space.
    public sealed class StudioPen
    {
        enum Mode { None, Point, NewPoint, HandleIn, HandleOut }

        public BezierPath Path;
        public int Selected = -1;
        public float HitRadius = 0.03f;
        Mode _mode;
        Vector2 _grab;

        public bool IsEditing => Path != null;
        public bool IsDragging => _mode != Mode.None;

        // Called before every event: undo / redo can swap the path object under us.
        public void Bind(BezierPath path)
        {
            Path = path;
            if (path == null || Selected >= path.Points.Count)
                Selected = -1;
        }

        public void Reset()
        {
            Selected = -1;
            _mode = Mode.None;
        }

        static bool HasHandle(Vector2 h) => h.sqrMagnitude > 1e-10f;

        public void MouseDown(Vector2 p, bool alt)
        {
            _mode = Mode.None;
            if (Path == null)
                return;
            var pts = Path.Points;
            float r2 = HitRadius * HitRadius;

            // Clicking the first point of an open path with enough points closes it.
            if (!Path.Closed && pts.Count >= 3 && (pts[0].P - p).sqrMagnitude <= r2)
            {
                Path.Closed = true;
                Selected = 0;
                return;
            }
            // Handle ends.
            for (int i = 0; i < pts.Count; i++)
            {
                var pt = pts[i];
                if (HasHandle(pt.In) && (pt.P + pt.In - p).sqrMagnitude <= r2)
                {
                    GrabHandle(i, Mode.HandleIn, alt);
                    return;
                }
                if (HasHandle(pt.Out) && (pt.P + pt.Out - p).sqrMagnitude <= r2)
                {
                    GrabHandle(i, Mode.HandleOut, alt);
                    return;
                }
            }
            // Points: the nearest one inside the radius.
            int best = -1;
            float bestD = r2;
            for (int i = 0; i < pts.Count; i++)
            {
                float d = (pts[i].P - p).sqrMagnitude;
                if (d <= bestD)
                {
                    best = i;
                    bestD = d;
                }
            }
            if (best >= 0)
            {
                Selected = best;
                _mode = Mode.Point;
                _grab = pts[best].P - p;
                return;
            }
            if (!Path.Closed || pts.Count < 3)
            {
                pts.Add(new BezierPoint { P = p });
                Selected = pts.Count - 1;
                _mode = Mode.NewPoint;
                return;
            }
            // Closed outline: a click on an edge inserts a corner point there.
            if (NearestSegment(p, out int seg, out var at) && (at - p).sqrMagnitude <= r2)
            {
                pts.Insert(seg + 1, new BezierPoint { P = at });
                Selected = seg + 1;
                _mode = Mode.Point;
                _grab = Vector2.zero;
                return;
            }
            Selected = -1;
        }

        void GrabHandle(int i, Mode m, bool alt)
        {
            Selected = i;
            _mode = m;
            if (alt)
            {
                var pt = Path.Points[i];
                pt.Broken = true;
                Path.Points[i] = pt;
            }
        }

        public void MouseDrag(Vector2 p, bool alt)
        {
            if (Path == null || _mode == Mode.None || Selected < 0 || Selected >= Path.Points.Count)
                return;
            var pt = Path.Points[Selected];
            switch (_mode)
            {
                case Mode.Point:
                    pt.P = p + _grab;
                    break;
                case Mode.NewPoint:
                    pt.Out = p - pt.P;
                    pt.In = -pt.Out;
                    break;
                case Mode.HandleOut:
                    pt.Out = p - pt.P;
                    if (alt) pt.Broken = true;
                    if (!pt.Broken) pt.In = -pt.Out;
                    break;
                case Mode.HandleIn:
                    pt.In = p - pt.P;
                    if (alt) pt.Broken = true;
                    if (!pt.Broken) pt.Out = -pt.In;
                    break;
            }
            Path.Points[Selected] = pt;
        }

        public void MouseUp() => _mode = Mode.None;

        // Fewer than 3 points left just makes the path not Valid; Closed stays as it was.
        public bool DeleteSelected()
        {
            if (Path == null || Selected < 0 || Selected >= Path.Points.Count)
                return false;
            Path.Points.RemoveAt(Selected);
            Selected = -1;
            _mode = Mode.None;
            return true;
        }

        public bool Nudge(Vector2 delta)
        {
            if (Path == null || Selected < 0 || Selected >= Path.Points.Count)
                return false;
            var pt = Path.Points[Selected];
            pt.P += delta;
            Path.Points[Selected] = pt;
            return true;
        }

        bool NearestSegment(Vector2 p, out int seg, out Vector2 at)
        {
            seg = -1;
            at = default;
            var pts = Path.Points;
            float best = float.MaxValue;
            var buf = new List<Vector2>();
            for (int i = 0; i < pts.Count; i++)
            {
                buf.Clear();
                SegmentPoints(pts[i], pts[(i + 1) % pts.Count], 12, buf, true);
                for (int k = 0; k + 1 < buf.Count; k++)
                {
                    var q = Closest(buf[k], buf[k + 1], p);
                    float d = (q - p).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        seg = i;
                        at = q;
                    }
                }
            }
            return seg >= 0;
        }

        static Vector2 Closest(Vector2 a, Vector2 b, Vector2 p)
        {
            var ab = b - a;
            float l2 = ab.sqrMagnitude;
            if (l2 < 1e-12f)
                return a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
            return a + ab * t;
        }

        static void SegmentPoints(BezierPoint a, BezierPoint b, int steps, List<Vector2> into, bool includeEnd)
        {
            if (a.Out == Vector2.zero && b.In == Vector2.zero)
            {
                into.Add(a.P);
                if (includeEnd)
                    into.Add(b.P);
                return;
            }
            var p1 = a.P + a.Out;
            var p2 = b.P + b.In;
            for (int s = 0; s < steps; s++)
            {
                float t = (float)s / steps, u = 1f - t;
                into.Add(u * u * u * a.P + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * b.P);
            }
            if (includeEnd)
                into.Add(b.P);
        }

        // The outline for drawing (works on open paths too). A closed path is not repeated at the end.
        public static Vector2[] Polyline(BezierPath path, int steps = 12)
        {
            var res = new List<Vector2>();
            if (path == null || path.Points == null || path.Points.Count == 0)
                return res.ToArray();
            var pts = path.Points;
            int segs = path.Closed ? pts.Count : pts.Count - 1;
            for (int i = 0; i < segs; i++)
                SegmentPoints(pts[i], pts[(i + 1) % pts.Count], steps, res, false);
            if (!path.Closed)
                res.Add(pts[pts.Count - 1].P);
            return res.ToArray();
        }
    }
}
