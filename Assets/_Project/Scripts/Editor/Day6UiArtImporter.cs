using System.IO;
using System.Linq;
using System.Text;
using Musketeers.Data;
using UnityEditor;
using UnityEngine;

namespace Musketeers.EditorTools
{
    // Deliberately narrow: imports only explicitly listed Day 6 images; never invokes cosmetics/fit wiring.
    public static class Day6UiArtImporter
    {
        [MenuItem("Tools/Musketeers/Import Day 6 UI Art")]
        public static void Import()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameArt>("Assets/_Project/Data/GameArt.asset");
            if (catalog == null) throw new System.InvalidOperationException("GameArt catalog is missing.");
            foreach (string key in new[] { "UI/buttons/change-name", "UI/buttons/leaderboard-history", "UI/badges/killcam", "UI/buttons/skip-replay", "Board/shared/spawn-local", "Board/shared/spawn-other",
                "UI/leaderboard/tab_icons", "UI/leaderboard/placement_borders", "UI/leaderboard/matej_leaderboard",
                "UI/leaderboard/matic_leaderboard", "UI/leaderboard/nik_leaderboard", "UI/leaderboard/aljaž_leaderboard" })
            {
                string source = "Art/" + key + ".png";
                if (!File.Exists(source)) source = Directory.GetFiles(Path.GetDirectoryName(source), "*.png").FirstOrDefault(p => p.Normalize(NormalizationForm.FormC) == source.Normalize(NormalizationForm.FormC)) ?? source;
                string target = "Assets/_Project/Art/" + key + ".png";
                if (!File.Exists(source)) { Debug.LogWarning("Missing UI source: " + source); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(source, target, true);
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(target);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = 4096;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
                if (key.EndsWith("tab_icons") || key.EndsWith("placement_borders"))
                {
                    Slice(catalog, target, importer, key.EndsWith("tab_icons"));
                    continue;
                }
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(target);
                int index = catalog.Sprites.FindIndex(e => e.Key == key);
                var entry = new GameArt.Entry { Key = key, Sprite = sprite };
                if (index >= 0) catalog.Sprites[index] = entry; else catalog.Sprites.Add(entry);
            }
            catalog.Invalidate();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("Day 6 UI art imported (UI, leaderboard and spawn markers); cosmetic fits untouched.");
        }

        static void Slice(GameArt catalog, string target, TextureImporter importer, bool tabs)
        {
            catalog.Sprites.RemoveAll(e => e.Key == "UI/leaderboard/" + (tabs ? "tab_icons" : "placement_borders"));
            importer.spriteImportMode = SpriteImportMode.Multiple;
            var cuts = tabs ? new[] { new Rect(55, 0, 1060, 793), new Rect(1115, 0, 855, 793) }
                : new[] { new Rect(50, 538, 2070, 160), new Rect(50, 366, 2070, 160), new Rect(50, 196, 2070, 160), new Rect(50, 22, 2070, 160) };
            var names = tabs ? new[] { "history-tab", "leaderboard-tab" } : new[] { "rank-1", "rank-2", "rank-3", "rank-4" };
            var metadata = new SpriteMetaData[cuts.Length];
            for (int i = 0; i < cuts.Length; i++) metadata[i] = new SpriteMetaData { name = names[i], rect = cuts[i], pivot = new Vector2(0.5f, 0.5f), alignment = 0 };
#pragma warning disable CS0618
            importer.spritesheet = metadata;
#pragma warning restore CS0618
            importer.SaveAndReimport();
            foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath(target).OfType<Sprite>())
            {
                string key = "UI/leaderboard/" + sprite.name;
                int index = catalog.Sprites.FindIndex(e => e.Key == key);
                var entry = new GameArt.Entry { Key = key, Sprite = sprite };
                if (index >= 0) catalog.Sprites[index] = entry; else catalog.Sprites.Add(entry);
            }
        }
    }
}
