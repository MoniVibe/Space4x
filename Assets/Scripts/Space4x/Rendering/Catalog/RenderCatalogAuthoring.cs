using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;
using Space4X.Rendering;
using Unity.Mathematics;

namespace Space4X.Rendering.Catalog
{
    [DisallowMultipleComponent]
    public class RenderCatalogAuthoring : MonoBehaviour
    {
        public Space4XRenderCatalogDefinition CatalogDefinition;
        private BlobAssetReference<Space4XRenderMeshCatalog> _runtimeCatalogRef;
        private Entity _runtimeCatalogEntity = Entity.Null;
        private World _world;
        private bool _ownsCatalogEntity;

        private static void LogInfo(string message)
        {
#if UNITY_EDITOR
            Debug.Log(message);
#else
            if (Debug.isDebugBuild && !Application.isBatchMode)
            {
                Debug.Log(message);
            }
#endif
        }

        private void Start()
        {
            if (!Application.isPlaying) return;
            if (CatalogDefinition == null) return;

            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null || !_world.IsCreated) return;

            Bootstrap(_world.EntityManager);
        }

        private void OnDisable()
        {
            CleanupRuntimeCatalog();
        }

        private void OnDestroy()
        {
            CleanupRuntimeCatalog();
        }

        private void Bootstrap(EntityManager em)
        {
            if (_runtimeCatalogRef.IsCreated ||
                (_runtimeCatalogEntity != Entity.Null && em.Exists(_runtimeCatalogEntity)))
            {
                return;
            }

            using var existingCatalogQuery = em.CreateEntityQuery(ComponentType.ReadOnly<Space4XRenderCatalogSingleton>());
            if (!existingCatalogQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            LogInfo("[RenderCatalogAuthoring] Bootstrapping RenderCatalog at runtime...");
            var entries = CatalogDefinition.Entries;
            if (entries == null || entries.Length == 0) return;

            var validEntries = new System.Collections.Generic.List<Space4XRenderCatalogDefinition.Entry>();
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].Mesh != null && entries[i].Material != null)
                {
                    validEntries.Add(entries[i]);
                }
            }

            if (validEntries.Count == 0) return;

            var meshes = new Mesh[validEntries.Count];
            var materials = new Material[validEntries.Count];

            for (int i = 0; i < validEntries.Count; i++)
            {
                meshes[i] = validEntries[i].Mesh;
                materials[i] = validEntries[i].Material;
            }

            var renderMeshArray = new RenderMeshArray(materials, meshes);

            using var builder = new BlobBuilder(Allocator.Temp);
            ref var catalogRoot = ref builder.ConstructRoot<Space4XRenderMeshCatalog>();
            var blobEntries = builder.Allocate(ref catalogRoot.Entries, validEntries.Count);

            for (int i = 0; i < validEntries.Count; i++)
            {
                var src = validEntries[i];
                blobEntries[i] = new Space4XRenderMeshCatalogEntry
                {
                    ArchetypeId = (ushort)src.ArchetypeId,
                    MaterialIndex = i,
                    MeshIndex = i,
                    SubMesh = src.SubMesh,
                    BoundsCenter = new float3(src.BoundsCenter.x, src.BoundsCenter.y, src.BoundsCenter.z),
                    BoundsExtents = new float3(src.BoundsExtents.x, src.BoundsExtents.y, src.BoundsExtents.z)
                };
            }

            _runtimeCatalogRef = builder.CreateBlobAssetReference<Space4XRenderMeshCatalog>(Allocator.Persistent);

            _runtimeCatalogEntity = em.CreateEntity();
            _ownsCatalogEntity = true;
            em.AddComponentData(_runtimeCatalogEntity, new Space4XRenderCatalogSingleton { Catalog = _runtimeCatalogRef });
            em.AddSharedComponentManaged(_runtimeCatalogEntity, renderMeshArray);

            LogInfo($"[RenderCatalogAuthoring] Created catalog singleton entity {_runtimeCatalogEntity} with {validEntries.Count} valid entries.");
        }

        private void CleanupRuntimeCatalog()
        {
            if (_ownsCatalogEntity &&
                _world != null &&
                _world.IsCreated &&
                _runtimeCatalogEntity != Entity.Null &&
                _world.EntityManager.Exists(_runtimeCatalogEntity))
            {
                _world.EntityManager.DestroyEntity(_runtimeCatalogEntity);
            }

            _runtimeCatalogEntity = Entity.Null;
            _ownsCatalogEntity = false;

            if (_runtimeCatalogRef.IsCreated)
            {
                _runtimeCatalogRef.Dispose();
                _runtimeCatalogRef = default;
            }
        }
    }

    /// <summary>
    /// Baker that builds the render catalog blob and a RenderMeshArray for Entities Graphics.
    /// </summary>
    public class RenderCatalogBaker : Baker<RenderCatalogAuthoring>
    {
        public override void Bake(RenderCatalogAuthoring authoring)
        {
            LogInfo("[Space4X RenderCatalogBaker] Bake started. Force Recompile.");

            var catalogDefinition = authoring.CatalogDefinition;
            if (catalogDefinition == null)
            {
                Debug.LogWarning("[Space4X RenderCatalogBaker] No CatalogDefinition assigned.");
                return;
            }

            var entries = catalogDefinition.Entries;
            if (entries == null)
            {
                Debug.LogWarning("[Space4X RenderCatalogBaker] CatalogDefinition.Entries is null.");
                return;
            }

            if (entries.Length == 0)
            {
#if UNITY_EDITOR
                Debug.LogError(
                    "[Space4X RenderCatalogBaker] Catalog has no entries. " +
                    "At least one entry is required, or nothing will render.");
#endif
                return;
            }

            LogInfo($"[Space4X RenderCatalogBaker] Entries count: {entries.Length}");

            // 1. Filter valid entries (skip null meshes/materials)
            var validEntries = new System.Collections.Generic.List<Space4XRenderCatalogDefinition.Entry>();
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].Mesh != null && entries[i].Material != null)
                {
                    validEntries.Add(entries[i]);
                }
                else
                {
#if UNITY_EDITOR
                    Debug.LogWarning(
                        $"[Space4X RenderCatalogBaker] Skipping entry {i} (ArchetypeId={entries[i].ArchetypeId}) due to null mesh/material");
#endif
                }
            }

            if (validEntries.Count == 0)
            {
#if UNITY_EDITOR
                Debug.LogError("[Space4X RenderCatalogBaker] No valid entries found. Catalog will be empty.");
#endif
                return;
            }

            // 2. Build RenderMeshArray from valid entries
            var meshes    = new Mesh[validEntries.Count];
            var materials = new Material[validEntries.Count];

            for (int i = 0; i < validEntries.Count; i++)
            {
                meshes[i]    = validEntries[i].Mesh;
                materials[i] = validEntries[i].Material;
            }

            var renderMeshArray = new RenderMeshArray(materials, meshes);

            // 3. Build RenderMesh catalog blob (ArchetypeId -> indices)
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var catalogRoot = ref builder.ConstructRoot<Space4XRenderMeshCatalog>();
            var blobEntries = builder.Allocate(ref catalogRoot.Entries, validEntries.Count);

            for (int i = 0; i < validEntries.Count; i++)
            {
                var src = validEntries[i];

                blobEntries[i] = new Space4XRenderMeshCatalogEntry
                {
                    ArchetypeId   = (ushort)src.ArchetypeId,
                    MaterialIndex = i,
                    MeshIndex     = i,
                    SubMesh       = src.SubMesh,
                    BoundsCenter  = new float3(src.BoundsCenter.x, src.BoundsCenter.y, src.BoundsCenter.z),
                    BoundsExtents = new float3(src.BoundsExtents.x, src.BoundsExtents.y, src.BoundsExtents.z)
                };
            }

            var catalogRef = builder.CreateBlobAssetReference<Space4XRenderMeshCatalog>(Allocator.Persistent);

            // 3. Create singleton entity
            var entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new Space4XRenderCatalogSingleton
            {
                Catalog = catalogRef
            });

            AddSharedComponentManaged(entity, renderMeshArray);

            LogInfo($"[Space4X RenderCatalogBaker] Created catalog singleton entity {entity} with {validEntries.Count} valid entries.");
        }

        private static void LogInfo(string message)
        {
#if UNITY_EDITOR
            Debug.Log(message);
#else
            if (Debug.isDebugBuild && !Application.isBatchMode)
            {
                Debug.Log(message);
            }
#endif
        }
    }
}
