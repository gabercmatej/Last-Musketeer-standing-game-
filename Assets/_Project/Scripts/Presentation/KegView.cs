using Musketeers.Sim;
using PrimeTween;
using UnityEngine;
using UnityEngine.Rendering;

namespace Musketeers.Presentation
{
    // A powder keg with its fuse number in big text (§5). Ring tinted with the thrower's color.
    public sealed class KegView : MonoBehaviour
    {
        public int Id { get; private set; }
        public Int2 GridPos { get; private set; }

        SpriteRenderer _body;
        SpriteRenderer _ring;
        SortingGroup _group;
        WorldLabel _fuse;

        public void Build(ArtLibrary art, WorldLabels labels, int id, Color ownerColor)
        {
            Id = id;
            _group = gameObject.AddComponent<SortingGroup>();
            var kegColor = art.Theme != null ? art.Theme.KegColor : new Color(0.3f, 0.18f, 0.1f);
            bool realArt = art.Has(Musketeers.Data.GameArt.Keg);
            // Owner ring on the ground, then the keg standing on it (bottom pivot for the real art).
            _ring = art.MakeRenderer("Owner", transform, PlaceholderArt.Ring, ownerColor, 0);
            _ring.transform.localScale = realArt ? new Vector3(0.85f, 0.42f, 1f) : Vector3.one * 0.95f;
            _ring.transform.localPosition = realArt ? new Vector3(0f, -0.3f, 0f) : Vector3.zero;
            if (realArt)
            {
                var shadow = art.MakeRenderer("Shadow", transform, art.Sprite(Musketeers.Data.GameArt.Shadow, PlaceholderArt.SoftCircle), new Color(0, 0, 0, 0.7f), 0);
                shadow.transform.localPosition = new Vector3(0f, -0.3f, 0f);
                shadow.transform.localScale = new Vector3(0.8f, 1.4f, 1f);
            }
            _body = art.MakeRenderer("Keg", transform, art.KegSprite, realArt ? Color.white : Color.Lerp(kegColor, Color.white, 0.25f), 1);
            _body.transform.localScale = Vector3.one * (realArt ? 1f : 0.8f);
            _body.transform.localPosition = realArt ? new Vector3(0f, -0.34f, 0f) : Vector3.zero;
            _fuse = labels.Create("", "keg-fuse");
            _fuse.Follow = transform;
            _fuse.Offset = new Vector3(0f, realArt ? 0.62f : 0.05f, 0f);
        }

        void OnDestroy()
        {
            _fuse?.Destroy();
            Tween.StopAll(transform);
        }

        public void SetGridPos(Int2 p)
        {
            GridPos = p;
            transform.localPosition = BoardMath.ToWorld(p);
            _group.sortingOrder = Layers.Keg(p.Y);
        }

        public void SetGridPosLogical(Int2 p)
        {
            GridPos = p;
            _group.sortingOrder = Layers.Keg(p.Y);
        }

        public void SetFuse(int fuse) => _fuse.Text = fuse.ToString();

        public void ShowFuse(bool show) => _fuse.SetVisible(show);

        public SpriteRenderer Sprite => _body;
    }
}
