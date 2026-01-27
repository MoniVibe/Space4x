using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using PureDOTS.Systems;

namespace PureDOTS.Runtime.WorldGen.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(TimeSystemGroup))]
    [UpdateAfter(typeof(CoreSingletonBootstrapSystem))]
    public partial struct SurfaceFieldsChunkBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            EnsureQueue(state.EntityManager);
            state.Enabled = false;
        }

        public void OnUpdate(ref SystemState state)
        {
        }

        private static void EnsureQueue(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SurfaceFieldsChunkRequestQueue>());
            Entity entity;

            if (query.IsEmptyIgnoreFilter)
            {
                entity = entityManager.CreateEntity(
                    typeof(SurfaceFieldsChunkRequestQueue),
                    typeof(SurfaceFieldsDomainConfig),
                    typeof(SurfaceFieldsStreamingConfig));
                entityManager.AddBuffer<SurfaceFieldsChunkRequest>(entity);
                entityManager.SetComponentData(entity, SurfaceFieldsDomainConfig.Default);
                entityManager.SetComponentData(entity, SurfaceFieldsStreamingConfig.Default);
                return;
            }

            entity = query.GetSingletonEntity();

            if (!entityManager.HasComponent<SurfaceFieldsDomainConfig>(entity))
            {
                entityManager.AddComponentData(entity, SurfaceFieldsDomainConfig.Default);
            }
            else
            {
                var config = entityManager.GetComponentData<SurfaceFieldsDomainConfig>(entity);
                config.CellsPerChunk = math.max(config.CellsPerChunk, new int2(1, 1));
                config.CellSize = math.max(config.CellSize, 0.1f);
                entityManager.SetComponentData(entity, config);
            }

            if (!entityManager.HasBuffer<SurfaceFieldsChunkRequest>(entity))
            {
                entityManager.AddBuffer<SurfaceFieldsChunkRequest>(entity);
            }

            if (!entityManager.HasComponent<SurfaceFieldsStreamingConfig>(entity))
            {
                entityManager.AddComponentData(entity, SurfaceFieldsStreamingConfig.Default);
            }
            else
            {
                var config = entityManager.GetComponentData<SurfaceFieldsStreamingConfig>(entity);
                config.LoadRadiusChunks = math.max(0, config.LoadRadiusChunks);
                config.KeepRadiusChunks = math.max(config.KeepRadiusChunks, config.LoadRadiusChunks);
                config.MaxNewChunksPerTick = math.max(0, config.MaxNewChunksPerTick);
                entityManager.SetComponentData(entity, config);
            }

            if (entityManager.HasComponent<SurfaceFieldsSphereCubeQuadDomainConfig>(entity))
            {
                var config = entityManager.GetComponentData<SurfaceFieldsSphereCubeQuadDomainConfig>(entity);
                config.Radius = math.max(0.001f, config.Radius);
                config.ChunksPerFace = math.max(config.ChunksPerFace, new int2(1, 1));
                entityManager.SetComponentData(entity, config);
            }
        }
    }
}
