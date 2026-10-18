namespace Musketeers.Sim
{
    // §5 Powder Keg. Sits on a floor tile and blocks movement like a crate.
    public sealed class Keg
    {
        public int Id;
        public int Owner;        // slot of the thrower, -1 for map/scenario kegs
        public Int2 Pos;
        public int Fuse;         // ticks until it explodes on its own
        public bool Detonating;  // set when a shot hits it; Phase 5 (Day 2) explodes it

        public Keg Clone() => (Keg)MemberwiseClone();

        internal void AddToHash(ref Fnv1a h)
        {
            h.Add(Id);
            h.Add(Owner);
            h.Add(Pos.X);
            h.Add(Pos.Y);
            h.Add(Fuse);
            h.Add(Detonating);
        }

        public override string ToString() => $"Keg#{Id} owner={Owner} {Pos} fuse{Fuse}{(Detonating ? " detonating" : "")}";
    }
}
