using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;

namespace Space4X.Systems
{
    /// <summary>
    /// Ensures a WorldMetrics singleton exists and resets counts each frame.
    /// Space4X-specific systems (e.g., WorldMetricsUpdaterSystem) populate the fields after this runs.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct WorldMetricsSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var metricsEntity = SystemAPI.TryGetSingletonEntity<WorldMetrics>(out var entity)
                ? entity
                : state.EntityManager.CreateEntity(typeof(WorldMetrics));

            var metrics = SystemAPI.GetComponentRW<WorldMetrics>(metricsEntity);
            metrics.ValueRW.MiningVesselCount = 0;
            metrics.ValueRW.CarrierCount = 0;
            metrics.ValueRW.AsteroidCount = 0;
        }
    }
}
