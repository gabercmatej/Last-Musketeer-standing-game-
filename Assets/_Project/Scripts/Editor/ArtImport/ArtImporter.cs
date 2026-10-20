using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Musketeers.EditorTools.ArtImport
{
    // Tools/Musketeers/Import Art: turns Matej's source folders (root Art/ and Audio/, never edited) into
    // game-ready copies under Assets/_Project and wires them into the data assets. Re-runnable: files
    // are only rewritten when their bytes change, and hand-tuned anchors/offsets are kept.
    public static class ArtImporter
    {
        public const string DstArt = "Assets/_Project/Art";
        static string Root => Directory.GetParent(Application.dataPath).FullName;
        static string SrcArt => Path.Combine(Root, "Art");

        // Per-pose size relative to the idle body (the generator fit every pose to its canvas height).
        static readonly Dictionary<string, float> PoseScale = new Dictionary<string, float>
        {
            { "body", 1f }, { "idle_b", 1f }, { "hurt", 1f }, { "lunge", 1f }, { "victory", 1.04f },
        };

        // Sheet item names in reading order (ART_GUIDE §4.2 slots; Matej's sheets, Oct 19).
        public static readonly Dictionary<string, string[]> SheetNames = new Dictionary<string, string[]>
        {
            { "hats", new[] { "hat_plumed_purple", "hat_plumed_green", "hat_plumed_blue", "hat_plumed_red", "hat_plumed_white",
                              "hat_cowboy", "hat_propeller", "hat_helmet_pink", "hat_bat_mask" } },
            { "capes", new[] { "cape_purple", "cape_green", "cape_blue", "cape_red", "cape_white", "cape_black", "cape_slovenia", "cape_bat" } },
            { "boots", new[] { "boots_black", "boots_purple", "boots_green", "boots_blue", "boots_red", "boots_brown",
                               "boots_flipflops", "boots_crocs", "boots_sneakers", "boots_barefoot" } },
            { "face", new[] { "face_moustache", "face_beard", "face_monocle", "face_sunglasses", "face_mask_purple",
                              "face_mask_green", "face_mask_blue", "face_mask_red", "face_mask_black" } },
        };

        // Source sheet (relative to Art/Cosmetics) → (slot folder, front view?)
        static readonly (string src, string slot, bool front)[] Sheets =
        {
            ("hats-sheet.png", "hats", false), ("capes-sheet.png", "capes", false), ("boots-sheet.png", "boots", false),
            ("face/face-base-sheet.png", "face", false),
            ("victory/hats-front-sheet.png", "hats", true), ("victory/capes-front-sheet.png", "capes", true),
            ("victory/boots-front-sheet.png", "boots", true), ("face/face-victory-sheet.png", "face", true),
        };

        static readonly HashSet<string> Skip = new HashSet<string> { "Board/courtyard/background-v1.png" };

        // Results the wiring step reads (canvas-pixel anchors per character id and pose).
        public static readonly Dictionary<string, Dictionary<string, CharacterProcessor.Anchors>> AnchorsFound =
            new Dictionary<string, Dictionary<string, CharacterProcessor.Anchors>>();

        static readonly List<string> Log = new List<string>();
        static readonly HashSet<string> Used = new HashSet<string>();
        static int _written, _unchanged;

        [MenuItem("Tools/Musketeers/Import Art")]
        public static void ImportAll() => Run(resetTuning: false);

        [MenuItem("Tools/Musketeers/Import Art (reset anchors and offsets)")]
        public static void ImportReset()
        {
            if (EditorUtility.DisplayDialog("Reset tuning?", "Re-import and overwrite every character anchor and cosmetic offset with the importer defaults?", "Reset", "Cancel"))
                Run(resetTuning: true);
        }

        public static void Run(bool resetTuning)
        {
            Log.Clear();
            Used.Clear();
            AnchorsFound.Clear();
            _written = _unchanged = 0;
            var t0 = DateTime.Now;
            if (!Directory.Exists(SrcArt))
            {
                Debug.LogError("[ArtImporter] No source folder " + SrcArt);
                return;
            }
            AssetDatabase.StartAssetEditing();
            try
            {
                float cosmeticScale = Characters();
                Cosmetics(cosmeticScale);
                Board();
                Objects();
                Simple("FX", 256, 256, square: false);
                Simple("UI/icons/actions", 232, 232, square: true);
                Simple("UI/icons/status", 232, 232, square: true);
                Simple("UI/badges", 232, 232, square: true);
                Simple("UI/emotes", 232, 232, square: true);
                Ui();
                Unused();
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            AudioCopier.Run(Log);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ArtWiring.Run(resetTuning, Log);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ArtImporter] Done in {(DateTime.Now - t0).TotalSeconds:0.0}s: {_written} files written, {_unchanged} unchanged.\n" + string.Join("\n", Log));
        }

        // ---------- characters ----------

        static float Characters()
        {
            string dir = Path.Combine(SrcArt, "Characters");
            var scales = new List<float>();
            foreach (var charDir in Directory.GetDirectories(dir).OrderBy(d => d))
            {
                string id = Ascii(Path.GetFileName(charDir)).ToLowerInvariant();
                var files = Directory.GetFiles(charDir, "*.png");
                string bodySrc = files.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f) == "body")
                                 ?? files.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).StartsWith("body"));
                if (bodySrc == null)
                {
                    Log.Add($"MISSING Characters/{id}/body.png: keeping the placeholder");
                    continue;
                }
                var body = Img.Load(bodySrc);
                body.Clean();
                float scale = (float)CharacterProcessor.IdleHeight / CharacterProcessor.FigureHeight(body);
                scales.Add(scale);
                AnchorsFound[id] = new Dictionary<string, CharacterProcessor.Anchors>();
                if (Path.GetFileNameWithoutExtension(bodySrc) != "body")
                    Log.Add($"Characters/{id}: using {Path.GetFileName(bodySrc)} as body.png");

                foreach (var f in files)
                {
                    string name = Path.GetFileNameWithoutExtension(f);
                    string pose = f == bodySrc ? "body" : name;
                    if (pose == "portrait")
                    {
                        var p = Img.Load(f);
                        p.Clean();
                        WritePng($"Characters/{id}/portrait.png", p.Resize(512, 512), f);
                        continue;
                    }
                    if (!PoseScale.TryGetValue(pose, out float ps))
                    {
                        if (f != bodySrc)
                            Log.Add($"UNEXPECTED Characters/{id}/{name}.png (not a known pose): skipped");
                        continue;
                    }
                    var img = pose == "body" ? body : Img.Load(f);
                    if (pose != "body")
                        img.Clean();
                    bool front = pose == "victory";
                    var r = CharacterProcessor.Place(img, scale * ps, front, pose, CharacterProcessor.IdleHeight);
                    if (r.Clipped)
                        Log.Add($"WARN Characters/{id}/{pose}: pose wider than the 512 canvas, edges clipped");
                    WritePng($"Characters/{id}/{pose}.png", r.Image, f);
                    AnchorsFound[id][pose] = r.Anchors;
                }
                foreach (var need in new[] { "hurt", "victory", "portrait" })
                {
                    if (!files.Any(f => Path.GetFileNameWithoutExtension(f) == need))
                        Log.Add($"MISSING Characters/{id}/{need}.png");
                }
            }
            return scales.Count > 0 ? scales.Average() : 0.28f;
        }

        // ---------- cosmetics ----------

        static void Cosmetics(float scale)
        {
            string dir = Path.Combine(SrcArt, "Cosmetics");
            foreach (var (src, slot, front) in Sheets)
            {
                string path = Path.Combine(dir, src);
                if (!File.Exists(path))
                {
                    Log.Add($"MISSING Cosmetics/{src}");
                    continue;
                }
                var sheet = Img.Load(path);
                sheet.Clean();
                var items = SheetSlicer.Slice(sheet);
                var names = SheetNames[slot];
                if (items.Count != names.Length)
                    Log.Add($"WARN Cosmetics/{src}: found {items.Count} items, expected {names.Length}; extra/missing ones are numbered");
                var sliced = new List<string>();
                for (int i = 0; i < items.Count; i++)
                {
                    string name = i < names.Length ? names[i] : $"{slot}_{i + 1:00}";
                    string file = name + (front ? "_front" : "") + ".png";
                    var img = items[i];
                    // One boot, rendered twice (FIXES_v0.2 §1.2): keep a single boot of the pair.
                    if (slot == "boots")
                        img = front ? BootCrop.FrontBoot(img) : BootCrop.SideBoot(img, name);
                    WritePng($"Cosmetics/{slot}/{file}", img.Scale(scale).PadToMultipleOf4(), path);
                    sliced.Add(name);
                }
                Log.Add($"Sliced Cosmetics/{src} → {string.Join(", ", sliced)}{(front ? " (front)" : "")}");
            }
            // Loose items drawn on their own (ART_GUIDE names, e.g. Cosmetics/hats/hat_tricorne.png).
            foreach (var slotDir in Directory.GetDirectories(dir))
            {
                string slot = Path.GetFileName(slotDir);
                if (!SheetNames.ContainsKey(slot))
                    continue;
                foreach (var f in Directory.GetFiles(slotDir, "*.png"))
                {
                    if (f.Contains("sheet"))
                        continue;
                    var img = Img.Load(f);
                    img.Clean();
                    WritePng($"Cosmetics/{slot}/{Path.GetFileName(f).ToLowerInvariant()}", img.Trim().Scale(scale).PadToMultipleOf4(), f);
                }
            }
        }

        // ---------- board ----------

        static void Board()
        {
            foreach (var mapDir in Directory.GetDirectories(Path.Combine(SrcArt, "Board")))
            {
                string map = Ascii(Path.GetFileName(mapDir)).ToLowerInvariant();
                string F(string n) => Path.Combine(mapDir, n);
                // Props share one factor per map so pillar and crate keep their relative size; a crate is
                // 92% of a tile wide.
                float propScale = 0.25f;
                if (File.Exists(F("crate.png")))
                {
                    var crate = Img.Load(F("crate.png"));
                    crate.Clean();
                    var c = crate.Trim();
                    propScale = 0.92f * 256f / c.W;
                    WritePng($"Board/{map}/crate.png", c.Scale(propScale).PadToMultipleOf4(), F("crate.png"));
                }
                foreach (var f in Directory.GetFiles(mapDir, "*.png"))
                {
                    string name = Path.GetFileNameWithoutExtension(f);
                    if (Skip.Contains($"Board/{Path.GetFileName(mapDir)}/{Path.GetFileName(f)}"))
                    {
                        Used.Add(f);
                        Log.Add($"Ignored Board/{Path.GetFileName(mapDir)}/{Path.GetFileName(f)} (older take)");
                        continue;
                    }
                    var img = Img.Load(f);
                    if (name.StartsWith("floor") || name == "pit")
                        WritePng($"Board/{map}/{name}.png", img.Resize(256, 256), f);
                    else if (name == "crack_overlay" || name == "warn_overlay")
                    {
                        img.Clean();
                        WritePng($"Board/{map}/{name}.png", img.Resize(256, 256), f);
                    }
                    else if (name == "pillar" || name == "crate_broken")
                    {
                        img.Clean();
                        WritePng($"Board/{map}/{name}.png", img.Trim().Scale(propScale).PadToMultipleOf4(), f);
                    }
                    else if (name == "background")
                        WriteJpg($"Board/{map}/background.jpg", FitMax(img, 2048), f);
                    else if (name != "crate")
                        Log.Add($"UNEXPECTED Board/{map}/{name}.png: skipped");
                }
                foreach (var need in new[] { "floor_01", "pillar", "crate", "pit" })
                {
                    if (!File.Exists(F(need + ".png")))
                        Log.Add($"MISSING Board/{map}/{need}.png");
                }
            }
        }

        // ---------- objects and weapons ----------

        static void Objects()
        {
            Prop("Objects/keg.png", 184, 0, "Objects/keg.png");
            Prop("Objects/crown.png", 192, 0, "Objects/crown.png");
            Prop("Objects/lasso.png", 160, 0, "Objects/lasso.png");
            Prop("Weapons/musket.png", 240, 0, "Weapons/musket.png");
            Prop("Weapons/rapier.png", 220, 0, "Weapons/rapier.png");
            string shadow = Path.Combine(SrcArt, "Objects/shadow.png");
            if (File.Exists(shadow))
            {
                // A soft ellipse: no speck cleaning (it's all faint), just trim and size it under a unit.
                var s = Img.Load(shadow).Trim(6);
                WritePng("Objects/shadow.png", s.Resize(224, Math.Max(16, Mathf.RoundToInt(224f * s.H / s.W))).PadToMultipleOf4(), shadow);
            }
            if (!File.Exists(Path.Combine(SrcArt, "Objects/grapple_hook.png")) && File.Exists(Path.Combine(SrcArt, "Objects/lasso.png")))
                Log.Add("Objects/grapple_hook.png missing: the lasso is used as Grappin's hook");
        }

        static void Prop(string src, int width, int height, string dst)
        {
            string path = Path.Combine(SrcArt, src);
            if (!File.Exists(path))
            {
                Log.Add("MISSING " + src);
                return;
            }
            var img = Img.Load(path);
            img.Clean();
            var t = img.Trim();
            var scaled = height > 0 ? t.Fit(width, height) : t.Scale((float)width / t.W);
            WritePng(dst, scaled.PadToMultipleOf4(), path);
        }

        // Icons, badges, emotes, FX: trimmed, fitted into a box, optionally centred on a square canvas.
        static void Simple(string folder, int boxW, int boxH, bool square)
        {
            string dir = Path.Combine(SrcArt, folder);
            if (!Directory.Exists(dir))
            {
                Log.Add("MISSING folder " + folder);
                return;
            }
            foreach (var f in Directory.GetFiles(dir, "*.png"))
            {
                var img = Img.Load(f);
                img.Clean();
                var fit = img.Trim().Fit(boxW, boxH);
                var outImg = square ? fit.Pad(256, 256) : fit.PadToMultipleOf4();
                WritePng($"{folder}/{Path.GetFileName(f).ToLowerInvariant()}", outImg, f);
            }
        }

        // ---------- UI ----------

        static void Ui()
        {
            void Fit(string src, int w, int h)
            {
                string path = Path.Combine(SrcArt, src);
                if (!File.Exists(path)) { Log.Add("MISSING " + src); return; }
                var img = Img.Load(path);
                img.Clean();
                WritePng(src, img.Trim().Fit(w, h).PadToMultipleOf4(), path);
            }
            Fit("UI/logo/logo.png", 1024, 1024);
            Fit("UI/logo/logo_small.png", 512, 512);
            Fit("UI/panels/panel.png", 512, 512);
            Fit("UI/panels/button.png", 512, 512);
            Fit("UI/panels/button_pressed.png", 512, 512);
            Fit("UI/panels/banner.png", 1024, 1024);
            Fit("UI/panels/bubble.png", 256, 256);
            foreach (var b in new[] { "settings", "lore", "fullscreen", "exit-fullscreen", "leave-game", "wardrobe", "change-player" })
                Fit("UI/buttons/" + b + ".png", 256, 256);

            void Bg(string src, string dst)
            {
                string path = Path.Combine(SrcArt, src);
                if (!File.Exists(path)) { Log.Add("MISSING " + src); return; }
                WriteJpg(dst, FitMax(Img.Load(path), 2048), path);
            }
            Bg("UI/backgrounds/key_art.png", "UI/backgrounds/key_art.jpg");
            Bg("UI/backgrounds/lobby.png", "UI/backgrounds/lobby.jpg");
            Bg("UI/backgrounds/main-menu-left.png", "UI/backgrounds/menu.jpg");
            Bg("UI/backgrounds/lobby-bridge.png", "UI/backgrounds/lobby_bridge.jpg");
            Bg("UI/backgrounds/results-lobby.png", "UI/backgrounds/results.jpg");
            Bg("UI/backgrounds/results-lobby-bridge.png", "UI/backgrounds/results_bridge.jpg");
            if (File.Exists(Path.Combine(SrcArt, "UI/backgrounds/loading.png")))
                Bg("UI/backgrounds/loading.png", "UI/backgrounds/loading.jpg");
            else
                Log.Add("UI/backgrounds/loading.png missing: the loading screen uses splash-screen.png");
            Bg("splash-screen.png", "UI/backgrounds/splash.jpg");
        }

        static Img FitMax(Img img, int max) => img.W <= max && img.H <= max ? img : img.Fit(max, max);

        static void Unused()
        {
            foreach (var f in Directory.GetFiles(SrcArt, "*.png", SearchOption.AllDirectories))
            {
                if (!Used.Contains(f))
                    Log.Add("UNUSED source file " + f.Substring(SrcArt.Length + 1));
            }
        }

        // ---------- writing ----------

        static void WritePng(string rel, Img img, string source) => Write(rel, img.EncodePng(), source);

        static void WriteJpg(string rel, Img img, string source)
        {
            // Crop to multiples of 4 so the texture can be DXT/crunch compressed.
            var cropped = img.Crop(new RectInt(0, 0, img.W & ~3, img.H & ~3));
            var opaque = new Img(cropped.W, cropped.H);
            for (int i = 0; i < cropped.Px.Length; i++)
            {
                var p = cropped.Px[i];
                opaque.Px[i] = new Color32(p.r, p.g, p.b, 255);
            }
            Write(rel, opaque.EncodeJpg(88), source);
        }

        static void Write(string rel, byte[] bytes, string source)
        {
            Used.Add(source);
            string path = Path.Combine(Root, DstArt, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(bytes))
            {
                _unchanged++;
                return;
            }
            File.WriteAllBytes(path, bytes);
            _written++;
        }

        // "aljaž" → "aljaz": asset paths and PlayerPrefs keys stay plain ASCII.
        public static string Ascii(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s.Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
