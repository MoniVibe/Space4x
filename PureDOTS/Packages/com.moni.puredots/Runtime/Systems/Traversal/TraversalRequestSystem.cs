using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Traversal;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Traversal
{
    /// <summary>
    /// Consumes traversal requests and initializes execution state when capability checks pass.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(TraversalExecutionSystem))]
    public partial struct TraversalRequestSystem : ISystem
    {
        private ComponentLookup<TraversalLink> _linkLookup;
        private ComponentLookup<BodyDimensions> _dimensionsLookup;
        private ComponentLookup<MobilityCaps> _capsLookup;
        private ComponentLookup<TraversalExecutionState> _executionLookup;
        private ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _linkLookup = state.GetComponentLookup<TraversalLink>(true);
            _dimensionsLookup = state.GetComponentLookup<BodyDimensions>(true);
            _capsLookup = state.GetComponentLookup<MobilityCaps>(true);
            _executionLookup = state.GetComponentLookup<TraversalExecutionState>(false);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            _linkLookup.Update(ref state);
            _dimensionsLookup.Update(ref state);
            _capsLookup.Update(ref state);
            _executionLookup.Update(ref state);
            _transformLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (requests, entity) in SystemAPI.Query<DynamicBuffer<TraversalRequest>>().WithEntityAccess())
            {
                if (requests.Length == 0)
                {
                    continue;
                }

                if (!_dimensionsLookup.HasComponent(entity) || !_capsLookup.HasComponent(entity))
                {
                    continue;
                }

                if (_executionLookup.HasComponent(entity) && _executionLookup[entity].IsActive != 0)
                {
                    continue;
                }

                var request = requests[0];
                if (request.LinkEntity == Entity.Null || !_linkLookup.HasComponent(request.LinkEntity))
                {
                    requests.RemoveAt(0);
                    continue;
                }

                var link = _linkLookup[request.LinkEntity];
                var dimensions = _dimensionsLookup[entity];
                var caps = _capsLookup[entity];

                if (!TraversalUtility.CanTraverse(link, dimensions, caps))
                {
                    requests.RemoveAt(0);
                    continue;
                }

                var startPos = link.StartPosition;
                var endPos = link.EndPosition;
                if (_transformLookup.HasComponent(entity))
                {
                    var current = _transformLookup[entity].Position;
                    if (math.distance(current, startPos) > 0.01f)
                    {
                        startPos = current;
                    }
                }

                var exec = new TraversalExecutionState
                {
                    LinkEntity = request.LinkEntity,
                    Type = link.Type,
                    StartPosition = startPos,
                    EndPosition = endPos,
                    ArcHeight = link.Execution.ArcHeight,
                    Duration = math.max(0.01f, link.Execution.Duration),
                    LandingSnapDistance = link.Execution.LandingSnapDistance,
                    LandingSnapVerticalTolerance = link.Execution.LandingSnapVerticalTolerance,
                    Elapsed = 0f,
                    StartTick = timeState.Tick,
                    IsActive = 1
                };

                if (_executionLookup.HasComponent(entity))
                {
                    ecb.SetComponent(entity, exec);
                }
                else
                {
                    ecb.AddComponent(entity, exec);
                }

                requests.RemoveAt(0);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
