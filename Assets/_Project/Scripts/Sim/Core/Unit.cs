namespace Musketeers.Sim
{
    // One musketeer. Units are indexed by slot in GameState.Units, so Slot == index.
    public sealed class Unit
    {
        public int Slot;
        public int Character;          // CharacterDef index; the sim only cares about Ability
        public Ability Ability;
        public Int2 Pos;
        public int Hp;
        public int Grit;
        public bool Loaded;
        public bool Alive;
        public int LastAttacker = -1;  // §6 Kill credit: who last damaged/pushed/yanked/swapped/exploded us
        public int LastAttackTurn;
        public bool Parrying;          // §5 Parry lasts one tick; cleared at the start and end of every tick

        public static Unit Spawn(int slot, int character, Ability ability, Int2 pos, GameConfig cfg)
        {
            return new Unit
            {
                Slot = slot,
                Character = character,
                Ability = ability,
                Pos = pos,
                Hp = cfg.MaxHp,
                Grit = cfg.StartGrit,
                Loaded = cfg.StartLoaded,   // §4: musket starts loaded (GameConfig.StartLoaded)
                Alive = true,
            };
        }

        public Unit Clone() => (Unit)MemberwiseClone();

        internal void AddToHash(ref Fnv1a h)
        {
            h.Add(Slot);
            h.Add(Character);
            h.Add((int)Ability);
            h.Add(Pos.X);
            h.Add(Pos.Y);
            h.Add(Hp);
            h.Add(Grit);
            h.Add(Loaded);
            h.Add(Alive);
            h.Add(LastAttacker);
            h.Add(LastAttackTurn);
            h.Add(Parrying);
        }

        public override string ToString() =>
            $"{(char)('A' + Slot)} {Pos} hp{Hp} grit{Grit} {(Loaded ? "loaded" : "empty")} {(Alive ? "alive" : "dead")}" +
            $"{(Parrying ? " parrying" : "")} lastAttacker={LastAttacker}@t{LastAttackTurn}";
    }
}
