using PureDOTS.Environment;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Streaming;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

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
        public int MaxChunkRebuildsPerFrame;
        public int NearRebuildCap;
        public float NearRadius;

        public static Space4XAsteroidChunkMeshRebuildConfig Default => new()
        {
            MaxChunkRebuildsPerFrame = 4,
            NearRebuildCap = 2,
            NearRadius = 40f
        };
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XPresentationLifecycleSystem))]
    public partial class Space4XAsteroidChunkMeshQueueSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<TerrainWorldConfig>();
        }

        protected override void OnUpdate()
        {
            if (RuntimeMode.IsHeadless)
            {
                return;
            }

            EnsureQueue();
            var queueEntity = SystemAPI.GetSingletonEntity<Space4XAsteroidChunkMeshRebuildQueue>();
            var queue = EntityManager.GetBuffer<Space4XAsteroidChunkRebuildRequest>(queueEntity);
            var volumeConfigLookup = GetComponentLookup<Space4XAsteroidVolumeConfig>(true);
            volumeConfigLookup.Update(this);

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

                var hasState = SystemAPI.HasComponent<Space4XAsteroidChunkMeshState>(entity);
                var state = hasState ? SystemAPI.GetComponent<Space4XAsteroidChunkMeshState>(entity) : default;
                var hasMesh = SystemAPI.HasComponent<Space4XAsteroidChunkMeshReference>(entity);
                var needsBuild = !hasMesh || !hasState || version > state.LastBuiltVersion;
                if (!needsBuild)
                {
                    continue;
                }

                if (hasState && version <= state.LastQueuedVersion)
                {
                    continue;
                }

                queue.Add(new Space4XAsteroidChunkRebuildRequest { Chunk = entity });
                state.LastQueuedVersion = version;
                if (hasState)
                {
                    EntityManager.SetComponentData(entity, state);
                }
                else
                {
                    EntityManager.AddComponentData(entity, state);
                }
            }
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

        protected override void OnCreate()
        {
            RequireForUpdate<TerrainWorldConfig>();
            RequireForUpdate<Space4XAsteroidChunkMeshRebuildQueue>();
            _vertices = new NativeList<Vector3>(2048, Allocator.Persistent);
            _normals = new NativeList<Vector3>(2048, Allocator.Persistent);
            _uvs = new NativeList<Vector2>(2048, Allocator.Persistent);
            _indices = new NativeList<int>(4096, Allocator.Persistent);
            _colors = new NativeList<Color32>(2048, Allocator.Persistent);
            _chunkLookup = default;
            _chunkLookupCount = -1;
        }

        protected override void OnUpdate()
        {
            if (RuntimeMode.IsHeadless)
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

            var config = Space4XAsteroidChunkMeshRebuildConfig.Default;
            if (EntityManager.HasComponent<Space4XAsteroidChunkMeshRebuildConfig>(queueEntity))
            {
                config = EntityManager.GetComponentData<Space4XAsteroidChunkMeshRebuildConfig>(queueEntity);
            }

            var focus = ResolveFocusPosition();
            var nearRadiusSq = math.max(0f, config.NearRadius);
            nearRadiusSq *= nearRadiusSq;
            var remainingBudget = math.max(0, config.MaxChunkRebuildsPerFrame);
            var nearBudget = math.max(0, config.NearRebuildCap);

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

            while (nearBudget > 0 && queue.Length > 0)
            {
                var idx = FindClosestWithin(queue, focus, nearRadiusSq);
                if (idx < 0)
                {
                    break;
                }

                if (TryRebuildChunk(queue[idx].Chunk, ref voxelAccessor, material, renderMeshDesc, terrainConfig))
                {
                    nearBudget--;
                }

                queue.RemoveAt(idx);
            }

            while (remainingBudget > 0 && queue.Length > 0)
            {
                var idx = FindClosest(queue, focus);
                if (idx < 0)
                {
                    break;
                }

                if (TryRebuildChunk(queue[idx].Chunk, ref voxelAccessor, material, renderMeshDesc, terrainConfig))
                {
                    remainingBudget--;
                }

                queue.RemoveAt(idx);
            }
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

            if (_chunkLookup.IsCreated)
            {
                _chunkLookup.Dispose();
            }

            foreach (var reference in SystemAPI.Query<Space4XAsteroidChunkMeshReference>())
            {
                if (reference.Mesh != null)
                {
                    Object.Destroy(reference.Mesh);
                }
            }
        }

        private Material ResolveMaterial()
        {
            if (SystemAPI.TryGetSingletonEntity<Space4XAsteroidChunkRenderConfig>(out var configEntity))
            {
                var config = EntityManager.GetComponentData<Space4XAsteroidChunkRenderConfig>(configEntity);
                if (EntityManager.HasComponent<Space4XAsteroidChunkPaletteConfig>(configEntity))
                {
                    var palette = EntityManager.GetComponentData<Space4XAsteroidChunkPaletteConfig>(configEntity).Palette;
                    if (palette != null && config.Material != null && config.Material.HasProperty("_MaterialPalette"))
                    {
                        config.Material.SetTexture("_MaterialPalette", palette);
                    }
                }

                return config.Material;
            }

            var paletteTexture = default(Texture2D);
            if (SystemAPI.TryGetSingletonEntity<Space4XAsteroidChunkPaletteConfig>(out var paletteEntity))
            {
                paletteTexture = EntityManager.GetComponentData<Space4XAsteroidChunkPaletteConfig>(paletteEntity).Palette;
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

            var entity = EntityManager.CreateEntity(typeof(Space4XAsteroidChunkRenderConfig));
            EntityManager.SetComponentData(entity, new Space4XAsteroidChunkRenderConfig
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

        private int FindClosestWithin(DynamicBuffer<Space4XAsteroidChunkRebuildRequest> queue, float3 focus, float maxDistanceSq)
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

        private int FindClosest(DynamicBuffer<Space4XAsteroidChunkRebuildRequest> queue, float3 focus)
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
            TerrainWorldConfig terrainConfig)
        {
            if (!EntityManager.Exists(entity) || !EntityManager.HasComponent<TerrainChunk>(entity))
            {
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

            BuildChunkMesh(ref voxelAccessor, chunk, terrainConfig.VoxelSize, _vertices, _normals, _uvs, _colors, _indices);

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
                mesh.Clear();
                mesh.SetVertices(_vertices.AsArray());
                mesh.SetNormals(_normals.AsArray());
                mesh.SetUVs(0, _uvs.AsArray());
                mesh.SetColors(_colors.AsArray());
                mesh.SetTriangles(_indices.AsArray(), 0);
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
            NativeList<int> indices)
        {
            var dims = chunk.VoxelsPerChunk;
            if (dims.x <= 0 || dims.y <= 0 || dims.z <= 0)
            {
                return;
            }

            for (int z = 0; z < dims.z; z++)
            {
                for (int y = 0; y < dims.y; y++)
                {
                    for (int x = 0; x < dims.x; x++)
                    {
                        var voxelCoord = new int3(x, y, z);
                        if (!accessor.TrySampleVoxel(chunk.VolumeEntity, chunk.ChunkCoord, voxelCoord, out var voxelSample) ||
                            voxelSample.SolidMask == 0)
                        {
                            continue;
                        }

                        for (int dir = 0; dir < TerrainVoxelMath.NeighborOffsets.Length; dir++)
                        {
                            var offset = TerrainVoxelMath.NeighborOffsets[dir];
                            if (accessor.TrySampleNeighbor(chunk.VolumeEntity, chunk.ChunkCoord, voxelCoord, offset, out var neighbor) &&
                                neighbor.SolidMask != 0)
                            {
                                continue;
                            }

                            AppendFace(vertices, normals, uvs, colors, indices, voxelCoord, voxelSize, dir, voxelSample.MaterialId);
                        }
                    }
                }
            }
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
            byte materialId)
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
            var color = new Color32(materialId, 0, 0, 255);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
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
            if (RuntimeMode.IsHeadless)
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
                        Object.Destroy(reference.Mesh);
                    }
                }

                EntityManager.RemoveComponent<Space4XAsteroidChunkMeshCleanup>(entity);
                EntityManager.RemoveComponent<Space4XAsteroidChunkMeshReference>(entity);
            }

            entities.Dispose();
        }
    }
}
