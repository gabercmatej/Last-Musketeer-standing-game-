using Musketeers.Data;
using PrimeTween;
using UnityEngine;

namespace Musketeers.Presentation
{
    // Short-lived effects: Matej's FX sprites (ART_GUIDE §4.5) animated with tweens, falling back to the
    // code-drawn placeholders, plus tracers, rope lines, tile bursts and popups. Each one cleans itself up.
    public sealed class Fx : MonoBehaviour
    {
        [SerializeField] private ArtLibrary _art;
        [SerializeField] private WorldLabels _labels;
        [SerializeField] private CameraRig _camera;

        public WorldLabels Labels => _labels;
        public ArtLibrary Art => _art;

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

        // ---------- sprite FX by key ----------

        // Pops a GameArt FX sprite: grows from `from` to `to` (world-unit widths), optionally rotated, then fades.
        // Without the art it draws the placeholder shape tinted with `fallbackTint`.
        public SpriteRenderer Burst(string key, Sprite fallback, Color fallbackTint, Vector3 pos, float from, float to, float duration, float angle = 0f, Ease ease = Ease.OutQuad)
        {
            bool real = _art.Has(key);
            var sprite = real ? _art.Game.Get(key) : fallback;
            float k = SizeOf(sprite);
            var sr = Sprite(sprite, pos, real ? Color.white : fallbackTint, from * k);
            sr.transform.localRotation = Quaternion.Euler(0, 0, angle);
            Tween.Scale(sr.transform, to * k, duration, ease);
            FadeOut(sr, duration * 0.55f, duration * 0.45f);
            return sr;
        }

        // Muzzle flash pointing along `dir` from the barrel tip.
        public void MuzzleFlash(Vector3 tip, Vector3 dir, float duration)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            if (_art.Has(GameArt.MuzzleFlash))
            {
                var sr = Sprite(_art.Game.Get(GameArt.MuzzleFlash), tip, Color.white, 0.35f * SizeOf(_art.Game.Get(GameArt.MuzzleFlash)));
                sr.transform.localRotation = Quaternion.Euler(0, 0, angle);
                Tween.Scale(sr.transform, 0.7f * SizeOf(sr.sprite), duration * 0.4f, Ease.OutQuad);
                FadeOut(sr, duration * 0.6f, duration * 0.3f);
            }
            else
                Flash(tip, new Color(1f, 0.85f, 0.3f), 0.2f, 0.6f, duration, PlaceholderArt.Star);
        }

        // A smoke puff that drifts up and fades.
        public void Smoke(Vector3 pos, float size, float duration)
        {
            var sr = Burst(GameArt.Smoke, PlaceholderArt.SoftCircle, new Color(0.9f, 0.9f, 0.9f, 0.6f), pos, size * 0.5f, size, duration);
            Tween.LocalPositionY(sr.transform, pos.y + 0.35f, duration, Ease.OutQuad);
        }

        // A dust cloud at the feet, flipped to trail behind a mover heading along `dirX` (-1/0/1).
        public void Dust(Vector3 feet, float dirX, float size, float duration)
        {
            var sr = Burst(GameArt.Dust, PlaceholderArt.SoftCircle, new Color(0.85f, 0.75f, 0.6f, 0.55f), feet, size * 0.6f, size, duration);
            if (dirX < 0f)
            {
                var s = sr.transform.localScale;
                sr.transform.localScale = new Vector3(-s.x, s.y, 1f);
            }
            Tween.LocalPositionX(sr.transform, feet.x - dirX * 0.2f, duration, Ease.OutQuad);
        }

        // World width of a sprite at scale 1, so sizes above are in world units regardless of the art.
        static float SizeOf(Sprite s)
        {
            if (s == null)
                return 1f;
            float w = s.bounds.size.x;
            return w > 0.0001f ? 1f / w : 1f;
        }

        // ---------- lines ----------

        // A tracer that draws from a to b, then fades.
        public void Tracer(Vector3 a, Vector3 b, Color c, float width, float duration)
        {
            var sr = _art.MakeRenderer("tracer", transform, PlaceholderArt.Pixel, c, Layers.Fx);
            PlaceLine(sr.transform, a, a + (b - a) * 0.01f, width);
            float grow = duration * 0.35f;
            Tween.Custom(0.01f, 1f, grow, t => { if (sr != null) PlaceLine(sr.transform, a, a + (b - a) * t, width); }, Ease.OutQuad);
            FadeOut(sr, duration - grow, grow);
        }

        // Grappin: the lasso flies out to `end` on a rope, holds, then reels back to `home` (which may move).
        public void Hook(Vector3 start, Vector3 end, float outTime, float holdTime, float backTime, Transform home)
        {
            var rope = _art.MakeRenderer("rope", transform, PlaceholderArt.Pixel, new Color(0.62f, 0.45f, 0.25f), Layers.Fx - 2);
            bool real = _art.Has(GameArt.Hook);
            var head = Sprite(real ? _art.Game.Get(GameArt.Hook) : PlaceholderArt.Ring, start, real ? Color.white : new Color(0.85f, 0.85f, 0.9f),
                0.38f * SizeOf(real ? _art.Game.Get(GameArt.Hook) : PlaceholderArt.Ring), Layers.Fx - 1);
            float total = outTime + holdTime + backTime;
            Tween.Custom(0f, 1f, total, t =>
            {
                if (rope == null || head == null)
                    return;
                float time = t * total;
                Vector3 from = home != null ? home.position - transform.position + new Vector3(0f, 0.25f, 0f) : start;
                Vector3 tip = time < outTime ? Vector3.Lerp(start, end, time / outTime)
                    : time < outTime + holdTime ? end
                    : Vector3.Lerp(end, from, (time - outTime - holdTime) / Mathf.Max(0.001f, backTime));
                head.transform.localPosition = tip;
                PlaceLine(rope.transform, from, tip, 0.035f);
            }, Ease.Linear).OnComplete(() =>
            {
                if (rope != null) Destroy(rope.gameObject);
                if (head != null) Destroy(head.gameObject);
            });
        }

        // Emote: a speech bubble with the emote icon pops up over a unit's head (following it), then fades.
        public void Emote(Transform follow, Vector3 offset, Sprite icon, float seconds)
        {
            var root = new GameObject("emote").transform;
            root.SetParent(transform, false);
            bool realBubble = _art.Has(GameArt.Bubble);
            var bubble = _art.MakeRenderer("bubble", root, realBubble ? _art.Game.Get(GameArt.Bubble) : PlaceholderArt.Circle,
                realBubble ? Color.white : new Color(1f, 0.97f, 0.9f), Layers.Fx + 5);
            bubble.transform.localScale = Vector3.one * 0.85f * SizeOf(bubble.sprite);
            var face = _art.MakeRenderer("icon", root, icon != null ? icon : PlaceholderArt.Star, icon != null ? Color.white : new Color(1f, 0.8f, 0.2f), Layers.Fx + 6);
            face.transform.localScale = Vector3.one * 0.5f * SizeOf(face.sprite);
            face.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            root.localScale = Vector3.one * 0.2f;
            Tween.Scale(root, 1f, 0.25f, Ease.OutBack);
            Tween.Custom(0f, 1f, seconds, t =>
            {
                if (root == null) return;
                if (follow != null)
                    root.localPosition = follow.localPosition + offset + new Vector3(0f, Mathf.Sin(t * 6f) * 0.03f, 0f);
                float a = t < 0.8f ? 1f : 1f - (t - 0.8f) / 0.2f;
                bubble.color = new Color(bubble.color.r, bubble.color.g, bubble.color.b, a);
                face.color = new Color(face.color.r, face.color.g, face.color.b, a);
            }, Ease.Linear).OnComplete(() => { if (root != null) Destroy(root.gameObject); });
        }

        // ---------- placeholder shapes (still used for rings and glows) ----------

        public void Flash(Vector3 pos, Color c, float from, float to, float duration, Sprite sprite = null)
        {
            var sr = Sprite(sprite != null ? sprite : PlaceholderArt.Ring, pos, c, from);
            Tween.Scale(sr.transform, to, duration, Ease.OutQuad);
            FadeOut(sr, duration, 0f);
        }

        public void TileBurst(Vector3 pos, Color c, float duration)
        {
            var sr = Sprite(PlaceholderArt.RoundedSquare, pos, c, 0.6f, Layers.Fx - 3);
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
