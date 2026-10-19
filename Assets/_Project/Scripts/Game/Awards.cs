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
    // everyone tied for the top; Swan Dive and Regicide go to everyone who did it at least once. Names here are placeholders until Day 6 moves them to a data asset.
    public static class Awards
    {
        public static List<Award> Compute(MatchStats st)
        {
            var list = new List<Award>();
            Most(list, st.ShotHits, "sharpshooter", "Sharpshooter", "Most shots on target");
            Most(list, st.ShotsParried, "wall_of_steel", "Wall of Steel", "Most shots parried");
            Most(list, st.Bonks, "bonk_lord", "Bonk Lord", "Most collisions");
            Any(list, st.SelfRingOuts, "swan_dive", "Swan Dive", "Jumped into a pit all alone");
            Any(list, st.CrownKills, "regicide", "Regicide", "Took the Crown off someone");
            Most(list, st.KegDamage, "powder_monkey", "Powder Monkey", "Most keg damage");
            Most(list, st.Fizzles, "click_click", "Click Click", "Most fizzled actions");
            for (int p = 0; p < st.DamageDealt.Length; p++)
            {
                if (st.DamageDealt[p] == 0)
                    list.Add(new Award { Player = p, Id = "pacifist", Name = "Pacifist", Description = "Dealt zero damage" });
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
