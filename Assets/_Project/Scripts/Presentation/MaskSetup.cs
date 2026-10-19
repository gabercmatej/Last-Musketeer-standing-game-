using Musketeers.Data;
using UnityEngine;

namespace Musketeers.Presentation
{
    // Wires one item's own SpriteMask: the mask cuts only that renderer (custom range = its sorting order,
    // because SpriteMasks ignore SortingGroups) and the renderer reacts to it. Extracted from CharacterRig
    // so the rule is testable without building a whole rig.
    public static class MaskSetup
    {
        public static void Apply(SpriteMask mask, SpriteRenderer target, BezierPath path)
        {
            var origin = Vector2.zero;
            var sprite = target.enabled ? PolygonMask.Sprite(path, out origin) : null;
            bool has = sprite != null;
            mask.isCustomRangeActive = true;
            mask.frontSortingOrder = target.sortingOrder;       // front = order, back = order - 1
            mask.backSortingOrder = target.sortingOrder - 1;
            mask.alphaCutoff = 0.5f;
            mask.enabled = has;
            if (has)
            {
                mask.sprite = sprite;
                mask.transform.localPosition = origin;
            }
            // Invert keeps the inside of the path; otherwise the path is cut away.
            target.maskInteraction = !has ? SpriteMaskInteraction.None
                : path.Invert ? SpriteMaskInteraction.VisibleInsideMask : SpriteMaskInteraction.VisibleOutsideMask;
        }
    }
}
