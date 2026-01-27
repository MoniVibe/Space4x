// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.Logistics.Components
{
    public struct LogisticsRoute : IComponentData
    {
        public int RouteId;
        public byte Status;
    }

    public struct HaulRequest : IComponentData
    {
        public int RequestId;
        public Entity Source;
        public Entity Destination;
    }

    public struct MaintenanceTicket : IComponentData
    {
        public int TicketId;
        public Entity Target;
        public byte Severity;
    }
}
