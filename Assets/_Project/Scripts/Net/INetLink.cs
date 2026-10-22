using Musketeers.Sim;

namespace Musketeers.Net
{
    // What NetTransport needs from the wire (TECH_DESIGN §5). MatchNet implements it with RPCs; EditMode tests
    // implement it with an in-process hub, so the whole protocol is testable without NGO.
    // Inbound messages go the other way: the wire calls the NetTransport.HostOn* / ClientOn* methods.
    public interface INetLink
    {
        // client -> host
        void SendReady();
        void SendPlan(int turnSeq, TurnPlan plan, bool locked);
        void SendTurnDone(int turnSeq, uint stateHash);

        // host -> every remote client (the host applies its own copy directly)
        void BroadcastBegin(int turnSeq, int durationMs);
        void BroadcastLock(int turnSeq, int mask);
        void BroadcastResolve(int turnSeq, TurnPlan[] plans);

        // host -> one client: the full MatchState after turnSeq (resync)
        void SendSnapshot(ulong clientId, int turnSeq, byte[] data);
    }

    // TurnPlan(s) on the wire: 22 bytes per plan, fixed layout.
    public static class PlanCodec
    {
        public const int PlanBytes = 1 + TurnPlan.Length * 7;

        public static byte[] Encode(TurnPlan plan)
        {
            var b = new byte[PlanBytes];
            Write(b, 0, plan);
            return b;
        }

        public static TurnPlan Decode(byte[] data)
        {
            if (data == null || data.Length != PlanBytes)
                throw new System.FormatException("Bad plan size.");
            return Read(data, 0);
        }

        public static byte[] Encode(TurnPlan[] plans)
        {
            var b = new byte[plans.Length * PlanBytes];
            for (int i = 0; i < plans.Length; i++)
                Write(b, i * PlanBytes, plans[i]);
            return b;
        }

        public static TurnPlan[] DecodeAll(byte[] data)
        {
            if (data == null || data.Length % PlanBytes != 0)
                throw new System.FormatException("Bad plans size.");
            var plans = new TurnPlan[data.Length / PlanBytes];
            for (int i = 0; i < plans.Length; i++)
                plans[i] = Read(data, i * PlanBytes);
            return plans;
        }

        static void Write(byte[] b, int o, TurnPlan p)
        {
            b[o++] = p.Slot;
            for (int t = 0; t < TurnPlan.Length; t++)
            {
                var a = p[t];
                b[o++] = (byte)a.Type;
                b[o++] = (byte)a.Dir;
                b[o++] = (byte)a.Target.X;
                b[o++] = (byte)(a.Target.X >> 8);
                b[o++] = (byte)a.Target.Y;
                b[o++] = (byte)(a.Target.Y >> 8);
                b[o++] = a.Unit;
            }
        }

        static TurnPlan Read(byte[] b, int o)
        {
            var p = new TurnPlan(b[o++]);
            for (int t = 0; t < TurnPlan.Length; t++)
            {
                var type = (ActionType)b[o++];
                var dir = (Dir)(b[o++] & 3);
                int x = (short)(b[o] | (b[o + 1] << 8));
                int y = (short)(b[o + 2] | (b[o + 3] << 8));
                o += 4;
                int unit = b[o++];
                if ((int)type > (int)ActionType.Signature)
                    type = ActionType.Idle;
                p[t] = new PlannedAction(type, dir, new Int2(x, y), unit);
            }
            return p;
        }
    }
}
