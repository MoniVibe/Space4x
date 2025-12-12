using Unity.Entities;
using UnityEngine;
using Space4X.Rendering;

public class CheckSingleton
{
    public static void Execute()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null)
        {
            Debug.Log("No default world.");
            return;
        }

        var query = world.EntityManager.CreateEntityQuery(typeof(Space4XRenderCatalogSingleton));
        if (query.IsEmpty)
        {
            Debug.Log("Space4XRenderCatalogSingleton NOT found.");
        }
        else
        {
            Debug.Log($"Space4XRenderCatalogSingleton FOUND. Count: {query.CalculateEntityCount()}");
        }
        
        var renderKeyQuery = world.EntityManager.CreateEntityQuery(typeof(RenderKey));
        Debug.Log($"RenderKey entities count: {renderKeyQuery.CalculateEntityCount()}");
    }
}
