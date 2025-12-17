using PureDOTS.Rendering;
using Unity.Entities;
using UnityEngine;

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

        var query = world.EntityManager.CreateEntityQuery(typeof(RenderCatalogSingleton));
        if (query.IsEmpty)
        {
            Debug.Log("RenderCatalogSingleton NOT found.");
        }
        else
        {
            Debug.Log($"RenderCatalogSingleton FOUND. Count: {query.CalculateEntityCount()}");
        }
        
        var renderKeyQuery = world.EntityManager.CreateEntityQuery(typeof(RenderKey));
        Debug.Log($"RenderKey entities count: {renderKeyQuery.CalculateEntityCount()}");
    }
}
