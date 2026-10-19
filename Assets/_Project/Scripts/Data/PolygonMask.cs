using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Musketeers.Data
{
    // The boot and hand masks are polygons in pose space. At runtime they become small white sprites for
    // SpriteMasks (built once per shape and cached); the editor's compositor tests pixels directly.
    public static class PolygonMask
    {
        const float Ppu = 160f;   // mask texels per world unit; SpriteMask cuts at alpha 0.5
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static bool Valid(Vector2[] poly) => poly != null && poly.Length >= 3;

        // Even-odd rule, so a concave outline works too.
        public static bool Contains(Vector2[] poly, Vector2 p)
        {
            if (!Valid(poly))
                return false;
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                var a = poly[i];
                var b = poly[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        public static Rect Bounds(Vector2[] poly)
        {
            var min = poly[0];
            var max = poly[0];
            foreach (var p in poly)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        public static Sprite Sprite(BezierPath path, out Vector2 origin) =>
            Sprite(path != null && path.Valid ? path.Flatten() : null, out origin);

        // A sprite whose pivot is the bottom-left of the polygon's bounds: place it at `origin`.
        public static Sprite Sprite(Vector2[] poly, out Vector2 origin)
        {
            origin = Vector2.zero;
            if (!Valid(poly))
                return null;
            var b = Bounds(poly);
            origin = b.min;
            string key = Key(poly);
            if (Cache.TryGetValue(key, out var cached) && cached != null)
                return cached;
            int w = Mathf.Max(2, Mathf.CeilToInt(b.width * Ppu) + 2);
            int h = Mathf.Max(2, Mathf.CeilToInt(b.height * Ppu) + 2);
            var px = new Color32[w * h];
            // 2×2 supersampling gives the mask a smoother edge at the 0.5 cut.
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int hits = 0;
                for (int s = 0; s < 4; s++)
                {
                    var p = new Vector2(b.xMin + (x + 0.25f + 0.5f * (s & 1)) / Ppu, b.yMin + (y + 0.25f + 0.5f * (s >> 1)) / Ppu);
                    if (Contains(poly, p))
                        hits++;
                }
                px[y * w + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 4));
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "PolygonMask", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var sprite = UnityEngine.Sprite.Create(tex, new Rect(0, 0, w, h), Vector2.zero, Ppu, 0, SpriteMeshType.FullRect);
            sprite.name = "PolygonMask";
            sprite.hideFlags = HideFlags.DontSave;
            Cache[key] = sprite;
            return sprite;
        }

        static string Key(Vector2[] poly)
        {
            var sb = new StringBuilder();
            foreach (var p in poly)
                sb.Append(Mathf.RoundToInt(p.x * 1000)).Append(',').Append(Mathf.RoundToInt(p.y * 1000)).Append(';');
            return sb.ToString();
        }
    }
}
