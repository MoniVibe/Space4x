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

        var query = world.EntityManager.CreateEntityQuery(typeof(RenderPresentationCatalog));
        if (query.IsEmpty)
        {
            Debug.Log("RenderPresentationCatalog NOT found.");
        }
        else
        {
            Debug.Log($"RenderPresentationCatalog FOUND. Count: {query.CalculateEntityCount()}");
        }
        
        var renderKeyQuery = world.EntityManager.CreateEntityQuery(typeof(RenderKey));
        Debug.Log($"RenderKey entities count: {renderKeyQuery.CalculateEntityCount()}");
    }
}
