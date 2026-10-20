namespace Musketeers.Sim
{
    // The sim can't load Unity assets, so built-in maps are embedded here too.
    // Must match Assets/_Project/Data/Maps/*.txt; MapTests keeps them in sync.
    // Both are 15x9 (FIXES_v0.2 §3.1). Lines starting with '@' are directives (MapParser).
    public static class BuiltInMaps
    {
        // GAME_DESIGN §3 Map 1: walled edges, so nobody can be pushed off the outside.
        public const string Courtyard =
            "@walls\n" +
            "...............\n" +
            ".1....o.o....2.\n" +
            "...#.......#...\n" +
            ".....~...~.....\n" +
            ".o.....#.....o.\n" +
            ".....~...~.....\n" +
            "...#.......#...\n" +
            ".3....o.o....4.\n" +
            "...............\n";

        // GAME_DESIGN §3 Map 2: islands, bridges ('b') and water; the outside is water too.
        public const string Bridge =
            "~~~~~~~~~~~~~~~\n" +
            "~1..~~~~~~~..2~\n" +
            "~...bbb~bbb...~\n" +
            "~.o.~~.#.~~.o.~\n" +
            "~...~~...~~...~\n" +
            "~.o.~~.#.~~.o.~\n" +
            "~...bbb~bbb...~\n" +
            "~3..~~~~~~~..4~\n" +
            "~~~~~~~~~~~~~~~\n";
    }
}
