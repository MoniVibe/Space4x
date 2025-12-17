using PureDOTS.Rendering;
using Unity.Entities;
using Unity.Rendering;
using UnityEditor;
using UnityEngine;

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

        var catalogQuery = em.CreateEntityQuery(ComponentType.ReadOnly<RenderCatalogSingleton>());
        if (!catalogQuery.TryGetSingletonEntity<RenderCatalogSingleton>(out var catEntity))
        {
            Debug.LogWarning("[Space4XWorldDiag] No RenderCatalogSingleton.");
            return;
        }

        var catalogSingleton = em.GetComponentData<RenderCatalogSingleton>(catEntity);
        var rmaEntity = catalogSingleton.RenderMeshArrayEntity == Entity.Null ? catEntity : catalogSingleton.RenderMeshArrayEntity;
        if (!em.HasComponent<RenderMeshArray>(rmaEntity))
        {
            Debug.LogWarning("[Space4XWorldDiag] Catalog entity has no RenderMeshArray shared component.");
            return;
        }

        var rma = em.GetSharedComponentManaged<RenderMeshArray>(rmaEntity);
        if (!catalogSingleton.Catalog.IsCreated)
        {
            Debug.LogWarning("[Space4XWorldDiag] Catalog blob not created.");
            return;
        }

        ref var catalog = ref catalogSingleton.Catalog.Value;
        var materialCount = rma.MaterialReferences?.Length ?? 0;
        var meshCount = rma.MeshReferences?.Length ?? 0;
        Debug.Log($"[Space4XWorldDiag] RMA: {materialCount} mats, {meshCount} meshes. Variants: {catalog.Variants.Length} Themes: {catalog.Themes.Length}");
    }
}
