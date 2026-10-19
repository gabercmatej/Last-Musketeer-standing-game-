using System.Collections.Generic;
using System.IO;
using Musketeers.Data;
using Musketeers.EditorTools.ArtImport;
using UnityEditor;
using UnityEngine;

namespace Musketeers.EditorTools
{
    // Draws a character pose wearing cosmetics into an image on the CPU, using CosmeticLayout (the same
    // placement CharacterRig uses at runtime). Powers the Wardrobe Preview window and the exported
    // check sheets, without needing a scene or a camera.
    public static class WardrobeCompositor
    {
        const float Ppu = 256f;
        static readonly Dictionary<string, Img> Cache = new Dictionary<string, Img>();

        public static void ClearCache() => Cache.Clear();

        // `size` square canvas; the feet sit at (size/2, size*0.125). `zoom` = output px per sprite px.
        public static Img Compose(CharacterDef c, PoseKind kind, IList<CosmeticDef> items, int size, float zoom, bool showAnchors)
        {
            var img = new Img(size, size);
            var pose = c.Pose(kind);
            if (pose?.Sprite == null)
                return img;
            var origin = new Vector2(size * 0.5f, size * 0.125f);
            var layers = new List<(int order, Sprite sprite, Vector2 pos, float angle, float scale, float sx)>();
            layers.Add((0, pose.Sprite, Vector2.zero, 0f, 1f, 1f));
            foreach (var item in items)
            {
                var p = CosmeticLayout.Place(pose, item);
                if (p.Valid)
                    layers.Add((p.Order, p.Sprite, p.Position, p.Angle, p.Scale, p.ScaleX));
            }
            layers.Sort((a, b) => a.order.CompareTo(b.order));
            foreach (var l in layers)
                Draw(img, l.sprite, origin + l.pos * Ppu * zoom, l.angle, l.scale * zoom, l.sx);
            if (showAnchors)
            {
                var a = pose.Anchors;
                Dot(img, origin + a.HeadTop * Ppu * zoom, new Color32(255, 220, 0, 255));
                Dot(img, origin + a.Face * Ppu * zoom, new Color32(0, 230, 255, 255));
                Dot(img, origin + a.Back * Ppu * zoom, new Color32(255, 80, 200, 255));
                Dot(img, origin + a.Feet * Ppu * zoom, new Color32(80, 255, 80, 255));
                Dot(img, origin + a.Hand * Ppu * zoom, new Color32(255, 120, 0, 255));
            }
            return img;
        }

        static Img Pixels(Sprite s)
        {
            string path = AssetDatabase.GetAssetPath(s);
            if (!Cache.TryGetValue(path, out var img))
            {
                img = Img.Load(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path));
                Cache[path] = img;
            }
            return img;
        }

        // Inverse-mapped bilinear sampling of the sprite (rotated around and scaled about its pivot).
        static void Draw(Img dst, Sprite s, Vector2 at, float angleDeg, float scale, float stretchX)
        {
            var src = Pixels(s);
            // Sprite pixel space may differ from the file if the texture was size-capped on import.
            float texScale = src.W / s.rect.width;
            Vector2 pivot = s.pivot * texScale;
            float r = angleDeg * Mathf.Deg2Rad, cos = Mathf.Cos(r), sin = Mathf.Sin(r);
            float k = scale / texScale;   // output px per file px
            float ext = (src.W + src.H) * k * Mathf.Max(1f, stretchX);
            int x0 = Mathf.Max(0, Mathf.FloorToInt(at.x - ext)), x1 = Mathf.Min(dst.W - 1, Mathf.CeilToInt(at.x + ext));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(at.y - ext)), y1 = Mathf.Min(dst.H - 1, Mathf.CeilToInt(at.y + ext));
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = (x + 0.5f - at.x) / k, dy = (y + 0.5f - at.y) / k;
                // Inverse of: stretch x, then rotate (the runtime child transform does scale then rotation).
                float sx = (dx * cos + dy * sin) / stretchX + pivot.x - 0.5f;
                float sy = -dx * sin + dy * cos + pivot.y - 0.5f;
                if (sx < -1 || sy < -1 || sx > src.W || sy > src.H)
                    continue;
                var c = Sample(src, sx, sy);
                if (c.a <= 0.001f)
                    continue;
                int i = y * dst.W + x;
                var d = (Color)dst.Px[i];
                float a = c.a + d.a * (1 - c.a);
                var o = a > 0 ? (c * c.a + d * d.a * (1 - c.a)) / a : Color.clear;
                o.a = a;
                dst.Px[i] = o;
            }
        }

        static Color Sample(Img s, float x, float y)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            Color P(int px, int py)
            {
                if (px < 0 || py < 0 || px >= s.W || py >= s.H)
                    return Color.clear;
                Color c = s.Px[py * s.W + px];
                return new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);   // premultiplied
            }
            var c00 = P(ix, iy); var c10 = P(ix + 1, iy); var c01 = P(ix, iy + 1); var c11 = P(ix + 1, iy + 1);
            var c0 = Color.Lerp(c00, c10, fx); var c1 = Color.Lerp(c01, c11, fx);
            var pm = Color.Lerp(c0, c1, fy);
            return pm.a > 0 ? new Color(pm.r / pm.a, pm.g / pm.a, pm.b / pm.a, pm.a) : Color.clear;
        }

        static void Dot(Img img, Vector2 p, Color32 c)
        {
            for (int dy = -3; dy <= 3; dy++)
            for (int dx = -3; dx <= 3; dx++)
            {
                int x = Mathf.RoundToInt(p.x) + dx, y = Mathf.RoundToInt(p.y) + dy;
                if (x >= 0 && y >= 0 && x < img.W && y < img.H && dx * dx + dy * dy <= 9)
                    img.Px[y * img.W + x] = c;
            }
        }

        // ---------- check sheets (Tools menu) ----------

        [MenuItem("Tools/Musketeers/Export Wardrobe Sheets")]
        public static void ExportSheets()
        {
            ClearCache();
            var roster = AssetDatabase.LoadAssetAtPath<CharacterRoster>("Assets/_Project/Data/CharacterRoster.asset");
            var catalog = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>("Assets/_Project/Data/CosmeticCatalog.asset");
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp/Wardrobe");
            Directory.CreateDirectory(dir);
            const int cell = 256;
            foreach (var slot in Loadout.Slots)
            {
                var items = catalog.BySlot(slot);
                foreach (var kind in new[] { PoseKind.Idle, PoseKind.Hurt, PoseKind.Victory, PoseKind.Lunge })
                {
                    var sheet = new Img(cell * roster.Count, cell * items.Count);
                    for (int r = 0; r < items.Count; r++)
                    for (int ci = 0; ci < roster.Count; ci++)
                    {
                        var c = roster.Get(ci);
                        var img = Compose(c, kind, new[] { items[r] }, cell, 0.5f, false);
                        Fill(sheet, ci * cell, (items.Count - 1 - r) * cell, cell, ((ci + r) & 1) == 0 ? new Color32(70, 70, 78, 255) : new Color32(84, 84, 92, 255));
                        Over(sheet, img, ci * cell, (items.Count - 1 - r) * cell);
                    }
                    File.WriteAllBytes(Path.Combine(dir, $"{slot}_{kind}.png"), sheet.EncodePng());
                }
            }
            // One outfit per character in every pose, with anchors.
            const int big = 512;
            var outfit = new Img(big * 4, big * roster.Count);
            for (int ci = 0; ci < roster.Count; ci++)
            {
                var c = roster.Get(ci);
                var items = new List<CosmeticDef>();
                foreach (var slot in Loadout.Slots)
                {
                    var it = catalog.Get(c.DefaultLoadout.Get(slot));
                    if (it != null) items.Add(it);
                }
                for (int k = 0; k < 4; k++)
                {
                    Fill(outfit, k * big, (roster.Count - 1 - ci) * big, big, new Color32(70, 70, 78, 255));
                    Over(outfit, Compose(c, (PoseKind)k, items, big, 1f, true), k * big, (roster.Count - 1 - ci) * big);
                }
            }
            File.WriteAllBytes(Path.Combine(dir, "outfits.png"), outfit.EncodePng());
            Debug.Log("[Wardrobe] Exported check sheets to " + dir);
        }

        static void Fill(Img img, int x0, int y0, int size, Color32 c)
        {
            for (int y = y0; y < y0 + size; y++)
            for (int x = x0; x < x0 + size; x++)
                img.Px[y * img.W + x] = c;
        }

        static void Over(Img dst, Img src, int ox, int oy)
        {
            for (int y = 0; y < src.H; y++)
            for (int x = 0; x < src.W; x++)
            {
                Color s = src.Px[y * src.W + x];
                if (s.a <= 0) continue;
                int i = (oy + y) * dst.W + ox + x;
                Color d = dst.Px[i];
                dst.Px[i] = Color.Lerp(d, new Color(s.r, s.g, s.b, 1f), s.a);
            }
        }
    }
}
