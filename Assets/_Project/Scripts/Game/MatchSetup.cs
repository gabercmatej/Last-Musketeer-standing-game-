using Musketeers.Sim;

namespace Musketeers.Game
{
    // Everything needed to start a match: who plays which slot, with which character, on which map.
    // Built by the menu (offline) or the lobby (Day 4); the same object goes to every machine.
    public sealed class MatchSetup
    {
        public string MapAscii = BuiltInMaps.Courtyard;
        public GameConfig Config = new GameConfig();
        public uint Seed = 1;
        public Ability[] Abilities;     // per slot
        public int[] Characters;        // CharacterDef index per slot
        public string[] Names;          // display name per slot
        public bool[] IsBot;            // per slot
        public int LocalSlot;           // the slot this machine's player controls

        public int PlayerCount => Abilities.Length;
    }
}
