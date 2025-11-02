using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Ensures villager-like consumers have proximity query settings and result components.
    /// This keeps consumer systems decoupled from the specific spatial lookup implementation.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VillagerSystemGroup), OrderFirst = true)]
    public partial struct ProximityQuerySetupSystem : ISystem
    {
        private EntityQuery _consumerQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _consumerQuery = SystemAPI.QueryBuilder()
                .WithAll<VillagerJob, LocalTransform>()
                .Build();

            state.RequireForUpdate(_consumerQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var em = state.EntityManager;

            using var entities = _consumerQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (!em.HasComponent<ResourceProximitySettings>(entity))
                {
                    ecb.AddComponent(entity, ResourceProximitySettings.CreateDefault());
                }

                if (!em.HasComponent<ResourceProximityResult>(entity))
                {
                    ecb.AddComponent(entity, new ResourceProximityResult
                    {
                        NearestResource = Entity.Null,
                        NearestPosition = float3.zero,
                        NearestDistanceSq = float.PositiveInfinity
                    });
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Fallback proximity provider: computes nearest resource for each consumer by scanning transforms.
    /// Replaceable by grid-backed providers that write the same result components.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VillagerSystemGroup))]
    [UpdateBefore(typeof(VillagerJobAssignmentSystem))]
    public partial struct ResourceProximityFallbackSystem : ISystem
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

            // Prefer registry if available
            NativeArray<ResourceSiteInfo> resourcesArray;
            if (SystemAPI.TryGetSingletonEntity<ResourceRegistryTag>(out var registryEntity) &&
                state.EntityManager.HasBuffer<ResourceRegistryEntry>(registryEntity))
            {
                var registry = state.EntityManager.GetBuffer<ResourceRegistryEntry>(registryEntity);
                var temp = new NativeArray<ResourceSiteInfo>(registry.Length, Allocator.Temp);
                for (int i = 0; i < registry.Length; i++)
                {
                    temp[i] = new ResourceSiteInfo
                    {
                        Entity = registry[i].Entity,
                        Position = registry[i].Position
                    };
                }

                resourcesArray = temp;
            }
            else
            {
                using var resourceSites = new NativeList<ResourceSiteInfo>(Allocator.Temp);
                foreach (var (transform, entity) in SystemAPI
                             .Query<RefRO<LocalTransform>>()
                             .WithAll<ResourceSourceConfig>()
                             .WithEntityAccess())
                {
                    resourceSites.Add(new ResourceSiteInfo
                    {
                        Entity = entity,
                        Position = transform.ValueRO.Position
                    });
                }

                if (resourceSites.Length == 0)
                {
                    return;
                }

                resourcesArray = resourceSites.AsArray();
            }

            var job = new PopulateProximityResultJob
            {
                Resources = resourcesArray,
                TransformLookup = _transformLookup
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        private struct ResourceSiteInfo
        {
            public Entity Entity;
            public float3 Position;
        }

        [BurstCompile]
        private partial struct PopulateProximityResultJob : IJobEntity
        {
            [ReadOnly] public NativeArray<ResourceSiteInfo> Resources;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;

            public void Execute(ref ResourceProximityResult result, in ResourceProximitySettings settings, in LocalTransform transform)
            {
                var bestEntity = Entity.Null;
                var bestDistSq = float.PositiveInfinity;

                var maxDistSq = settings.SearchRadius * settings.SearchRadius;

                for (var i = 0; i < Resources.Length; i++)
                {
                    var site = Resources[i];
                    var dSq = math.lengthsq(site.Position - transform.Position);
                    if (dSq > maxDistSq)
                    {
                        continue;
                    }

                    if (dSq < bestDistSq)
                    {
                        bestDistSq = dSq;
                        bestEntity = site.Entity;
                    }
                }

                result.NearestResource = bestEntity;
                result.NearestPosition = bestEntity == Entity.Null ? result.NearestPosition : TransformLookup.HasComponent(bestEntity) ? TransformLookup[bestEntity].Position : result.NearestPosition;
                result.NearestDistanceSq = float.IsPositiveInfinity(bestDistSq) ? result.NearestDistanceSq : bestDistSq;
            }
        }
    }
}


