using Unity.Entities;

namespace PureDOTS.Runtime.AI
{
    /// <summary>
    /// Generic job ticket type for shared task coordination.
    /// </summary>
    public enum JobTicketType : byte
    {
        None = 0,
        Gather = 1
    }

    /// <summary>
    /// Lifecycle state for a job ticket.
    /// </summary>
    public enum JobTicketState : byte
    {
        Open = 0,
        Claimed = 1,
        InProgress = 2,
        Done = 3,
        Cancelled = 4
    }

    /// <summary>
    /// Shared job ticket describing a single unit of work.
    /// </summary>
    public struct JobTicket : IComponentData
    {
        public JobTicketType Type;
        public JobTicketState State;
        public Entity SourceEntity;
        public Entity TargetEntity;
        public Entity DestinationEntity;
        public Entity Assignee;
        public ushort ResourceTypeIndex;
        public float WorkAmount;
        public byte RequiredWorkers;
        public byte MinWorkers;
        public byte IsSingleItem;
        public float ItemMass;
        public uint ClaimExpiresTick;
        public uint LastStateTick;
        public uint BatchKey;
        public ulong JobKey;
    }

    /// <summary>
    /// Assignment state for agents participating in job tickets.
    /// </summary>
    public struct JobAssignment : IComponentData
    {
        public Entity Ticket;
        public uint CommitTick;
    }

    /// <summary>
    /// Ordered batch of job tickets to execute in sequence.
    /// </summary>
    public struct JobBatchEntry : IBufferElementData
    {
        public Entity Ticket;
    }

    /// <summary>
    /// Group membership for cooperative job tickets.
    /// </summary>
    public struct JobTicketGroupMember : IBufferElementData
    {
        public Entity Villager;
    }
}
