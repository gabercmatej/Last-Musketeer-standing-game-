using System;
using System.Collections.Generic;
using UnityEngine;

namespace Musketeers.Presentation
{
    // Sprites drawn in code (CLAUDE.md "Placeholder art"): white/grey shapes that get tinted, so the game
    // never depends on an art file. Every sprite is 1 world unit across. Built once, on first use.
    public static class PlaceholderArt
    {
        const int Size = 64;
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Pixel => Get("pixel", 4, (x, y) => 1f);
        public static Sprite Square => Get("square", 8, (x, y) => 1f);
        public static Sprite Circle => Get("circle", Size, (x, y) => Sdf(Len(x, y) - 0.46f));
        public static Sprite Ring => Get("ring", Size, (x, y) => Sdf(Math.Abs(Len(x, y) - 0.42f) - 0.045f));
        public static Sprite SoftCircle => Get("soft", Size, (x, y) => Mathf.Clamp01(1f - Len(x, y) / 0.5f));
        // A diagonal cross (the "contested" marker's crossed blades).
        public static Sprite Cross => Get("cross", Size, (x, y) =>
        {
            float d = Math.Min(Math.Abs(x - y), Math.Abs(x + y)) * 0.7071f;
            return Sdf(Math.Max(d - 0.05f, Math.Max(Math.Abs(x), Math.Abs(y)) - 0.34f));
        });

        // A crescent arc bulging toward +x, thick in the middle and tapering to points (the parry shield).
        public static Sprite Crescent => Get("crescent", 96, (x, y) =>
        {
            float ang = Mathf.Atan2(y, x);
            const float Half = 1.15f;
            if (Math.Abs(ang) > Half)
                return 0f;
            float w = 0.015f + 0.11f * (1f - Math.Abs(ang) / Half);
            return Sdf(Math.Abs(Len(x, y) - 0.4f) - w);
        });

        public static Sprite RoundedSquare => Get("rsquare", Size, (x, y) => Sdf(Box(x, y, 0.40f, 0.40f) - 0.07f));

        // Floor with a darker 1-pixel rim, which reads as subtle grid lines once tiles sit side by side.
        public static Sprite Floor => GetColored("floor", Size, (x, y) =>
        {
            bool rim = Math.Abs(x) > 0.5f - 1.5f / Size || Math.Abs(y) > 0.5f - 1.5f / Size;
            float v = rim ? 0.80f : 1f;
            return new Color(v, v, v, 1f);
        });

        // A hollow square (white rim, clear inside): thin grid lines between tiles, tinted dark by the board.
        public static Sprite Outline => GetColored("outline", Size, (x, y) =>
        {
            bool rim = Math.Abs(x) > 0.5f - 1.5f / Size || Math.Abs(y) > 0.5f - 1.5f / Size;
            return new Color(1f, 1f, 1f, rim ? 1f : 0f);
        });

        public static Sprite Pillar => GetColored("pillar", Size, (x, y) =>
        {
            float a = Sdf(Box(x, y, 0.36f, 0.36f) - 0.06f);
            float top = y > 0.05f ? 1f : 0.82f;
            float inner = Box(x, y, 0.26f, 0.26f) < 0 ? 0.92f : 1f;
            float v = top * inner;
            return new Color(v, v, v, a);
        });

        public static Sprite Crate => GetColored("crate", Size, (x, y) =>
        {
            float a = Sdf(Box(x, y, 0.40f, 0.40f) - 0.02f);
            bool rim = Box(x, y, 0.32f, 0.32f) > 0;
            bool cross = Math.Abs(x - y) < 0.06f || Math.Abs(x + y) < 0.06f;
            float v = rim || cross ? 0.72f : 1f;
            return new Color(v, v, v, a);
        });

        public static Sprite Pit => GetColored("pit", Size, (x, y) =>
        {
            float edge = Mathf.Clamp01((0.5f - Math.Max(Math.Abs(x), Math.Abs(y))) / 0.12f);
            float v = 0.55f + 0.45f * edge;
            return new Color(v, v, v, 1f);
        });

        public static Sprite Keg => GetColored("keg", Size, (x, y) =>
        {
            float a = Sdf(Box(x, y, 0.28f, 0.34f) - 0.08f);
            bool band = Math.Abs(Math.Abs(y) - 0.2f) < 0.04f;
            float v = band ? 0.55f : 1f - 0.25f * Math.Abs(x) / 0.36f;
            return new Color(v, v, v, a);
        });

        public static Sprite Triangle => Poly("triangle", new[] { new Vector2(0, 0.45f), new Vector2(0.38f, -0.3f), new Vector2(-0.38f, -0.3f) });

        public static Sprite Arrow => Poly("arrow", new[]
        {
            new Vector2(0, 0.48f), new Vector2(0.34f, 0.08f), new Vector2(0.12f, 0.08f), new Vector2(0.12f, -0.45f),
            new Vector2(-0.12f, -0.45f), new Vector2(-0.12f, 0.08f), new Vector2(-0.34f, 0.08f),
        });

        public static Sprite Crown => Poly("crown", new[]
        {
            new Vector2(-0.44f, -0.30f), new Vector2(0.44f, -0.30f), new Vector2(0.46f, 0.30f), new Vector2(0.22f, 0.02f),
            new Vector2(0f, 0.40f), new Vector2(-0.22f, 0.02f), new Vector2(-0.46f, 0.30f),
        });

        public static Sprite Check => Poly("check", new[]
        {
            new Vector2(-0.44f, 0.02f), new Vector2(-0.12f, -0.14f), new Vector2(0.32f, 0.40f),
            new Vector2(0.46f, 0.26f), new Vector2(-0.12f, -0.40f), new Vector2(-0.30f, -0.12f),
        });

        public static Sprite Musket => Poly("musket", new[]
        {
            new Vector2(-0.48f, -0.18f), new Vector2(-0.16f, -0.06f), new Vector2(0.48f, -0.06f), new Vector2(0.48f, 0.06f),
            new Vector2(-0.14f, 0.06f), new Vector2(-0.30f, 0.16f), new Vector2(-0.48f, 0.04f),
        });

        public static Sprite Star => Poly("star", StarPoints(8, 0.48f, 0.2f));

        public static Sprite Heart => Get("heart", Size, (x, y) =>
        {
            // Supersampled implicit heart: (X² + Y² − 1)³ − X²Y³ ≤ 0.
            float hits = 0;
            for (int sy = 0; sy < 3; sy++)
            for (int sx = 0; sx < 3; sx++)
            {
                float px = (x + (sx - 1) / (3f * Size)) * 2.6f;
                float py = (y + (sy - 1) / (3f * Size)) * 2.6f + 0.15f;
                float q = px * px + py * py - 1f;
                if (q * q * q - px * px * py * py * py <= 0) hits++;
            }
            return hits / 9f;
        });

        static Vector2[] StarPoints(int points, float outer, float inner)
        {
            var v = new Vector2[points * 2];
            for (int i = 0; i < v.Length; i++)
            {
                float ang = Mathf.PI * i / points + Mathf.PI / 2;
                float r = i % 2 == 0 ? outer : inner;
                v[i] = new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r);
            }
            return v;
        }

        static float Len(float x, float y) => Mathf.Sqrt(x * x + y * y);

        static float Box(float x, float y, float hx, float hy)
        {
            float dx = Math.Abs(x) - hx, dy = Math.Abs(y) - hy;
            float outside = Len(Math.Max(dx, 0), Math.Max(dy, 0));
            return outside + Math.Min(Math.Max(dx, dy), 0);
        }

        // Signed distance (in sprite units) → anti-aliased alpha.
        static float Sdf(float d) => Mathf.Clamp01(0.5f - d * Size);

        static Sprite Poly(string key, Vector2[] pts) => Get(key, Size, (x, y) =>
        {
            float hits = 0;
            for (int sy = 0; sy < 3; sy++)
            for (int sx = 0; sx < 3; sx++)
            {
                if (Inside(pts, x + (sx - 1) / (3f * Size), y + (sy - 1) / (3f * Size)))
                    hits++;
            }
            return hits / 9f;
        });

        static bool Inside(Vector2[] poly, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].y > y) != (poly[j].y > y) &&
                    x < (poly[j].x - poly[i].x) * (y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            }
            return inside;
        }

        static Sprite Get(string key, int size, Func<float, float, float> alpha) =>
            GetColored(key, size, (x, y) => new Color(1, 1, 1, alpha(x, y)));

        // x, y run from -0.5 to 0.5 across the sprite (pixel centers).
        static Sprite GetColored(string key, int size, Func<float, float, Color> shade)
        {
            if (Cache.TryGetValue(key, out var cached) && cached != null)
                return cached;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Placeholder_" + key,
                filterMode = size <= 8 ? FilterMode.Point : FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            var px = new Color[size * size];
            for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
                px[j * size + i] = shade((i + 0.5f) / size - 0.5f, (j + 0.5f) / size - 0.5f);
            tex.SetPixels(px);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
            sprite.name = key;
            sprite.hideFlags = HideFlags.DontSave;
            Cache[key] = sprite;
            return sprite;
        }
    }
}
