using UnityEngine;

namespace Musketeers.Data
{
    // The musket and the rapier as fittable items (Plan 006). Rest = musket on the shoulder / rapier at
    // rest; Active = musket aimed / rapier raised. Each state has its own fit set anchored to a hand.
    [CreateAssetMenu(menuName = "Musketeers/Weapon", fileName = "Weapon")]
    public sealed class WeaponDef : ScriptableObject
    {
        public string Id = "weapon";
        public bool Musket;
        public Sprite Sprite;
        public FitSet Rest = new FitSet { Name = "rest" };
        public FitSet Active = new FitSet { Name = "active" };

        // An in-memory weapon with an identity fit, for callers that only have a sprite.
        public static WeaponDef Temporary(Sprite sprite, bool musket)
        {
            var w = CreateInstance<WeaponDef>();
            w.hideFlags = HideFlags.HideAndDontSave;
            w.Musket = musket;
            w.Sprite = sprite;
            return w;
        }

        public FitSet For(bool active) => active ? Active : Rest;
    }
}
