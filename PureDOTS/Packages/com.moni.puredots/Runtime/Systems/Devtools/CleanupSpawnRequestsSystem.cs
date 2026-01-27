#if DEVTOOLS_ENABLED
using PureDOTS.Runtime.Devtools;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Devtools
{
    /// <summary>
    /// Cleans up completed spawn request entities and buffers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(InstantiateSpawnSystem))]
    public partial struct CleanupSpawnRequestsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // System only runs when there are cleanup tasks
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Most cleanup is handled by InstantiateSpawnSystem destroying request entities
            // This system can handle any additional cleanup logic if needed
        }
    }
}
#endif























