// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Interception
{
    public struct InterceptTicket : IComponentData
    {
        public int TicketId;
        public byte State; // 0=pending,1=active,2=complete
    }

    public struct InterceptTarget : IComponentData
    {
        public Entity TargetEntity;
        public float3 LastKnownPosition;
        public float3 VelocityEstimate;
        public uint ObservationTick;
    }

    public struct InterceptSolution : IComponentData
    {
        public float3 AimPosition;
        public float ETA;
    }
}
