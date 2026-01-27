// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.Decision
{
    /// <summary>
    /// Ticket component that tracks decision lifecycle (pending → assigned → complete).
    /// </summary>
    public struct DecisionTicket : IComponentData
    {
        public int TicketId;
        public byte State;
    }

    /// <summary>
    /// Outstanding decision requests, derived from needs or other triggers.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct DecisionRequestElement : IBufferElementData
    {
        public byte NeedType;
        public byte Priority;
    }

    /// <summary>
    /// Assigned action result. ActionId maps to data-driven action tables.
    /// </summary>
    public struct DecisionAssignment : IComponentData
    {
        public int ActionId;
        public byte Status;
    }
}
