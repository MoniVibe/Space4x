using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Resets per-frame presentation pooling stats so spawn/recycle systems can accumulate deltas.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct PresentationPoolStatsResetSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonRW<PresentationPoolStats>(out var stats))
            {
                return;
            }

            var value = stats.ValueRO;
            value.SpawnedThisFrame = 0;
            value.RecycledThisFrame = 0;
            stats.ValueRW = value;
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
