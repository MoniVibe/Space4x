using UnityEngine;
using UnityEditor;
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Space4X.Mining;
using Space4X.Registry;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Editor
{
    using Debug = UnityEngine.Debug;

    /// <summary>
    /// Diagnoses why entities may not be visible - checks baking, subscenes, and render systems.
    /// </summary>
    public static class CheckEntityVisibility
    {
        [MenuItem("Tools/Space4X/Check Entity Visibility")]
        public static void CheckVisibility()
        {
            UnityDebug.Log("=== Entity Visibility Check ===\n");

            // Check if we're in Play mode
            if (!Application.isPlaying)
            {
                UnityDebug.LogWarning("⚠ Not in Play Mode! Entities only exist at runtime.");
                UnityDebug.LogWarning("   Enter Play Mode to see entities created by Baker.");
                return;
            }

            // Check for DOTS world
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                UnityDebug.LogError("✗ No DOTS World found! Entities won't exist.");
                return;
            }
            UnityDebug.Log($"✓ DOTS World found: {world.Name}");

            var entityManager = world.EntityManager;
            
            // Check for entities with key components
            var carrierQuery = entityManager.CreateEntityQuery(typeof(Carrier), typeof(Unity.Transforms.LocalTransform));
            var vesselQuery = entityManager.CreateEntityQuery(typeof(MiningVessel), typeof(Unity.Transforms.LocalTransform));
            var asteroidQuery = entityManager.CreateEntityQuery(typeof(Asteroid), typeof(Unity.Transforms.LocalTransform));

            var carrierCount = carrierQuery.CalculateEntityCount();
            var vesselCount = vesselQuery.CalculateEntityCount();
            var asteroidCount = asteroidQuery.CalculateEntityCount();
            var configAssets = Resources.FindObjectsOfTypeAll<Space4XMiningVisualConfig>();
            var hasConfig = configAssets != null && configAssets.Length > 0;

            UnityDebug.Log($"\n--- Entity Counts ---");
            UnityDebug.Log($"Carriers: {carrierCount}");
            UnityDebug.Log($"Mining Vessels: {vesselCount}");
            UnityDebug.Log($"Asteroids: {asteroidCount}");
            UnityDebug.Log($"Visual Config: {(hasConfig ? "Found" : "MISSING!")}");

            if (carrierCount == 0 && vesselCount == 0 && asteroidCount == 0)
            {
                UnityDebug.LogError("\n✗ NO ENTITIES FOUND!");
                UnityDebug.LogError("Possible causes:");
                UnityDebug.LogError("1. Scene doesn't have a SubScene with Space4XMiningDemoAuthoring");
                UnityDebug.LogError("2. SubScene is not loaded (check SubScene window/Inspector)");
                UnityDebug.LogError("3. Baker didn't run (check for errors during scene conversion)");
                UnityDebug.LogError("4. Entities were created in a different World");
                
                // Check for SubScenes
                var subscenes = Object.FindObjectsByType<Unity.Scenes.SubScene>(FindObjectsSortMode.None);
                UnityDebug.Log($"\n--- SubScenes Found ---");
                UnityDebug.Log($"SubScene count: {subscenes.Length}");
                foreach (var subscene in subscenes)
                {
                    UnityDebug.Log($"  - {subscene.name}: {(subscene.IsLoaded ? "LOADED" : "NOT LOADED")}");
                }
                
                if (subscenes.Length == 0)
                {
                    UnityDebug.LogError("\n✗ NO SubScenes found! Entities created by Baker won't exist without a SubScene.");
                    UnityDebug.LogError("   SOLUTION: The scene needs a SubScene GameObject with SubScene component.");
                    UnityDebug.LogError("   The Space4XMiningDemoAuthoring GameObject should be inside the SubScene.");
                }
            }
            else
            {
                UnityDebug.Log("\n✓ Entities found! Checking positions...");
                
                // Log positions of first few entities
                if (carrierCount > 0)
                {
                    using var carriers = carrierQuery.ToEntityArray(Allocator.Temp);
                    UnityDebug.Log($"\n--- Carrier Positions ---");
                    for (int i = 0; i < System.Math.Min(3, carriers.Length); i++)
                    {
                        var transform = entityManager.GetComponentData<Unity.Transforms.LocalTransform>(carriers[i]);
                        UnityDebug.Log($"Carrier {i}: Position={transform.Position}, Scale={transform.Scale}");
                    }
                }

                if (!hasConfig)
                {
                    UnityDebug.LogError("\n✗ Space4XMiningVisualConfig MISSING!");
                    UnityDebug.LogError("   Entities exist but won't render without the visual config.");
                    UnityDebug.LogError("   Check Space4XMiningDemoAuthoring Baker added the config.");
                }
            }

            // Check render system
            UnityDebug.Log("\n--- Render System Check ---");
            // Space4XMiningDebugRenderSystem is an ISystem (struct), not ComponentSystemBase
            // Check if it exists by checking for the visual config singleton it requires
            if (hasConfig)
            {
                UnityDebug.Log($"✓ Space4XMiningDebugRenderSystem should be running (has config singleton)");
            }
            else
            {
                UnityDebug.LogWarning("⚠ Space4XMiningDebugRenderSystem cannot run without Space4XMiningVisualConfig singleton");
            }

            // Cleanup
            carrierQuery.Dispose();
            vesselQuery.Dispose();
            asteroidQuery.Dispose();

            UnityDebug.Log("\n=== Check Complete ===");
        }
    }
}

