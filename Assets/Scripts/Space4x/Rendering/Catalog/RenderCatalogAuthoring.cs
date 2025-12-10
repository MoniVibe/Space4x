using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;
using Space4X.Rendering;

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
                Debug.LogWarning("[Space4X RenderCatalogBaker] CatalogDefinition has 0 entries.");
                return;
            }

            Debug.Log($"[Space4X RenderCatalogBaker] Entries count: {entries.Length}");

            // 1. Build RenderMeshArray
            var meshes    = new Mesh[entries.Length];
            var materials = new Material[entries.Length];

            for (int i = 0; i < entries.Length; i++)
            {
                meshes[i]    = entries[i].Mesh;
                materials[i] = entries[i].Material;
            }

            var renderMeshArray = new RenderMeshArray(materials, meshes);

            // 2. Build RenderMesh catalog blob (ArchetypeId -> indices)
            var builder = new BlobBuilder(Allocator.Temp);
            ref var catalogRoot = ref builder.ConstructRoot<Space4XRenderMeshCatalog>();
            var blobEntries = builder.Allocate(ref catalogRoot.Entries, entries.Length);

            for (int i = 0; i < entries.Length; i++)
            {
                var src = entries[i];

                blobEntries[i] = new Space4XRenderMeshCatalogEntry
                {
                    ArchetypeId   = src.ArchetypeId,
                    MaterialIndex = i,
                    MeshIndex     = i,
                    SubMesh       = src.SubMesh
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

            Debug.Log($"[Space4X RenderCatalogBaker] Created catalog singleton entity {entity} with {entries.Length} entries.");
        }
    }
}
