using System.IO;
using Musketeers.Data;
using UnityEditor;
using UnityEngine;

namespace Musketeers.EditorTools
{
    // Deliberately narrow: imports only the four new UI images; never invokes cosmetics/fit wiring.
    public static class Day6UiArtImporter
    {
        [MenuItem("Tools/Musketeers/Import Day 6 UI Art")]
        public static void Import()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameArt>("Assets/_Project/Data/GameArt.asset");
            if (catalog == null) throw new System.InvalidOperationException("GameArt catalog is missing.");
            foreach (string key in new[] { "UI/buttons/change-name", "UI/buttons/leaderboard-history", "UI/badges/killcam", "UI/buttons/skip-replay" })
            {
                string source = "Art/" + key + ".png";
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
                importer.maxTextureSize = 1024;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(target);
                int index = catalog.Sprites.FindIndex(e => e.Key == key);
                var entry = new GameArt.Entry { Key = key, Sprite = sprite };
                if (index >= 0) catalog.Sprites[index] = entry; else catalog.Sprites.Add(entry);
            }
            catalog.Invalidate();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("Day 6 UI art imported (4 images); cosmetic fits untouched.");
        }
    }
}
