namespace Musketeers.Sim
{
    // Every tuning number in the game. The first block mirrors GAME_DESIGN §11 one to one; the second
    // holds rule constants that §4–§6 state in prose. No other sim file may hard-code these.
    // Treat Default as read-only; make a new GameConfig to tweak values (tests, balance runs).
    public sealed class GameConfig
    {
        public static readonly GameConfig Default = new GameConfig();

        // --- §11 table ---
        public int BoardWidth = 15;           // maps are 15x9 (the map text is the source of truth; these document it)
        public int BoardHeight = 9;
        public int MaxHp = 3;
        public int StartGrit = 2;
        public int MaxGrit = 5;
        public int GritPerTurn = 1;
        public bool StartLoaded = true;       // §4 musket starts loaded; false = everyone starts unloaded (Matej's test knob)
        public int TicksPerTurn = 3;
        public int PlanningSeconds = 30;
        public int RevealMs = 1500;           // §11 says 1.5 s; the sim is integer-only, so milliseconds
        public int FireRange = 5;
        public int KegThrowRange = 3;
        public int KegFuseTicks = 3;
        public int MaxKegsPerPlayer = 1;
        public int KegKickDistance = 3;
        public int DashCost = 1;
        public int ParryCost = 1;
        public int KegCost = 2;
        public int SignatureCost = 5;
        public int GrappleRange = 5;
        public int BullRushDistance = 3;
        public int BullRushPush = 5;          // FIXES_4E §8.4: was 4
        public int BullRushDamage = 1;        // FIXES_4E §8.4
        public int CrumbleStartTurn = 5;
        public int CrumbleEvery = 2;
        public int PointsToWin = 12;
        public int WinPoints = 3;
        public int KillPoints = 1;
        public int CrownKillPoints = 2;
        public int EnvironmentalDeathPenalty = 1; // Day 6: charged on every fall, independent of kill credit

        // --- rule constants from §4–§6 prose ---
        public int CatchBreathGrit = 1;       // §5 Catch Breath
        public int ShovePush = 1;             // §6 Phase 3.2
        public int GuardBreakPush = 2;        // §6 Phase 3.2
        public int MaxPushStrength = 3;       // §6 Phase 3.3
        public int ShotDamage = 1;            // §5 Fire
        public int BonkDamage = 1;            // §6 Phase 2.2 and 3.4
        public int WallSlamDamage = 1;        // §6 Phase 3.4
        public int SqueezeDamage = 1;         // §6 Phase 3.3
        public int KegDamage = 1;             // §6 Phase 5.3
        public int KegPush = 1;               // §6 Phase 5.3: orthogonal neighbours pushed 1 outward
        public int TripleChargeShots = 3;     // §4 Triple Charge: shots per charge (range is FireRange)
        public int TripleChargeMaxDamage = 3; // §4 Triple Charge: max damage any one musketeer takes from one use
        public int TripleChargePush = 1;      // §4 Triple Charge: tiles each hit slides the target
        public int CrateGritChancePct = 50;   // FIXES_4E §5.2: chance a destroyed crate pays the destroyer Grit
        public int CrateGrit = 1;             // FIXES_4E §5.2: Grit paid (capped at MaxGrit)
        public int RandomPitsMin = 3;         // FIXES_4E §5.3: random pits per round on '@randompits' maps
        public int RandomPitsMax = 6;
        public int RandomCrates = 3;          // FIXES_4E §5.3: random crates per round on '@randomcrates' maps
        public int KillCreditTurns = 1;       // §6 Kill credit: last attacker counts this turn or last turn
    }
}
