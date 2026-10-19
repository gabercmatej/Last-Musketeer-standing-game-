using System;
using System.Collections.Generic;
using System.IO;
using Musketeers.Data;
using Musketeers.EditorTools.ArtImport;
using UnityEditor;
using UnityEngine;

namespace Musketeers.EditorTools
{
    // Draws a musketeer pose wearing items into an image on the CPU, with the same CosmeticLayout and
    // layer order CharacterRig uses at runtime (boot masks, darker back boot, musket behind the body,
    // rapier with the front hand redrawn over it). Powers the Fitting Editor and the check sheets without
    // needing a scene or a camera.
    public static class RigCompositor
    {
        public const float Ppu = 256f;
        static readonly Dictionary<string, Img> Cache = new Dictionary<string, Img>();

        public sealed class Options
        {
            public bool Weapons = true;
            public bool Raised;          // rapier up (parry)
            public bool Anchors;         // bake anchor dots (check sheets)
            public PoseKind? Onion;      // another pose drawn translucent on top
            public Color32 Background = new Color32(0, 0, 0, 0);
        }

        struct Layer
        {
            public int Order;
            public Sprite Sprite;
            public Vector2 Pos;
            public float Angle;
            public Vector2 Scale;
            public float Shade;
            public float Alpha;
            public Vector2[][] Hide;     // pose-space polygons this layer is cut out of
            public Vector2[] Only;       // pose-space polygon this layer is limited to
        }

        public static void ClearCache() => Cache.Clear();

        // The feet sit at Origin(size); `zoom` = output px per sprite px.
        public static Vector2 Origin(int size) => new Vector2(size * 0.5f, size * 0.125f);

        public static Img Compose(CharacterDef c, PoseKind asked, IList<CosmeticDef> items, int size, float zoom, Options opt = null)
        {
            opt = opt ?? new Options();
            var img = new Img(size, size);
            if (opt.Background.a > 0)
            {
                for (int i = 0; i < img.Px.Length; i++)
                    img.Px[i] = opt.Background;
            }
            if (c == null || !c.HasPose(PoseKind.Idle))
                return img;
            var kind = c.Resolve(asked);
            var art = c.Art(kind);
            var fit = c.FitFor(kind);
            var layers = new List<Layer>();
            bool boots = false;
            foreach (var item in items)
            {
                if (item == null)
                    continue;
                for (int foot = 0; foot < (item.Slot == CosmeticSlot.Boots ? 2 : 1); foot++)
                {
                    var p = CosmeticLayout.Place(art, fit, c.Id, kind, item, foot == 1);
                    if (!p.Valid)
                        continue;
                    boots |= item.Slot == CosmeticSlot.Boots;
                    layers.Add(From(p));
                }
            }
            var bodyHide = boots ? new[] { fit.BootMaskBack, fit.BootMaskFront } : null;
            layers.Add(new Layer { Order = 0, Sprite = art.Sprite, Pos = Vector2.zero, Scale = Vector2.one, Shade = 1f, Alpha = 1f, Hide = bodyHide });
            if (opt.Weapons)
            {
                var musket = Sprite(GameArt.Musket);
                var rapier = Sprite(GameArt.Rapier);
                var m = CosmeticLayout.Weapon(fit, musket, true);
                if (m.Valid) layers.Add(From(m));
                var r = CosmeticLayout.Weapon(fit, rapier, false, opt.Raised ? 62f : (float?)null);
                if (r.Valid)
                {
                    layers.Add(From(r));
                    if (PolygonMask.Valid(fit.HandMask))
                        layers.Add(new Layer { Order = CosmeticLayout.Order(RigPart.Hand), Sprite = art.Sprite, Scale = Vector2.one, Shade = 1f, Alpha = 1f, Only = fit.HandMask });
                }
            }
            layers.Sort((a, b) => a.Order.CompareTo(b.Order));
            var origin = Origin(size);
            float k = Ppu * zoom;
            foreach (var l in layers)
                Draw(img, l, origin, k);
            if (opt.Onion.HasValue && c.HasPose(opt.Onion.Value))
                Draw(img, new Layer { Sprite = c.Art(opt.Onion.Value).Sprite, Scale = Vector2.one, Shade = 1f, Alpha = 0.35f }, origin, k);
            if (opt.Anchors)
            {
                foreach (AnchorId id in Enum.GetValues(typeof(AnchorId)))
                    Dot(img, origin + fit.Get(id).Position * k, AnchorColor(id));
            }
            return img;
        }

        static Layer From(LayerPlacement p) =>
            new Layer { Order = p.Order, Sprite = p.Sprite, Pos = p.Position, Angle = p.Angle, Scale = p.Scale, Shade = p.Shade, Alpha = 1f };

        public static Color32 AnchorColor(AnchorId id)
        {
            switch (id)
            {
                case AnchorId.HeadTop: return new Color32(255, 220, 0, 255);
                case AnchorId.Face: return new Color32(0, 230, 255, 255);
                case AnchorId.Back: return new Color32(255, 80, 200, 255);
                case AnchorId.FootBack: return new Color32(40, 170, 40, 255);
                case AnchorId.FootFront: return new Color32(120, 255, 120, 255);
                case AnchorId.HandBack: return new Color32(200, 90, 0, 255);
                default: return new Color32(255, 160, 60, 255);
            }
        }

        static Sprite Sprite(string key)
        {
            var ga = AssetDatabase.LoadAssetAtPath<GameArt>("Assets/_Project/Data/GameArt.asset");
            return ga != null ? ga.Get(key) : null;
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

        // Inverse-mapped bilinear sampling of the sprite (scaled, then rotated about its pivot, like the
        // runtime child transform), clipped by the layer's polygons in pose space.
        static void Draw(Img dst, Layer l, Vector2 origin, float k)
        {
            if (l.Sprite == null)
                return;
            var src = Pixels(l.Sprite);
            // Sprite pixel space may differ from the file if the texture was size-capped on import.
            float texScale = src.W / l.Sprite.rect.width;
            Vector2 pivot = l.Sprite.pivot * texScale;
            float r = l.Angle * Mathf.Deg2Rad, cos = Mathf.Cos(r), sin = Mathf.Sin(r);
            float ppuFile = l.Sprite.pixelsPerUnit * texScale;   // file px per world unit at scale 1
            Vector2 at = origin + l.Pos * k;
            float sx = Mathf.Abs(l.Scale.x), sy = Mathf.Abs(l.Scale.y);
            float ext = (src.W + src.H) / ppuFile * Mathf.Max(sx, sy) * k;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(at.x - ext)), x1 = Mathf.Min(dst.W - 1, Mathf.CeilToInt(at.x + ext));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(at.y - ext)), y1 = Mathf.Min(dst.H - 1, Mathf.CeilToInt(at.y + ext));
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var pose = new Vector2((x + 0.5f - origin.x) / k, (y + 0.5f - origin.y) / k);
                if (l.Only != null && !PolygonMask.Contains(l.Only, pose))
                    continue;
                if (l.Hide != null && (PolygonMask.Contains(l.Hide[0], pose) || PolygonMask.Contains(l.Hide[1], pose)))
                    continue;
                // World offset from the pivot → unrotate → unscale → file pixels.
                float dx = (x + 0.5f - at.x) / k, dy = (y + 0.5f - at.y) / k;
                float ux = dx * cos + dy * sin, uy = -dx * sin + dy * cos;
                float fx = ux / l.Scale.x * ppuFile + pivot.x - 0.5f;
                float fy = uy / l.Scale.y * ppuFile + pivot.y - 0.5f;
                if (fx < -1 || fy < -1 || fx > src.W || fy > src.H)
                    continue;
                var c = Sample(src, fx, fy);
                c.a *= l.Alpha;
                if (c.a <= 0.001f)
                    continue;
                c.r *= l.Shade; c.g *= l.Shade; c.b *= l.Shade;
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
            var c0 = Color.Lerp(P(ix, iy), P(ix + 1, iy), fx);
            var c1 = Color.Lerp(P(ix, iy + 1), P(ix + 1, iy + 1), fx);
            var pm = Color.Lerp(c0, c1, fy);
            return pm.a > 0 ? new Color(pm.r / pm.a, pm.g / pm.a, pm.b / pm.a, pm.a) : Color.clear;
        }

        static void Dot(Img img, Vector2 p, Color32 c)
        {
            for (int dy = -4; dy <= 4; dy++)
            for (int dx = -4; dx <= 4; dx++)
            {
                int x = Mathf.RoundToInt(p.x) + dx, y = Mathf.RoundToInt(p.y) + dy;
                int d2 = dx * dx + dy * dy;
                if (x >= 0 && y >= 0 && x < img.W && y < img.H && d2 <= 16)
                    img.Px[y * img.W + x] = d2 >= 9 ? new Color32(0, 0, 0, 255) : c;
            }
        }

        // ---------- check sheets ----------

        public static readonly PoseKind[] SheetPoses = { PoseKind.Idle, PoseKind.Hurt, PoseKind.Victory, PoseKind.Lunge };

        // Rows = musketeers, columns = poses, each cell wearing `items(character)`.
        public static Img Sheet(CharacterRoster roster, Func<CharacterDef, IList<CosmeticDef>> items, int cell, float zoom, Options opt)
        {
            var poses = new List<PoseKind>(SheetPoses);
            if (roster.Characters.Length > 0 && roster.Get(0).HasPose(PoseKind.IdleB))
                poses.Insert(1, PoseKind.IdleB);
            var sheet = new Img(cell * poses.Count, cell * roster.Count);
            for (int ci = 0; ci < roster.Count; ci++)
            for (int pi = 0; pi < poses.Count; pi++)
            {
                var c = roster.Get(ci);
                var o = new Options { Weapons = opt.Weapons, Raised = opt.Raised, Anchors = opt.Anchors, Background = ((ci + pi) & 1) == 0 ? new Color32(70, 70, 78, 255) : new Color32(84, 84, 92, 255) };
                var img = Compose(c, poses[pi], items(c), cell, zoom, o);
                sheet.Blit(img, pi * cell, (roster.Count - 1 - ci) * cell);
            }
            return sheet;
        }

        [MenuItem("Tools/Musketeers/Export Fitting Sheets")]
        public static void ExportSheets()
        {
            ClearCache();
            var roster = AssetDatabase.LoadAssetAtPath<CharacterRoster>("Assets/_Project/Data/CharacterRoster.asset");
            var catalog = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>("Assets/_Project/Data/CosmeticCatalog.asset");
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp/Fitting");
            Directory.CreateDirectory(dir);
            foreach (var item in catalog.Items)
            {
                var sheet = Sheet(roster, c => new[] { item }, 320, 0.6f, new Options { Weapons = false });
                File.WriteAllBytes(Path.Combine(dir, item.Id + ".png"), sheet.EncodePng());
            }
            var outfits = Sheet(roster, c => Outfit(catalog, c.DefaultLoadout), 400, 0.75f, new Options { Weapons = true, Anchors = true });
            File.WriteAllBytes(Path.Combine(dir, "_outfits.png"), outfits.EncodePng());
            Debug.Log("[Fitting] Exported check sheets to " + dir);
        }

        public static List<CosmeticDef> Outfit(CosmeticCatalog catalog, Loadout l)
        {
            var list = new List<CosmeticDef>();
            foreach (var slot in Loadout.Slots)
            {
                var it = catalog.Get(l.Get(slot));
                if (it != null)
                    list.Add(it);
            }
            return list;
        }
    }
}
