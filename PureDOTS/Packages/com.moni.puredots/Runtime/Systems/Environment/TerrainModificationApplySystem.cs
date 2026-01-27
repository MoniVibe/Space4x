using PureDOTS.Environment;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Environment
{
    /// <summary>
    /// Applies terrain modification requests and emits terrain change events.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(EnvironmentSystemGroup))]
    [UpdateBefore(typeof(TerrainChangeProcessorSystem))]
    public partial struct TerrainModificationApplySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<TerrainModificationQueue>();
            state.RequireForUpdate<TerrainVersion>();
            state.RequireForUpdate<TerrainWorldConfig>();
        }

        [BurstCompile]
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

            if (!SystemAPI.TryGetSingletonEntity<TerrainVersion>(out var terrainVersionEntity))
            {
                return;
            }

            if (!state.EntityManager.HasBuffer<TerrainChangeEvent>(terrainVersionEntity))
            {
                state.EntityManager.AddBuffer<TerrainChangeEvent>(terrainVersionEntity);
            }

            var budget = SystemAPI.GetComponent<TerrainModificationBudget>(queueEntity);
            var dirtyRegions = SystemAPI.GetBuffer<TerrainDirtyRegion>(queueEntity);
            dirtyRegions.Clear();
            var modificationEvents = SystemAPI.GetBuffer<TerrainModificationEvent>(queueEntity);
            modificationEvents.Clear();

            var requests = SystemAPI.GetBuffer<TerrainModificationRequest>(queueEntity);
            if (requests.Length == 0)
            {
                return;
            }

            var requestsCopy = new NativeList<TerrainModificationRequest>(requests.Length, Allocator.Temp);
            for (int i = 0; i < requests.Length; i++)
            {
                requestsCopy.Add(requests[i]);
            }
            requests.Clear();

            var terrainConfig = SystemAPI.GetSingleton<TerrainWorldConfig>();
            var volumeLookup = SystemAPI.GetComponentLookup<TerrainVolume>(true);
            volumeLookup.Update(ref state);
            var localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            localTransformLookup.Update(ref state);

            var changeEvents = state.EntityManager.GetBuffer<TerrainChangeEvent>(terrainVersionEntity);
            var terrainVersion = SystemAPI.GetComponent<TerrainVersion>(terrainVersionEntity);
            uint nextVersion = terrainVersion.Value + 1u;

            var processed = math.min(requestsCopy.Length, budget.MaxEditsPerTick);
            using var chunkMap = BuildChunkLookup(ref state);
            for (int i = 0; i < processed; i++)
            {
                var request = requestsCopy[i];

                var flags = ResolveChangeEventFlags(request);
                var changeFlags = ResolveChangeFlags(request);
                var volumeEntity = request.VolumeEntity;
                var volumeOrigin = ResolveVolumeOrigin(volumeEntity, terrainConfig, volumeLookup);
                var hasVolumeTransform = false;
                var volumeLocalToWorld = float4x4.identity;
                if (volumeEntity != Entity.Null)
                {
                    if (localTransformLookup.HasComponent(volumeEntity))
                    {
                        var localTransform = localTransformLookup[volumeEntity];
                        volumeLocalToWorld = float4x4.TRS(localTransform.Position, localTransform.Rotation, new float3(localTransform.Scale));
                        hasVolumeTransform = true;
                    }
                }

                var volumeWorldToLocal = hasVolumeTransform ? math.inverse(volumeLocalToWorld) : float4x4.identity;

                ResolveBounds(request, hasVolumeTransform, volumeLocalToWorld, volumeWorldToLocal,
                    out var localBounds, out var worldBounds, out var localStart, out var localEnd);

                var localRequest = request;
                localRequest.Start = localStart;
                localRequest.End = localEnd;

                var clearedVoxels = ApplyModificationToChunks(ref state, chunkMap, terrainConfig, volumeOrigin, volumeEntity, localRequest,
                    localBounds.Min, localBounds.Max, nextVersion, timeState.Tick);

                // Reacquire buffers after any structural changes performed during chunk updates.
                dirtyRegions = SystemAPI.GetBuffer<TerrainDirtyRegion>(queueEntity);
                modificationEvents = SystemAPI.GetBuffer<TerrainModificationEvent>(queueEntity);
                var surfaceTileVersions = SystemAPI.GetBuffer<TerrainSurfaceTileVersion>(queueEntity);
                var undergroundChunkVersions = SystemAPI.GetBuffer<TerrainUndergroundChunkVersion>(queueEntity);
                changeEvents = state.EntityManager.GetBuffer<TerrainChangeEvent>(terrainVersionEntity);

                changeEvents.Add(new TerrainChangeEvent
                {
                    Version = nextVersion,
                    WorldMin = worldBounds.Min,
                    WorldMax = worldBounds.Max,
                    Flags = flags
                });

                if (dirtyRegions.Length < budget.MaxDirtyRegionsPerTick)
                {
                    dirtyRegions.Add(new TerrainDirtyRegion
                    {
                        WorldMin = worldBounds.Min,
                        WorldMax = worldBounds.Max,
                        Version = nextVersion,
                        Flags = flags
                    });
                }

                if ((changeFlags & TerrainModificationFlags.AffectsSurface) != 0)
                {
                    UpdateSurfaceTileVersions(surfaceTileVersions, terrainConfig, worldBounds.Min, worldBounds.Max, nextVersion);
                }

                if ((changeFlags & TerrainModificationFlags.AffectsVolume) != 0)
                {
                    UpdateUndergroundChunkVersions(undergroundChunkVersions, terrainConfig, volumeEntity, volumeOrigin,
                        localBounds.Min, localBounds.Max, nextVersion);
                }

                if (clearedVoxels > 0)
                {
                    var worldStart = request.Space == TerrainModificationSpace.VolumeLocal
                        ? (hasVolumeTransform ? TransformPoint(volumeLocalToWorld, localStart) : localStart)
                        : request.Start;
                    var worldEnd = request.Space == TerrainModificationSpace.VolumeLocal
                        ? (hasVolumeTransform ? TransformPoint(volumeLocalToWorld, localEnd) : localEnd)
                        : request.End;

                    modificationEvents.Add(new TerrainModificationEvent
                    {
                        WorldPosition = (worldStart + worldEnd) * 0.5f,
                        WorldDirection = math.normalizesafe(worldEnd - worldStart, new float3(0f, 1f, 0f)),
                        Radius = math.max(0f, request.Radius),
                        ClearedVoxels = clearedVoxels,
                        ToolKind = request.ToolKind,
                        Shape = request.Shape,
                        VolumeEntity = request.VolumeEntity,
                        Tick = timeState.Tick
                    });
                }
            }

            requestsCopy.Dispose();
        }

        private static TerrainModificationFlags ResolveChangeFlags(in TerrainModificationRequest request)
        {
            var flags = request.Flags;
            if (flags == TerrainModificationFlags.None)
            {
                flags = TerrainModificationFlags.AffectsSurface | TerrainModificationFlags.AffectsVolume;
            }

            return flags;
        }

        private static byte ResolveChangeEventFlags(in TerrainModificationRequest request)
        {
            var flags = ResolveChangeFlags(request);
            byte result = 0;
            if ((flags & TerrainModificationFlags.AffectsSurface) != 0)
            {
                result |= TerrainChangeEvent.FlagHeightChanged;
            }

            if ((flags & TerrainModificationFlags.AffectsMaterial) != 0)
            {
                result |= TerrainChangeEvent.FlagSurfaceMaterialChanged;
            }

            if ((flags & TerrainModificationFlags.AffectsVolume) != 0)
            {
                result |= TerrainChangeEvent.FlagVolumeChanged;
            }

            return result;
        }

        private static MinMaxAABB ComputeBounds(in TerrainModificationRequest request, float3 start, float3 end)
        {
            var min = math.min(start, end);
            var max = math.max(start, end);

            var extent = new float3(math.max(0f, request.Radius), math.max(0f, request.Depth), math.max(0f, request.Radius));
            min -= extent;
            max += extent;

            return new MinMaxAABB { Min = min, Max = max };
        }

        private static float3 ResolveVolumeOrigin(Entity volumeEntity, in TerrainWorldConfig config, ComponentLookup<TerrainVolume> volumeLookup)
        {
            if (volumeEntity != Entity.Null && volumeLookup.HasComponent(volumeEntity))
            {
                return volumeLookup[volumeEntity].LocalOrigin;
            }

            return config.VolumeWorldOrigin;
        }

        private static void ResolveBounds(
            in TerrainModificationRequest request,
            bool hasVolumeTransform,
            in float4x4 volumeLocalToWorld,
            in float4x4 volumeWorldToLocal,
            out MinMaxAABB localBounds,
            out MinMaxAABB worldBounds,
            out float3 localStart,
            out float3 localEnd)
        {
            if (request.Space == TerrainModificationSpace.VolumeLocal)
            {
                localStart = request.Start;
                localEnd = request.End;
                localBounds = ComputeBounds(request, localStart, localEnd);
                worldBounds = hasVolumeTransform ? TransformBounds(localBounds, volumeLocalToWorld) : localBounds;
                return;
            }

            worldBounds = ComputeBounds(request, request.Start, request.End);
            if (hasVolumeTransform)
            {
                localStart = TransformPoint(volumeWorldToLocal, request.Start);
                localEnd = TransformPoint(volumeWorldToLocal, request.End);
                localBounds = ComputeBounds(request, localStart, localEnd);
            }
            else
            {
                localStart = request.Start;
                localEnd = request.End;
                localBounds = worldBounds;
            }
        }

        private static MinMaxAABB TransformBounds(in MinMaxAABB bounds, in float4x4 matrix)
        {
            var min = bounds.Min;
            var max = bounds.Max;

            var v0 = TransformPoint(matrix, new float3(min.x, min.y, min.z));
            var v1 = TransformPoint(matrix, new float3(max.x, min.y, min.z));
            var v2 = TransformPoint(matrix, new float3(min.x, max.y, min.z));
            var v3 = TransformPoint(matrix, new float3(min.x, min.y, max.z));
            var v4 = TransformPoint(matrix, new float3(max.x, max.y, min.z));
            var v5 = TransformPoint(matrix, new float3(min.x, max.y, max.z));
            var v6 = TransformPoint(matrix, new float3(max.x, min.y, max.z));
            var v7 = TransformPoint(matrix, new float3(max.x, max.y, max.z));

            var newMin = math.min(math.min(math.min(v0, v1), math.min(v2, v3)), math.min(math.min(v4, v5), math.min(v6, v7)));
            var newMax = math.max(math.max(math.max(v0, v1), math.max(v2, v3)), math.max(math.max(v4, v5), math.max(v6, v7)));

            return new MinMaxAABB { Min = newMin, Max = newMax };
        }

        private static float3 TransformPoint(in float4x4 matrix, float3 point)
        {
            var result = math.mul(matrix, new float4(point, 1f));
            return result.xyz;
        }

        private static NativeParallelHashMap<TerrainChunkKey, Entity> BuildChunkLookup(ref SystemState state)
        {
            var query = state.GetEntityQuery(ComponentType.ReadOnly<TerrainChunk>());
            var chunkCount = query.CalculateEntityCount();
            var map = new NativeParallelHashMap<TerrainChunkKey, Entity>(math.max(1, chunkCount), Allocator.Temp);

            using var chunkData = query.ToComponentDataArray<TerrainChunk>(Allocator.Temp);
            using var chunkEntities = query.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < chunkData.Length; i++)
            {
                var chunk = chunkData[i];
                var entity = chunkEntities[i];
                map.TryAdd(new TerrainChunkKey
                {
                    VolumeEntity = chunk.VolumeEntity,
                    ChunkCoord = chunk.ChunkCoord
                }, entity);
            }

            return map;
        }

        private static int ApplyModificationToChunks(
            ref SystemState state,
            in NativeParallelHashMap<TerrainChunkKey, Entity> chunkMap,
            in TerrainWorldConfig config,
            float3 volumeOrigin,
            Entity volumeEntity,
            in TerrainModificationRequest request,
            float3 localMin,
            float3 localMax,
            uint version,
            uint currentTick)
        {
            if (chunkMap.IsEmpty)
            {
                return 0;
            }

            var chunkSize = new float3(config.VoxelsPerChunk.x, config.VoxelsPerChunk.y, config.VoxelsPerChunk.z) * config.VoxelSize;
            var minChunk = (int3)math.floor((localMin - volumeOrigin) / chunkSize);
            var maxChunk = (int3)math.floor((localMax - volumeOrigin) / chunkSize);
            var clearedVoxels = 0;

            for (int z = minChunk.z; z <= maxChunk.z; z++)
            {
                for (int y = minChunk.y; y <= maxChunk.y; y++)
                {
                    for (int x = minChunk.x; x <= maxChunk.x; x++)
                    {
                        var coord = new int3(x, y, z);
                        var key = new TerrainChunkKey { VolumeEntity = volumeEntity, ChunkCoord = coord };
                        if (!chunkMap.TryGetValue(key, out var chunkEntity))
                        {
                            continue;
                        }

                        var chunk = state.EntityManager.GetComponentData<TerrainChunk>(chunkEntity);
                        var voxelsPerChunk = chunk.VoxelsPerChunk;
                        if (voxelsPerChunk.x <= 0 || voxelsPerChunk.y <= 0 || voxelsPerChunk.z <= 0)
                        {
                            continue;
                        }

                        var voxelCount = voxelsPerChunk.x * voxelsPerChunk.y * voxelsPerChunk.z;
                        DynamicBuffer<TerrainVoxelRuntime> runtime;
                        if (!state.EntityManager.HasBuffer<TerrainVoxelRuntime>(chunkEntity))
                        {
                            runtime = state.EntityManager.AddBuffer<TerrainVoxelRuntime>(chunkEntity);
                            runtime.ResizeUninitialized(voxelCount);
                            InitializeRuntimeBuffer(chunk, runtime, voxelCount);
                        }
                        else
                        {
                            runtime = state.EntityManager.GetBuffer<TerrainVoxelRuntime>(chunkEntity);
                            if (runtime.Length != voxelCount)
                            {
                                runtime.ResizeUninitialized(voxelCount);
                                InitializeRuntimeBuffer(chunk, runtime, voxelCount);
                            }
                        }

                        clearedVoxels += ApplyModificationToChunkBuffer(config, volumeOrigin, coord, voxelsPerChunk, request, localMin, localMax, runtime);
                        MarkChunkDirty(ref state, chunkEntity, version, currentTick);
                    }
                }
            }

            return clearedVoxels;
        }

        private static void InitializeRuntimeBuffer(in TerrainChunk chunk, DynamicBuffer<TerrainVoxelRuntime> runtime, int voxelCount)
        {
            if (chunk.BaseBlob.IsCreated)
            {
                ref var baseBlob = ref chunk.BaseBlob.Value;
                if (baseBlob.SolidMask.Length < voxelCount)
                {
                    for (int i = 0; i < voxelCount; i++)
                    {
                        runtime[i] = default;
                    }
                    return;
                }

                for (int i = 0; i < voxelCount; i++)
                {
                    runtime[i] = new TerrainVoxelRuntime
                    {
                        SolidMask = baseBlob.SolidMask[i],
                        MaterialId = baseBlob.MaterialId.Length > i ? baseBlob.MaterialId[i] : (byte)0,
                        DepositId = baseBlob.DepositId.Length > i ? baseBlob.DepositId[i] : (byte)0,
                        OreGrade = baseBlob.OreGrade.Length > i ? baseBlob.OreGrade[i] : (byte)0,
                        Damage = 0
                    };
                }
            }
            else
            {
                for (int i = 0; i < voxelCount; i++)
                {
                    runtime[i] = default;
                }
            }
        }

        private static int ApplyModificationToChunkBuffer(
            in TerrainWorldConfig config,
            float3 volumeOrigin,
            int3 chunkCoord,
            int3 voxelsPerChunk,
            in TerrainModificationRequest request,
            float3 localMin,
            float3 localMax,
            DynamicBuffer<TerrainVoxelRuntime> runtime)
        {
            var chunkSize = new float3(voxelsPerChunk.x, voxelsPerChunk.y, voxelsPerChunk.z) * config.VoxelSize;
            var chunkOrigin = volumeOrigin + new float3(chunkCoord.x * chunkSize.x, chunkCoord.y * chunkSize.y, chunkCoord.z * chunkSize.z);

            var minLocal = (localMin - chunkOrigin) / config.VoxelSize;
            var maxLocal = (localMax - chunkOrigin) / config.VoxelSize;
            var minVoxel = new int3(
                math.clamp((int)math.floor(minLocal.x), 0, voxelsPerChunk.x - 1),
                math.clamp((int)math.floor(minLocal.y), 0, voxelsPerChunk.y - 1),
                math.clamp((int)math.floor(minLocal.z), 0, voxelsPerChunk.z - 1));
            var maxVoxel = new int3(
                math.clamp((int)math.floor(maxLocal.x), 0, voxelsPerChunk.x - 1),
                math.clamp((int)math.floor(maxLocal.y), 0, voxelsPerChunk.y - 1),
                math.clamp((int)math.floor(maxLocal.z), 0, voxelsPerChunk.z - 1));

            var voxelSize = config.VoxelSize;
            var radiusSq = request.Radius * request.Radius;
            var minDepthY = math.min(request.Start.y, request.End.y) - math.max(0f, request.Depth);
            var maxDepthY = math.max(request.Start.y, request.End.y);
            var clearedCount = 0;

            for (int z = minVoxel.z; z <= maxVoxel.z; z++)
            {
                for (int y = minVoxel.y; y <= maxVoxel.y; y++)
                {
                    for (int x = minVoxel.x; x <= maxVoxel.x; x++)
                    {
                        var localPos = chunkOrigin + new float3((x + 0.5f) * voxelSize, (y + 0.5f) * voxelSize, (z + 0.5f) * voxelSize);
                        if (request.Depth > 0f && (localPos.y < minDepthY || localPos.y > maxDepthY))
                        {
                            continue;
                        }

                        if (!IsInsideShape(request, localPos, radiusSq))
                        {
                            continue;
                        }

                        var index = x + (y * voxelsPerChunk.x) + (z * voxelsPerChunk.x * voxelsPerChunk.y);
                        var voxel = runtime[index];
                        if (ApplyVoxelModification(request, ref voxel))
                        {
                            clearedCount++;
                        }
                        runtime[index] = voxel;
                    }
                }
            }

            return clearedCount;
        }

        private static bool IsInsideShape(in TerrainModificationRequest request, float3 worldPos, float radiusSq)
        {
            switch (request.Shape)
            {
                case TerrainModificationShape.Tunnel:
                case TerrainModificationShape.Ramp:
                    return DistanceSqPointToSegment(worldPos, request.Start, request.End) <= radiusSq;
                default:
                    return math.lengthsq(worldPos - request.Start) <= radiusSq;
            }
        }

        private static float DistanceSqPointToSegment(float3 point, float3 a, float3 b)
        {
            var ab = b - a;
            var lengthSq = math.lengthsq(ab);
            if (lengthSq <= 1e-5f)
            {
                return math.lengthsq(point - a);
            }

            var t = math.clamp(math.dot(point - a, ab) / lengthSq, 0f, 1f);
            var closest = a + ab * t;
            return math.lengthsq(point - closest);
        }

        private static bool ApplyVoxelModification(in TerrainModificationRequest request, ref TerrainVoxelRuntime voxel)
        {
            switch (request.Kind)
            {
                case TerrainModificationKind.Fill:
                    voxel.SolidMask = 1;
                    if (request.MaterialId != 0)
                    {
                        voxel.MaterialId = request.MaterialId;
                    }
                    voxel.Damage = 0;
                    return false;
                case TerrainModificationKind.PaintMaterial:
                    if (request.MaterialId != 0)
                    {
                        voxel.MaterialId = request.MaterialId;
                    }
                    return false;
                default:
                    if (request.ToolKind == TerrainModificationToolKind.Microwave)
                    {
                        if (voxel.SolidMask != 0)
                        {
                            var delta = request.DamageDelta == 0 ? (byte)8 : request.DamageDelta;
                            var threshold = request.DamageThreshold == 0 ? (byte)255 : request.DamageThreshold;
                            var damage = math.min(255, voxel.Damage + delta);
                            if (damage >= threshold)
                            {
                                voxel.SolidMask = 0;
                                voxel.Damage = 0;
                                return true;
                            }
                            else
                            {
                                voxel.Damage = (byte)damage;
                            }
                        }
                    }
                    else
                    {
                        if (voxel.SolidMask != 0)
                        {
                            voxel.SolidMask = 0;
                            voxel.Damage = 0;
                            return true;
                        }
                    }
                    break;
            }

            return false;
        }

        private static void MarkChunkDirty(ref SystemState state, Entity chunkEntity, uint version, uint tick)
        {
            if (state.EntityManager.HasComponent<TerrainChunkDirty>(chunkEntity))
            {
                var dirty = state.EntityManager.GetComponentData<TerrainChunkDirty>(chunkEntity);
                dirty.EditVersion = math.max(dirty.EditVersion, version);
                dirty.LastEditTick = tick;
                state.EntityManager.SetComponentData(chunkEntity, dirty);
            }
            else
            {
                state.EntityManager.AddComponentData(chunkEntity, new TerrainChunkDirty
                {
                    EditVersion = version,
                    LastEditTick = tick
                });
            }
        }

        private static void UpdateSurfaceTileVersions(
            DynamicBuffer<TerrainSurfaceTileVersion> buffer,
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
                    SetSurfaceTileVersion(buffer, new int2(x, z), version);
                }
            }
        }

        private static void SetSurfaceTileVersion(DynamicBuffer<TerrainSurfaceTileVersion> buffer, int2 coord, uint version)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].TileCoord.Equals(coord))
                {
                    var entry = buffer[i];
                    entry.Version = version;
                    buffer[i] = entry;
                    return;
                }
            }

            buffer.Add(new TerrainSurfaceTileVersion
            {
                TileCoord = coord,
                Version = version
            });
        }

        private static void UpdateUndergroundChunkVersions(
            DynamicBuffer<TerrainUndergroundChunkVersion> buffer,
            in TerrainWorldConfig config,
            Entity volumeEntity,
            float3 volumeOrigin,
            float3 localMin,
            float3 localMax,
            uint version)
        {
            var chunkSize = new float3(config.VoxelsPerChunk.x, config.VoxelsPerChunk.y, config.VoxelsPerChunk.z) * config.VoxelSize;
            var minCoord = (int3)math.floor((localMin - volumeOrigin) / chunkSize);
            var maxCoord = (int3)math.floor((localMax - volumeOrigin) / chunkSize);

            for (int z = minCoord.z; z <= maxCoord.z; z++)
            {
                for (int y = minCoord.y; y <= maxCoord.y; y++)
                {
                    for (int x = minCoord.x; x <= maxCoord.x; x++)
                    {
                        SetUndergroundChunkVersion(buffer, volumeEntity, new int3(x, y, z), version);
                    }
                }
            }
        }

        private static void SetUndergroundChunkVersion(
            DynamicBuffer<TerrainUndergroundChunkVersion> buffer,
            Entity volumeEntity,
            int3 coord,
            uint version)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].VolumeEntity == volumeEntity && buffer[i].ChunkCoord.Equals(coord))
                {
                    var entry = buffer[i];
                    entry.Version = version;
                    buffer[i] = entry;
                    return;
                }
            }

            buffer.Add(new TerrainUndergroundChunkVersion
            {
                VolumeEntity = volumeEntity,
                ChunkCoord = coord,
                Version = version
            });
        }
    }
}
