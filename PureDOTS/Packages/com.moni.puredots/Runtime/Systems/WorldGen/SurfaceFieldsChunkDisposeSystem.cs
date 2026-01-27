using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.WorldGen.Systems
{
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct SurfaceFieldsChunkDisposeSystem : ISystem
    {
        private EntityQuery _cleanupQuery;

        public void OnCreate(ref SystemState state)
        {
            _cleanupQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<SurfaceFieldsChunkCleanup>(),
                ComponentType.Exclude<SurfaceFieldsChunkComponent>());
        }

        public void OnUpdate(ref SystemState state)
        {
            DisposeAndRemove(state.EntityManager, _cleanupQuery);
        }

        public void OnDestroy(ref SystemState state)
        {
            DisposeAll(ref state);
        }

        private static void DisposeAndRemove(EntityManager entityManager, EntityQuery query)
        {
            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            using var entities = query.ToEntityArray(Allocator.Temp);
            using var cleanups = query.ToComponentDataArray<SurfaceFieldsChunkCleanup>(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                var cleanup = cleanups[i];
                if (cleanup.Chunk.IsCreated)
                {
                    cleanup.Chunk.Dispose();
                }

                entityManager.RemoveComponent<SurfaceFieldsChunkCleanup>(entities[i]);
            }
        }

        private static void DisposeAll(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SurfaceFieldsChunkCleanup>());
            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            using var entities = query.ToEntityArray(Allocator.Temp);
            using var cleanups = query.ToComponentDataArray<SurfaceFieldsChunkCleanup>(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                var cleanup = cleanups[i];
                if (cleanup.Chunk.IsCreated)
                {
                    cleanup.Chunk.Dispose();
                }

                if (entityManager.HasComponent<SurfaceFieldsChunkCleanup>(entities[i]))
                {
                    entityManager.RemoveComponent<SurfaceFieldsChunkCleanup>(entities[i]);
                }
            }
        }
    }
}
