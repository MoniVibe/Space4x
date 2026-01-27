using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Navigation;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Navigation
{
    /// <summary>
    /// Detects path failures (cannot traverse segment) and enqueues new NavRequest with updated knowledge.
    /// Handles cases where entities discover blocked routes and need to replan.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ColdPathSystemGroup))]
    [UpdateAfter(typeof(NavKnowledgeGraphBuilderSystem))]
    public partial struct NavReplanSystem : ISystem
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

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            // Check entities with NavPath for path failures
            foreach (var (navPath, pathSegments, entity) in
                SystemAPI.Query<RefRW<NavPath>, DynamicBuffer<NavPathSegment>>()
                .WithEntityAccess())
            {
                if (navPath.ValueRO.IsValid == 0)
                {
                    continue;
                }

                int currentSegmentIndex = navPath.ValueRO.CurrentSegmentIndex;
                if (currentSegmentIndex >= pathSegments.Length)
                {
                    continue;
                }

                var segment = pathSegments[currentSegmentIndex].Segment;

                // Check if segment is blocked
                bool isBlocked = false;

                // Check if transport entity is destroyed/null
                if (segment.Kind == NavSegmentKind.UseTransport && segment.Transport != Entity.Null)
                {
                    if (!state.EntityManager.Exists(segment.Transport))
                    {
                        isBlocked = true;
                    }
                }

                // Check if edge state marks segment as closed
                // TODO: Check NavEdgeState on the edge/node

                if (isBlocked)
                {
                    // Mark path as invalid
                    navPath.ValueRW.IsValid = 0;

                    // TODO: When KnownFacts exists:
                    // 1. Learn new fact about blocked route
                    // 2. Trigger replanning with updated knowledge

                    // For now, create new PathRequest to replan
                    if (SystemAPI.HasComponent<PathRequest>(entity))
                    {
                        var pathRequest = SystemAPI.GetComponent<PathRequest>(entity);
                        var newRequest = pathRequest;
                        newRequest.RequestTick = timeState.Tick;
                        newRequest.IsActive = 1;
                        ecb.SetComponent(entity, newRequest);
                    }
                }
            }

            ecb.Playback(state.EntityManager);
        }
    }
}






















