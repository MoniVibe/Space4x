using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Navigation;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Navigation
{
    /// <summary>
    /// Expands meta paths to detailed NavPath segments.
    /// Refines high-level segments (MoveRegion, UseTransport) into local grid paths where needed.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ColdPathSystemGroup))]
    [UpdateAfter(typeof(MultiModalRoutePlannerSystem))]
    public partial struct NavPathBuilderSystem : ISystem
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
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            // Process NavPath entities that need refinement
            foreach (var (navPath, pathSegments, entity) in
                SystemAPI.Query<RefRW<NavPath>, DynamicBuffer<NavPathSegment>>()
                .WithEntityAccess())
            {
                if (navPath.ValueRO.IsValid == 0)
                {
                    continue;
                }

                // Refine segments that need local grid expansion
                // For now, segments are already at appropriate granularity
                // In future, this could expand MoveRegion segments into MoveLocal segments
                // when entering/exiting regions

                // TODO: When local grid is available:
                // 1. For MoveRegion segments, add MoveLocal segments at start/end
                // 2. For UseTransport segments, add MoveLocal segments to reach transport
                // 3. Refine waypoints based on local grid nodes
            }
        }
    }
}






















