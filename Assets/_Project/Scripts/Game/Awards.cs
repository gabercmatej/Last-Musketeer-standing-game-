using System.Collections.Generic;
using Musketeers.Sim;

namespace Musketeers.Game
{
    public struct Award
    {
        public int Player;
        public string Id;      // badge file name under Art/UI/badges
        public string Name;
        public string Description;
    }

    // Post-match badges from MatchStats (GAME_DESIGN §8.5). "Most" awards need at least 1 and go to
    // everyone tied for the top; Regicide goes to everyone who did it at least once. Swan Dive is a Most award. Names here are placeholders until Day 6 moves them to a data asset.
    public static class Awards
    {
        public static List<Award> Compute(MatchStats st)
        {
            var list = new List<Award>();
            Most(list, st.ShotHits, "sharpshooter", "Sharpshooter", "Landed the most musket hits this match. Every bullet had an address.");
            Most(list, st.ShotsParried, "wall_of_steel", "Wall of Steel", "Parried the most shots. Bullets went in, ripostes came out.");
            Most(list, st.Bonks, "bonk_lord", "Bonk Lord", "Bumped into other musketeers more than anyone. Personal space is merely a suggestion.");
            Most(list, st.SelfRingOuts, "swan_dive", "Swan Dive", "Fell into a pit or the river the most times, all on his own. Graceful. Pointless. Wet.");
            Any(list, st.CrownKills, "regicide", "Regicide", "Brought down the Crowned leader. Heavy is the head that wears the crown, and lighter once you're done with it.");
            Most(list, st.KegDamage, "powder_monkey", "Powder Monkey", "Dealt the most damage with powder kegs. Ka-boom is a love language.");
            Most(list, st.Fizzles, "click_click", "Click Click", "Pulled the trigger on an empty musket the most times. Click. Click. Oops.");
            for (int p = 0; p < st.DamageDealt.Length; p++)
            {
                if (st.DamageDealt[p] == 0)
                    list.Add(new Award { Player = p, Id = "pacifist", Name = "Pacifist", Description = "Dealt zero damage the whole match. A true man of peace, or just a terrible shot." });
            }
            return list;
        }

        // Yes/no achievements (§8.5 "fell into a pit on his own", "killed the Crowned"): everyone who did it.
        static void Any(List<Award> list, int[] values, string id, string name, string desc)
        {
            for (int p = 0; p < values.Length; p++)
            {
                if (values[p] > 0)
                    list.Add(new Award { Player = p, Id = id, Name = name, Description = desc });
            }
        }

        static void Most(List<Award> list, int[] values, string id, string name, string desc)
        {
            int best = 0;
            foreach (var v in values)
            {
                if (v > best)
                    best = v;
            }
            if (best <= 0)
                return;
            for (int p = 0; p < values.Length; p++)
            {
                if (values[p] == best)
                    list.Add(new Award { Player = p, Id = id, Name = name, Description = desc });
            }
        }
    }
}
