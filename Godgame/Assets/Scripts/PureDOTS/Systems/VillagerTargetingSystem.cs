using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Resolves target entities referenced by villager AI into explicit world positions.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VillagerSystemGroup))]
    [UpdateAfter(typeof(VillagerJobAssignmentSystem))]
    public partial struct VillagerTargetingSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
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

            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _transformLookup.Update(ref state);

            var job = new ResolveTargetPositionsJob
            {
                TransformLookup = _transformLookup
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct ResolveTargetPositionsJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;

            public void Execute(ref VillagerAIState aiState)
            {
                if (aiState.TargetEntity == Entity.Null)
                {
                    aiState.TargetPosition = float3.zero;
                    return;
                }

                if (TransformLookup.TryGetComponent(aiState.TargetEntity, out var targetTransform))
                {
                    aiState.TargetPosition = targetTransform.Position;
                }
                else
                {
                    aiState.TargetEntity = Entity.Null;
                    aiState.TargetPosition = float3.zero;
                }
            }
        }
    }
}
