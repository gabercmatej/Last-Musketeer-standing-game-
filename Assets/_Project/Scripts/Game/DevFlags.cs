namespace Musketeers.Game
{
    // Developer switches (BUILD_PLAN Day 3). Only honoured in the Editor and development builds,
    // except AutoPlay, which the ?autoplay=N URL can also turn on for smoke-testing a deployed build.
    public static class DevFlags
    {
        // True in the Editor and in development builds.
        public static bool Available => UnityEngine.Debug.isDebugBuild;

        // F2: the local player plans every unit.
        public static bool ControlAll;
        // Human slots are planned by bots and locked at once, so a match plays itself (MCP checks).
        public static bool AutoPlay;
        // Start a vs-bots match straight away on load (with AutoPlay); 0 = off, else player count.
        public static int AutoStartPlayers;
        // Planning seconds override for quick checks; 0 = GameConfig.PlanningSeconds.
        public static float PlanningSeconds;
    }
}
