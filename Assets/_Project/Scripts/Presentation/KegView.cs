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
            _ring = art.MakeRenderer("Owner", transform, PlaceholderArt.Ring, ownerColor, 0);
            _ring.transform.localScale = Vector3.one * 0.95f;
            var kegColor = art.Theme != null ? art.Theme.KegColor : new Color(0.3f, 0.18f, 0.1f);
            bool realArt = art.KegSprite != PlaceholderArt.Keg;
            _body = art.MakeRenderer("Keg", transform, art.KegSprite, realArt ? Color.white : Color.Lerp(kegColor, Color.white, 0.25f), 1);
            _body.transform.localScale = Vector3.one * 0.8f;
            _fuse = labels.Create("", "keg-fuse");
            _fuse.Follow = transform;
            _fuse.Offset = new Vector3(0f, 0.05f, 0f);
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
            _group.sortingOrder = Layers.Keg + Layers.Row(p.Y);
        }

        public void SetGridPosLogical(Int2 p)
        {
            GridPos = p;
            _group.sortingOrder = Layers.Keg + Layers.Row(p.Y);
        }

        public void SetFuse(int fuse) => _fuse.Text = fuse.ToString();

        public void ShowFuse(bool show) => _fuse.SetVisible(show);
    }
}
