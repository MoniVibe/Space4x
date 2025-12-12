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
    }

    /// <summary>
    /// Baker that builds the render catalog blob and a RenderMeshArray for Entities Graphics.
    /// </summary>
    public class RenderCatalogBaker : Baker<RenderCatalogAuthoring>
    {
        public override void Bake(RenderCatalogAuthoring authoring)
        {
            Debug.Log("[Space4X RenderCatalogBaker] Bake started.");

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

            Debug.Log($"[Space4X RenderCatalogBaker] Entries count: {entries.Length}");

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
            var builder = new BlobBuilder(Allocator.Temp);
            ref var catalogRoot = ref builder.ConstructRoot<Space4XRenderMeshCatalog>();
            var blobEntries = builder.Allocate(ref catalogRoot.Entries, validEntries.Count);

            for (int i = 0; i < validEntries.Count; i++)
            {
                var src = validEntries[i];

                blobEntries[i] = new Space4XRenderMeshCatalogEntry
                {
                    ArchetypeId   = src.ArchetypeId,
                    MaterialIndex = i,
                    MeshIndex     = i,
                    SubMesh       = src.SubMesh,
                    BoundsCenter  = new float3(src.BoundsCenter.x, src.BoundsCenter.y, src.BoundsCenter.z),
                    BoundsExtents = new float3(src.BoundsExtents.x, src.BoundsExtents.y, src.BoundsExtents.z)
                };
            }

            var catalogRef = builder.CreateBlobAssetReference<Space4XRenderMeshCatalog>(Allocator.Persistent);
            builder.Dispose();

            // 3. Create singleton entity
            var entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new Space4XRenderCatalogSingleton
            {
                Catalog = catalogRef
            });

            AddSharedComponentManaged(entity, renderMeshArray);

            Debug.Log($"[Space4X RenderCatalogBaker] Created catalog singleton entity {entity} with {validEntries.Count} valid entries.");
        }
    }
}
