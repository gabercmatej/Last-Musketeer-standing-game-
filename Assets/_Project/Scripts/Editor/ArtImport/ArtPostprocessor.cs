using UnityEditor;
using UnityEngine;

namespace Musketeers.EditorTools.ArtImport
{
    // Import settings by folder (ART_GUIDE §3), so dropping a file in the right place is all it takes:
    // sprites at 256 PPU, pivots per asset type, no mipmaps, crunched compression for a small Web build,
    // and per-folder max sizes. Audio: Vorbis by default; on Web (which uses AAC) music stays compressed
    // in memory and short sounds are decompressed on load.
    public sealed class ArtPostprocessor : AssetPostprocessor
    {
        const string Art = "Assets/_Project/Art/";
        const string Audio = "Assets/_Project/Audio/";

        // Bump to force a re-import of everything under Art/ and Audio/ when these rules change.
        public override uint GetVersion() => 4;

        void OnPreprocessTexture()
        {
            string p = assetPath.Replace('\\', '/');
            if (!p.StartsWith(Art) || p.StartsWith(Art + "Placeholder/") || p.StartsWith(Art + "Fonts/"))
                return;
            string rel = p.Substring(Art.Length);
            string name = System.IO.Path.GetFileNameWithoutExtension(p);
            var ti = (TextureImporter)assetImporter;

            // These explicitly sliced final UI sheets must retain their import mode and source detail.
            if (rel.StartsWith("UI/leaderboard/") || rel == "Board/shared/spawn-local.png" || rel == "Board/shared/spawn-other.png")
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = rel.EndsWith("tab_icons.png") || rel.EndsWith("placement_borders.png") ? SpriteImportMode.Multiple : SpriteImportMode.Single;
                ti.spritePixelsPerUnit = 256; ti.mipmapEnabled = false; ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp; ti.filterMode = FilterMode.Bilinear;
                ti.textureCompression = TextureImporterCompression.CompressedHQ; ti.crunchedCompression = false;
                ti.compressionQuality = 100; ti.maxTextureSize = 4096; ti.npotScale = TextureImporterNPOTScale.None;
                var finalSettings = new TextureImporterSettings(); ti.ReadTextureSettings(finalSettings);
                finalSettings.spriteMeshType = SpriteMeshType.FullRect; finalSettings.spriteAlignment = (int)SpriteAlignment.Center;
                finalSettings.spritePivot = new Vector2(0.5f, 0.5f); ti.SetTextureSettings(finalSettings);
                return;
            }

            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 256;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.sRGBTexture = true;
            ti.filterMode = FilterMode.Bilinear;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.isReadable = false;
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.crunchedCompression = true;
            ti.compressionQuality = 60;
            ti.maxTextureSize = MaxSize(rel);
            if (rel.EndsWith(".jpg"))
            {
                // 4F §4: full-screen backgrounds (menu, splash, lobby, results, arena) are stretched over the whole
                // window, so crunch / normal-quality blocks show as blur. Keep them sharp: high quality, no crunch.
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.crunchedCompression = false;
                ti.compressionQuality = 100;
            }

            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.spriteAlignment = (int)SpriteAlignment.Custom;
            s.spritePivot = Pivot(rel, name);
            s.spriteBorder = Border(rel, name);
            ti.SetTextureSettings(s);
        }

        static int MaxSize(string rel)
        {
            if (rel.EndsWith(".jpg"))
                return 2048;                      // backgrounds: sources are 1672x941, so this is the source size
            if (rel.StartsWith("Board/"))
                return rel.Contains("/floor") || rel.Contains("/pit") ? 1024 : 512;
            if (rel.StartsWith("UI/logo/") || rel.StartsWith("UI/panels/"))
                return 1024;
            return 512;                           // characters, cosmetics, props, weapons, FX, icons
        }

        // Feet for character poses (ART_GUIDE §3), bottom centre for tall props, the grip for weapons.
        static Vector2 Pivot(string rel, string name)
        {
            if (rel.StartsWith("Characters/"))
                return name == "portrait" ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 0.125f);
            if (rel.StartsWith("Board/") && (name == "pillar" || name == "crate"))
                return new Vector2(0.5f, 0f);
            if (rel == "Objects/keg.png")
                return new Vector2(0.5f, 0f);
            if (rel == "Weapons/musket.png")
                return new Vector2(0.24f, 0.36f);
            if (rel == "Weapons/rapier.png")
                return new Vector2(0.12f, 0.5f);
            if (rel == "FX/muzzle_flash.png")
                return new Vector2(0.04f, 0.5f);
            return new Vector2(0.5f, 0.5f);
        }

        // 9-slice borders (px) for the parchment/wood panels so UI Toolkit stretches only the middle.
        static Vector4 Border(string rel, string name)
        {
            if (!rel.StartsWith("UI/panels/"))
                return Vector4.zero;
            switch (name)
            {
                case "panel": return new Vector4(80, 80, 80, 80);
                case "button":
                case "button_pressed": return new Vector4(54, 30, 54, 30);
                case "banner": return new Vector4(200, 50, 200, 50);
                default: return Vector4.zero;
            }
        }

        void OnPreprocessAudio()
        {
            string p = assetPath.Replace('\\', '/');
            if (!p.StartsWith(Audio))
                return;
            bool music = p.StartsWith(Audio + "Music/");
            var ai = (UnityEditor.AudioImporter)assetImporter;
            ai.forceToMono = !music;
            ai.loadInBackground = music;
            var s = ai.defaultSampleSettings;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = music ? 0.45f : 0.55f;
            s.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            s.preloadAudioData = !music;
            ai.defaultSampleSettings = s;
            // Web decodes through the browser (AAC) and has no Streaming load type.
            var web = s;
            web.loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            ai.SetOverrideSampleSettings("WebGL", web);
        }
    }
}
