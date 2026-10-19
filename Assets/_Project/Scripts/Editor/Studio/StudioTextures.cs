using System.Collections.Generic;
using Musketeers.EditorTools.ArtImport;
using UnityEngine;

namespace Musketeers.EditorTools.Studio
{
    // Uploads composed images to GPU textures the window can draw.
    public sealed class StudioTextures
    {
        readonly Dictionary<string, Texture2D> _tex = new Dictionary<string, Texture2D>();

        public Texture2D Get(string key) => _tex.TryGetValue(key, out var t) ? t : null;

        public void Upload(string key, Img img)
        {
            if (!_tex.TryGetValue(key, out var t) || t == null || t.width != img.W || t.height != img.H)
            {
                if (t != null)
                    Object.DestroyImmediate(t);
                t = new Texture2D(img.W, img.H, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, filterMode = FilterMode.Bilinear };
                _tex[key] = t;
            }
            t.SetPixels32(img.Px);
            t.Apply();
        }

        public void Clear()
        {
            foreach (var t in _tex.Values)
            {
                if (t != null)
                    Object.DestroyImmediate(t);
            }
            _tex.Clear();
        }
    }
}
