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

        if (!em.CreateEntityQuery(ComponentType.ReadOnly<RenderMeshArray>())
              .TryGetSingletonEntity<RenderMeshArray>(out var rmaEntity))
        {
            Debug.LogWarning("[Space4XWorldDiag] No RenderMeshArray singleton.");
            return;
        }

        var rma = em.GetSharedComponentManaged<RenderMeshArray>(rmaEntity);

        if (!em.CreateEntityQuery(ComponentType.ReadOnly<Space4XRenderCatalogSingleton>())
              .TryGetSingletonEntity<Space4XRenderCatalogSingleton>(out var catEntity))
        {
            Debug.LogWarning("[Space4XWorldDiag] No Space4XRenderCatalogSingleton.");
            return;
        }

        var catSingleton = em.GetComponentData<Space4XRenderCatalogSingleton>(catEntity);
        if (!catSingleton.Catalog.IsCreated)
        {
            Debug.LogWarning("[Space4XWorldDiag] Catalog blob not created.");
            return;
        }

        ref var catalog = ref catSingleton.Catalog.Value;
        Debug.Log($"[Space4XWorldDiag] RMA: {rma.Materials.Length} mats, {rma.Meshes.Length} meshes. Catalog entries: {catalog.Entries.Length}");
    }
}
