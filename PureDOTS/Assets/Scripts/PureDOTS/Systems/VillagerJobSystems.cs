using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Assigns gatherer villagers to nearby resource sources when they lack a worksite.
    /// Provides a lightweight baseline for resource loops without relying on legacy registries.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VillagerSystemGroup))]
    [UpdateAfter(typeof(VillagerStatusSystem))]
    public partial struct VillagerJobAssignmentSystem : ISystem
    {
        private EntityQuery _gathererQuery;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<ResourceProximityResult> _proximityLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _gathererQuery = SystemAPI.QueryBuilder()
                .WithAll<VillagerJob, VillagerAIState, LocalTransform>()
                .WithNone<VillagerDeadTag, PlaybackGuardTag>()
                .Build();

            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _proximityLookup = state.GetComponentLookup<ResourceProximityResult>(true);

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate(_gathererQuery);
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
            _proximityLookup.Update(ref state);

            using var resourceEntities = new NativeList<ResourceSiteInfo>(Allocator.TempJob);
            foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<ResourceSourceConfig>().WithEntityAccess())
            {
                resourceEntities.Add(new ResourceSiteInfo
                {
                    Entity = entity,
                    Position = transform.ValueRO.Position
                });
            }

            if (resourceEntities.Length == 0)
            {
                return;
            }

            var job = new AssignGatherersJob
            {
                Resources = resourceEntities.AsArray(),
                TransformLookup = _transformLookup,
                MaxSearchDistanceSq = 50f * 50f,
                ProximityLookup = _proximityLookup
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        public struct ResourceSiteInfo
        {
            public Entity Entity;
            public float3 Position;
        }

        [BurstCompile]
        public partial struct AssignGatherersJob : IJobEntity
        {
            [ReadOnly] public NativeArray<ResourceSiteInfo> Resources;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public float MaxSearchDistanceSq;
            [ReadOnly] public ComponentLookup<ResourceProximityResult> ProximityLookup;

            public void Execute(Entity entity, ref VillagerJob job, ref VillagerAIState aiState, in LocalTransform transform)
            {
                if (job.Type != VillagerJob.JobType.Gatherer || job.WorksiteEntity != Entity.Null)
                {
                    return;
                }

                // Prefer proximity provider result if available
                if (ProximityLookup.HasComponent(entity))
                {
                    var result = ProximityLookup[entity];
                    if (result.NearestResource != Entity.Null)
                    {
                        job.WorksiteEntity = result.NearestResource;
                        aiState.TargetEntity = result.NearestResource;
                        return;
                    }
                }

                if (Resources.Length == 0)
                {
                    return;
                }

                var bestEntity = Entity.Null;
                var bestScore = float.MaxValue;

                for (var i = 0; i < Resources.Length; i++)
                {
                    var resource = Resources[i];
                    var distanceSq = math.lengthsq(resource.Position - transform.Position);

                    if (distanceSq > MaxSearchDistanceSq)
                    {
                        continue;
                    }

                    if (distanceSq < bestScore)
                    {
                        bestScore = distanceSq;
                        bestEntity = resource.Entity;
                    }
                }

                if (bestEntity == Entity.Null)
                {
                    return;
                }

                job.WorksiteEntity = bestEntity;
                aiState.TargetEntity = bestEntity;
            }
        }
    }
}
