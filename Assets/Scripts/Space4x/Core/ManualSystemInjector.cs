using PureDOTS.Rendering;
using Unity.Entities;
using UnityEngine;

public class ManualSystemInjector : MonoBehaviour
{
    void Start()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;

        Debug.Log("[ManualSystemInjector] Injecting Space4X systems...");

        // Use reflection to bypass generic constraints and extension method issues
        // This is a last resort to force injection for debugging
        
        AddSystemViaReflection(world, typeof(ApplyRenderVariantSystem));
        AddSystemViaReflection(world, typeof(Space4X_TestRenderKeySpawnerSystem));
        AddSystemViaReflection(world, typeof(DebugVerifyVisualsSystem));
    }

    void AddSystemViaReflection(World world, System.Type systemType)
    {
        // Check if system exists
        var getExistingMethod = typeof(World).GetMethod("GetExistingSystem", new System.Type[] { typeof(System.Type) });
        var handle = (SystemHandle)getExistingMethod.Invoke(world, new object[] { systemType });
        
        if (handle == SystemHandle.Null)
        {
            // Add system
            // For unmanaged systems, we need to use World.AddSystem<T>() via reflection
            var addSystemMethod = typeof(World).GetMethod("AddSystem", new System.Type[] { });
            var genericMethod = addSystemMethod.MakeGenericMethod(systemType);
            genericMethod.Invoke(world, null);
            
            Debug.Log($"[ManualSystemInjector] Added system: {systemType.Name}");
        }
        else
        {
            Debug.Log($"[ManualSystemInjector] System already exists: {systemType.Name}");
        }
    }
}
