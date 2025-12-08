using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Text;
using System.Linq;
using PureDOTS.Demo.Rendering;
using PureDOTS.Runtime;
using PureDOTS.Runtime.Platform;
using Space4X.Mining;
using Space4X.Demo;

public class DiagnoseRuntime
{
    public static void Execute()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== DIAGNOSTIC REPORT ===");

        // 1. Scene & World Sanity
        sb.AppendLine("\n-- Scene & World Sanity --");
        sb.AppendLine($"Active Scene: {SceneManager.GetActiveScene().name}");
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            sb.AppendLine($"Loaded Scene {i}: {scene.name} (isLoaded: {scene.isLoaded})");
        }

        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null)
        {
            sb.AppendLine("Default World is NULL!");
            Debug.Log(sb.ToString());
            return;
        }
        sb.AppendLine($"World: {world.Name}");

        var em = world.EntityManager;

        // Check Singletons
        CheckSingleton<PureDOTS.Runtime.TimeState>(em, "TimeState", sb);
        CheckSingleton<PureDOTS.Runtime.RewindState>(em, "RewindState", sb);
        CheckSingleton<RenderMeshArraySingleton>(em, "RenderMeshArraySingleton", sb);

        // 2. Entity Sample Dump
        sb.AppendLine("\n-- Entity Sample Dump --");
        
        // Helper to dump entity
        void DumpEntity(string label, EntityQuery query)
        {
            if (query.IsEmptyIgnoreFilter)
            {
                sb.AppendLine($"{label}: NONE FOUND");
                return;
            }
            var entity = query.GetSingletonEntity(); // Just get one
            sb.AppendLine($"{label} (Entity {entity.Index}):");
            var types = em.GetComponentTypes(entity);
            foreach (var t in types)
            {
                sb.AppendLine($"  - {t.GetManagedType().Name}");
            }
            
            // Check specific components
            bool hasLocalTransform = em.HasComponent<LocalTransform>(entity);
            bool hasL2W = em.HasComponent<LocalToWorld>(entity);
            bool hasBounds = em.HasComponent<WorldRenderBounds>(entity);
            bool hasMMI = em.HasComponent<MaterialMeshInfo>(entity);
            bool hasURPMM = em.HasComponent<URPMaterialMesh>(entity); // Assuming this exists or similar
            
            sb.AppendLine($"  > LocalTransform: {hasLocalTransform}");
            sb.AppendLine($"  > LocalToWorld: {hasL2W}");
            sb.AppendLine($"  > WorldRenderBounds: {hasBounds}");
            sb.AppendLine($"  > MaterialMeshInfo: {hasMMI}");
            
            if (hasLocalTransform)
            {
                var lt = em.GetComponentData<LocalTransform>(entity);
                sb.AppendLine($"  > Position: {lt.Position}");
            }
        }

        // Define queries for samples
        // Villager (assuming VillagerJob or similar tag)
        // Note: I don't have the exact tag for Villager, guessing based on context or generic query
        // The prompt mentions "VillagerJob" or "VillagerVisualTag".
        // I'll try to find entities with specific components.
        
        // Carrier -> PlatformTag
        DumpEntity("Carrier", em.CreateEntityQuery(ComponentType.ReadOnly<PlatformTag>()));
        
        // Miner -> MiningVesselTag
        DumpEntity("Miner", em.CreateEntityQuery(ComponentType.ReadOnly<MiningVesselTag>()));
        
        // Asteroid -> ResourceNodeTag
        DumpEntity("Asteroid", em.CreateEntityQuery(ComponentType.ReadOnly<ResourceNodeTag>()));

        // Village/Home/Work -> I'll look for entities with these names or components if I knew them.
        // Assuming "Village" might have a specific component.
        // I'll try to find generic entities if specific tags fail.

        // 3. Render Setup Systems
        sb.AppendLine("\n-- Render Setup Systems --");
        foreach (var system in world.Systems)
        {
            var type = system.GetType();
            if (type.Name.Contains("Render") || type.Name.Contains("Visual") || type.Name.Contains("Presentation"))
            {
                sb.AppendLine($"System: {type.Name}");
                // Can't easily check Enabled/UpdateInGroup via reflection on SystemBase/ISystem wrapper easily in this context without more boilerplate,
                // but I can check if it's in the world.
            }
        }

        // 4. Camera & Layers
        sb.AppendLine("\n-- Camera & Layers --");
        var camera = Camera.main;
        if (camera != null)
        {
            sb.AppendLine($"Main Camera: {camera.name}");
            sb.AppendLine($"  Position: {camera.transform.position}");
            sb.AppendLine($"  Rotation: {camera.transform.rotation.eulerAngles}");
            sb.AppendLine($"  Near: {camera.nearClipPlane}, Far: {camera.farClipPlane}");
            sb.AppendLine($"  Culling Mask: {Convert.ToString(camera.cullingMask, 2)} ({camera.cullingMask})");
        }
        else
        {
            sb.AppendLine("Main Camera: NULL");
            var allCameras = Camera.allCameras;
            sb.AppendLine($"Total Cameras: {allCameras.Length}");
            foreach(var cam in allCameras)
            {
                 sb.AppendLine($"  Camera: {cam.name}, Enabled: {cam.enabled}");
            }
        }

        // 5. Quick Visibility Check
        sb.AppendLine("\n-- Quick Visibility Check --");
        var mmiQuery = em.CreateEntityQuery(ComponentType.ReadOnly<MaterialMeshInfo>());
        int mmiCount = mmiQuery.CalculateEntityCount();
        sb.AppendLine($"Entities with MaterialMeshInfo: {mmiCount}");

        var noMmiQuery = em.CreateEntityQuery(
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.Exclude<MaterialMeshInfo>()
        );
        int noMmiCount = noMmiQuery.CalculateEntityCount();
        sb.AppendLine($"Entities with LocalTransform but NO MaterialMeshInfo: {noMmiCount}");

        Debug.Log(sb.ToString());
    }

    static void CheckSingleton<T>(EntityManager em, string name, StringBuilder sb) where T : unmanaged, IComponentData
    {
        var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
        if (query.IsEmptyIgnoreFilter)
        {
            sb.AppendLine($"{name}: MISSING");
        }
        else
        {
            sb.AppendLine($"{name}: PRESENT");
        }
    }
}
