// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Interception
{
    [BurstCompile]
    public partial struct InterceptPlannerStubSystem : ISystem
    {
        [BurstCompile] public void OnCreate(ref SystemState state) { }
        [BurstCompile] public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (ticket, target, solution) in SystemAPI.Query<RefRW<InterceptTicket>, RefRO<InterceptTarget>, RefRW<InterceptSolution>>())
            {
                if (ticket.ValueRO.State == 2)
                    continue;

                var currentSolution = solution.ValueRO;
                currentSolution.AimPosition = target.ValueRO.LastKnownPosition + target.ValueRO.VelocityEstimate;
                currentSolution.ETA = math.length(currentSolution.AimPosition) * 0.01f;
                solution.ValueRW = currentSolution;

                var newTicket = ticket.ValueRO;
                newTicket.State = 1;
                ticket.ValueRW = newTicket;
            }
        }
    }
}
