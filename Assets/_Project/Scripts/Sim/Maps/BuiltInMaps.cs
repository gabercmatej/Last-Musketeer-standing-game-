namespace Musketeers.Sim
{
    // The sim can't load Unity assets, so built-in maps are embedded here too.
    // Must match Assets/_Project/Data/Maps/*.txt; MapTests keeps them in sync.
    public static class BuiltInMaps
    {
        // GAME_DESIGN §3 Map 1.
        public const string Courtyard =
            ".........\n" +
            ".1..o..2.\n" +
            "..#...#..\n" +
            "...~.~...\n" +
            ".o.....o.\n" +
            "...~.~...\n" +
            "..#...#..\n" +
            ".3..o..4.\n" +
            ".........\n";
    }
}
