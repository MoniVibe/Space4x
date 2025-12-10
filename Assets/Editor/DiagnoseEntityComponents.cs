using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEditor;
using Space4X.Rendering;

public static class DiagnoseEntityComponents
{
    [MenuItem("Space4X/Diagnose Entity Components")]
    public static void Diagnose()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null)
        {
            Debug.LogError("No default world found. Are you in Play Mode?");
            return;
        }

        var entityManager = world.EntityManager;
        
        // Query for Space4X RenderKey entities (canonical type)
        var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<RenderKey>());

        int renderKeyCount = query.CalculateEntityCount();
        Debug.Log($"[DiagnoseEntityComponents] RenderKey entities count: {renderKeyCount}");

        if (query.IsEmpty)
        {
            Debug.LogError("[DiagnoseEntityComponents] No entities with RenderKey found.");
            return;
        }

        using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
        foreach (var entity in entities)
        {
            bool hasTransform = entityManager.HasComponent<LocalTransform>(entity);
            bool hasMaterialMeshInfo = entityManager.HasComponent<MaterialMeshInfo>(entity);
            bool hasRenderBounds = entityManager.HasComponent<RenderBounds>(entity);
            bool hasWorldRenderBounds = entityManager.HasComponent<WorldRenderBounds>(entity);

            Debug.Log($"[Space4X] Entity {entity.Index}:{entity.Version} (Has RenderKey):\n" +
                      $"- LocalTransform: {hasTransform}\n" +
                      $"- MaterialMeshInfo: {hasMaterialMeshInfo}\n" +
                      $"- RenderBounds: {hasRenderBounds}\n" +
                      $"- WorldRenderBounds: {hasWorldRenderBounds}");

            if (hasTransform)
            {
                var transform = entityManager.GetComponentData<LocalTransform>(entity);
                Debug.Log($"  Position: {transform.Position}, Rotation: {transform.Rotation}, Scale: {transform.Scale}");
            }
            
            // Avoid accessing RenderBounds.Value directly if AABB is missing reference
            if (hasRenderBounds)
            {
                Debug.Log($"  RenderBounds: Present (Details skipped to avoid compilation errors)");
            }
        }
    }
}
