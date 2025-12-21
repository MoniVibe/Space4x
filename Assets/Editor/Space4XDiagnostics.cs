#if SPACE4X_DIAGNOSTICS
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using UnityEngine;
using UnityEditor;
using PureDOTS.Runtime;
// using PureDOTS.LegacyScenario.Village;
// using PureDOTS.LegacyScenario.Rendering;
// using Space4X.Scenario;
// using Space4X.Registry;
// using Space4X.Mining;
using PureDOTS.Runtime.Platform;
// using Unity.Rendering; // Added for MaterialMeshInfo

public class Space4XDiagnostics
{
    [MenuItem("Tools/Diagnose Space4X Visibility")]
    public static void RunDiagnosis()
    {
        Debug.Log("=== Space4X Visibility Diagnosis ===");

        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null)
        {
            Debug.LogError("Default World not found!");
            return;
        }
        var em = world.EntityManager;

        // 1. Check Camera
        var cam = Camera.main;
        if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
        if (cam != null)
        {
            Debug.Log($"Camera: {cam.name}, Pos: {cam.transform.position}, CullingMask: {cam.cullingMask:X}");
        }
        else
        {
            Debug.LogError("CRITICAL: No Camera found!");
        }

        // 2. Check Singletons
        CheckComponentDataSingleton<VillageWorldTag>(em, "VillageWorldTag");
        CheckSharedComponentDataSingleton<RenderMeshArraySingleton>(em, "RenderMeshArraySingleton");
        CheckComponentDataSingleton<ScenarioState>(em, "ScenarioState");
        
        // 3. Check RenderMeshArray Content
        CheckRenderMeshArray(em);

        // 4. Check Entities
        CheckEntities<VillagerTag>(em, "Villager");
        CheckEntities<PlatformTag>(em, "Carrier (PlatformTag)");
        CheckEntities<MiningVesselTag>(em, "Miner (MiningVesselTag)");
        // CheckEntities<ResourceNodeTag>(em, "Asteroid (ResourceNodeTag)");
    }

    static void CheckComponentDataSingleton<T>(EntityManager em, string name) where T : unmanaged, IComponentData
    {
        var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
        if (query.IsEmptyIgnoreFilter)
            Debug.LogWarning($"Singleton MISSING: {name}");
        else
            Debug.Log($"Singleton FOUND: {name}");
    }

    static void CheckSharedComponentDataSingleton<T>(EntityManager em, string name) where T : struct, ISharedComponentData
    {
        var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
        if (query.IsEmptyIgnoreFilter)
            Debug.LogWarning($"Singleton MISSING: {name}");
        else
            Debug.Log($"Singleton FOUND: {name}");
    }

    static void CheckRenderMeshArray(EntityManager em)
    {
        var query = em.CreateEntityQuery(ComponentType.ReadOnly<RenderMeshArraySingleton>());
        if (!query.IsEmptyIgnoreFilter)
        {
            var entity = query.GetSingletonEntity();
            var singleton = em.GetSharedComponentManaged<RenderMeshArraySingleton>(entity);
            // var rma = singleton.Value;
            // if (rma != null)
            // {
            //     Debug.Log($"RenderMeshArray: Meshes={rma.Meshes.Length}, Materials={rma.MaterialReferences.Length}");
            //     for (int i = 0; i < rma.MaterialReferences.Length; i++)
            //     {
            //         var mat = rma.MaterialReferences[i];
            //         Debug.Log($"  Mat[{i}]: {(mat != null ? mat.name : "null")} (Shader: {(mat != null ? mat.shader.name : "null")})");
            //     }
            // }
            // else
            // {
            //     Debug.LogError("RenderMeshArray is NULL inside singleton!");
            // }
        }
    }

    static void CheckEntities<T>(EntityManager em, string label) where T : unmanaged, IComponentData
    {
        var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
        var count = query.CalculateEntityCount();
        Debug.Log($"Entities with {label}: {count}");

        if (count > 0)
        {
            using var entities = query.ToEntityArray(Allocator.Temp);
            var e = entities[0];
            Debug.Log($"  Sample {label}: {e}");

            bool hasLT = em.HasComponent<LocalTransform>(e);
            bool hasL2W = em.HasComponent<LocalToWorld>(e);
            // bool hasMMI = em.HasComponent<MaterialMeshInfo>(e);

            Debug.Log($"    Has LocalTransform: {hasLT}");
            Debug.Log($"    Has LocalToWorld: {hasL2W}");
            // Debug.Log($"    Has MaterialMeshInfo: {hasMMI}");

            if (hasLT)
            {
                var lt = em.GetComponentData<LocalTransform>(e);
                Debug.Log($"    Pos: {lt.Position}, Scale: {lt.Scale}");
            }
            if (hasL2W)
            {
                var l2w = em.GetComponentData<LocalToWorld>(e);
                Debug.Log($"    WorldPos: {l2w.Position}");
            }
            // if (hasMMI)
            // {
            //     var mmi = em.GetComponentData<MaterialMeshInfo>(e);
            //     Debug.Log($"    MMI: MeshID={mmi.Mesh}, MatID={mmi.Material}");
            // }
        }
    }
}
#endif
