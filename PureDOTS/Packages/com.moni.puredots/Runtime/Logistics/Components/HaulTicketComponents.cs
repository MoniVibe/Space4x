using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Logistics.Components
{
    public enum HaulTicketStatus : byte
    {
        Pending = 0,
        InProgress = 1,
        Delivered = 2,
        Expired = 3,
        Cancelled = 4
    }

    public struct HaulTicket : IComponentData
    {
        public Entity SourceStorage;
        public Entity DestinationStorage;
        public FixedString64Bytes ResourceId;
        public ushort ResourceTypeIndex;
        public float RequestedAmount;
        public float RemainingAmount;
        public byte Priority;
        public uint CreatedTick;
        public uint ExpiryTick;
        public Entity JobEntity;
        public HaulTicketStatus Status;
    }

    public struct HaulTicketPolicy : IComponentData
    {
        public uint DefaultExpiryTicks;
        public byte RequeueOnExpiry;
    }
}
