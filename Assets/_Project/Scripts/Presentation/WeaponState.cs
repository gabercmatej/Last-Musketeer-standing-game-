namespace Musketeers.Presentation
{
    // Plan 006 weapon layering rules. The aimed musket takes the front hand, so the rapier is hidden while
    // it is up (the ~0.6 s of a shot or a reload). The front-hand overlay is drawn whenever the front hand
    // holds something: the rapier (at rest or raised) or the aimed musket.
    public static class WeaponState
    {
        public static bool RapierVisible(bool weaponsOn, bool musketAimed, bool rapierPlacementValid) =>
            weaponsOn && !musketAimed && rapierPlacementValid;

        public static bool HandOverlayVisible(bool rapierVisible, bool musketAimedAndVisible, bool handPathValid) =>
            (rapierVisible || musketAimedAndVisible) && handPathValid;
    }
}
