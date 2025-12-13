using Unity.Entities;
using Unity.Rendering;
using UnityEditor;
using UnityEngine;
using Space4X.Rendering;

public static class Space4XWorldDiag
{
    [MenuItem("Tools/Space4X/Print Render Catalog State")]
    public static void PrintRenderCatalogState()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null)
        {
            Debug.LogWarning("[Space4XWorldDiag] No default world.");
            return;
        }

        var em = world.EntityManager;

        var catalogQuery = em.CreateEntityQuery(ComponentType.ReadOnly<Space4XRenderCatalogSingleton>());
        if (!catalogQuery.TryGetSingletonEntity<Space4XRenderCatalogSingleton>(out var catEntity))
        {
            Debug.LogWarning("[Space4XWorldDiag] No Space4XRenderCatalogSingleton.");
            return;
        }

        if (!em.HasComponent<RenderMeshArray>(catEntity))
        {
            Debug.LogWarning("[Space4XWorldDiag] Catalog entity has no RenderMeshArray shared component.");
            return;
        }

        var rma = em.GetSharedComponentManaged<RenderMeshArray>(catEntity);
        var catSingleton = em.GetComponentData<Space4XRenderCatalogSingleton>(catEntity);
        if (!catSingleton.Catalog.IsCreated)
        {
            Debug.LogWarning("[Space4XWorldDiag] Catalog blob not created.");
            return;
        }

        ref var catalog = ref catSingleton.Catalog.Value;
        var materialCount = rma.MaterialReferences?.Length ?? 0;
        var meshCount = rma.MeshReferences?.Length ?? 0;
        Debug.Log($"[Space4XWorldDiag] RMA: {materialCount} mats, {meshCount} meshes. Catalog entries: {catalog.Entries.Length}");
    }
}
