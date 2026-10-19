using System;
using System.Collections.Generic;
using UnityEngine;

namespace Musketeers.Data
{
    // One pen-tool point. In/Out are handle offsets relative to P; zero = sharp corner.
    // Broken = the two handles move independently (an editor rule; the data only stores it).
    [Serializable]
    public struct BezierPoint
    {
        public Vector2 P;
        public Vector2 In;
        public Vector2 Out;
        public bool Broken;
    }

    // A mask outline (Plan 006): points with handles. Flatten() turns it into the polygon that the cached
    // PolygonMask sprite and the editor's pixel test both use, so the runtime and the editor agree.
    [Serializable]
    public sealed class BezierPath
    {
        public List<BezierPoint> Points = new List<BezierPoint>();
        public bool Closed;
        public bool Invert;     // item masks only: keep the inside instead of cutting it away

        public bool Valid => Closed && Points != null && Points.Count >= 3;

        public Vector2[] Flatten(int segmentsPerCurve = 12)
        {
            if (!Valid)
                return new Vector2[0];
            int steps = Mathf.Max(1, segmentsPerCurve);
            var res = new List<Vector2>(Points.Count * 2);
            for (int i = 0; i < Points.Count; i++)
            {
                var a = Points[i];
                var b = Points[(i + 1) % Points.Count];
                if (a.Out == Vector2.zero && b.In == Vector2.zero)
                {
                    res.Add(a.P);
                    continue;
                }
                var p0 = a.P;
                var p1 = a.P + a.Out;
                var p2 = b.P + b.In;
                var p3 = b.P;
                for (int s = 0; s < steps; s++)
                {
                    float t = (float)s / steps, u = 1f - t;
                    res.Add(u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3);
                }
            }
            return res.ToArray();
        }

        public BezierPath Clone()
        {
            return new BezierPath { Points = new List<BezierPoint>(Points ?? new List<BezierPoint>()), Closed = Closed, Invert = Invert };
        }

        // `point` maps positions, `vector` maps handle offsets (so a translation doesn't move handles).
        public BezierPath Map(Func<Vector2, Vector2> point, Func<Vector2, Vector2> vector)
        {
            var r = new BezierPath { Closed = Closed, Invert = Invert };
            if (Points != null)
            {
                foreach (var p in Points)
                    r.Points.Add(new BezierPoint { P = point(p.P), In = vector(p.In), Out = vector(p.Out), Broken = p.Broken });
            }
            return r;
        }

        public static BezierPath FromPolygon(Vector2[] poly)
        {
            var r = new BezierPath();
            if (poly == null || poly.Length == 0)
                return r;
            foreach (var p in poly)
                r.Points.Add(new BezierPoint { P = p });
            r.Closed = poly.Length >= 3;
            return r;
        }
    }
}
