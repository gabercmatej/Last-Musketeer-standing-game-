namespace Musketeers.Sim
{
    // One thing that happened, for playback, announcer, awards and tests. See SimEventKind for
    // which fields each kind uses. Unit/Other/Keg are -1 when unused.
    public struct SimEvent
    {
        public const int None = -1;

        public SimEventKind Kind;
        public byte Tick;
        public SimPhase Phase;
        public sbyte Unit;
        public sbyte Other;
        public short Keg;
        public Int2 From;
        public Int2 To;
        public Dir Dir;
        public sbyte Amount;
        public byte Reason;

        public override string ToString() =>
            $"t{Tick} {Phase,-17} {Kind,-14} unit={Unit} other={Other} keg={Keg} {From}->{To} {Dir} amount={Amount} reason={Reason}";
    }
}
