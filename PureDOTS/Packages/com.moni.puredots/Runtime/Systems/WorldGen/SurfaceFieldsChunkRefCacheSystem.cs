using PureDOTS.Environment;
using PureDOTS.Runtime.WorldGen;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Runtime.WorldGen.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial struct SurfaceFieldsChunkRefCacheBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            using var query = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<SurfaceFieldsChunkRefCache>());
            if (!query.IsEmptyIgnoreFilter)
            {
                state.Enabled = false;
                return;
            }

            var entity = state.EntityManager.CreateEntity(typeof(SurfaceFieldsChunkRefCache));
            state.EntityManager.AddBuffer<SurfaceFieldsChunkRef>(entity);
            state.EntityManager.AddComponent<SurfaceFieldsChunkRefCacheDirty>(entity);
            state.EntityManager.SetComponentData(entity, new SurfaceFieldsChunkRefCache { Count = 0 });
            state.Enabled = false;
        }

        public void OnUpdate(ref SystemState state)
        {
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(RecordSimulationSystemGroup))]
    [UpdateBefore(typeof(FixedStepSimulationSystemGroup))]
    public partial struct SurfaceFieldsChunkRefCacheSystem : ISystem
    {
        private EntityQuery _chunkQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SurfaceFieldsChunkRefCache>();
            state.RequireForUpdate<SurfaceFieldsChunkRefCacheDirty>();
            _chunkQuery = state.GetEntityQuery(ComponentType.ReadOnly<SurfaceFieldsChunkComponent>());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var cacheEntity = SystemAPI.GetSingletonEntity<SurfaceFieldsChunkRefCache>();
            var cache = SystemAPI.GetComponentRW<SurfaceFieldsChunkRefCache>(cacheEntity);

            var currentCount = _chunkQuery.CalculateEntityCount();

            var buffer = SystemAPI.GetBuffer<SurfaceFieldsChunkRef>(cacheEntity);
            buffer.Clear();
            if (currentCount > 0)
            {
                buffer.ResizeUninitialized(currentCount);
                var index = 0;
                foreach (var chunk in SystemAPI.Query<RefRO<SurfaceFieldsChunkComponent>>())
                {
                    buffer[index++] = new SurfaceFieldsChunkRef
                    {
                        ChunkCoord = chunk.ValueRO.ChunkCoord,
                        Chunk = chunk.ValueRO.Chunk
                    };
                }
            }

            cache.ValueRW.Count = currentCount;
            state.EntityManager.RemoveComponent<SurfaceFieldsChunkRefCacheDirty>(cacheEntity);
        }
    }
}
