using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Godgame.Rendering.Catalog
{
    [DisallowMultipleComponent]
    public class GodgameRenderCatalogAuthoring : MonoBehaviour
    {
        public GodgameRenderCatalogDefinition CatalogDefinition;

        [Header("Fallbacks")]
        [SerializeField] private Material fallbackMaterial;
        [SerializeField] private Mesh fallbackMesh;

        public Material FallbackMaterial => fallbackMaterial;
        public Mesh FallbackMesh => fallbackMesh;

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
    }

    public sealed class GodgameRenderCatalogBaker : Baker<GodgameRenderCatalogAuthoring>
    {
        public override void Bake(GodgameRenderCatalogAuthoring authoring)
        {
            var catalogDefinition = authoring.CatalogDefinition;
            if (catalogDefinition == null || catalogDefinition.Entries == null || catalogDefinition.Entries.Length == 0)
            {
#if UNITY_EDITOR
                Debug.LogWarning("[GodgameRenderCatalog] No catalog definition assigned.");
#endif
                return;
            }

            if (authoring.FallbackMaterial == null || authoring.FallbackMesh == null)
            {
                Debug.LogError("[GodgameRenderCatalog] Fallback material/mesh is not assigned. Assign M_EntitiesFallback_URP and a mesh.");
                return;
            }

            var validEntries = new List<GodgameRenderCatalogDefinition.Entry>(catalogDefinition.Entries.Length);
            foreach (var entry in catalogDefinition.Entries)
            {
                if (entry.Mesh == null || entry.Material == null)
                {
#if UNITY_EDITOR
                    Debug.LogWarning($"[GodgameRenderCatalog] Skipping entry for key {entry.Key} due to missing mesh or material.");
#endif
                    continue;
                }

                validEntries.Add(entry);
            }

            if (validEntries.Count == 0)
            {
#if UNITY_EDITOR
                Debug.LogWarning("[GodgameRenderCatalog] No valid entries available; catalog will not bake.");
#endif
                return;
            }

            if (!TryBuildCatalogAssets(validEntries, authoring.FallbackMaterial, authoring.FallbackMesh, out var catalogRef, out var renderMeshArray))
                return;

            var singletonEntity = GetEntity(TransformUsageFlags.None);
            AddComponent(singletonEntity, new GodgameRenderCatalogSingleton { Catalog = catalogRef });
            AddSharedComponentManaged(singletonEntity, renderMeshArray);
#if UNITY_EDITOR
            Debug.Log($"[GodgameRenderCatalog] Baked {validEntries.Count} entries (+ fallback).");
#endif
        }

        private static bool TryBuildCatalogAssets(
            List<GodgameRenderCatalogDefinition.Entry> validEntries,
            Material fallbackMaterial,
            Mesh fallbackMesh,
            out BlobAssetReference<GodgameRenderMeshCatalog> catalogRef,
            out RenderMeshArray renderMeshArray)
        {
            catalogRef = default;
            renderMeshArray = default;

            if (fallbackMaterial == null || fallbackMesh == null)
            {
                Debug.LogError("[GodgameRenderCatalog] Cannot build catalog without fallback assets.");
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
            ref var root = ref builder.ConstructRoot<GodgameRenderMeshCatalog>();
            var blobEntries = builder.Allocate(ref root.Entries, entryCount);

            var fallbackBounds = fallbackMesh.bounds;
            blobEntries[0] = new GodgameRenderMeshCatalogEntry
            {
                ArchetypeId = -1,
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
                blobEntries[slot] = new GodgameRenderMeshCatalogEntry
                {
                    ArchetypeId = src.Key,
                    MaterialIndex = slot,
                    MeshIndex = slot,
                    SubMesh = src.SubMesh,
                    BoundsCenter = new float3(src.BoundsCenter.x, src.BoundsCenter.y, src.BoundsCenter.z),
                    BoundsExtents = new float3(src.BoundsExtents.x, src.BoundsExtents.y, src.BoundsExtents.z)
                };
            }

            catalogRef = builder.CreateBlobAssetReference<GodgameRenderMeshCatalog>(Allocator.Persistent);
            return true;
        }
    }
}
