using System;
using Unity.Collections;
using Unity.Netcode;

namespace Musketeers.Net
{
    // One seat on the wire (NetworkList<LobbySlot>). The outfit travels as the Loadout.Serialize() text
    // ("hat|face|cape|boots") instead of three separate ids: same data, one field.
    public struct LobbySlot : INetworkSerializable, IEquatable<LobbySlot>
    {
        public ulong ClientId;
        public FixedString64Bytes Name;
        public int Character;
        public bool Ready;
        public bool IsBot;
        public bool InResults;
        public FixedString128Bytes Loadout;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref Name);
            serializer.SerializeValue(ref Character);
            serializer.SerializeValue(ref Ready);
            serializer.SerializeValue(ref IsBot);
            serializer.SerializeValue(ref InResults);
            serializer.SerializeValue(ref Loadout);
        }

        public bool Equals(LobbySlot o) =>
            ClientId == o.ClientId && Name.Equals(o.Name) && Character == o.Character && Ready == o.Ready &&
            IsBot == o.IsBot && InResults == o.InResults && Loadout.Equals(o.Loadout);

        public static LobbySlot From(LobbyEntry e) => new LobbySlot
        {
            ClientId = e.ClientId,
            Name = new FixedString64Bytes(e.Name ?? ""),
            Character = e.Character,
            Ready = e.Ready,
            IsBot = e.IsBot,
            InResults = e.InResults,
            Loadout = new FixedString128Bytes(e.Loadout ?? ""),
        };

        public LobbyEntry ToEntry() => new LobbyEntry
        {
            ClientId = ClientId,
            Name = Name.ToString(),
            Character = Character,
            Ready = Ready,
            IsBot = IsBot,
            InResults = InResults,
            Loadout = Loadout.ToString(),
        };
    }

    // The lobby's match rules on the wire (NetworkVariable<LobbySettings>).
    public struct LobbySettings : INetworkSerializable, IEquatable<LobbySettings>
    {
        public int Map;
        public int PointsToWin;
        public int Hearts;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Map);
            serializer.SerializeValue(ref PointsToWin);
            serializer.SerializeValue(ref Hearts);
        }

        public bool Equals(LobbySettings o) => Map == o.Map && PointsToWin == o.PointsToWin && Hearts == o.Hearts;

        public static LobbySettings From(LobbySettingsData d) => new LobbySettings { Map = d.Map, PointsToWin = d.PointsToWin, Hearts = d.Hearts };

        public LobbySettingsData ToData() => new LobbySettingsData { Map = Map, PointsToWin = PointsToWin, Hearts = Hearts };
    }
}
