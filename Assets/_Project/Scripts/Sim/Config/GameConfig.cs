namespace Musketeers.Sim
{
    // Every tuning number in the game. The first block mirrors GAME_DESIGN §11 one to one; the second
    // holds rule constants that §4–§6 state in prose. No other sim file may hard-code these.
    // Treat Default as read-only; make a new GameConfig to tweak values (tests, balance runs).
    public sealed class GameConfig
    {
        public static readonly GameConfig Default = new GameConfig();

        // --- §11 table ---
        public int BoardSize = 9;
        public int MaxHp = 3;
        public int StartGrit = 2;
        public int MaxGrit = 5;
        public int GritPerTurn = 1;
        public int TicksPerTurn = 3;
        public int PlanningSeconds = 25;
        public int RevealMs = 1500;           // §11 says 1.5 s; the sim is integer-only, so milliseconds
        public int FireRange = 5;
        public int DoubleChargeRange = 7;
        public int KegThrowRange = 3;
        public int KegFuseTicks = 3;
        public int MaxKegsPerPlayer = 1;
        public int KegKickDistance = 3;
        public int DashCost = 1;
        public int ParryCost = 1;
        public int KegCost = 2;
        public int SignatureCost = 3;
        public int GrappleRange = 5;
        public int SwitcherooRange = 5;
        public int BullRushDistance = 3;
        public int BullRushPush = 2;
        public int CrumbleStartTurn = 5;
        public int CrumbleEvery = 2;
        public int PointsToWin = 12;
        public int WinPoints = 3;
        public int KillPoints = 1;
        public int CrownKillPoints = 2;

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
        public int DoubleChargePierce = 2;    // §4 Double Charge
        public int KillCreditTurns = 1;       // §6 Kill credit: last attacker counts this turn or last turn
    }
}
