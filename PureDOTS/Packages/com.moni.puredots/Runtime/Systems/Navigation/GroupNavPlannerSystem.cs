using PureDOTS.Runtime.Armies;
using PureDOTS.Runtime.Bands;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Navigation;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Navigation
{
    /// <summary>
    /// Group-level navigation planner.
    /// Uses NavRequest from group-level AI to plan paths for groups (bands, armies, fleets).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ColdPathSystemGroup))]
    [UpdateAfter(typeof(MultiModalRoutePlannerSystem))]
    public partial struct GroupNavPlannerSystem : ISystem
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

            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            // Process bands with path requests
            foreach (var (bandStats, pathRequest, entity) in
                SystemAPI.Query<RefRO<BandStats>, RefRO<PathRequest>>()
                .WithEntityAccess())
            {
                if (pathRequest.ValueRO.IsActive == 0)
                {
                    continue;
                }

                // Ensure GroupNavComponent exists
                if (!SystemAPI.HasComponent<GroupNavComponent>(entity))
                {
                    ecb.AddComponent(entity, new GroupNavComponent
                    {
                        NavPathEntity = entity, // Group's own entity has the NavPath
                        NextWaypointIndex = 0,
                        IsActive = 1
                    });
                }

                // Ensure NavPath exists (will be populated by MultiModalRoutePlannerSystem)
                if (!SystemAPI.HasComponent<NavPath>(entity))
                {
                    ecb.AddComponent<NavPath>(entity);
                }

                if (!SystemAPI.HasBuffer<NavPathSegment>(entity))
                {
                    ecb.AddBuffer<NavPathSegment>(entity);
                }
            }

            // Process armies with path requests
            foreach (var (armyStats, pathRequest, entity) in
                SystemAPI.Query<RefRO<ArmyStats>, RefRO<PathRequest>>()
                .WithEntityAccess())
            {
                if (pathRequest.ValueRO.IsActive == 0)
                {
                    continue;
                }

                // Ensure GroupNavComponent exists
                if (!SystemAPI.HasComponent<GroupNavComponent>(entity))
                {
                    ecb.AddComponent(entity, new GroupNavComponent
                    {
                        NavPathEntity = entity,
                        NextWaypointIndex = 0,
                        IsActive = 1
                    });
                }

                // Ensure NavPath exists
                if (!SystemAPI.HasComponent<NavPath>(entity))
                {
                    ecb.AddComponent<NavPath>(entity);
                }

                if (!SystemAPI.HasBuffer<NavPathSegment>(entity))
                {
                    ecb.AddBuffer<NavPathSegment>(entity);
                }
            }

            // Update GroupNavComponent with current target from NavPath
            foreach (var (groupNav, navPath, pathSegments, entity) in
                SystemAPI.Query<RefRW<GroupNavComponent>, RefRO<NavPath>, DynamicBuffer<NavPathSegment>>()
                .WithEntityAccess())
            {
                if (navPath.ValueRO.IsValid == 0)
                {
                    groupNav.ValueRW.IsActive = 0;
                    continue;
                }

                int segmentIndex = navPath.ValueRO.CurrentSegmentIndex;
                if (segmentIndex < pathSegments.Length)
                {
                    var segment = pathSegments[segmentIndex].Segment;
                    groupNav.ValueRW.CurrentTargetPosition = segment.EndPosition;
                    groupNav.ValueRW.NextWaypointIndex = segmentIndex;
                    groupNav.ValueRW.IsActive = 1;
                }
            }

            ecb.Playback(state.EntityManager);
        }
    }
}

