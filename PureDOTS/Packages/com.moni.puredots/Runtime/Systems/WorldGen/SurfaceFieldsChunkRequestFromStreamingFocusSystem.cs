using PureDOTS.Runtime.Streaming;
using PureDOTS.Runtime.WorldGen.Domain;
using PureDOTS.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.WorldGen.Systems
{
    [UpdateInGroup(typeof(RecordSimulationSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.Streaming.StreamingFocusUpdateSystem))]
    [UpdateBefore(typeof(SurfaceFieldsChunkGenerateSystem))]
    public partial struct SurfaceFieldsChunkRequestFromStreamingFocusSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SurfaceFieldsChunkRequestQueue>();
            state.RequireForUpdate<SurfaceFieldsDomainConfig>();
            state.RequireForUpdate<SurfaceFieldsStreamingConfig>();
            state.RequireForUpdate<StreamingFocus>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<SurfaceFieldsStreamingConfig>();
            var loadRadius = math.max(0, config.LoadRadiusChunks);

            var domainConfig = SystemAPI.GetSingleton<SurfaceFieldsDomainConfig>();

            var queueEntity = SystemAPI.GetSingletonEntity<SurfaceFieldsChunkRequestQueue>();
            var requestBuffer = state.EntityManager.GetBuffer<SurfaceFieldsChunkRequest>(queueEntity);
            var hasSphereDomain = state.EntityManager.HasComponent<SurfaceFieldsSphereCubeQuadDomainConfig>(queueEntity);
            var sphereConfig = hasSphereDomain
                ? state.EntityManager.GetComponentData<SurfaceFieldsSphereCubeQuadDomainConfig>(queueEntity)
                : default;

            var estimated = math.max(1, (2 * loadRadius + 1) * (2 * loadRadius + 1));
            using var requested = new NativeParallelHashMap<int3, byte>(math.max(estimated, requestBuffer.Length + 1), Allocator.Temp);
            for (int i = 0; i < requestBuffer.Length; i++)
            {
                requested.TryAdd(requestBuffer[i].ChunkCoord, 0);
            }

            if (hasSphereDomain)
            {
                var sphereDomain = CreateSphereDomain(domainConfig, sphereConfig);
                foreach (var focus in SystemAPI.Query<RefRO<StreamingFocus>>())
                {
                    var center = sphereDomain.ChunkCoordFromWorld(focus.ValueRO.Position);
                    EnqueueSphereSquare(requested, requestBuffer, sphereDomain, center, loadRadius);
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

                foreach (var focus in SystemAPI.Query<RefRO<StreamingFocus>>())
                {
                    var center = planarDomain.ChunkCoordFromWorld(focus.ValueRO.Position);
                    EnqueueSquare(requested, requestBuffer, center, loadRadius);
                }
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

        private static void EnqueueSquare(
            NativeParallelHashMap<int3, byte> requested,
            DynamicBuffer<SurfaceFieldsChunkRequest> buffer,
            int3 center,
            int radius)
        {
            for (int dz = -radius; dz <= radius; dz++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var coord = new int3(center.x + dx, center.y, center.z + dz);
                    if (requested.TryAdd(coord, 0))
                    {
                        buffer.Add(new SurfaceFieldsChunkRequest { ChunkCoord = coord });
                    }
                }
            }
        }

        private static void EnqueueSphereSquare(
            NativeParallelHashMap<int3, byte> requested,
            DynamicBuffer<SurfaceFieldsChunkRequest> buffer,
            in SphereCubeQuadDomainProvider domain,
            int3 center,
            int radius)
        {
            var sampleGrid = new int2(domain.CellsPerChunk.x >> 1, domain.CellsPerChunk.y >> 1);
            for (int dv = -radius; dv <= radius; dv++)
            {
                for (int du = -radius; du <= radius; du++)
                {
                    var candidate = new int3(center.x + du, center.y, center.z + dv);
                    var worldPos = domain.ToWorld(candidate, sampleGrid);
                    var coord = domain.ChunkCoordFromWorld(worldPos);
                    if (requested.TryAdd(coord, 0))
                    {
                        buffer.Add(new SurfaceFieldsChunkRequest { ChunkCoord = coord });
                    }
                }
            }
        }
    }
}
