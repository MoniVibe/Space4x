#if DEVTOOLS_ENABLED
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Devtools;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Devtools
{
    /// <summary>
    /// Updates spawn telemetry singleton with per-tick metrics.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct SpawnTelemetrySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // Ensure telemetry singleton exists
            if (!SystemAPI.HasSingleton<SpawnTelemetry>())
            {
                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(entity, new SpawnTelemetry());
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var telemetry = SystemAPI.GetSingletonRW<SpawnTelemetry>();

            // Reset per-tick counters
            telemetry.ValueRW.RequestsThisTick = 0;
            telemetry.ValueRW.ValidatedThisTick = 0;
            telemetry.ValueRW.SpawnedThisTick = 0;
            telemetry.ValueRW.FailuresThisTick = 0;
            telemetry.ValueRW.FailuresByReason_TooSteep = 0;
            telemetry.ValueRW.FailuresByReason_Overlap = 0;
            telemetry.ValueRW.FailuresByReason_OutOfBounds = 0;
            telemetry.ValueRW.FailuresByReason_ForbiddenVolume = 0;
            telemetry.ValueRW.FailuresByReason_NotOnNavmesh = 0;

            // Count requests
            using (var requestQuery = SystemAPI.QueryBuilder().WithAll<SpawnRequest>().Build())
            {
                telemetry.ValueRW.RequestsThisTick = requestQuery.CalculateEntityCount();
            }

            // Count validated/spawned/failed (would need to track in systems)
            // For now, placeholder - systems would update telemetry as they process
        }
    }
}
#endif























