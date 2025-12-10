using Unity.Entities;
using Unity.Rendering;
using UnityEngine;
using UnityEditor;
using Space4X.Rendering.Catalog;

/// <summary>
/// Obsolete diagnostic tool for render catalog.
/// Render catalog is now working - use Space4XRenderCatalogSmokeTest instead.
/// Kept in repo for historical reference.
/// </summary>
public static class DiagnoseRenderCatalog
{
    // Obsolete: Render catalog now working, use smoketests instead
    // [MenuItem("Space4X/Diagnose Render Catalog")]
    // public static void Diagnose()
    // {
    //     var world = World.DefaultGameObjectInjectionWorld;
    //     if (world == null)
    //     {
    //         Debug.LogError("No default world found. Are you in Play Mode?");
    //         return;
    //     }
    //
    //     var entityManager = world.EntityManager;
    //     var query = entityManager.CreateEntityQuery(typeof(Space4XRenderMeshArraySingleton));
    //
    //     if (query.IsEmpty)
    //     {
    //         Debug.LogError("Space4XRenderMeshArraySingleton not found in the default world.");
    //         return;
    //     }
    //
    //     var entity = query.GetSingletonEntity();
    //     var singleton = entityManager.GetSharedComponentManaged<Space4XRenderMeshArraySingleton>(entity);
    //
    //     // RenderMeshArray is a struct, but it holds arrays.
    //     // Value is the RenderMeshArray.
    //
    //     int meshCount = singleton.Value.MeshReferences.Length;
    //     int matCount = singleton.Value.MaterialReferences.Length;
    //
    //     Debug.Log($"[Space4X] Render Catalog Diagnosis:\n" +
    //               $"Entity: {entity}\n" +
    //               $"Mesh Count: {meshCount}\n" +
    //               $"Material Count: {matCount}");
    //
    //     if (meshCount > 0 && matCount > 0)
    //     {
    //         Debug.Log("[Space4X] Render Catalog is populated correctly.");
    //     }
    //     else
    //     {
    //         Debug.LogError("[Space4X] Render Catalog is empty!");
    //     }
    // }
}
