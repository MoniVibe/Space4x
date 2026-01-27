using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Emits telemetry counters for presentation pooling so HUD/analytics can surface them.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PresentationRecycleSystem))]
    public partial struct PresentationPoolTelemetrySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PresentationPoolStats>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<TelemetryStream>(out var telemetryEntity))
            {
                return;
            }

            var stats = SystemAPI.GetSingleton<PresentationPoolStats>();
            var buffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);

            buffer.AddMetric("presentation.pool.active", stats.ActiveVisuals);
            buffer.AddMetric("presentation.pool.spawnedFrame", stats.SpawnedThisFrame);
            buffer.AddMetric("presentation.pool.recycledFrame", stats.RecycledThisFrame);
            buffer.AddMetric("presentation.pool.totalSpawned", stats.TotalSpawned);
            buffer.AddMetric("presentation.pool.totalRecycled", stats.TotalRecycled);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
