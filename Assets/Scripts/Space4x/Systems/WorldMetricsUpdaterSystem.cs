using PureDOTS.Runtime.Components;
using Space4X.Registry;
using Unity.Burst;
using Unity.Entities;

namespace Space4X.Systems
{
    /// <summary>
    /// Updates Space4X-specific entity counts in WorldMetrics singleton.
    /// Runs after WorldMetricsSystem to populate game-specific counts.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(WorldMetricsSystem))]
    public partial struct WorldMetricsUpdaterSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Only update if WorldMetrics exists
            if (!SystemAPI.HasSingleton<WorldMetrics>())
            {
                return;
            }

            var metrics = SystemAPI.GetSingletonRW<WorldMetrics>();

            // Count Space4X-specific entities
            var miningVesselQuery = SystemAPI.QueryBuilder().WithAll<MiningVessel>().Build();
            metrics.ValueRW.MiningVesselCount = miningVesselQuery.CalculateEntityCount();

            var carrierQuery = SystemAPI.QueryBuilder().WithAll<CarrierTag>().Build();
            metrics.ValueRW.CarrierCount = carrierQuery.CalculateEntityCount();

            var asteroidQuery = SystemAPI.QueryBuilder().WithAll<Asteroid>().Build();
            metrics.ValueRW.AsteroidCount = asteroidQuery.CalculateEntityCount();
        }
    }
}



