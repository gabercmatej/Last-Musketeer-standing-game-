namespace Musketeers.Sim
{
    // §5 Powder Keg. Sits on a floor tile and blocks movement like a crate.
    public sealed class Keg
    {
        public int Id;
        public int Owner;        // slot of the thrower, -1 for map/scenario kegs
        public Int2 Pos;
        public int Fuse;         // ticks until it explodes on its own
        public bool Detonating;  // hit by a shot (or landed on something): explodes in Phase 5 this tick
        public int DetonatedBy = -1;  // first shooter (initiative order) to hit it; takes the kill credit

        public Keg Clone() => (Keg)MemberwiseClone();

        internal void AddToHash(ref Fnv1a h)
        {
            h.Add(Id);
            h.Add(Owner);
            h.Add(Pos.X);
            h.Add(Pos.Y);
            h.Add(Fuse);
            h.Add(Detonating);
            h.Add(DetonatedBy);
        }

        public override string ToString() => $"Keg#{Id} owner={Owner} {Pos} fuse{Fuse}{(Detonating ? " detonating" : "")}";
    }
}
