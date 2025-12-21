using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Collections;
using Space4X.Registry;
using UnityEngine.Scripting.APIUpdating;

//#define SPACE4X_DIAGNOSE_RENDER

#if SPACE4X_DIAGNOSE_RENDER
[MovedFrom(true, null, null, "DiagnoseDOTSRender")]
public class DiagnoseScenarioRender : MonoBehaviour
{
    private bool _hasRun = false;
    private float _timer = 0f;

    void Update()
    {
        if (_hasRun) return;

        _timer += Time.deltaTime;
        if (_timer < 2.0f) return; // Wait 2 seconds for entities to spawn

        _hasRun = true;
        RunDiagnostics();
    }

    void RunDiagnostics()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null)
        {
            Debug.LogError("Default World not found!");
            return;
        }

        var em = world.EntityManager;

        Debug.Log("=== Scenario Render Diagnostics ===");

        // 1. Carrier (check for Carrier component, lifecycle system adds CarrierPresentationTag)
        CheckEntity(em, typeof(Carrier), "Carrier");

        // 2. Craft (check for MiningVessel component, lifecycle system adds CraftPresentationTag)
        CheckEntity(em, typeof(MiningVessel), "Craft/MiningVessel");

        // 3. Asteroid
        CheckEntity(em, typeof(Asteroid), "Asteroid");
        
        Debug.Log("=== End Diagnostics ===");
    }

    void CheckEntity(EntityManager em, System.Type componentType, string label)
    {
        var query = em.CreateEntityQuery(componentType);
        if (query.IsEmpty)
        {
            Debug.LogWarning($"No entities found for {label}");
            return;
        }

        var entities = query.ToEntityArray(Allocator.Temp);
        var entity = entities[0];
        entities.Dispose();

        Debug.Log($"--- Inspecting {label} (Entity: {entity}) ---");

        // LocalTransform
        if (em.HasComponent<LocalTransform>(entity))
        {
            var lt = em.GetComponentData<LocalTransform>(entity);
            Debug.Log($"LocalTransform.Position: {lt.Position}");
        }
        else
        {
            Debug.LogError("Missing LocalTransform!");
        }

        // Check components presence
        LogComponentPresence<LocalTransform>(em, entity, "LocalTransform");
        LogComponentPresence<MaterialMeshInfo>(em, entity, "MaterialMeshInfo");
        LogComponentPresence<WorldRenderBounds>(em, entity, "WorldRenderBounds");
        LogComponentPresence<RenderBounds>(em, entity, "RenderBounds");
        
        // RenderMeshArray is Shared Component
        bool hasRMA = em.HasComponent<RenderMeshArray>(entity);
        Debug.Log($"RenderMeshArray: {(hasRMA ? "YES" : "NO")}");

        // Check RenderLODData and RenderCullable
        LogComponentPresence<RenderLODData>(em, entity, "RenderLODData");
        LogComponentPresence<RenderCullable>(em, entity, "RenderCullable");

        // MaterialMeshInfo Indices
        if (em.HasComponent<MaterialMeshInfo>(entity))
        {
            // Accessing MeshID/MaterialID might crash if invalid, so we skip detailed index logging for now
            // or wrap in try-catch if possible (but it's a struct property).
            // We'll just log that the component exists.
            Debug.Log("MaterialMeshInfo component is present.");
            
            if (hasRMA)
            {
                var rma = em.GetSharedComponentManaged<RenderMeshArray>(entity);
                if (rma.Meshes != null && rma.Materials != null)
                {
                    Debug.Log($"RenderMeshArray: Meshes={rma.Meshes.Length}, Materials={rma.Materials.Length}");
                }
                else
                {
                     Debug.LogError("RenderMeshArray has null arrays!");
                }
            }
        }

        // Check Tags
        if (em.HasComponent<Disabled>(entity)) Debug.LogError("Entity has Disabled tag!");
        if (em.HasComponent<DisableRendering>(entity)) Debug.LogError("Entity has DisableRendering tag!");
        if (em.HasComponent<Prefab>(entity)) Debug.LogError("Entity has Prefab tag!");
    }

    void LogComponentPresence<T>(EntityManager em, Entity entity, string name) where T : unmanaged, IComponentData
    {
        bool has = em.HasComponent<T>(entity);
        Debug.Log($"{name}: {(has ? "YES" : "NO")}");
    }
}
#endif
