using Musketeers.Data;
using UnityEditor;
using UnityEngine;

namespace Musketeers.EditorTools.Studio
{
    // Pure fit math plus the one write path every Studio edit goes through.
    public static class StudioEdit
    {
        // A drag in pose space becomes an offset in anchor space (it turns and scales with the anchor).
        public static ItemFit Move(ItemFit f, Anchor a, Vector2 poseDelta, bool mirrored)
        {
            var d = CosmeticLayout.Rotate(poseDelta, -a.Rotation) / a.S;
            if (mirrored)
                d.x = -d.x;
            f.Offset += d;
            return f;
        }

        public static ItemFit Rotate(ItemFit f, float degrees, bool mirrored)
        {
            f.Rotation += mirrored ? -degrees : degrees;
            return f;
        }

        public static ItemFit ScaleBy(ItemFit f, float factor)
        {
            f.Scale = Mathf.Clamp(f.S * factor, 0.05f, 10f);
            return f;
        }

        // `characterOrNull` = the musketeer id when "Override for this musketeer" is on. Does not save to disk.
        public static void Write(IFitTarget t, UnityEngine.Object owner, PoseKind pose, string characterOrNull, ItemFit fit, bool linkAll)
        {
            Undo.RecordObject(owner, "Fit " + t.Id);
            if (linkAll)
                FitEdit.SetLinked(t, characterOrNull, pose, fit, FitEdit.StudioPoses);
            else
                FitEdit.Set(t, pose, characterOrNull, fit);
            if (owner is CosmeticDef cd)
                cd.Tuned = true;
            EditorUtility.SetDirty(owner);
        }
    }
}
