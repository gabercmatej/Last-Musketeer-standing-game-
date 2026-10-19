using UnityEngine;

namespace Musketeers.Data
{
    // Default weapon fits that reproduce the look from before WeaponDef existed, computed from a musketeer's
    // anchors. Rest = identity (the hand anchor already holds the resting angle). Active puts the weapon in
    // the front hand: the musket aimed straight ahead (anchor rotation cancelled, back-hand scale kept),
    // the rapier raised at the old parry angle.
    public static class WeaponDefaults
    {
        public const float RaisedAngle = 62f;   // the old CharacterRig parry angle

        public static ItemFit Active(bool musket, PoseFit pose) =>
            musket
                ? new ItemFit(0f, 0f, pose.HandBack.S / pose.HandFront.S, -pose.HandFront.Rotation)
                : new ItemFit(0f, 0f, 1f, RaisedAngle - pose.HandFront.Rotation);

        // Idle drives Global; every other pose the fit has gets its own override (the anchors differ per pose).
        public static void Fill(WeaponDef weapon, CharacterFit fit)
        {
            weapon.Rest.Global = new ItemFit(0f, 0f, 1f);
            weapon.Rest.Overrides.Clear();
            weapon.Active.Overrides.Clear();
            var idle = fit != null ? fit.Get(PoseKind.Idle) : null;
            weapon.Active.Global = idle != null ? Active(weapon.Musket, idle) : new ItemFit(0f, 0f, 1f);
            if (fit == null)
                return;
            foreach (var kind in FitEdit.StudioPoses)
            {
                var p = fit.Get(kind);
                if (kind == PoseKind.Idle || p == null)
                    continue;
                FitEdit.Set(weapon.Active, kind, "", Active(weapon.Musket, p));
            }
        }
    }
}
