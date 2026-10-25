namespace Musketeers.Sim
{
    // The sim can't load Unity assets, so built-in maps are embedded here too.
    // Must match Assets/_Project/Data/Maps/*.txt; MapTests keeps them in sync.
    // Both are 15x9 (FIXES_v0.2 §3.1). Lines starting with '@' are directives (MapParser).
    public static class BuiltInMaps
    {
        // GAME_DESIGN §3 Map 1: walled edges, so nobody can be pushed off the outside. 'x' = removed corner
        // tiles (FIXES_4E §4.6); pits and crates are random every round (§5.3).
        public const string Courtyard =
            "@walls\n" +
            "@randompits\n" +
            "@randomcrates\n" +
            "xx...........xx\n" +
            "x1...........2x\n" +
            "...#.......#...\n" +
            "...............\n" +
            "...............\n" +
            "...............\n" +
            "...#.......#...\n" +
            "x3...........4x\n" +
            "xx...........xx\n";

        // GAME_DESIGN §3 Map 2: islands, bridges ('b') and water. 'x' = removed corner tiles (FIXES_4E §4.5);
        // crates are random every round (§5.3), no random pits.
        public const string Bridge =
            "@randomcrates\n" +
            "xx~.........~xx\n" +
            "x1....~b~....2x\n" +
            "....~~~b~~~....\n" +
            ".~..~~...~~..~.\n" +
            ".~#.bb...bb.#~.\n" +   // Day 6: pillars (2,4), (12,4), clear centre
            ".~..~~...~~..~.\n" +
            "....~~~b~~~....\n" +
            "x3....~b~....4x\n" +
            "xx~.........~xx\n";
    }
}
