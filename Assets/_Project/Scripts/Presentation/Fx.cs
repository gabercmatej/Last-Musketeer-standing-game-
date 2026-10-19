using PrimeTween;
using UnityEngine;

namespace Musketeers.Presentation
{
    // Short-lived placeholder effects: tracers, flashes, bursts, popups. Each one cleans itself up.
    public sealed class Fx : MonoBehaviour
    {
        [SerializeField] private ArtLibrary _art;
        [SerializeField] private WorldLabels _labels;
        [SerializeField] private CameraRig _camera;

        public WorldLabels Labels => _labels;

        // A line segment as a stretched sprite (no LineRenderer material to keep in the Web build).
        public static void PlaceLine(Transform t, Vector3 a, Vector3 b, float width)
        {
            Vector3 d = b - a;
            t.localPosition = (a + b) * 0.5f;
            t.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            t.localScale = new Vector3(Mathf.Max(0.001f, d.magnitude), width, 1f);
        }

        public SpriteRenderer Sprite(Sprite sprite, Vector3 pos, Color c, float scale, int order = Layers.Fx)
        {
            var sr = _art.MakeRenderer("fx", transform, sprite, c, order);
            sr.transform.localPosition = pos;
            sr.transform.localScale = Vector3.one * scale;
            return sr;
        }

        // A tracer that draws from a to b, then fades.
        public void Tracer(Vector3 a, Vector3 b, Color c, float width, float duration)
        {
            var sr = _art.MakeRenderer("tracer", transform, PlaceholderArt.Pixel, c, Layers.Fx);
            PlaceLine(sr.transform, a, a + (b - a) * 0.01f, width);
            float grow = duration * 0.35f;
            Tween.Custom(0.01f, 1f, grow, t => { if (sr != null) PlaceLine(sr.transform, a, a + (b - a) * t, width); }, Ease.OutQuad);
            FadeOut(sr, duration - grow, grow);
        }

        public void Flash(Vector3 pos, Color c, float from, float to, float duration, Sprite sprite = null)
        {
            var sr = Sprite(sprite != null ? sprite : PlaceholderArt.Ring, pos, c, from);
            Tween.Scale(sr.transform, to, duration, Ease.OutQuad);
            FadeOut(sr, duration, 0f);
        }

        public void TileBurst(Vector3 pos, Color c, float duration)
        {
            var sr = Sprite(PlaceholderArt.RoundedSquare, pos, c, 0.6f, Layers.Fx - 1);
            Tween.Scale(sr.transform, 1.05f, duration * 0.4f, Ease.OutBack);
            FadeOut(sr, duration * 0.6f, duration * 0.4f);
        }

        public void Popup(string text, Vector3 pos, Color c, float duration, string cssClass = "popup") =>
            _labels.Popup(text, pos, c, duration, cssClass);

        public void Shake(float strength, float duration) => _camera?.Shake(strength, duration);

        static void FadeOut(SpriteRenderer sr, float duration, float delay)
        {
            var c = sr.color;
            Tween.Color(sr, new Color(c.r, c.g, c.b, 0f), Mathf.Max(0.01f, duration), Ease.InQuad, startDelay: delay)
                .OnComplete(sr, s => Destroy(s.gameObject));
        }
    }
}
