using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Updates world time tick counter.
    /// Runs in InitializationSystemGroup to ensure time is updated before simulation systems execute.
    /// This aligns with DOTS 1.4 lifecycle where "Update world time" occurs in InitializationSystemGroup.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct TimeStepSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // Superseded by TimeTickSystem; keep disabled to avoid double-advancing ticks.
            state.Enabled = false;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
        }
    }
}
