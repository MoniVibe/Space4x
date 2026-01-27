using PureDOTS.Environment;
using PureDOTS.Runtime.Streaming;
using PureDOTS.Runtime.WorldGen.Domain;
using PureDOTS.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.WorldGen.Systems
{
    [UpdateInGroup(typeof(RecordSimulationSystemGroup))]
    [UpdateAfter(typeof(SurfaceFieldsChunkGenerateSystem))]
    public partial struct SurfaceFieldsChunkEvictSystem : ISystem
    {
        private EntityQuery _chunkQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SurfaceFieldsStreamingConfig>();
            state.RequireForUpdate<SurfaceFieldsDomainConfig>();
            state.RequireForUpdate<SurfaceFieldsChunkRequestQueue>();
            state.RequireForUpdate<SurfaceFieldsChunkComponent>();
            state.RequireForUpdate<StreamingFocus>();

            _chunkQuery = state.GetEntityQuery(ComponentType.ReadOnly<SurfaceFieldsChunkComponent>());
        }

        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<SurfaceFieldsStreamingConfig>();
            if (config.EnableEviction == 0)
            {
                return;
            }

            var loadRadius = math.max(0, config.LoadRadiusChunks);
            var keepRadius = math.max(loadRadius, math.max(0, config.KeepRadiusChunks));

            var domainConfig = SystemAPI.GetSingleton<SurfaceFieldsDomainConfig>();
            var queueEntity = SystemAPI.GetSingletonEntity<SurfaceFieldsChunkRequestQueue>();
            var hasSphereDomain = state.EntityManager.HasComponent<SurfaceFieldsSphereCubeQuadDomainConfig>(queueEntity);
            var sphereConfig = hasSphereDomain
                ? state.EntityManager.GetComponentData<SurfaceFieldsSphereCubeQuadDomainConfig>(queueEntity)
                : default;

            using var entities = _chunkQuery.ToEntityArray(Allocator.Temp);
            using var chunks = _chunkQuery.ToComponentDataArray<SurfaceFieldsChunkComponent>(Allocator.Temp);
            using var toDestroy = new NativeList<Entity>(Allocator.Temp);

            if (hasSphereDomain)
            {
                var sphereDomain = CreateSphereDomain(domainConfig, sphereConfig);
                var keepDistanceSq = ComputeKeepDistanceSq(sphereDomain, keepRadius);

                using var focusSurfacePositions = new NativeList<float3>(Allocator.Temp);
                foreach (var focus in SystemAPI.Query<RefRO<StreamingFocus>>())
                {
                    var dir = math.normalizesafe(focus.ValueRO.Position - sphereDomain.Center, new float3(0f, 0f, 1f));
                    focusSurfacePositions.Add(sphereDomain.Center + dir * sphereDomain.Radius);
                }

                if (focusSurfacePositions.Length == 0)
                {
                    return;
                }

                var centerGrid = new int2(sphereDomain.CellsPerChunk.x >> 1, sphereDomain.CellsPerChunk.y >> 1);
                for (int i = 0; i < entities.Length; i++)
                {
                    var coord = chunks[i].ChunkCoord;
                    var chunkCenter = sphereDomain.ToWorld(coord, centerGrid);

                    var minDistSq = float.MaxValue;
                    for (int f = 0; f < focusSurfacePositions.Length; f++)
                    {
                        var d = math.lengthsq(chunkCenter - focusSurfacePositions[f]);
                        if (d < minDistSq)
                        {
                            minDistSq = d;
                        }
                    }

                    if (minDistSq > keepDistanceSq)
                    {
                        toDestroy.Add(entities[i]);
                    }
                }
            }
            else
            {
                var planarDomain = new PlanarXZDomainProvider
                {
                    CellsPerChunk = domainConfig.CellsPerChunk,
                    CellSize = domainConfig.CellSize,
                    WorldOriginXZ = domainConfig.WorldOriginXZ,
                    LatitudeOriginZ = domainConfig.LatitudeOriginZ,
                    LatitudeInvRange = domainConfig.LatitudeInvRange
                };

                using var centers = new NativeList<int3>(Allocator.Temp);
                foreach (var focus in SystemAPI.Query<RefRO<StreamingFocus>>())
                {
                    centers.Add(planarDomain.ChunkCoordFromWorld(focus.ValueRO.Position));
                }

                if (centers.Length == 0)
                {
                    return;
                }

                for (int i = 0; i < entities.Length; i++)
                {
                    var coord = chunks[i].ChunkCoord;
                    if (!IsWithinAny(centers, coord, keepRadius))
                    {
                        toDestroy.Add(entities[i]);
                    }
                }
            }

            if (toDestroy.Length > 0)
            {
                state.EntityManager.DestroyEntity(toDestroy.AsArray());
                MarkChunkCacheDirty(state.EntityManager);
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

        private static float ComputeKeepDistanceSq(in SphereCubeQuadDomainProvider domain, int keepRadiusChunks)
        {
            var cellSize = math.max(0.001f, domain.CellSize);
            var chunkSize = cellSize * math.max(domain.CellsPerChunk.x, domain.CellsPerChunk.y);
            var keepDistance = chunkSize * math.max(0, keepRadiusChunks);
            return keepDistance * keepDistance;
        }

        private static bool IsWithinAny(NativeList<int3> centers, int3 coord, int keepRadius)
        {
            for (int i = 0; i < centers.Length; i++)
            {
                var center = centers[i];
                if (coord.y != center.y)
                {
                    continue;
                }

                var dx = math.abs(coord.x - center.x);
                var dz = math.abs(coord.z - center.z);
                var d = math.max(dx, dz);
                if (d <= keepRadius)
                {
                    return true;
                }
            }

            return false;
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
