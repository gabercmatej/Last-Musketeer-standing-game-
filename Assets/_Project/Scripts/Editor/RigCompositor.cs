using System;
using System.Collections.Generic;
using System.IO;
using Musketeers.Data;
using Musketeers.EditorTools.ArtImport;
using Musketeers.Presentation;
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
            public bool ShowMusket = true;   // with Weapons: draw the musket
            public bool ShowRapier = true;   // with Weapons: draw the rapier
            public bool Raised;          // rapier up (parry)
            public bool MusketAimed;     // musket up in the front hand, rapier hidden
            public float AimExtra;       // north/south tilt on top of the aimed fit
            public bool Anchors;         // bake anchor dots (check sheets)
            public PoseKind? Onion;      // another pose drawn translucent on top
            public Color32 Background = new Color32(0, 0, 0, 0);
        }

        // One drawn layer as the Studio canvas sees it (hand overlay excluded). Key = the CosmeticDef id,
        // "musket", "rapier" or "body". Layers come out sorted back to front.
        public struct ComposedLayer
        {
            public string Key;
            public RigPart Part;
            public bool BackFoot;
            public LayerPlacement Placement;
            public Sprite Sprite;
            public Vector2[] ItemMask;      // item-local polygon, null = none
            public bool ItemMaskInvert;
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
            public Vector2[] ItemMask;   // item-space polygon (sprite-local units at scale 1)
            public bool ItemMaskInvert;
        }

        public static void ClearCache() => Cache.Clear();

        // The feet sit at Origin(size); `zoom` = output px per sprite px.
        public static Vector2 Origin(int size) => new Vector2(size * 0.5f, size * 0.125f);

        public static Img Compose(CharacterDef c, PoseKind asked, IList<CosmeticDef> items, int size, float zoom, Options opt = null) =>
            Compose(c, asked, items, size, zoom, opt, null);

        public static Img Compose(CharacterDef c, PoseKind asked, IList<CosmeticDef> items, int size, float zoom, Options opt, List<ComposedLayer> layersOut)
        {
            opt = opt ?? new Options();
            layersOut?.Clear();
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
            var info = new List<ComposedLayer>();
            bool boots = false;
            if (items != null)
            {
                foreach (var item in items)
                {
                    if (item == null)
                        continue;
                    for (int foot = 0; foot < (item.Slot == CosmeticSlot.Boots ? 2 : 1); foot++)
                    {
                        bool back = foot == 1;
                        var p = CosmeticLayout.Place(art, fit, c.Id, kind, item, back);
                        if (!p.Valid)
                            continue;
                        boots |= item.Slot == CosmeticSlot.Boots;
                        var l = From(p);
                        SetMask(ref l, CosmeticLayout.ItemMask(item, art, kind));
                        layers.Add(l);
                        info.Add(Info(item.Id, CosmeticLayout.PartFor(item.Slot, back), back, p, l));
                    }
                }
            }
            var bodyHide = boots ? new[] { fit.BootPathBack.Flatten(), fit.BootPathFront.Flatten() } : null;
            layers.Add(new Layer { Order = 0, Sprite = art.Sprite, Pos = Vector2.zero, Scale = Vector2.one, Shade = 1f, Alpha = 1f, Hide = bodyHide });
            info.Add(new ComposedLayer { Key = "body", Part = RigPart.Body, Placement = new LayerPlacement { Valid = true, Sprite = art.Sprite, Scale = Vector2.one, Order = 0, Shade = 1f }, Sprite = art.Sprite });
            if (opt.Weapons)
            {
                var catalog = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>("Assets/_Project/Data/CosmeticCatalog.asset");
                WeaponDef musketTemp = null, rapierTemp = null;
                var musketDef = catalog != null && catalog.Musket != null && catalog.Musket.Sprite != null ? catalog.Musket : (musketTemp = WeaponDef.Temporary(Sprite(GameArt.Musket), true));
                var rapierDef = catalog != null && catalog.Rapier != null && catalog.Rapier.Sprite != null ? catalog.Rapier : (rapierTemp = WeaponDef.Temporary(Sprite(GameArt.Rapier), false));
                bool musketShown = false, rapierShown = false;
                if (opt.ShowMusket)
                {
                    var m = CosmeticLayout.Weapon(fit, musketDef, c.Id, kind, opt.MusketAimed, opt.AimExtra);
                    if (m.Valid)
                    {
                        var l = From(m);
                        SetMask(ref l, musketDef.For(opt.MusketAimed).Masks.For(kind, false));
                        layers.Add(l);
                        info.Add(Info("musket", RigPart.Musket, false, m, l));
                        musketShown = true;
                    }
                }
                var r = CosmeticLayout.Weapon(fit, rapierDef, c.Id, kind, opt.Raised);
                rapierShown = opt.ShowRapier && WeaponState.RapierVisible(true, opt.MusketAimed, r.Valid);
                if (rapierShown)
                {
                    var l = From(r);
                    SetMask(ref l, rapierDef.For(opt.Raised).Masks.For(kind, false));
                    layers.Add(l);
                    info.Add(Info("rapier", RigPart.Rapier, false, r, l));
                }
                var handPoly = fit.HandPath != null ? fit.HandPath.Flatten() : null;
                if (WeaponState.HandOverlayVisible(rapierShown, musketShown && opt.MusketAimed, PolygonMask.Valid(handPoly)))
                    layers.Add(new Layer { Order = CosmeticLayout.Order(RigPart.Hand), Sprite = art.Sprite, Scale = Vector2.one, Shade = 1f, Alpha = 1f, Only = handPoly });
                if (musketTemp != null) UnityEngine.Object.DestroyImmediate(musketTemp);
                if (rapierTemp != null) UnityEngine.Object.DestroyImmediate(rapierTemp);
            }
            StableSort(layers, l => l.Order);
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
            if (layersOut != null)
            {
                StableSort(info, l => l.Placement.Order);
                layersOut.AddRange(info);
            }
            return img;
        }

        static void StableSort<T>(List<T> list, Func<T, int> key)
        {
            var idx = new int[list.Count];
            for (int i = 0; i < idx.Length; i++) idx[i] = i;
            var copy = list.ToArray();
            Array.Sort(idx, (a, b) => { int d = key(copy[a]).CompareTo(key(copy[b])); return d != 0 ? d : a.CompareTo(b); });
            for (int i = 0; i < idx.Length; i++) list[i] = copy[idx[i]];
        }

        static void SetMask(ref Layer l, BezierPath mask)
        {
            if (mask != null && mask.Valid)
            {
                var poly = mask.Flatten();
                if (PolygonMask.Valid(poly))
                {
                    l.ItemMask = poly;
                    l.ItemMaskInvert = mask.Invert;
                }
            }
        }

        static ComposedLayer Info(string key, RigPart part, bool back, LayerPlacement p, Layer l) =>
            new ComposedLayer { Key = key, Part = part, BackFoot = back, Placement = p, Sprite = p.Sprite, ItemMask = l.ItemMask, ItemMaskInvert = l.ItemMaskInvert };

        // ---------- pose space <-> item-local space (same math as Draw) ----------

        // Item-local units = world units from the sprite pivot at scale 1 (what the runtime child SpriteMask sees).
        public static Vector2 ToLocal(LayerPlacement p, Vector2 posePoint)
        {
            float r = p.Angle * Mathf.Deg2Rad, cos = Mathf.Cos(r), sin = Mathf.Sin(r);
            float dx = posePoint.x - p.Position.x, dy = posePoint.y - p.Position.y;
            float ux = dx * cos + dy * sin, uy = -dx * sin + dy * cos;
            return new Vector2(ux / p.Scale.x, uy / p.Scale.y);
        }

        public static Vector2 ToPose(LayerPlacement p, Vector2 local)
        {
            float r = p.Angle * Mathf.Deg2Rad, cos = Mathf.Cos(r), sin = Mathf.Sin(r);
            float ux = local.x * p.Scale.x, uy = local.y * p.Scale.y;
            return new Vector2(ux * cos - uy * sin + p.Position.x, ux * sin + uy * cos + p.Position.y);
        }

        // Index of the top-most item/weapon layer with a visible, unmasked pixel at `posePoint` (-1 = none).
        // The body is ignored.
        public static int HitTest(IList<ComposedLayer> layers, Vector2 posePoint, float alphaMin = 0.1f)
        {
            if (layers == null)
                return -1;
            for (int i = layers.Count - 1; i >= 0; i--)
            {
                var l = layers[i];
                if (l.Key == "body" || l.Sprite == null || !l.Placement.Valid)
                    continue;
                var local = ToLocal(l.Placement, posePoint);
                if (l.ItemMask != null && PolygonMask.Contains(l.ItemMask, local) != l.ItemMaskInvert)
                    continue;
                var src = Pixels(l.Sprite);
                float texScale = src.W / l.Sprite.rect.width;
                Vector2 pivot = l.Sprite.pivot * texScale;
                float ppuFile = l.Sprite.pixelsPerUnit * texScale;
                float fx = local.x * ppuFile + pivot.x - 0.5f, fy = local.y * ppuFile + pivot.y - 0.5f;
                if (fx < -1 || fy < -1 || fx > src.W || fy > src.H)
                    continue;
                if (Sample(src, fx, fy).a >= alphaMin)
                    return i;
            }
            return -1;
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
                // Item mask, in sprite-local units at scale 1; the signed scale handles the mirrored far boot.
                if (l.ItemMask != null && PolygonMask.Contains(l.ItemMask, new Vector2(ux / l.Scale.x, uy / l.Scale.y)) != l.ItemMaskInvert)
                    continue;
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

        // Rows = musketeers, columns = (pose, options) pairs, each cell wearing `items(character)`.
        public static Img Sheet(CharacterRoster roster, Func<CharacterDef, IList<CosmeticDef>> items, int cell, float zoom, IList<KeyValuePair<PoseKind, Options>> columns)
        {
            var sheet = new Img(cell * columns.Count, cell * roster.Count);
            for (int ci = 0; ci < roster.Count; ci++)
            for (int pi = 0; pi < columns.Count; pi++)
            {
                var c = roster.Get(ci);
                var o = columns[pi].Value;
                var cellOpt = new Options
                {
                    Weapons = o.Weapons, ShowMusket = o.ShowMusket, ShowRapier = o.ShowRapier, Raised = o.Raised,
                    MusketAimed = o.MusketAimed, AimExtra = o.AimExtra, Anchors = o.Anchors,
                    Background = ((ci + pi) & 1) == 0 ? new Color32(70, 70, 78, 255) : new Color32(84, 84, 92, 255),
                };
                var img = Compose(c, columns[pi].Key, items(c), cell, zoom, cellOpt);
                sheet.Blit(img, pi * cell, (roster.Count - 1 - ci) * cell);
            }
            return sheet;
        }

        static List<KeyValuePair<PoseKind, Options>> Columns(Func<PoseKind, Options> make, params PoseKind[] poses)
        {
            var list = new List<KeyValuePair<PoseKind, Options>>();
            foreach (var p in poses)
                list.Add(new KeyValuePair<PoseKind, Options>(p, make(p)));
            return list;
        }

        public static string DocsSheetDir() =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, "docs/playtest/fitting-v0.2.1b");

        [MenuItem("Tools/Musketeers/Export Fitting Sheets")]
        public static void ExportSheets() => ExportSheets(DocsSheetDir());

        public static void ExportSheets(string dir)
        {
            ClearCache();
            var roster = AssetDatabase.LoadAssetAtPath<CharacterRoster>("Assets/_Project/Data/CharacterRoster.asset");
            var catalog = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>("Assets/_Project/Data/CosmeticCatalog.asset");
            Directory.CreateDirectory(dir);
            var poses = FitEdit.StudioPoses;
            var none = new CosmeticDef[0];
            foreach (var item in catalog.Items)
            {
                if (item == null)
                    continue;
                var sheet = Sheet(roster, c => new[] { item }, 320, 0.6f, Columns(p => new Options { Weapons = false }, poses));
                File.WriteAllBytes(Path.Combine(dir, item.Id + ".png"), sheet.EncodePng());
            }
            var musketCols = Columns(p => new Options { ShowRapier = false }, poses);
            musketCols.AddRange(Columns(p => new Options { ShowRapier = false, MusketAimed = true }, poses));
            File.WriteAllBytes(Path.Combine(dir, "musket.png"), Sheet(roster, c => none, 320, 0.6f, musketCols).EncodePng());
            var rapierCols = Columns(p => new Options { ShowMusket = false }, poses);
            rapierCols.AddRange(Columns(p => new Options { ShowMusket = false, Raised = true }, poses));
            File.WriteAllBytes(Path.Combine(dir, "rapier.png"), Sheet(roster, c => none, 320, 0.6f, rapierCols).EncodePng());
            var outfits = Sheet(roster, c => Outfit(catalog, c.DefaultLoadout), 400, 0.75f, Columns(p => new Options { Weapons = true, Anchors = true }, poses));
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
