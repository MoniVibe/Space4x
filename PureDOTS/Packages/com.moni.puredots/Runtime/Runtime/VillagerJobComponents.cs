using PureDOTS.Runtime.Resource;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Components
{
    public struct VillagerJobTicket : IComponentData
    {
        public uint TicketId;
        public VillagerJob.JobType JobType;
        public ushort ResourceTypeIndex;
        public Entity ResourceEntity;
        public Entity StorehouseEntity;
        public byte Priority;
        public byte Phase;
        public float ReservedUnits;
        public uint AssignedTick;
        public uint LastProgressTick;
    }

    public struct VillagerJobProgress : IComponentData
    {
        public float Gathered;
        public float Delivered;
        public float TimeInPhase;
        public uint LastUpdateTick;
    }

    public struct VillagerJobCarryItem : IBufferElementData
    {
        public ushort ResourceTypeIndex;
        public float Amount;
        public byte TierId;
        public ushort AverageQuality;

        public ResourcePayloadSummary AsPayload()
        {
            return new ResourcePayloadSummary
            {
                ResourceTypeIndex = ResourceTypeIndex,
                Amount = Amount,
                TierId = TierId,
                AverageQuality = AverageQuality
            };
        }

        public void ApplyPayload(in ResourcePayloadSummary payload)
        {
            ResourceTypeIndex = payload.ResourceTypeIndex;
            Amount = payload.Amount;
            TierId = payload.TierId;
            AverageQuality = payload.AverageQuality;
        }

        public static VillagerJobCarryItem FromPayload(in ResourcePayloadSummary payload)
        {
            return new VillagerJobCarryItem
            {
                ResourceTypeIndex = payload.ResourceTypeIndex,
                Amount = payload.Amount,
                TierId = payload.TierId,
                AverageQuality = payload.AverageQuality
            };
        }
    }

    public struct VillagerJobHistorySample : IBufferElementData
    {
        public uint Tick;
        public uint TicketId;
        public VillagerJob.JobPhase Phase;
        public float Gathered;
        public float Delivered;
        public float3 TargetPosition;
    }

    public enum VillagerJobEventType : byte
    {
        None = 0,
        JobAssigned = 1,
        JobProgress = 2,
        JobCompleted = 3,
        JobInterrupted = 4
    }

    public struct VillagerJobEvent : IBufferElementData
    {
        public uint Tick;
        public Entity Villager;
        public VillagerJobEventType EventType;
        public ushort ResourceTypeIndex;
        public float Amount;
        public uint TicketId;
    }

    public struct VillagerJobEventStream : IComponentData { }

    public struct VillagerJobTicketSequence : IComponentData
    {
        public uint Value;
    }

    public struct VillagerJobRequestQueue : IComponentData { }

    public struct VillagerJobRequest : IBufferElementData
    {
        public Entity Villager;
        public VillagerJob.JobType JobType;
        public byte Priority;
    }

    public struct VillagerJobDeliveryQueue : IComponentData { }

    public struct VillagerJobDeliveryCommand : IBufferElementData
    {
        public Entity Villager;
        public Entity Storehouse;
        public ushort ResourceTypeIndex;
        public float Amount;
        public uint TicketId;
    }

    public struct VillagerJobDiagnostics : IComponentData
    {
        public uint Frame;
        public int TotalVillagers;
        public int IdleVillagers;
        public int AssignedVillagers;
        public int PendingRequests;
        public int ActiveTickets;
    }
}
