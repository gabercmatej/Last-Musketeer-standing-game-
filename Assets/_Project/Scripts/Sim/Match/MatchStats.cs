namespace Musketeers.Sim
{
    // Per-player counters for the GAME_DESIGN §8 awards, derived only from event logs:
    //   Sharpshooter  ShotHits        Wall of Steel  ShotsParried    Bonk Lord   Bonks
    //   Swan Dive     SelfRingOuts    Regicide       CrownKills      Powder Monkey KegDamage
    //   Pacifist      DamageDealt==0  Click Click    Fizzles
    // plus Kills, Deaths, Ripostes and ShotsFired for the scoreboard and Hall of Fame.
    public sealed class MatchStats
    {
        public readonly int[] ShotsFired;
        public readonly int[] ShotHits;       // musket + Triple Charge hits on units (not ripostes)
        public readonly int[] Ripostes;       // reflected shots that hit
        public readonly int[] ShotsParried;   // shots blocked by parry (blocked explosions don't count)
        public readonly int[] Bonks;          // one per collision a player was part of
        public readonly int[] SelfRingOuts;   // fell (pit, off board, crumble) with nobody credited
        public readonly int[] CrownKills;
        public readonly int[] KegDamage;      // explosion damage credited to this player
        public readonly int[] DamageDealt;    // all damage credited to this player on other units
        public readonly int[] Fizzles;
        public readonly int[] Kills;
        public readonly int[] Deaths;

        public MatchStats(int players)
        {
            ShotsFired = new int[players];
            ShotHits = new int[players];
            Ripostes = new int[players];
            ShotsParried = new int[players];
            Bonks = new int[players];
            SelfRingOuts = new int[players];
            CrownKills = new int[players];
            KegDamage = new int[players];
            DamageDealt = new int[players];
            Fizzles = new int[players];
            Kills = new int[players];
            Deaths = new int[players];
        }

        internal int[][] All => new[] { ShotsFired, ShotHits, Ripostes, ShotsParried, Bonks, SelfRingOuts, CrownKills, KegDamage, DamageDealt, Fizzles, Kills, Deaths };

        // Unit slots are player slots (every player has a unit every round, sitting out = dead).
        public void Record(EventLog log, int crowned)
        {
            for (int i = 0; i < log.Count; i++)
            {
                var e = log[i];
                switch (e.Kind)
                {
                    case SimEventKind.ShotFired:
                        ShotsFired[e.Unit]++;
                        break;
                    case SimEventKind.Fizzled:
                        Fizzles[e.Unit]++;
                        break;
                    case SimEventKind.Bonk:
                        Bonks[e.Unit]++;
                        break;
                    case SimEventKind.Parried:
                        if (e.Keg == SimEvent.None && e.Reason == 0)
                            ShotsParried[e.Unit]++;
                        break;
                    case SimEventKind.Damaged:
                        if (e.Other < 0 || e.Other == e.Unit)
                            break;
                        DamageDealt[e.Other] += e.Amount;
                        switch ((DamageKind)e.Reason)
                        {
                            case DamageKind.Shot:
                            case DamageKind.TripleCharge: ShotHits[e.Other]++; break;
                            case DamageKind.ReflectedShot: Ripostes[e.Other]++; break;
                            case DamageKind.Explosion: KegDamage[e.Other] += e.Amount; break;
                        }
                        break;
                    case SimEventKind.Died:
                        Deaths[e.Unit]++;
                        if (e.Other >= 0)
                        {
                            Kills[e.Other]++;
                            if (e.Unit == crowned)
                                CrownKills[e.Other]++;
                        }
                        else if (e.Reason == (byte)DeathCause.Fell)
                        {
                            SelfRingOuts[e.Unit]++;
                        }
                        break;
                }
            }
        }

        public MatchStats Clone()
        {
            var c = new MatchStats(ShotsFired.Length);
            var src = All;
            var dst = c.All;
            for (int k = 0; k < src.Length; k++)
                System.Array.Copy(src[k], dst[k], src[k].Length);
            return c;
        }

        internal void AddToHash(ref Fnv1a h)
        {
            foreach (var arr in All)
            {
                for (int i = 0; i < arr.Length; i++)
                    h.Add(arr[i]);
            }
        }
    }
}
