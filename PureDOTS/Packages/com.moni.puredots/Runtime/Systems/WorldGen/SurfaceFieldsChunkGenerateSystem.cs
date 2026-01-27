using PureDOTS.Environment;
using PureDOTS.Runtime.WorldGen.Domain;
using PureDOTS.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.WorldGen.Systems
{
    [UpdateInGroup(typeof(RecordSimulationSystemGroup))]
    [UpdateAfter(typeof(SurfaceFieldsChunkRequestFromStreamingFocusSystem))]
    public partial struct SurfaceFieldsChunkGenerateSystem : ISystem
    {
        private EntityQuery _existingChunksQuery;
        private EntityQuery _constraintMapQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldRecipeComponent>();
            state.RequireForUpdate<WorldGenDefinitionsComponent>();
            state.RequireForUpdate<SurfaceFieldsChunkRequestQueue>();
            state.RequireForUpdate<SurfaceFieldsDomainConfig>();
            state.RequireForUpdate<SurfaceFieldsStreamingConfig>();

            _existingChunksQuery = state.GetEntityQuery(ComponentType.ReadOnly<SurfaceFieldsChunkComponent>());
            _constraintMapQuery = state.GetEntityQuery(ComponentType.ReadOnly<SurfaceConstraintMapComponent>());
        }

        public void OnUpdate(ref SystemState state)
        {
            var entityManager = state.EntityManager;

            var queueEntity = SystemAPI.GetSingletonEntity<SurfaceFieldsChunkRequestQueue>();
            var requestBuffer = entityManager.GetBuffer<SurfaceFieldsChunkRequest>(queueEntity);
            if (requestBuffer.IsEmpty)
            {
                return;
            }

            var streamingConfig = SystemAPI.GetSingleton<SurfaceFieldsStreamingConfig>();
            var maxNewChunks = streamingConfig.MaxNewChunksPerTick <= 0 ? int.MaxValue : math.max(1, streamingConfig.MaxNewChunksPerTick);
            var generatedCount = 0;
            using var pending = new NativeList<SurfaceFieldsChunkRequest>(Allocator.Temp);

            var recipeComponent = SystemAPI.GetSingleton<WorldRecipeComponent>();
            if (!recipeComponent.Recipe.IsCreated)
            {
                requestBuffer.Clear();
                return;
            }

            var definitionsComponent = SystemAPI.GetSingleton<WorldGenDefinitionsComponent>();
            if (!definitionsComponent.Definitions.IsCreated)
            {
                requestBuffer.Clear();
                return;
            }

            var domainConfig = SystemAPI.GetSingleton<SurfaceFieldsDomainConfig>();
            var hasSphereDomain = state.EntityManager.HasComponent<SurfaceFieldsSphereCubeQuadDomainConfig>(queueEntity);
            var sphereConfig = hasSphereDomain
                ? state.EntityManager.GetComponentData<SurfaceFieldsSphereCubeQuadDomainConfig>(queueEntity)
                : default;
            var planarDomain = new PlanarXZDomainProvider
            {
                CellsPerChunk = domainConfig.CellsPerChunk,
                CellSize = domainConfig.CellSize,
                WorldOriginXZ = domainConfig.WorldOriginXZ,
                LatitudeOriginZ = domainConfig.LatitudeOriginZ,
                LatitudeInvRange = domainConfig.LatitudeInvRange
            };
            var sphereDomain = hasSphereDomain
                ? CreateSphereDomain(domainConfig, sphereConfig)
                : default;

            ref var recipe = ref recipeComponent.Recipe.Value;
            var stageIndex = FindSurfaceFieldsStageIndex(ref recipe);
            if (stageIndex < 0)
            {
                requestBuffer.Clear();
                return;
            }

            ref var stage = ref recipe.Stages[stageIndex];
            ref var definitions = ref definitionsComponent.Definitions.Value;

            var constraints = ResolveConstraintSampler(_constraintMapQuery);

            using var existingEntities = _existingChunksQuery.ToEntityArray(Allocator.Temp);
            using var existingChunks = _existingChunksQuery.ToComponentDataArray<SurfaceFieldsChunkComponent>(Allocator.Temp);
            using var existingMap = new NativeParallelHashMap<int3, Entity>(math.max(1, existingEntities.Length), Allocator.Temp);
            for (int i = 0; i < existingEntities.Length; i++)
            {
                existingMap.TryAdd(existingChunks[i].ChunkCoord, existingEntities[i]);
            }

            using var planned = new NativeParallelHashMap<int3, byte>(math.max(1, requestBuffer.Length), Allocator.Temp);

            for (int i = 0; i < requestBuffer.Length; i++)
            {
                var request = requestBuffer[i];
                if (existingMap.ContainsKey(request.ChunkCoord))
                {
                    continue;
                }

                if (!planned.TryAdd(request.ChunkCoord, 0))
                {
                    continue;
                }

                if (generatedCount >= maxNewChunks)
                {
                    pending.Add(request);
                    continue;
                }

                var chunkBlob = hasSphereDomain
                    ? SurfaceFieldsGenerator.GenerateChunk(
                        ref recipe,
                        ref stage,
                        (uint)stageIndex,
                        ref definitions,
                        in sphereDomain,
                        in constraints,
                        request.ChunkCoord,
                        Allocator.Persistent)
                    : SurfaceFieldsGenerator.GenerateChunk(
                        ref recipe,
                        ref stage,
                        (uint)stageIndex,
                        ref definitions,
                        in planarDomain,
                        in constraints,
                        request.ChunkCoord,
                        Allocator.Persistent);

                var chunkEntity = entityManager.CreateEntity(typeof(SurfaceFieldsChunkComponent), typeof(SurfaceFieldsChunkCleanup));
                entityManager.SetComponentData(chunkEntity, new SurfaceFieldsChunkComponent
                {
                    ChunkCoord = request.ChunkCoord,
                    QuantizedHash = chunkBlob.Value.QuantizedHash,
                    Chunk = chunkBlob
                });
                entityManager.SetComponentData(chunkEntity, new SurfaceFieldsChunkCleanup
                {
                    Chunk = chunkBlob
                });

                existingMap.TryAdd(request.ChunkCoord, chunkEntity);
                generatedCount++;
            }

            requestBuffer.Clear();
            if (pending.Length > 0)
            {
                requestBuffer.AddRange(pending.AsArray());
            }

            if (generatedCount > 0)
            {
                MarkChunkCacheDirty(entityManager);
            }
        }

        private static SphereCubeQuadDomainProvider CreateSphereDomain(
            in SurfaceFieldsDomainConfig domainConfig,
            in SurfaceFieldsSphereCubeQuadDomainConfig sphereConfig)
        {
            var chunksPerFace = math.max(sphereConfig.ChunksPerFace, new int2(1, 1));
            var cellsPerChunk = math.max(domainConfig.CellsPerChunk, new int2(1, 1));
            var faceCellsX = math.max(1, chunksPerFace.x * cellsPerChunk.x);
            var radius = math.max(0.001f, sphereConfig.Radius);

            return new SphereCubeQuadDomainProvider
            {
                CellsPerChunk = cellsPerChunk,
                CellSize = (2f * radius) / faceCellsX,
                Center = sphereConfig.Center,
                Radius = radius,
                ChunksPerFace = chunksPerFace
            };
        }

        private static int FindSurfaceFieldsStageIndex(ref WorldRecipeBlob recipe)
        {
            for (int i = 0; i < recipe.Stages.Length; i++)
            {
                if (recipe.Stages[i].Kind == WorldGenStageKind.SurfaceFields)
                {
                    return i;
                }
            }

            return -1;
        }

        private static SurfaceConstraintMapSampler ResolveConstraintSampler(EntityQuery query)
        {
            if (query.IsEmptyIgnoreFilter)
            {
                return default;
            }

            using var maps = query.ToComponentDataArray<SurfaceConstraintMapComponent>(Allocator.Temp);
            for (int i = 0; i < maps.Length; i++)
            {
                if (maps[i].Map.IsCreated)
                {
                    return new SurfaceConstraintMapSampler(maps[i].Map);
                }
            }

            return default;
        }

        private static void MarkChunkCacheDirty(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SurfaceFieldsChunkRefCache>());
            if (!query.TryGetSingletonEntity<SurfaceFieldsChunkRefCache>(out var cacheEntity))
            {
                return;
            }

            if (!entityManager.HasComponent<SurfaceFieldsChunkRefCacheDirty>(cacheEntity))
            {
                entityManager.AddComponent<SurfaceFieldsChunkRefCacheDirty>(cacheEntity);
            }
        }
    }
}
