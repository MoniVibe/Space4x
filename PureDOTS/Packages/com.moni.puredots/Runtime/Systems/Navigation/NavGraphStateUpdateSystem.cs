using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Navigation;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Navigation
{
    /// <summary>
    /// Applies operation effects (blockades, sieges, wars) to NavEdgeState.
    /// Updates edge states based on dynamic world conditions.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ColdPathSystemGroup))]
    [UpdateAfter(typeof(NavGraphHierarchySystem))]
    public partial struct NavGraphStateUpdateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            // TODO: When operations systems exist (blockades, sieges, wars):
            // 1. Query blockade entities and apply NavEdgeState to affected edges
            // 2. Query siege entities and apply NavEdgeState to affected edges
            // 3. Query war/conflict entities and apply NavEdgeState to affected edges
            // 4. Remove expired NavEdgeState components (ExpirationTick < current tick)

            // For now, this is a placeholder that can be extended when operations systems are implemented

            // Clean up expired edge states
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
            foreach (var (edgeState, entity) in SystemAPI.Query<RefRO<NavEdgeState>>().WithEntityAccess())
            {
                if (edgeState.ValueRO.ExpirationTick > 0 && 
                    edgeState.ValueRO.ExpirationTick < timeState.Tick)
                {
                    ecb.RemoveComponent<NavEdgeState>(entity);
                }
            }
            ecb.Playback(state.EntityManager);
        }
    }
}






















