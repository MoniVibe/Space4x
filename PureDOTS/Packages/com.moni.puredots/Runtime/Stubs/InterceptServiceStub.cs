// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Interception
{
    public static class InterceptServiceStub
    {
        public static void RequestIntercept(EntityManager manager, Entity entity, Entity target, float3 lastKnownPos, float3 velocityEstimate)
        {
            if (!manager.HasComponent<InterceptTicket>(entity))
            {
                manager.AddComponentData(entity, new InterceptTicket
                {
                    TicketId = entity.Index,
                    State = 0
                });
            }

            if (!manager.HasComponent<InterceptTarget>(entity))
            {
                manager.AddComponentData(entity, new InterceptTarget
                {
                    TargetEntity = target,
                    LastKnownPosition = lastKnownPos,
                    VelocityEstimate = velocityEstimate,
                    ObservationTick = 0
                });
            }
            else
            {
                manager.SetComponentData(entity, new InterceptTarget
                {
                    TargetEntity = target,
                    LastKnownPosition = lastKnownPos,
                    VelocityEstimate = velocityEstimate,
                    ObservationTick = 0
                });
            }

            if (!manager.HasComponent<InterceptSolution>(entity))
            {
                manager.AddComponentData(entity, new InterceptSolution
                {
                    AimPosition = lastKnownPos,
                    ETA = 0f
                });
            }
        }
    }
}
