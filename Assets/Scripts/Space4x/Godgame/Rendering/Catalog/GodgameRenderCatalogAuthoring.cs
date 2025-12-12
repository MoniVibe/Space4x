using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace Godgame.Rendering.Catalog
{
    [DisallowMultipleComponent]
    public class GodgameRenderCatalogAuthoring : MonoBehaviour
    {
        public GodgameRenderCatalogDefinition CatalogDefinition;
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

            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<GodgameRenderMeshCatalog>();
            var blobEntries = builder.Allocate(ref root.Entries, validEntries.Count);

            for (int i = 0; i < validEntries.Count; i++)
            {
                var src = validEntries[i];
                blobEntries[i] = new GodgameRenderMeshCatalogEntry
                {
                    ArchetypeId = src.Key,
                    MaterialIndex = i,
                    MeshIndex = i,
                    SubMesh = src.SubMesh,
                    BoundsCenter = new float3(src.BoundsCenter.x, src.BoundsCenter.y, src.BoundsCenter.z),
                    BoundsExtents = new float3(src.BoundsExtents.x, src.BoundsExtents.y, src.BoundsExtents.z)
                };
            }

            var catalogRef = builder.CreateBlobAssetReference<GodgameRenderMeshCatalog>(Allocator.Persistent);

            var meshes = new Mesh[validEntries.Count];
            var materials = new Material[validEntries.Count];
            for (int i = 0; i < validEntries.Count; i++)
            {
                meshes[i] = validEntries[i].Mesh;
                materials[i] = validEntries[i].Material;
            }

            var renderMeshArray = new RenderMeshArray(materials, meshes);
            var singletonEntity = GetEntity(TransformUsageFlags.None);
            AddComponent(singletonEntity, new GodgameRenderCatalogSingleton { Catalog = catalogRef });
            AddSharedComponentManaged(singletonEntity, renderMeshArray);
#if UNITY_EDITOR
            Debug.Log($"[GodgameRenderCatalog] Baked {validEntries.Count} entries.");
#endif
        }
    }
}
