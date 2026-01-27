using PureDOTS.Environment;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Navigation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Environment
{
    /// <summary>
    /// Stub updater for derived nav caches (surface tiles + underground chunks).
    /// </summary>
    [UpdateInGroup(typeof(EnvironmentSystemGroup))]
    [UpdateAfter(typeof(TerrainModificationApplySystem))]
    public partial struct TerrainDerivedNavCacheSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<TerrainModificationQueue>();
            state.RequireForUpdate<TerrainWorldConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (!SystemAPI.TryGetSingletonEntity<TerrainModificationQueue>(out var queueEntity))
            {
                return;
            }

            var dirtyRegions = SystemAPI.GetBuffer<TerrainDirtyRegion>(queueEntity);
            if (dirtyRegions.Length == 0)
            {
                return;
            }

            var config = SystemAPI.GetSingleton<TerrainWorldConfig>();

            using var surfaceLookup = BuildSurfaceTileLookup(ref state);
            using var undergroundLookup = BuildUndergroundChunkLookup(ref state);

            for (int i = 0; i < dirtyRegions.Length; i++)
            {
                var region = dirtyRegions[i];
                UpdateSurfaceTiles(ref state, surfaceLookup, config, region.WorldMin, region.WorldMax, region.Version);
                UpdateUndergroundChunks(ref state, undergroundLookup, config, region.WorldMin, region.WorldMax, region.Version);
            }
        }

        private static NativeParallelHashMap<int2, Entity> BuildSurfaceTileLookup(ref SystemState state)
        {
            var query = state.GetEntityQuery(ComponentType.ReadOnly<SurfaceNavTile>());
            var count = query.CalculateEntityCount();
            var map = new NativeParallelHashMap<int2, Entity>(math.max(1, count), Allocator.Temp);

            using var tiles = query.ToComponentDataArray<SurfaceNavTile>(Allocator.Temp);
            using var entities = query.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < tiles.Length; i++)
            {
                map.TryAdd(tiles[i].TileCoord, entities[i]);
            }

            return map;
        }

        private static NativeParallelHashMap<int3, Entity> BuildUndergroundChunkLookup(ref SystemState state)
        {
            var query = state.GetEntityQuery(ComponentType.ReadOnly<UndergroundNavChunk>());
            var count = query.CalculateEntityCount();
            var map = new NativeParallelHashMap<int3, Entity>(math.max(1, count), Allocator.Temp);

            using var chunks = query.ToComponentDataArray<UndergroundNavChunk>(Allocator.Temp);
            using var entities = query.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < chunks.Length; i++)
            {
                map.TryAdd(chunks[i].ChunkCoord, entities[i]);
            }

            return map;
        }

        private static void UpdateSurfaceTiles(
            ref SystemState state,
            NativeParallelHashMap<int2, Entity> lookup,
            in TerrainWorldConfig config,
            float3 worldMin,
            float3 worldMax,
            uint version)
        {
            var tileSize = new float2(
                math.max(0.01f, config.SurfaceCellSize * config.SurfaceCellsPerTile.x),
                math.max(0.01f, config.SurfaceCellSize * config.SurfaceCellsPerTile.y));

            var minCoord = (int2)math.floor((worldMin.xz - config.SurfaceWorldOriginXZ) / tileSize);
            var maxCoord = (int2)math.floor((worldMax.xz - config.SurfaceWorldOriginXZ) / tileSize);

            for (int z = minCoord.y; z <= maxCoord.y; z++)
            {
                for (int x = minCoord.x; x <= maxCoord.x; x++)
                {
                    EnsureSurfaceTile(ref state, lookup, new int2(x, z), version);
                }
            }
        }

        private static void EnsureSurfaceTile(
            ref SystemState state,
            NativeParallelHashMap<int2, Entity> lookup,
            int2 coord,
            uint version)
        {
            if (lookup.TryGetValue(coord, out var entity))
            {
                var tile = state.EntityManager.GetComponentData<SurfaceNavTile>(entity);
                tile.Version = math.max(tile.Version, version);
                state.EntityManager.SetComponentData(entity, tile);
                return;
            }

            entity = state.EntityManager.CreateEntity(typeof(SurfaceNavTile));
            state.EntityManager.SetComponentData(entity, new SurfaceNavTile
            {
                TileCoord = coord,
                Version = version,
                Data = default
            });
            lookup.TryAdd(coord, entity);
        }

        private static void UpdateUndergroundChunks(
            ref SystemState state,
            NativeParallelHashMap<int3, Entity> lookup,
            in TerrainWorldConfig config,
            float3 worldMin,
            float3 worldMax,
            uint version)
        {
            var chunkSize = new float3(config.VoxelsPerChunk.x, config.VoxelsPerChunk.y, config.VoxelsPerChunk.z) * config.VoxelSize;
            var minCoord = (int3)math.floor((worldMin - config.VolumeWorldOrigin) / chunkSize);
            var maxCoord = (int3)math.floor((worldMax - config.VolumeWorldOrigin) / chunkSize);

            for (int z = minCoord.z; z <= maxCoord.z; z++)
            {
                for (int y = minCoord.y; y <= maxCoord.y; y++)
                {
                    for (int x = minCoord.x; x <= maxCoord.x; x++)
                    {
                        EnsureUndergroundChunk(ref state, lookup, new int3(x, y, z), version);
                    }
                }
            }
        }

        private static void EnsureUndergroundChunk(
            ref SystemState state,
            NativeParallelHashMap<int3, Entity> lookup,
            int3 coord,
            uint version)
        {
            if (lookup.TryGetValue(coord, out var entity))
            {
                var chunk = state.EntityManager.GetComponentData<UndergroundNavChunk>(entity);
                chunk.Version = math.max(chunk.Version, version);
                state.EntityManager.SetComponentData(entity, chunk);
                return;
            }

            entity = state.EntityManager.CreateEntity(typeof(UndergroundNavChunk));
            state.EntityManager.SetComponentData(entity, new UndergroundNavChunk
            {
                ChunkCoord = coord,
                Version = version,
                Data = default
            });
            lookup.TryAdd(coord, entity);
        }
    }
}
