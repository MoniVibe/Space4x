using System;
using PureDOTS.Environment;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Streaming;
using PureDOTS.Runtime.Components;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using UTime = UnityEngine.Time;
using UObject = UnityEngine.Object;

namespace Space4X.Presentation
{
    public sealed class Space4XAsteroidChunkRenderConfig : IComponentData
    {
        public Material Material;
    }

    public sealed class Space4XAsteroidChunkPaletteConfig : IComponentData
    {
        public Texture2D Palette;
    }

    public sealed class Space4XAsteroidChunkMeshReference : IComponentData
    {
        public Mesh Mesh;
    }

    public struct Space4XAsteroidChunkMeshState : IComponentData
    {
        public uint LastBuiltVersion;
        public uint LastQueuedVersion;
    }

    public struct Space4XAsteroidChunkMeshCleanup : ICleanupComponentData
    {
    }

    public struct Space4XAsteroidChunkMeshRebuildQueue : IComponentData
    {
    }

    public struct Space4XAsteroidChunkRebuildRequest : IBufferElementData
    {
        public Entity Chunk;
    }

    public struct Space4XAsteroidChunkMeshRebuildConfig : IComponentData
    {
        public float MaxBuildMillisecondsPerFrame;
        public int MinChunksPerFrame;
        public int NearRebuildCap;
        public float NearRadius;

        public static Space4XAsteroidChunkMeshRebuildConfig Default => new()
        {
            MaxBuildMillisecondsPerFrame = 3f,
            MinChunksPerFrame = 1,
            NearRebuildCap = 2,
            NearRadius = 40f
        };
    }

    public struct Space4XAsteroidChunkFaceCell
    {
        public byte Exists;
        public byte MaterialId;
        public byte OreGrade;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public struct Space4XAsteroidChunkMeshTelemetry : IComponentData
    {
        public float LastBuildMs;
        public int LastVertexCount;
        public int LastIndexCount;
        public int LastQuadCountBeforeGreedy;
        public int LastQuadCountAfterGreedy;
        public uint LastBuiltVersion;
        public uint LastBuiltTick;
    }

    public struct Space4XAsteroidChunkMeshFrameStats : IComponentData
    {
        public int ChunksBuiltThisFrame;
        public float TotalBuildMsThisFrame;
        public int TotalVertsThisFrame;
        public int TotalIndicesThisFrame;
        public float LastChunkBuildMs;
        public int QueueLength;
        public int SkippedDueToBudget;
        public float BudgetMs;
        public int MinChunksPerFrame;
    }
#endif

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XPresentationLifecycleSystem))]
    public partial class Space4XAsteroidChunkMeshQueueSystem : SystemBase
    {
        private NativeParallelHashMap<TerrainChunkKey, Entity> _knownChunks;

        protected override void OnCreate()
        {
            RequireForUpdate<TerrainWorldConfig>();
            _knownChunks = new NativeParallelHashMap<TerrainChunkKey, Entity>(128, Allocator.Persistent);
        }

        protected override void OnUpdate()
        {
            if (!RuntimeMode.IsRenderingEnabled)
            {
                return;
            }

            EnsureQueue();
            var queueEntity = SystemAPI.GetSingletonEntity<Space4XAsteroidChunkMeshRebuildQueue>();
            var queue = EntityManager.GetBuffer<Space4XAsteroidChunkRebuildRequest>(queueEntity);
            var volumeConfigLookup = GetComponentLookup<Space4XAsteroidVolumeConfig>(true);
            volumeConfigLookup.Update(this);
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var queuedThisFrame = new NativeParallelHashMap<Entity, uint>(256, Allocator.Temp);

            var currentMap = BuildCurrentChunkMap();
            foreach (var (chunk, entity) in SystemAPI.Query<RefRO<TerrainChunk>>().WithEntityAccess())
            {
                if (chunk.ValueRO.VolumeEntity != Entity.Null && !volumeConfigLookup.HasComponent(chunk.ValueRO.VolumeEntity))
                {
                    continue;
                }

                var version = 0u;
                if (SystemAPI.HasComponent<TerrainChunkDirty>(entity))
                {
                    version = SystemAPI.GetComponent<TerrainChunkDirty>(entity).EditVersion;
                }

                var key = new TerrainChunkKey { VolumeEntity = chunk.ValueRO.VolumeEntity, ChunkCoord = chunk.ValueRO.ChunkCoord };
                var isNew = !_knownChunks.ContainsKey(key);
                EnqueueChunk(queue, entity, version, force: isNew, ecb, ref queuedThisFrame);
                if (isNew)
                {
                    EnqueueNeighbors(queue, currentMap, key, version, ecb, ref queuedThisFrame);
                }
            }

            foreach (var entry in _knownChunks)
            {
                if (currentMap.IsCreated && currentMap.ContainsKey(entry.Key))
                {
                    continue;
                }

                EnqueueNeighbors(queue, currentMap, entry.Key, 0u, ecb, ref queuedThisFrame);
            }

            _knownChunks.Clear();
            if (currentMap.IsCreated)
            {
                foreach (var entry in currentMap)
                {
                    _knownChunks.TryAdd(entry.Key, entry.Value);
                }

                currentMap.Dispose();
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
            queuedThisFrame.Dispose();
        }

        private void EnsureQueue()
        {
            if (SystemAPI.HasSingleton<Space4XAsteroidChunkMeshRebuildQueue>())
            {
                return;
            }

            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponent<Space4XAsteroidChunkMeshRebuildQueue>(entity);
            EntityManager.AddBuffer<Space4XAsteroidChunkRebuildRequest>(entity);
            EntityManager.AddComponentData(entity, Space4XAsteroidChunkMeshRebuildConfig.Default);
        }

        protected override void OnDestroy()
        {
            if (_knownChunks.IsCreated)
            {
                _knownChunks.Dispose();
            }
        }

        private NativeParallelHashMap<TerrainChunkKey, Entity> BuildCurrentChunkMap()
        {
            var count = SystemAPI.QueryBuilder().WithAll<TerrainChunk>().Build().CalculateEntityCount();
            if (count <= 0)
            {
                return default;
            }

            var map = new NativeParallelHashMap<TerrainChunkKey, Entity>(count, Allocator.Temp);
            foreach (var (chunk, entity) in SystemAPI.Query<RefRO<TerrainChunk>>().WithEntityAccess())
            {
                map.TryAdd(new TerrainChunkKey
                {
                    VolumeEntity = chunk.ValueRO.VolumeEntity,
                    ChunkCoord = chunk.ValueRO.ChunkCoord
                }, entity);
            }

            return map;
        }

        private void EnqueueChunk(
            DynamicBuffer<Space4XAsteroidChunkRebuildRequest> queue,
            Entity entity,
            uint version,
            bool force,
            EntityCommandBuffer ecb,
            ref NativeParallelHashMap<Entity, uint> queuedThisFrame)
        {
            var hasState = EntityManager.HasComponent<Space4XAsteroidChunkMeshState>(entity);
            var state = hasState ? EntityManager.GetComponentData<Space4XAsteroidChunkMeshState>(entity) : default;
            var hasMesh = EntityManager.HasComponent<Space4XAsteroidChunkMeshReference>(entity);
            var needsBuild = !hasMesh || !hasState || version > state.LastBuiltVersion;

            if (!force && !needsBuild)
            {
                return;
            }

            var queuedVersion = version;
            if (hasState && queuedVersion <= state.LastBuiltVersion)
            {
                queuedVersion = state.LastBuiltVersion + 1;
            }

            if (hasState && queuedVersion <= state.LastQueuedVersion)
            {
                return;
            }

            if (queuedThisFrame.IsCreated)
            {
                if (queuedThisFrame.TryGetValue(entity, out var queued) && queuedVersion <= queued)
                {
                    return;
                }

                queuedThisFrame[entity] = queuedVersion;
            }

            queue.Add(new Space4XAsteroidChunkRebuildRequest { Chunk = entity });
            state.LastQueuedVersion = queuedVersion;
            if (hasState)
            {
                EntityManager.SetComponentData(entity, state);
            }
            else
            {
                ecb.AddComponent(entity, state);
            }
        }

        private void EnqueueNeighbors(
            DynamicBuffer<Space4XAsteroidChunkRebuildRequest> queue,
            NativeParallelHashMap<TerrainChunkKey, Entity> currentMap,
            TerrainChunkKey key,
            uint version,
            EntityCommandBuffer ecb,
            ref NativeParallelHashMap<Entity, uint> queuedThisFrame)
        {
            if (!currentMap.IsCreated)
            {
                return;
            }

            Span<int3> coords = stackalloc int3[6];
            coords[0] = new int3(key.ChunkCoord.x + 1, key.ChunkCoord.y, key.ChunkCoord.z);
            coords[1] = new int3(key.ChunkCoord.x - 1, key.ChunkCoord.y, key.ChunkCoord.z);
            coords[2] = new int3(key.ChunkCoord.x, key.ChunkCoord.y + 1, key.ChunkCoord.z);
            coords[3] = new int3(key.ChunkCoord.x, key.ChunkCoord.y - 1, key.ChunkCoord.z);
            coords[4] = new int3(key.ChunkCoord.x, key.ChunkCoord.y, key.ChunkCoord.z + 1);
            coords[5] = new int3(key.ChunkCoord.x, key.ChunkCoord.y, key.ChunkCoord.z - 1);

            for (int i = 0; i < 6; i++)
            {
                var neighborKey = new TerrainChunkKey { VolumeEntity = key.VolumeEntity, ChunkCoord = coords[i] };
                if (!currentMap.TryGetValue(neighborKey, out var neighborEntity))
                {
                    continue;
                }

                EnqueueChunk(queue, neighborEntity, version, force: true, ecb, ref queuedThisFrame);
            }
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XAsteroidChunkMeshQueueSystem))]
    public partial class Space4XAsteroidChunkMeshSystem : SystemBase
    {
        private NativeParallelHashMap<TerrainChunkKey, Entity> _chunkLookup;
        private int _chunkLookupCount;
        private NativeList<Vector3> _vertices;
        private NativeList<Vector3> _normals;
        private NativeList<Vector2> _uvs;
        private NativeList<int> _indices;
        private NativeList<Color32> _colors;
        private NativeList<Space4XAsteroidChunkFaceCell> _faceMask;
        private NativeList<byte> _faceUsed;
        private EntityQuery _renderConfigQuery;
        private EntityQuery _paletteConfigQuery;

        protected override void OnCreate()
        {
            RequireForUpdate<TerrainWorldConfig>();
            RequireForUpdate<Space4XAsteroidChunkMeshRebuildQueue>();
            _vertices = new NativeList<Vector3>(2048, Allocator.Persistent);
            _normals = new NativeList<Vector3>(2048, Allocator.Persistent);
            _uvs = new NativeList<Vector2>(2048, Allocator.Persistent);
            _indices = new NativeList<int>(4096, Allocator.Persistent);
            _colors = new NativeList<Color32>(2048, Allocator.Persistent);
            _faceMask = new NativeList<Space4XAsteroidChunkFaceCell>(1024, Allocator.Persistent);
            _faceUsed = new NativeList<byte>(1024, Allocator.Persistent);
            _chunkLookup = default;
            _chunkLookupCount = -1;
            _renderConfigQuery = GetEntityQuery(ComponentType.ReadOnly<Space4XAsteroidChunkRenderConfig>());
            _paletteConfigQuery = GetEntityQuery(ComponentType.ReadOnly<Space4XAsteroidChunkPaletteConfig>());
        }

        protected override void OnUpdate()
        {
            if (!RuntimeMode.IsRenderingEnabled)
            {
                return;
            }

            var terrainConfig = SystemAPI.GetSingleton<TerrainWorldConfig>();
            if (terrainConfig.VoxelSize <= 0f)
            {
                return;
            }

            var material = ResolveMaterial();
            if (material == null)
            {
                return;
            }

            EnsureChunkLookup();
            if (!_chunkLookup.IsCreated)
            {
                return;
            }

            var queueEntity = SystemAPI.GetSingletonEntity<Space4XAsteroidChunkMeshRebuildQueue>();
            var queue = EntityManager.GetBuffer<Space4XAsteroidChunkRebuildRequest>(queueEntity);
            if (queue.Length == 0)
            {
                return;
            }

            var pending = new NativeList<Space4XAsteroidChunkRebuildRequest>(queue.Length, Allocator.Temp);
            for (int i = 0; i < queue.Length; i++)
            {
                pending.Add(queue[i]);
            }
            queue.Clear();

            var config = Space4XAsteroidChunkMeshRebuildConfig.Default;
            if (EntityManager.HasComponent<Space4XAsteroidChunkMeshRebuildConfig>(queueEntity))
            {
                config = EntityManager.GetComponentData<Space4XAsteroidChunkMeshRebuildConfig>(queueEntity);
            }

            var focus = ResolveFocusPosition();
            var nearRadiusSq = math.max(0f, config.NearRadius);
            nearRadiusSq *= nearRadiusSq;
            var minChunks = math.max(1, config.MinChunksPerFrame);
            var nearBudget = math.max(0, config.NearRebuildCap);
            var startTime = UTime.realtimeSinceStartupAsDouble;
            var builtCount = 0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var frameStats = new Space4XAsteroidChunkMeshFrameStats
            {
                QueueLength = pending.Length,
                BudgetMs = config.MaxBuildMillisecondsPerFrame,
                MinChunksPerFrame = minChunks
            };
#endif

            var chunkLookup = GetComponentLookup<TerrainChunk>(true);
            var runtimeLookup = GetBufferLookup<TerrainVoxelRuntime>(true);
            chunkLookup.Update(this);
            runtimeLookup.Update(this);
            var voxelAccessor = new TerrainVoxelAccessor
            {
                ChunkLookup = _chunkLookup,
                Chunks = chunkLookup,
                RuntimeVoxels = runtimeLookup,
                WorldConfig = terrainConfig
            };

            var renderMeshDesc = new RenderMeshDescription();

            while (nearBudget > 0 && pending.Length > 0)
            {
                var idx = FindClosestWithin(pending, focus, nearRadiusSq);
                if (idx < 0)
                {
                    break;
                }

                chunkLookup.Update(this);
                runtimeLookup.Update(this);
                voxelAccessor.Chunks = chunkLookup;
                voxelAccessor.RuntimeVoxels = runtimeLookup;

                if (TryRebuildChunk(pending[idx].Chunk, ref voxelAccessor, material, renderMeshDesc, terrainConfig,
                        out var buildMs, out var vertexCount, out var indexCount, out var quadBefore, out var quadAfter))
                {
                    builtCount++;
                    nearBudget--;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    frameStats.ChunksBuiltThisFrame++;
                    frameStats.TotalBuildMsThisFrame += buildMs;
                    frameStats.TotalVertsThisFrame += vertexCount;
                    frameStats.TotalIndicesThisFrame += indexCount;
                    frameStats.LastChunkBuildMs = buildMs;
#endif
                }

                pending.RemoveAt(idx);
                if (ExceededTimeBudget(startTime, config.MaxBuildMillisecondsPerFrame, builtCount, minChunks))
                {
                    break;
                }
            }

            while (pending.Length > 0)
            {
                var idx = FindClosest(pending, focus);
                if (idx < 0)
                {
                    break;
                }

                chunkLookup.Update(this);
                runtimeLookup.Update(this);
                voxelAccessor.Chunks = chunkLookup;
                voxelAccessor.RuntimeVoxels = runtimeLookup;

                if (TryRebuildChunk(pending[idx].Chunk, ref voxelAccessor, material, renderMeshDesc, terrainConfig,
                        out var buildMs, out var vertexCount, out var indexCount, out var quadBefore, out var quadAfter))
                {
                    builtCount++;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    frameStats.ChunksBuiltThisFrame++;
                    frameStats.TotalBuildMsThisFrame += buildMs;
                    frameStats.TotalVertsThisFrame += vertexCount;
                    frameStats.TotalIndicesThisFrame += indexCount;
                    frameStats.LastChunkBuildMs = buildMs;
#endif
                }

                pending.RemoveAt(idx);
                if (ExceededTimeBudget(startTime, config.MaxBuildMillisecondsPerFrame, builtCount, minChunks))
                {
                    break;
                }
            }

            if (pending.Length > 0)
            {
                queue = EntityManager.GetBuffer<Space4XAsteroidChunkRebuildRequest>(queueEntity);
                for (int i = 0; i < pending.Length; i++)
                {
                    queue.Add(pending[i]);
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (pending.Length > 0)
            {
                frameStats.SkippedDueToBudget = pending.Length;
            }

            frameStats.QueueLength = pending.Length;
            if (EntityManager.HasComponent<Space4XAsteroidChunkMeshFrameStats>(queueEntity))
            {
                EntityManager.SetComponentData(queueEntity, frameStats);
            }
            else
            {
                EntityManager.AddComponentData(queueEntity, frameStats);
            }
#endif

            pending.Dispose();
        }

        protected override void OnDestroy()
        {
            if (_vertices.IsCreated)
            {
                _vertices.Dispose();
            }

            if (_normals.IsCreated)
            {
                _normals.Dispose();
            }

            if (_uvs.IsCreated)
            {
                _uvs.Dispose();
            }

            if (_indices.IsCreated)
            {
                _indices.Dispose();
            }

            if (_colors.IsCreated)
            {
                _colors.Dispose();
            }

            if (_faceMask.IsCreated)
            {
                _faceMask.Dispose();
            }

            if (_faceUsed.IsCreated)
            {
                _faceUsed.Dispose();
            }

            if (_chunkLookup.IsCreated)
            {
                _chunkLookup.Dispose();
            }

            foreach (var reference in SystemAPI.Query<Space4XAsteroidChunkMeshReference>())
            {
                if (reference.Mesh != null)
                {
                    UObject.Destroy(reference.Mesh);
                }
            }
        }

        private Material ResolveMaterial()
        {
            if (_renderConfigQuery.TryGetSingletonEntity<Space4XAsteroidChunkRenderConfig>(out var configEntity))
            {
                var config = EntityManager.GetComponentObject<Space4XAsteroidChunkRenderConfig>(configEntity);
                if (EntityManager.HasComponent<Space4XAsteroidChunkPaletteConfig>(configEntity))
                {
                    var palette = EntityManager.GetComponentObject<Space4XAsteroidChunkPaletteConfig>(configEntity).Palette;
                    if (palette != null && config.Material != null && config.Material.HasProperty("_MaterialPalette"))
                    {
                        config.Material.SetTexture("_MaterialPalette", palette);
                    }
                }

                return config.Material;
            }

            var paletteTexture = default(Texture2D);
            if (_paletteConfigQuery.TryGetSingletonEntity<Space4XAsteroidChunkPaletteConfig>(out var paletteEntity))
            {
                paletteTexture = EntityManager.GetComponentObject<Space4XAsteroidChunkPaletteConfig>(paletteEntity).Palette;
            }

            var paletteShader = Shader.Find("Space4X/AsteroidChunkPalette");
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (paletteTexture != null && paletteShader != null)
            {
                shader = paletteShader;
            }

            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader)
            {
                color = new Color(0.55f, 0.55f, 0.58f, 1f)
            };
            if (paletteTexture != null && material.HasProperty("_MaterialPalette"))
            {
                material.SetTexture("_MaterialPalette", paletteTexture);
            }

            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentObject(entity, new Space4XAsteroidChunkRenderConfig
            {
                Material = material
            });

            return material;
        }

        private void EnsureChunkLookup()
        {
            var count = SystemAPI.QueryBuilder().WithAll<TerrainChunk>().Build().CalculateEntityCount();
            if (count == _chunkLookupCount && _chunkLookup.IsCreated)
            {
                return;
            }

            if (_chunkLookup.IsCreated)
            {
                _chunkLookup.Dispose();
            }

            _chunkLookupCount = count;
            if (count <= 0)
            {
                _chunkLookup = default;
                return;
            }

            var map = new NativeParallelHashMap<TerrainChunkKey, Entity>(count, Allocator.Persistent);
            foreach (var (chunk, entity) in SystemAPI.Query<RefRO<TerrainChunk>>().WithEntityAccess())
            {
                map.TryAdd(new TerrainChunkKey
                {
                    VolumeEntity = chunk.ValueRO.VolumeEntity,
                    ChunkCoord = chunk.ValueRO.ChunkCoord
                }, entity);
            }

            _chunkLookup = map;
        }

        private float3 ResolveFocusPosition()
        {
            if (SystemAPI.TryGetSingleton<StreamingFocus>(out var focus))
            {
                return focus.Position;
            }

            return float3.zero;
        }

        private int FindClosestWithin(NativeList<Space4XAsteroidChunkRebuildRequest> queue, float3 focus, float maxDistanceSq)
        {
            var bestIdx = -1;
            var bestDistance = maxDistanceSq;

            for (int i = 0; i < queue.Length; i++)
            {
                var entity = queue[i].Chunk;
                if (!EntityManager.Exists(entity) || !EntityManager.HasComponent<TerrainChunk>(entity))
                {
                    continue;
                }

                var distanceSq = DistanceSqToFocus(entity, focus);
                if (distanceSq <= bestDistance)
                {
                    bestDistance = distanceSq;
                    bestIdx = i;
                }
            }

            return bestIdx;
        }

        private int FindClosest(NativeList<Space4XAsteroidChunkRebuildRequest> queue, float3 focus)
        {
            var bestIdx = -1;
            var bestDistance = float.MaxValue;

            for (int i = 0; i < queue.Length; i++)
            {
                var entity = queue[i].Chunk;
                if (!EntityManager.Exists(entity) || !EntityManager.HasComponent<TerrainChunk>(entity))
                {
                    continue;
                }

                var distanceSq = DistanceSqToFocus(entity, focus);
                if (distanceSq < bestDistance)
                {
                    bestDistance = distanceSq;
                    bestIdx = i;
                }
            }

            return bestIdx;
        }

        private float DistanceSqToFocus(Entity entity, float3 focus)
        {
            var worldPos = ResolveChunkWorldPosition(entity);
            return math.distancesq(worldPos, focus);
        }

        private float3 ResolveChunkWorldPosition(Entity entity)
        {
            if (SystemAPI.HasComponent<LocalToWorld>(entity))
            {
                return SystemAPI.GetComponentRO<LocalToWorld>(entity).ValueRO.Position;
            }

            if (!SystemAPI.HasComponent<LocalTransform>(entity))
            {
                return float3.zero;
            }

            var local = SystemAPI.GetComponentRO<LocalTransform>(entity).ValueRO.Position;
            if (!SystemAPI.HasComponent<Parent>(entity))
            {
                return local;
            }

            var parent = SystemAPI.GetComponentRO<Parent>(entity).ValueRO.Value;
            if (SystemAPI.HasComponent<LocalToWorld>(parent))
            {
                return math.transform(SystemAPI.GetComponentRO<LocalToWorld>(parent).ValueRO.Value, local);
            }

            if (SystemAPI.HasComponent<LocalTransform>(parent))
            {
                var parentTransform = SystemAPI.GetComponentRO<LocalTransform>(parent).ValueRO;
                var rotated = math.rotate(parentTransform.Rotation, local * parentTransform.Scale);
                return parentTransform.Position + rotated;
            }

            return local;
        }

        private bool TryRebuildChunk(
            Entity entity,
            ref TerrainVoxelAccessor voxelAccessor,
            Material material,
            RenderMeshDescription renderMeshDesc,
            TerrainWorldConfig terrainConfig,
            out float buildMs,
            out int vertexCount,
            out int indexCount,
            out int quadBefore,
            out int quadAfter)
        {
            var startTime = UTime.realtimeSinceStartupAsDouble;
            if (!EntityManager.Exists(entity) || !EntityManager.HasComponent<TerrainChunk>(entity))
            {
                buildMs = 0f;
                vertexCount = 0;
                indexCount = 0;
                quadBefore = 0;
                quadAfter = 0;
                return false;
            }

            var chunk = EntityManager.GetComponentData<TerrainChunk>(entity);
            var version = 0u;
            if (EntityManager.HasComponent<TerrainChunkDirty>(entity))
            {
                version = EntityManager.GetComponentData<TerrainChunkDirty>(entity).EditVersion;
            }

            _vertices.Clear();
            _normals.Clear();
            _uvs.Clear();
            _indices.Clear();
            _colors.Clear();

            quadBefore = 0;
            quadAfter = 0;
            BuildChunkMesh(ref voxelAccessor, chunk, terrainConfig.VoxelSize, _vertices, _normals, _uvs, _colors, _indices,
                _faceMask, _faceUsed, ref quadBefore, ref quadAfter);

            var hasMeshRef = EntityManager.HasComponent<Space4XAsteroidChunkMeshReference>(entity);
            Mesh mesh;
            if (hasMeshRef)
            {
                mesh = EntityManager.GetComponentData<Space4XAsteroidChunkMeshReference>(entity).Mesh;
            }
            else
            {
                mesh = new Mesh();
                EntityManager.AddComponentData(entity, new Space4XAsteroidChunkMeshReference { Mesh = mesh });
                EntityManager.AddComponent<Space4XAsteroidChunkMeshCleanup>(entity);
            }

            mesh.indexFormat = _vertices.Length > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            if (_vertices.Length == 0)
            {
                mesh.Clear();
            }
            else
            {
                var meshDataArray = Mesh.AllocateWritableMeshData(1);
                var meshData = meshDataArray[0];
                var meshVertexCount = _vertices.Length;
                var meshIndexCount = _indices.Length;
                var descriptors = new NativeArray<VertexAttributeDescriptor>(4, Allocator.Temp);
                descriptors[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3);
                descriptors[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3);
                descriptors[2] = new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4);
                descriptors[3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2);
                meshData.SetVertexBufferParams(meshVertexCount, descriptors);
                descriptors.Dispose();

                var vertexData = meshData.GetVertexData<Space4XAsteroidChunkVertex>();
                for (int i = 0; i < meshVertexCount; i++)
                {
                    vertexData[i] = new Space4XAsteroidChunkVertex
                    {
                        Position = _vertices[i],
                        Normal = _normals[i],
                        Color = _colors[i],
                        TexCoord0 = _uvs[i]
                    };
                }

                var indexFormat = _indices.Length > 65535
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16;
                meshData.SetIndexBufferParams(meshIndexCount, indexFormat);
                if (indexFormat == UnityEngine.Rendering.IndexFormat.UInt32)
                {
                    var indexData = meshData.GetIndexData<int>();
                    for (int i = 0; i < meshIndexCount; i++)
                    {
                        indexData[i] = _indices[i];
                    }
                }
                else
                {
                    var indexData = meshData.GetIndexData<ushort>();
                    for (int i = 0; i < meshIndexCount; i++)
                    {
                        indexData[i] = (ushort)_indices[i];
                    }
                }

                meshData.subMeshCount = 1;
                meshData.SetSubMesh(0, new SubMeshDescriptor(0, meshIndexCount), MeshUpdateFlags.DontRecalculateBounds);
                Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh);
                mesh.RecalculateBounds();
            }

            if (!EntityManager.HasComponent<MaterialMeshInfo>(entity))
            {
                var renderArray = new RenderMeshArray(new[] { material }, new[] { mesh });
                RenderMeshUtility.AddComponents(entity, EntityManager, renderMeshDesc, renderArray,
                    MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
            }

            var chunkSize = new float3(chunk.VoxelsPerChunk) * terrainConfig.VoxelSize;
            EntityManager.SetComponentData(entity, new RenderBounds
            {
                Value = new AABB { Center = chunkSize * 0.5f, Extents = chunkSize * 0.5f }
            });

            var hasState = EntityManager.HasComponent<Space4XAsteroidChunkMeshState>(entity);
            var state = hasState ? EntityManager.GetComponentData<Space4XAsteroidChunkMeshState>(entity) : default;
            state.LastBuiltVersion = version;
            if (hasState)
            {
                EntityManager.SetComponentData(entity, state);
            }
            else
            {
                EntityManager.AddComponentData(entity, state);
            }

            vertexCount = _vertices.Length;
            indexCount = _indices.Length;
            buildMs = (float)((UTime.realtimeSinceStartupAsDouble - startTime) * 1000.0);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var tick = 0u;
            if (SystemAPI.TryGetSingleton<TimeState>(out var timeState))
            {
                tick = timeState.Tick;
            }

            var telemetry = new Space4XAsteroidChunkMeshTelemetry
            {
                LastBuildMs = buildMs,
                LastVertexCount = vertexCount,
                LastIndexCount = indexCount,
                LastQuadCountBeforeGreedy = quadBefore,
                LastQuadCountAfterGreedy = quadAfter,
                LastBuiltVersion = version,
                LastBuiltTick = tick
            };

            if (EntityManager.HasComponent<Space4XAsteroidChunkMeshTelemetry>(entity))
            {
                EntityManager.SetComponentData(entity, telemetry);
            }
            else
            {
                EntityManager.AddComponentData(entity, telemetry);
            }
#endif

            return true;
        }

        private static void BuildChunkMesh(
            ref TerrainVoxelAccessor accessor,
            in TerrainChunk chunk,
            float voxelSize,
            NativeList<Vector3> vertices,
            NativeList<Vector3> normals,
            NativeList<Vector2> uvs,
            NativeList<Color32> colors,
            NativeList<int> indices,
            NativeList<Space4XAsteroidChunkFaceCell> faceMask,
            NativeList<byte> faceUsed,
            ref int quadBefore,
            ref int quadAfter)
        {
            var dims = chunk.VoxelsPerChunk;
            if (dims.x <= 0 || dims.y <= 0 || dims.z <= 0)
            {
                return;
            }

            GreedyMeshDirection(ref accessor, chunk, dims, voxelSize, TerrainVoxelFaceDirection.PosX,
                vertices, normals, uvs, colors, indices, faceMask, faceUsed, ref quadBefore, ref quadAfter);
            GreedyMeshDirection(ref accessor, chunk, dims, voxelSize, TerrainVoxelFaceDirection.NegX,
                vertices, normals, uvs, colors, indices, faceMask, faceUsed, ref quadBefore, ref quadAfter);
            GreedyMeshDirection(ref accessor, chunk, dims, voxelSize, TerrainVoxelFaceDirection.PosY,
                vertices, normals, uvs, colors, indices, faceMask, faceUsed, ref quadBefore, ref quadAfter);
            GreedyMeshDirection(ref accessor, chunk, dims, voxelSize, TerrainVoxelFaceDirection.NegY,
                vertices, normals, uvs, colors, indices, faceMask, faceUsed, ref quadBefore, ref quadAfter);
            GreedyMeshDirection(ref accessor, chunk, dims, voxelSize, TerrainVoxelFaceDirection.PosZ,
                vertices, normals, uvs, colors, indices, faceMask, faceUsed, ref quadBefore, ref quadAfter);
            GreedyMeshDirection(ref accessor, chunk, dims, voxelSize, TerrainVoxelFaceDirection.NegZ,
                vertices, normals, uvs, colors, indices, faceMask, faceUsed, ref quadBefore, ref quadAfter);
        }

        private static void GreedyMeshDirection(
            ref TerrainVoxelAccessor accessor,
            in TerrainChunk chunk,
            int3 dims,
            float voxelSize,
            TerrainVoxelFaceDirection face,
            NativeList<Vector3> vertices,
            NativeList<Vector3> normals,
            NativeList<Vector2> uvs,
            NativeList<Color32> colors,
            NativeList<int> indices,
            NativeList<Space4XAsteroidChunkFaceCell> faceMask,
            NativeList<byte> faceUsed,
            ref int quadBefore,
            ref int quadAfter)
        {
            int uDim;
            int vDim;
            int wDim;
            switch (face)
            {
                case TerrainVoxelFaceDirection.PosX:
                case TerrainVoxelFaceDirection.NegX:
                    uDim = dims.y;
                    vDim = dims.z;
                    wDim = dims.x;
                    break;
                case TerrainVoxelFaceDirection.PosY:
                case TerrainVoxelFaceDirection.NegY:
                    uDim = dims.x;
                    vDim = dims.z;
                    wDim = dims.y;
                    break;
                default:
                    uDim = dims.x;
                    vDim = dims.y;
                    wDim = dims.z;
                    break;
            }

            var maskSize = uDim * vDim;
            if (maskSize <= 0 || wDim <= 0)
            {
                return;
            }

            if (faceMask.Capacity < maskSize)
            {
                faceMask.Capacity = maskSize;
            }

            if (faceUsed.Capacity < maskSize)
            {
                faceUsed.Capacity = maskSize;
            }

            for (int w = 0; w < wDim; w++)
            {
                faceMask.ResizeUninitialized(maskSize);
                faceUsed.ResizeUninitialized(maskSize);
                for (int i = 0; i < maskSize; i++)
                {
                    faceMask[i] = default;
                    faceUsed[i] = 0;
                }

                for (int v = 0; v < vDim; v++)
                {
                    for (int u = 0; u < uDim; u++)
                    {
                        var voxelCoord = ResolveVoxelCoord(face, u, v, w);
                        if (!accessor.TrySampleVoxel(chunk.VolumeEntity, chunk.ChunkCoord, voxelCoord, out var voxelSample) ||
                            voxelSample.SolidMask == 0)
                        {
                            continue;
                        }

                        var neighborOffset = ResolveNeighborOffset(face);
                        if (accessor.TrySampleNeighbor(chunk.VolumeEntity, chunk.ChunkCoord, voxelCoord, neighborOffset, out var neighbor) &&
                            neighbor.SolidMask != 0)
                        {
                            continue;
                        }

                        var oreGrade = QuantizeOreGrade(voxelSample.OreGrade);
                        var cell = new Space4XAsteroidChunkFaceCell
                        {
                            Exists = 1,
                            MaterialId = voxelSample.MaterialId,
                            OreGrade = oreGrade
                        };

                        faceMask[u + v * uDim] = cell;
                        quadBefore++;
                    }
                }

                for (int v = 0; v < vDim; v++)
                {
                    for (int u = 0; u < uDim; u++)
                    {
                        var idx = u + v * uDim;
                        if (faceUsed[idx] != 0)
                        {
                            continue;
                        }

                        var cell = faceMask[idx];
                        if (cell.Exists == 0)
                        {
                            continue;
                        }

                        var width = 1;
                        var height = 1;
                        if (cell.Exists != 0)
                        {
                            while (u + width < uDim &&
                                   faceUsed[idx + width] == 0 &&
                                   CanMerge(cell, faceMask[idx + width]))
                            {
                                width++;
                            }

                            var done = false;
                            while (v + height < vDim && !done)
                            {
                                for (int x = 0; x < width; x++)
                                {
                                    var scanIdx = (u + x) + (v + height) * uDim;
                                    if (faceUsed[scanIdx] != 0 || !CanMerge(cell, faceMask[scanIdx]))
                                    {
                                        done = true;
                                        break;
                                    }
                                }

                                if (!done)
                                {
                                    height++;
                                }
                            }
                        }

                        for (int vy = 0; vy < height; vy++)
                        {
                            for (int vx = 0; vx < width; vx++)
                            {
                                faceUsed[(u + vx) + (v + vy) * uDim] = 1;
                            }
                        }

                        quadAfter++;
                        AppendMergedFace(ref accessor, chunk, vertices, normals, uvs, colors, indices, face, w, u, v, width, height,
                            voxelSize, cell.MaterialId, cell.OreGrade);
                    }
                }
            }
        }

        private static byte QuantizeOreGrade(byte oreGrade)
        {
            const int bins = 8;
            var bin = (oreGrade * (bins - 1) + 127) / 255;
            var value = bin * (255 / (bins - 1));
            return (byte)math.clamp(value, 0, 255);
        }

        private static bool CanMerge(in Space4XAsteroidChunkFaceCell a, in Space4XAsteroidChunkFaceCell b)
        {
            if (b.Exists == 0)
            {
                return false;
            }

            if (a.MaterialId != b.MaterialId || a.OreGrade != b.OreGrade)
            {
                return false;
            }

            return true;
        }

        private static int3 ResolveVoxelCoord(TerrainVoxelFaceDirection face, int u, int v, int w)
        {
            switch (face)
            {
                case TerrainVoxelFaceDirection.PosX:
                case TerrainVoxelFaceDirection.NegX:
                    return new int3(w, u, v);
                case TerrainVoxelFaceDirection.PosY:
                case TerrainVoxelFaceDirection.NegY:
                    return new int3(u, w, v);
                default:
                    return new int3(u, v, w);
            }
        }

        private static int3 ResolveNeighborOffset(TerrainVoxelFaceDirection face)
        {
            switch (face)
            {
                case TerrainVoxelFaceDirection.PosX:
                    return new int3(1, 0, 0);
                case TerrainVoxelFaceDirection.NegX:
                    return new int3(-1, 0, 0);
                case TerrainVoxelFaceDirection.PosY:
                    return new int3(0, 1, 0);
                case TerrainVoxelFaceDirection.NegY:
                    return new int3(0, -1, 0);
                case TerrainVoxelFaceDirection.PosZ:
                    return new int3(0, 0, 1);
                default:
                    return new int3(0, 0, -1);
            }
        }

        private static void AppendMergedFace(
            ref TerrainVoxelAccessor accessor,
            in TerrainChunk chunk,
            NativeList<Vector3> vertices,
            NativeList<Vector3> normals,
            NativeList<Vector2> uvs,
            NativeList<Color32> colors,
            NativeList<int> indices,
            TerrainVoxelFaceDirection face,
            int w,
            int u,
            int v,
            int width,
            int height,
            float voxelSize,
            byte materialId,
            byte oreGrade)
        {
            float3 v0;
            float3 v1;
            float3 v2;
            float3 v3;
            float3 normal;

            var u0 = u * voxelSize;
            var u1 = (u + width) * voxelSize;
            var v0p = v * voxelSize;
            var v1p = (v + height) * voxelSize;
            var uMax = u + width - 1;
            var vMax = v + height - 1;

            switch (face)
            {
                case TerrainVoxelFaceDirection.PosX:
                    {
                        var x = (w + 1) * voxelSize;
                        v0 = new float3(x, u0, v0p);
                        v1 = new float3(x, u1, v0p);
                        v2 = new float3(x, u1, v1p);
                        v3 = new float3(x, u0, v1p);
                        normal = new float3(1f, 0f, 0f);
                        break;
                    }
                case TerrainVoxelFaceDirection.NegX:
                    {
                        var x = w * voxelSize;
                        v0 = new float3(x, u0, v1p);
                        v1 = new float3(x, u1, v1p);
                        v2 = new float3(x, u1, v0p);
                        v3 = new float3(x, u0, v0p);
                        normal = new float3(-1f, 0f, 0f);
                        break;
                    }
                case TerrainVoxelFaceDirection.PosY:
                    {
                        var y = (w + 1) * voxelSize;
                        v0 = new float3(u0, y, v1p);
                        v1 = new float3(u1, y, v1p);
                        v2 = new float3(u1, y, v0p);
                        v3 = new float3(u0, y, v0p);
                        normal = new float3(0f, 1f, 0f);
                        break;
                    }
                case TerrainVoxelFaceDirection.NegY:
                    {
                        var y = w * voxelSize;
                        v0 = new float3(u0, y, v0p);
                        v1 = new float3(u1, y, v0p);
                        v2 = new float3(u1, y, v1p);
                        v3 = new float3(u0, y, v1p);
                        normal = new float3(0f, -1f, 0f);
                        break;
                    }
                case TerrainVoxelFaceDirection.PosZ:
                    {
                        var z = (w + 1) * voxelSize;
                        v0 = new float3(u1, v0p, z);
                        v1 = new float3(u1, v1p, z);
                        v2 = new float3(u0, v1p, z);
                        v3 = new float3(u0, v0p, z);
                        normal = new float3(0f, 0f, 1f);
                        break;
                    }
                default:
                    {
                        var z = w * voxelSize;
                        v0 = new float3(u0, v0p, z);
                        v1 = new float3(u0, v1p, z);
                        v2 = new float3(u1, v1p, z);
                        v3 = new float3(u1, v0p, z);
                        normal = new float3(0f, 0f, -1f);
                        break;
                    }
            }

            var start = vertices.Length;
            vertices.Add(new Vector3(v0.x, v0.y, v0.z));
            vertices.Add(new Vector3(v1.x, v1.y, v1.z));
            vertices.Add(new Vector3(v2.x, v2.y, v2.z));
            vertices.Add(new Vector3(v3.x, v3.y, v3.z));
            normals.Add(new Vector3(normal.x, normal.y, normal.z));
            normals.Add(new Vector3(normal.x, normal.y, normal.z));
            normals.Add(new Vector3(normal.x, normal.y, normal.z));
            normals.Add(new Vector3(normal.x, normal.y, normal.z));
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(1f, 0f));
            byte ao0;
            byte ao1;
            byte ao2;
            byte ao3;
            switch (face)
            {
                case TerrainVoxelFaceDirection.PosX:
                    ao0 = ComputeCornerAo(ref accessor, chunk, face, u, v, w, 0, 0);
                    ao1 = ComputeCornerAo(ref accessor, chunk, face, uMax, v, w, 1, 0);
                    ao2 = ComputeCornerAo(ref accessor, chunk, face, uMax, vMax, w, 1, 1);
                    ao3 = ComputeCornerAo(ref accessor, chunk, face, u, vMax, w, 0, 1);
                    break;
                case TerrainVoxelFaceDirection.NegX:
                    ao0 = ComputeCornerAo(ref accessor, chunk, face, u, vMax, w, 0, 1);
                    ao1 = ComputeCornerAo(ref accessor, chunk, face, uMax, vMax, w, 1, 1);
                    ao2 = ComputeCornerAo(ref accessor, chunk, face, uMax, v, w, 1, 0);
                    ao3 = ComputeCornerAo(ref accessor, chunk, face, u, v, w, 0, 0);
                    break;
                case TerrainVoxelFaceDirection.PosY:
                    ao0 = ComputeCornerAo(ref accessor, chunk, face, u, vMax, w, 0, 1);
                    ao1 = ComputeCornerAo(ref accessor, chunk, face, uMax, vMax, w, 1, 1);
                    ao2 = ComputeCornerAo(ref accessor, chunk, face, uMax, v, w, 1, 0);
                    ao3 = ComputeCornerAo(ref accessor, chunk, face, u, v, w, 0, 0);
                    break;
                case TerrainVoxelFaceDirection.NegY:
                    ao0 = ComputeCornerAo(ref accessor, chunk, face, u, v, w, 0, 0);
                    ao1 = ComputeCornerAo(ref accessor, chunk, face, uMax, v, w, 1, 0);
                    ao2 = ComputeCornerAo(ref accessor, chunk, face, uMax, vMax, w, 1, 1);
                    ao3 = ComputeCornerAo(ref accessor, chunk, face, u, vMax, w, 0, 1);
                    break;
                case TerrainVoxelFaceDirection.PosZ:
                    ao0 = ComputeCornerAo(ref accessor, chunk, face, uMax, v, w, 1, 0);
                    ao1 = ComputeCornerAo(ref accessor, chunk, face, uMax, vMax, w, 1, 1);
                    ao2 = ComputeCornerAo(ref accessor, chunk, face, u, vMax, w, 0, 1);
                    ao3 = ComputeCornerAo(ref accessor, chunk, face, u, v, w, 0, 0);
                    break;
                default:
                    ao0 = ComputeCornerAo(ref accessor, chunk, face, u, v, w, 0, 0);
                    ao1 = ComputeCornerAo(ref accessor, chunk, face, u, vMax, w, 0, 1);
                    ao2 = ComputeCornerAo(ref accessor, chunk, face, uMax, vMax, w, 1, 1);
                    ao3 = ComputeCornerAo(ref accessor, chunk, face, uMax, v, w, 1, 0);
                    break;
            }
            colors.Add(new Color32(materialId, oreGrade, 0, ao0));
            colors.Add(new Color32(materialId, oreGrade, 0, ao1));
            colors.Add(new Color32(materialId, oreGrade, 0, ao2));
            colors.Add(new Color32(materialId, oreGrade, 0, ao3));
            indices.Add(start + 0);
            indices.Add(start + 1);
            indices.Add(start + 2);
            indices.Add(start + 0);
            indices.Add(start + 2);
            indices.Add(start + 3);
        }

        private static byte ComputeCornerAo(
            ref TerrainVoxelAccessor accessor,
            in TerrainChunk chunk,
            TerrainVoxelFaceDirection face,
            int u,
            int v,
            int w,
            int uOffset,
            int vOffset)
        {
            var voxelCoord = ResolveVoxelCoord(face, u, v, w);
            ResolveFaceAxes(face, out var axisU, out var axisV);
            return ComputeVertexAo(ref accessor, chunk, voxelCoord, axisU, axisV, uOffset, vOffset);
        }

        private static void ResolveFaceAxes(TerrainVoxelFaceDirection face, out int3 axisU, out int3 axisV)
        {
            switch (face)
            {
                case TerrainVoxelFaceDirection.PosX:
                case TerrainVoxelFaceDirection.NegX:
                    axisU = new int3(0, 1, 0);
                    axisV = new int3(0, 0, 1);
                    break;
                case TerrainVoxelFaceDirection.PosY:
                case TerrainVoxelFaceDirection.NegY:
                    axisU = new int3(1, 0, 0);
                    axisV = new int3(0, 0, 1);
                    break;
                default:
                    axisU = new int3(1, 0, 0);
                    axisV = new int3(0, 1, 0);
                    break;
            }
        }

        private struct Space4XAsteroidChunkVertex
        {
            public Vector3 Position;
            public Vector3 Normal;
            public Color32 Color;
            public Vector2 TexCoord0;
        }

        private static byte ComputeVertexAo(
            ref TerrainVoxelAccessor accessor,
            in TerrainChunk chunk,
            int3 voxelCoord,
            int3 axisU,
            int3 axisV,
            int uOffset,
            int vOffset)
        {
            var sideA = uOffset == 0 ? -axisU : axisU;
            var sideB = vOffset == 0 ? -axisV : axisV;
            return ComputeVertexAo(ref accessor, chunk, voxelCoord, sideA, sideB);
        }

        private static byte ComputeVertexAo(
            ref TerrainVoxelAccessor accessor,
            in TerrainChunk chunk,
            int3 voxelCoord,
            int3 sideOffsetA,
            int3 sideOffsetB)
        {
            var sideA = IsSolid(ref accessor, chunk, voxelCoord, sideOffsetA);
            var sideB = IsSolid(ref accessor, chunk, voxelCoord, sideOffsetB);
            var corner = IsSolid(ref accessor, chunk, voxelCoord, sideOffsetA + sideOffsetB);

            var occlusion = sideA && sideB ? 3 : (sideA ? 1 : 0) + (sideB ? 1 : 0) + (corner ? 1 : 0);
            var ao = 255 - occlusion * 85;
            return (byte)math.clamp(ao, 0, 255);
        }

        private static bool IsSolid(
            ref TerrainVoxelAccessor accessor,
            in TerrainChunk chunk,
            int3 voxelCoord,
            int3 offset)
        {
            return accessor.TrySampleNeighbor(chunk.VolumeEntity, chunk.ChunkCoord, voxelCoord, offset, out var sample) &&
                   sample.SolidMask != 0;
        }

        private static bool ExceededTimeBudget(double startTime, float maxMs, int builtCount, int minChunks)
        {
            if (builtCount < minChunks)
            {
                return false;
            }

            if (maxMs <= 0f)
            {
                return true;
            }

            var elapsedMs = (UTime.realtimeSinceStartupAsDouble - startTime) * 1000.0;
            return elapsedMs >= maxMs;
        }

        private static void AppendFace(
            NativeList<Vector3> vertices,
            NativeList<Vector3> normals,
            NativeList<Vector2> uvs,
            NativeList<Color32> colors,
            NativeList<int> indices,
            int3 voxelCoord,
            float voxelSize,
            int directionIndex,
            byte materialId,
            byte oreGrade,
            int4 ao)
        {
            var basePos = new float3(voxelCoord) * voxelSize;
            var v0 = basePos;
            var v1 = basePos;
            var v2 = basePos;
            var v3 = basePos;
            var normal = float3.zero;

            switch ((TerrainVoxelFaceDirection)directionIndex)
            {
                case TerrainVoxelFaceDirection.PosX:
                    v0 += new float3(voxelSize, 0f, 0f);
                    v1 += new float3(voxelSize, voxelSize, 0f);
                    v2 += new float3(voxelSize, voxelSize, voxelSize);
                    v3 += new float3(voxelSize, 0f, voxelSize);
                    normal = new float3(1f, 0f, 0f);
                    break;
                case TerrainVoxelFaceDirection.NegX:
                    v0 += new float3(0f, 0f, voxelSize);
                    v1 += new float3(0f, voxelSize, voxelSize);
                    v2 += new float3(0f, voxelSize, 0f);
                    v3 += new float3(0f, 0f, 0f);
                    normal = new float3(-1f, 0f, 0f);
                    break;
                case TerrainVoxelFaceDirection.PosY:
                    v0 += new float3(0f, voxelSize, voxelSize);
                    v1 += new float3(voxelSize, voxelSize, voxelSize);
                    v2 += new float3(voxelSize, voxelSize, 0f);
                    v3 += new float3(0f, voxelSize, 0f);
                    normal = new float3(0f, 1f, 0f);
                    break;
                case TerrainVoxelFaceDirection.NegY:
                    v0 += new float3(0f, 0f, 0f);
                    v1 += new float3(voxelSize, 0f, 0f);
                    v2 += new float3(voxelSize, 0f, voxelSize);
                    v3 += new float3(0f, 0f, voxelSize);
                    normal = new float3(0f, -1f, 0f);
                    break;
                case TerrainVoxelFaceDirection.PosZ:
                    v0 += new float3(voxelSize, 0f, voxelSize);
                    v1 += new float3(voxelSize, voxelSize, voxelSize);
                    v2 += new float3(0f, voxelSize, voxelSize);
                    v3 += new float3(0f, 0f, voxelSize);
                    normal = new float3(0f, 0f, 1f);
                    break;
                case TerrainVoxelFaceDirection.NegZ:
                    v0 += new float3(0f, 0f, 0f);
                    v1 += new float3(0f, voxelSize, 0f);
                    v2 += new float3(voxelSize, voxelSize, 0f);
                    v3 += new float3(voxelSize, 0f, 0f);
                    normal = new float3(0f, 0f, -1f);
                    break;
            }

            var start = vertices.Length;
            vertices.Add(new Vector3(v0.x, v0.y, v0.z));
            vertices.Add(new Vector3(v1.x, v1.y, v1.z));
            vertices.Add(new Vector3(v2.x, v2.y, v2.z));
            vertices.Add(new Vector3(v3.x, v3.y, v3.z));
            normals.Add(new Vector3(normal.x, normal.y, normal.z));
            normals.Add(new Vector3(normal.x, normal.y, normal.z));
            normals.Add(new Vector3(normal.x, normal.y, normal.z));
            normals.Add(new Vector3(normal.x, normal.y, normal.z));
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(1f, 0f));
            colors.Add(new Color32(materialId, oreGrade, 0, (byte)ao.x));
            colors.Add(new Color32(materialId, oreGrade, 0, (byte)ao.y));
            colors.Add(new Color32(materialId, oreGrade, 0, (byte)ao.z));
            colors.Add(new Color32(materialId, oreGrade, 0, (byte)ao.w));
            indices.Add(start + 0);
            indices.Add(start + 1);
            indices.Add(start + 2);
            indices.Add(start + 0);
            indices.Add(start + 2);
            indices.Add(start + 3);
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XAsteroidChunkMeshSystem))]
    public partial class Space4XAsteroidChunkMeshCleanupSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            if (!RuntimeMode.IsRenderingEnabled)
            {
                return;
            }

            var entities = SystemAPI.QueryBuilder()
                .WithAll<Space4XAsteroidChunkMeshCleanup>()
                .WithNone<TerrainChunk>()
                .Build()
                .ToEntityArray(Allocator.Temp);

            foreach (var entity in entities)
            {
                if (EntityManager.HasComponent<Space4XAsteroidChunkMeshReference>(entity))
                {
                    var reference = EntityManager.GetComponentData<Space4XAsteroidChunkMeshReference>(entity);
                    if (reference.Mesh != null)
                    {
                    UObject.Destroy(reference.Mesh);
                    }
                }

                EntityManager.RemoveComponent<Space4XAsteroidChunkMeshCleanup>(entity);
                EntityManager.RemoveComponent<Space4XAsteroidChunkMeshReference>(entity);
            }

            entities.Dispose();
        }
    }
}
