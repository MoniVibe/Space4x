// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.Persistence
{
    public struct SaveChunkTag : IComponentData { }

    public struct SnapshotHandle : IComponentData
    {
        public int HandleId;
    }

    public struct DeserializationTicket : IComponentData
    {
        public int TicketId;
    }
}
