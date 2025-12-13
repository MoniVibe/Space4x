using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;
using Space4X.Rendering;
using Unity.Mathematics;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Space4X.Rendering.Catalog
{
    using Debug = UnityEngine.Debug;

    
    [DisallowMultipleComponent]
    public partial class RenderCatalogAuthoring : MonoBehaviour
    {
        public Space4XRenderCatalogDefinition CatalogDefinition;

        [Header("Fallbacks")]
        [SerializeField] private Material fallbackMaterial;
        [SerializeField] private Mesh fallbackMesh;

        private BlobAssetReference<Space4XRenderMeshCatalog> _runtimeCatalogRef;
        private Entity _runtimeCatalogEntity = Entity.Null;
        private World _world;
        private bool _ownsCatalogEntity;

        public Material FallbackMaterial => fallbackMaterial;
        public Mesh FallbackMesh => fallbackMesh;

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

#if UNITY_EDITOR
        private const string DefaultFallbackMaterialPath = "Assets/Shared/Rendering/Materials/M_EntitiesFallback_URP.mat";
        private const string DefaultFallbackMeshPath = "Assets/Shared/Rendering/Meshes/M_FallbackURPMesh.asset";

        private void Reset()
        {
            TryAssignDefaultFallbacks();
        }

        private void OnValidate()
        {
            TryAssignDefaultFallbacks();
        }

        private void TryAssignDefaultFallbacks()
        {
            if (fallbackMaterial == null)
            {
                fallbackMaterial = AssetDatabase.LoadAssetAtPath<Material>(DefaultFallbackMaterialPath);
            }

            if (fallbackMesh == null)
            {
                fallbackMesh = AssetDatabase.LoadAssetAtPath<Mesh>(DefaultFallbackMeshPath);
                if (fallbackMesh == null)
                {
                    fallbackMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                }
            }
        }
#endif

        private void Start()
        {
            if (!Application.isPlaying) return;
            if (CatalogDefinition == null) return;
            if (!ValidateFallbackAssets()) return;

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
            if (!ValidateFallbackAssets()) return;

            var validEntries = new System.Collections.Generic.List<Space4XRenderCatalogDefinition.Entry>();
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].Mesh != null && entries[i].Material != null)
                {
                    validEntries.Add(entries[i]);
                }
            }

            if (validEntries.Count == 0) return;

            if (!TryBuildCatalogAssets(validEntries, fallbackMaterial, fallbackMesh, out var renderMeshArray, out var catalogBlob))
            {
                return;
            }

            _runtimeCatalogRef = catalogBlob;

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

            if (authoring.FallbackMaterial == null || authoring.FallbackMesh == null)
            {
                Debug.LogError("[Space4X RenderCatalogBaker] Fallback material/mesh is not assigned. Assign M_EntitiesFallback_URP and a mesh.");
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

            if (!RenderCatalogAuthoring.TryBuildCatalogAssets(validEntries, authoring.FallbackMaterial, authoring.FallbackMesh, out var renderMeshArray, out var catalogRef))
            {
                Debug.LogError("[Space4X RenderCatalogBaker] Failed to build catalog assets.");
                return;
            }

            // 3. Create singleton entity
            var entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new Space4XRenderCatalogSingleton
            {
                Catalog = catalogRef
            });

            AddSharedComponentManaged(entity, renderMeshArray);

            LogInfo($"[Space4X RenderCatalogBaker] Created catalog singleton entity {entity} with {validEntries.Count} valid entries (+ fallback).");
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

    public partial class RenderCatalogAuthoring
    {
        private bool ValidateFallbackAssets()
        {
            var valid = true;
            if (fallbackMaterial == null)
            {
                Debug.LogError("[RenderCatalogAuthoring] FallbackMaterial is null. Assign M_EntitiesFallback_URP.");
                valid = false;
            }

            if (fallbackMesh == null)
            {
                Debug.LogError("[RenderCatalogAuthoring] FallbackMesh is null.");
                valid = false;
            }

            return valid;
        }

        internal static bool TryBuildCatalogAssets(
            System.Collections.Generic.List<Space4XRenderCatalogDefinition.Entry> validEntries,
            Material fallbackMaterial,
            Mesh fallbackMesh,
            out RenderMeshArray renderMeshArray,
            out BlobAssetReference<Space4XRenderMeshCatalog> catalogBlob)
        {
            renderMeshArray = default;
            catalogBlob = default;

            if (fallbackMaterial == null || fallbackMesh == null)
            {
                Debug.LogError("[RenderCatalogAuthoring] Cannot build catalog without fallback assets.");
                return false;
            }

            var entryCount = validEntries.Count + 1;
            var meshes = new Mesh[entryCount];
            var materials = new Material[entryCount];

            meshes[0] = fallbackMesh;
            materials[0] = fallbackMaterial;

            for (int i = 0; i < validEntries.Count; i++)
            {
                var slot = i + 1;
                meshes[slot] = validEntries[i].Mesh;
                materials[slot] = validEntries[i].Material;
            }

            renderMeshArray = new RenderMeshArray(materials, meshes);

            using var builder = new BlobBuilder(Allocator.Temp);
            ref var catalogRoot = ref builder.ConstructRoot<Space4XRenderMeshCatalog>();
            var blobEntries = builder.Allocate(ref catalogRoot.Entries, entryCount);

            var fallbackBounds = fallbackMesh.bounds;
            blobEntries[0] = new Space4XRenderMeshCatalogEntry
            {
                ArchetypeId = 0,
                MaterialIndex = 0,
                MeshIndex = 0,
                SubMesh = 0,
                BoundsCenter = new float3(fallbackBounds.center.x, fallbackBounds.center.y, fallbackBounds.center.z),
                BoundsExtents = new float3(fallbackBounds.extents.x, fallbackBounds.extents.y, fallbackBounds.extents.z)
            };

            for (int i = 0; i < validEntries.Count; i++)
            {
                var src = validEntries[i];
                var slot = i + 1;
                blobEntries[slot] = new Space4XRenderMeshCatalogEntry
                {
                    ArchetypeId = (ushort)src.ArchetypeId,
                    MaterialIndex = slot,
                    MeshIndex = slot,
                    SubMesh = src.SubMesh,
                    BoundsCenter = new float3(src.BoundsCenter.x, src.BoundsCenter.y, src.BoundsCenter.z),
                    BoundsExtents = new float3(src.BoundsExtents.x, src.BoundsExtents.y, src.BoundsExtents.z)
                };
            }

            catalogBlob = builder.CreateBlobAssetReference<Space4XRenderMeshCatalog>(Allocator.Persistent);
            return true;
        }
    }
}
