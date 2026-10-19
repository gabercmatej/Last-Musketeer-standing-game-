using System.Collections.Generic;
using System.IO;
using System.Linq;
using Musketeers.Data;
using UnityEditor;
using UnityEngine;

namespace Musketeers.EditorTools.ArtImport
{
    // Second half of Import Art: points the data assets at the processed files. Creates what's missing,
    // updates sprites every run, and only fills anchors/offsets that aren't set yet (or on a reset), so
    // Matej's Fitting Editor work survives re-imports.
    public static class ArtWiring
    {
        const string Art = "Assets/_Project/Art";
        const string Audio = "Assets/_Project/Audio";
        const string Data = "Assets/_Project/Data";
        const float Ppu = 256f;

        public static void Run(bool reset, List<string> log)
        {
            var gameArt = WireGameArt(log);
            var catalog = WireCosmetics(reset, log);
            WireCharacters(reset, log);
            WireMaps(log);
            WireAudio(log);
            EditorUtility.SetDirty(gameArt);
            EditorUtility.SetDirty(catalog);
        }

        // ---------- shared sprites ----------

        static GameArt WireGameArt(List<string> log)
        {
            var ga = Ensure<GameArt>($"{Data}/GameArt.asset");
            ga.Sprites.Clear();
            foreach (var folder in new[] { "Objects", "Weapons", "FX", "UI" })
            {
                foreach (var path in Files($"{Art}/{folder}", "*.png", "*.jpg"))
                {
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite == null)
                        continue;
                    string key = path.Substring(Art.Length + 1);
                    key = key.Substring(0, key.LastIndexOf('.'));
                    ga.Sprites.Add(new GameArt.Entry { Key = key, Sprite = sprite });
                }
            }
            ga.Invalidate();
            log.Add($"GameArt: {ga.Sprites.Count} sprites");
            return ga;
        }

        // ---------- cosmetics ----------

        static readonly Dictionary<string, string> Folder = new Dictionary<string, string>
        {
            { "hats", "Hat" }, { "face", "Face" }, { "capes", "Cape" }, { "boots", "Boots" },
        };

        static CosmeticCatalog WireCosmetics(bool reset, List<string> log)
        {
            var catalog = Ensure<CosmeticCatalog>($"{Data}/CosmeticCatalog.asset");
            catalog.Items.Clear();
            foreach (var slotFolder in new[] { "hats", "face", "capes", "boots" })
            {
                var slot = (CosmeticSlot)System.Enum.Parse(typeof(CosmeticSlot), Folder[slotFolder]);
                var ids = new List<string>(ArtImporter.SheetNames[slotFolder]);
                foreach (var path in Files($"{Art}/Cosmetics/{slotFolder}", "*.png"))
                {
                    string n = Path.GetFileNameWithoutExtension(path);
                    if (!n.EndsWith("_front") && !ids.Contains(n))
                        ids.Add(n);
                }
                foreach (var id in ids)
                {
                    var side = AssetDatabase.LoadAssetAtPath<Sprite>($"{Art}/Cosmetics/{slotFolder}/{id}.png");
                    var front = AssetDatabase.LoadAssetAtPath<Sprite>($"{Art}/Cosmetics/{slotFolder}/{id}_front.png");
                    if (side == null && front == null)
                        continue;
                    var def = Ensure<CosmeticDef>($"{Data}/Cosmetics/{id}.asset");
                    bool fresh = string.IsNullOrEmpty(def.Id) || def.Id == "item";
                    def.Id = id;
                    def.Slot = slot;
                    def.Sprite = side != null ? side : front;
                    def.FrontSprite = front;
                    if (fresh || string.IsNullOrEmpty(def.DisplayName) || def.DisplayName == "Item")
                        def.DisplayName = CosmeticTuning.DisplayName(id);
                    if (fresh || reset || !def.Tuned)
                        CosmeticTuning.Apply(def);
                    EditorUtility.SetDirty(def);
                    catalog.Items.Add(def);
                }
            }
            log.Add($"Cosmetics: {catalog.Items.Count} items ({string.Join(", ", catalog.Items.Select(i => i.Id))})");
            return catalog;
        }

        // ---------- characters ----------

        // Matched to Matej's art (Oct 19): Matej purple, Matic crimson, Aljaž blue, Nik green.
        static readonly Dictionary<string, Color> ArtColors = new Dictionary<string, Color>
        {
            { "matej", new Color(0.62f, 0.38f, 0.98f) },
            { "matic", new Color(0.93f, 0.27f, 0.30f) },
            { "aljaz", new Color(0.27f, 0.55f, 1.00f) },
            { "nik", new Color(0.27f, 0.80f, 0.40f) },
        };

        static void WireCharacters(bool reset, List<string> log)
        {
            var roster = AssetDatabase.LoadAssetAtPath<CharacterRoster>($"{Data}/CharacterRoster.asset");
            if (roster == null)
            {
                log.Add("No CharacterRoster yet: run Tools/Musketeers/Build Main Scene, then Import Art again");
                return;
            }
            foreach (var c in roster.Characters)
            {
                if (c == null)
                    continue;
                if (string.IsNullOrEmpty(c.Id) || c.Id == "musketeer")
                    c.Id = ArtImporter.Ascii(c.name).ToLowerInvariant();
                string dir = $"{Art}/Characters/{c.Id}";
                Sprite S(string n) => AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/{n}.png");
                c.Portrait = S("portrait") ?? c.Portrait;
                c.Idle = c.Idle ?? new PoseArt();
                c.IdleB = c.IdleB ?? new PoseArt();
                c.Hurt = c.Hurt ?? new PoseArt();
                c.Victory = c.Victory ?? new PoseArt();
                c.Lunge = c.Lunge ?? new PoseArt();
                SetPose(c.Idle, S("body"), false);
                SetPose(c.IdleB, S("idle_b"), false);
                SetPose(c.Hurt, S("hurt"), false);
                SetPose(c.Victory, S("victory"), true);
                SetPose(c.Lunge, S("lunge"), false);
                WireFit(c, reset, log);
                if (ArtColors.TryGetValue(c.Id, out var col) && (reset || IsDay3Color(c.Color)))
                    c.Color = col;
                if (reset || string.IsNullOrEmpty(c.DefaultLoadout.Hat))
                    c.DefaultLoadout = CosmeticTuning.DefaultLoadout(c.Id);
                EditorUtility.SetDirty(c);
            }
            log.Add("Characters wired: " + string.Join(", ", roster.Characters.Where(c => c != null).Select(c => c.Id)));
        }

        static bool IsDay3Color(Color c)
        {
            Color[] day3 = { new Color(0.20f, 0.52f, 1.00f), new Color(0.20f, 0.80f, 0.36f), new Color(0.70f, 0.38f, 1.00f), new Color(1.00f, 0.60f, 0.12f) };
            foreach (var d in day3)
            {
                if (Mathf.Abs(c.r - d.r) + Mathf.Abs(c.g - d.g) + Mathf.Abs(c.b - d.b) < 0.02f)
                    return true;
            }
            return false;
        }

        static void SetPose(PoseArt pose, Sprite sprite, bool front)
        {
            pose.Sprite = sprite;
            pose.Front = front;
        }

        // Data/Fits/<id>.asset: created once; a pose without a fit gets the committed default (FitDefaults.json)
        // or, for a pose nobody has fitted yet, the silhouette estimate. Existing fits are only replaced on a reset.
        static void WireFit(CharacterDef c, bool reset, List<string> log)
        {
            var fit = Ensure<CharacterFit>($"{Data}/Fits/{c.Id}.asset");
            c.Fit = fit;
            foreach (PoseKind kind in System.Enum.GetValues(typeof(PoseKind)))
            {
                if (!c.HasPose(kind) || (fit.Get(kind) != null && !reset))
                    continue;
                var pose = fit.GetOrAdd(kind);
                if (CosmeticTuning.TryPose(c.Id, kind, out var seed))
                    pose.CopyFrom(seed);
                else if (Estimate(c.Id, kind, out var est))
                {
                    pose.CopyFrom(est);
                    log.Add($"Fit {c.Id}/{kind}: silhouette estimate (fit it in Musketeers/Fitting Editor)");
                }
            }
            EditorUtility.SetDirty(fit);
        }

        static bool Estimate(string id, PoseKind kind, out PoseFit fit)
        {
            fit = null;
            string key = kind == PoseKind.Idle ? "body" : kind == PoseKind.IdleB ? "idle_b" : kind.ToString().ToLowerInvariant();
            if (!ArtImporter.AnchorsFound.TryGetValue(id, out var poses) || !poses.TryGetValue(key, out var a))
                return false;
            Vector2 U(Vector2 px) => new Vector2((px.x - CharacterProcessor.FeetX) / Ppu, (px.y - CharacterProcessor.FeetY) / Ppu);
            fit = new PoseFit { Pose = kind };
            fit.HeadTop = new Anchor(U(a.HeadTop).x, U(a.HeadTop).y, a.HeadAngle);
            fit.Face = new Anchor(U(a.Face).x, U(a.Face).y, a.HeadAngle);
            fit.Back = new Anchor(U(a.Back).x, U(a.Back).y);
            fit.HandBack = new Anchor(U(a.Hand).x - 0.35f, U(a.Hand).y, -38f, 0.72f);
            fit.HandFront = new Anchor(U(a.Hand).x, U(a.Hand).y, -70f, 0.72f);
            return true;
        }

        // ---------- maps and themes ----------

        static void WireMaps(List<string> log)
        {
            // Day 3's single theme becomes the Courtyard theme (same asset, so scene references survive).
            if (!AssetDatabase.IsValidFolder($"{Data}/Themes"))
                AssetDatabase.CreateFolder(Data, "Themes");
            if (File.Exists($"{Data}/BoardTheme.asset") && !File.Exists($"{Data}/Themes/Courtyard.asset"))
                AssetDatabase.MoveAsset($"{Data}/BoardTheme.asset", $"{Data}/Themes/Courtyard.asset");

            var catalog = Ensure<MapCatalog>($"{Data}/MapCatalog.asset");
            var maps = new List<MapDef>();
            foreach (var (id, name, desc) in new[]
                     {
                         ("courtyard", "The Courtyard", "Pillars, crates and four little pits. The classic."),
                         ("bridge", "The Bridge", "A wooden bridge over the river. Mind the edges."),
                     })
            {
                string title = char.ToUpperInvariant(id[0]) + id.Substring(1);
                var theme = Ensure<BoardTheme>($"{Data}/Themes/{title}.asset");
                string dir = $"{Art}/Board/{id}";
                Sprite S(string n) => AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/{n}");
                theme.FloorVariants = Files(dir, "floor_*.png").Select(p => AssetDatabase.LoadAssetAtPath<Sprite>(p)).Where(s => s != null).ToArray();
                theme.Pillar = S("pillar.png");
                theme.Crate = S("crate.png");
                theme.CrateBroken = S("crate_broken.png");
                theme.Pit = S("pit.png");
                theme.CrackOverlay = S("crack_overlay.png");
                theme.WarnOverlay = S("warn_overlay.png");
                theme.BackgroundArt = S("background.jpg");
                if (id == "bridge")
                    theme.Background = new Color(0.10f, 0.22f, 0.30f);
                EditorUtility.SetDirty(theme);

                var map = Ensure<MapDef>($"{Data}/Maps/{title}.asset");
                map.Id = id;
                map.DisplayName = name;
                if (string.IsNullOrEmpty(map.Description))
                    map.Description = desc;
                map.Layout = AssetDatabase.LoadAssetAtPath<TextAsset>($"{Data}/Maps/{id}.txt");
                map.Theme = theme;
                if (map.Layout == null)
                    log.Add($"MISSING {Data}/Maps/{id}.txt");
                EditorUtility.SetDirty(map);
                maps.Add(map);
            }
            catalog.Maps = maps.ToArray();
            EditorUtility.SetDirty(catalog);
            log.Add("Maps: " + string.Join(", ", maps.Select(m => m.Id)));
        }

        // ---------- audio ----------

        static void WireAudio(List<string> log)
        {
            var lib = Ensure<AudioLibrary>($"{Data}/AudioLibrary.asset");
            var groups = new SortedDictionary<string, List<AudioClip>>();
            foreach (var path in Files(Audio, "*.ogg", "*.wav"))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null)
                    continue;
                string rel = path.Substring(Audio.Length + 1);
                string dir = Path.GetDirectoryName(rel).Replace('\\', '/');
                string key = dir + "/" + AudioLibrary.StripVariant(Path.GetFileNameWithoutExtension(rel));
                if (!groups.TryGetValue(key, out var list))
                    groups[key] = list = new List<AudioClip>();
                list.Add(clip);
            }
            lib.Entries = groups.Select(g => new AudioLibrary.Entry { Key = g.Key, Clips = g.Value.OrderBy(c => c.name).ToArray() }).ToList();
            lib.Invalidate();
            EditorUtility.SetDirty(lib);
            log.Add($"AudioLibrary: {lib.Entries.Count} keys, {groups.Sum(g => g.Value.Count)} clips");
        }

        // ---------- helpers ----------

        static IEnumerable<string> Files(string assetDir, params string[] patterns)
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string full = Path.Combine(root, assetDir);
            if (!Directory.Exists(full))
                return Enumerable.Empty<string>();
            return patterns.SelectMany(pt => Directory.GetFiles(full, pt, SearchOption.AllDirectories))
                .Select(f => f.Substring(root.Length + 1).Replace('\\', '/'))
                .OrderBy(f => f);
        }

        public static T Ensure<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null)
                return a;
            string dir = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(dir).Replace('\\', '/'), Path.GetFileName(dir));
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }
    }
}
