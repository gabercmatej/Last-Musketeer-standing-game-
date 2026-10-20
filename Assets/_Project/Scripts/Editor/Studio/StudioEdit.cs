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

        // The musketeer an edit goes to: only when "Override for this musketeer" is on, otherwise everyone (null).
        public static string EditCharacter(StudioState s) => s.OverrideMusketeer ? s.CharId : null;

        // Where an edit starts from: the fit of whoever the edit is written for, so an own override
        // never leaks into the everyone fit.
        public static ItemFit StartFit(IFitTarget t, string characterOrNull, PoseKind pose) =>
            FitEdit.Resolve(t, characterOrNull ?? "", pose);

        public static ItemFit StartFit(IFitTarget t, StudioState s) => StartFit(t, EditCharacter(s), s.Pose);

        // The one place a Studio change to an asset gets recorded (the caller has already recorded Undo).
        // Any CosmeticDef write counts as tuned, so Import Art won't re-apply FitDefaults over it.
        public static void Touch(UnityEngine.Object owner, StudioState s)
        {
            if (owner == null)
                return;
            if (owner is CosmeticDef cd)
                cd.Tuned = true;
            s.MarkDirty(owner);
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
