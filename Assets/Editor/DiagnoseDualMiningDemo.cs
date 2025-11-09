using UnityEngine;
using UnityEditor;
using Space4X.Registry;
using PureDOTS.Authoring;
using PureDOTS.Runtime.Spatial;
using System;

namespace Space4X.Editor
{
    /// <summary>
    /// Diagnoses issues with the dual mining demo setup.
    /// Checks for missing components, configs, and setup issues.
    /// </summary>
    public static class DiagnoseDualMiningDemo
    {
        [MenuItem("Tools/Space4X/Diagnose Dual Mining Demo")]
        public static void Diagnose()
        {
            try
            {
                Debug.Log("=== Dual Mining Demo Diagnosis ===\n");

                // Check scene root objects
                CheckSceneRoot();
                
                // Check Space4X setup
                CheckSpace4XSetup();
                
                // Check Godgame setup
                CheckGodgameSetup();
                
                // Check camera and lighting
                CheckCameraAndLighting();
                
                // Check for terrain/ground
                CheckTerrain();
                
                // Check config assets
                CheckConfigAssets();
                
                Debug.Log("\n=== Diagnosis Complete ===");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Diagnostic script error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static void CheckSceneRoot()
        {
            Debug.Log("--- Scene Root Check ---");
            var configObj = GameObject.Find("PureDotsConfig");
            if (configObj == null)
            {
                Debug.LogError("✗ PureDotsConfig GameObject not found!");
            }
            else
            {
                Debug.Log("✓ PureDotsConfig GameObject found");
                var configAuthoring = configObj.GetComponent<PureDotsConfigAuthoring>();
                if (configAuthoring == null)
                {
                    Debug.LogError("✗ PureDotsConfigAuthoring component missing!");
                }
                else
                {
                    Debug.Log("✓ PureDotsConfigAuthoring component found");
                    // PureDotsConfigAuthoring has a public 'config' field, not 'runtimeConfig'
                    try
                    {
                        if (configAuthoring.config != null)
                        {
                            Debug.Log($"✓ Runtime config assigned: {configAuthoring.config.name}");
                        }
                        else
                        {
                            Debug.LogError("✗ Runtime config not assigned! (config field is null)");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"⚠ Error checking runtime config: {ex.Message}");
                    }
                }
            }
            
            var spatialObj = GameObject.Find("SpatialPartition");
            if (spatialObj == null)
            {
                Debug.LogWarning("✗ SpatialPartition GameObject not found!");
            }
            else
            {
                Debug.Log("✓ SpatialPartition GameObject found");
                var spatialAuthoring = spatialObj.GetComponent<SpatialPartitionAuthoring>();
                if (spatialAuthoring == null)
                {
                    Debug.LogWarning("✗ SpatialPartitionAuthoring component missing!");
                }
                else
                {
                    Debug.Log("✓ SpatialPartitionAuthoring component found");
                }
            }
        }

        private static void CheckSpace4XSetup()
        {
            Debug.Log("\n--- Space4X Setup Check ---");
            var space4xRoot = GameObject.Find("Space4X_MiningDemo");
            if (space4xRoot == null)
            {
                Debug.LogError("✗ Space4X_MiningDemo GameObject not found!");
                return;
            }
            
            Debug.Log($"✓ Space4X_MiningDemo GameObject found at position: {space4xRoot.transform.position}");
            
            var authoring = space4xRoot.GetComponent<Space4XMiningDemoAuthoring>();
            if (authoring == null)
            {
                Debug.LogError("✗ Space4XMiningDemoAuthoring component missing!");
                return;
            }
            
            Debug.Log("✓ Space4XMiningDemoAuthoring component found");
            
            // Check required components
            var configAuthoring = space4xRoot.GetComponent<PureDotsConfigAuthoring>();
            if (configAuthoring == null)
            {
                Debug.LogWarning("⚠ Space4X_MiningDemo should have PureDotsConfigAuthoring (it uses RequireComponent)");
            }
            else
            {
                Debug.Log("✓ PureDotsConfigAuthoring found on Space4X_MiningDemo");
            }
            
            var spatialAuthoring = space4xRoot.GetComponent<SpatialPartitionAuthoring>();
            if (spatialAuthoring == null)
            {
                Debug.LogWarning("⚠ Space4X_MiningDemo should have SpatialPartitionAuthoring (it uses RequireComponent)");
            }
            else
            {
                Debug.Log("✓ SpatialPartitionAuthoring found on Space4X_MiningDemo");
            }
            
            // Check serialized data - wrap in try-catch to avoid freezing
            // Use direct property access instead of SerializedObject to avoid potential freeze
            try
            {
                Debug.Log($"✓ Direct property access (avoiding SerializedObject):");
                Debug.Log($"  Carriers: {authoring.Carriers?.Length ?? 0} entries");
                Debug.Log($"  MiningVessels: {authoring.MiningVessels?.Length ?? 0} entries");
                Debug.Log($"  Asteroids: {authoring.Asteroids?.Length ?? 0} entries");
                
                if (authoring.Carriers != null && authoring.Carriers.Length == 0)
                {
                    Debug.LogWarning("⚠ Carriers array is empty! Entities won't spawn.");
                }
                if (authoring.MiningVessels != null && authoring.MiningVessels.Length == 0)
                {
                    Debug.LogWarning("⚠ MiningVessels array is empty! Entities won't spawn.");
                }
                if (authoring.Asteroids != null && authoring.Asteroids.Length == 0)
                {
                    Debug.LogWarning("⚠ Asteroids array is empty! Entities won't spawn.");
                }
                
                // Check for unusually large arrays that might cause freeze
                if (authoring.Carriers != null && authoring.Carriers.Length > 100)
                {
                    Debug.LogWarning($"⚠ WARNING: Carriers array has {authoring.Carriers.Length} entries - this might cause Unity to freeze when inspecting!");
                }
                if (authoring.MiningVessels != null && authoring.MiningVessels.Length > 100)
                {
                    Debug.LogWarning($"⚠ WARNING: MiningVessels array has {authoring.MiningVessels.Length} entries - this might cause Unity to freeze when inspecting!");
                }
                if (authoring.Asteroids != null && authoring.Asteroids.Length > 100)
                {
                    Debug.LogWarning($"⚠ WARNING: Asteroids array has {authoring.Asteroids.Length} entries - this might cause Unity to freeze when inspecting!");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"✗ Error reading component properties: {ex.Message}");
                Debug.LogError($"  This might indicate corrupted serialized data!");
            }
            
            Debug.Log("\nNOTE: Entities are created at runtime by the Baker when entering Play mode.");
            Debug.Log("Make sure you enter Play mode to see the entities spawn.");
        }

        private static void CheckGodgameSetup()
        {
            Debug.Log("\n--- Godgame Setup Check ---");
            var godgameRoot = GameObject.Find("Godgame_MiningDemo");
            if (godgameRoot == null)
            {
                Debug.LogWarning("⚠ Godgame_MiningDemo GameObject not found (expected in Space4X project)");
                return;
            }
            
            Debug.Log($"✓ Godgame_MiningDemo GameObject found at position: {godgameRoot.transform.position}");
            Debug.LogWarning("⚠ Godgame authoring components not available in Space4X project - this is expected.");
        }

        private static void CheckCameraAndLighting()
        {
            Debug.Log("\n--- Camera & Lighting Check ---");
            var camera = Camera.main;
            if (camera == null)
            {
                Debug.LogError("✗ Main Camera not found!");
            }
            else
            {
                Debug.Log($"✓ Main Camera found at position: {camera.transform.position}");
                var inputAuthoring = camera.GetComponent<Space4XCameraInputAuthoring>();
                if (inputAuthoring == null)
                {
                    Debug.LogWarning("⚠ Space4XCameraInputAuthoring missing - camera input won't work");
                }
                else
                {
                    Debug.Log("✓ Space4XCameraInputAuthoring found");
                }
            }
            
            var light = UnityEngine.Object.FindFirstObjectByType<Light>();
            if (light == null)
            {
                Debug.LogWarning("⚠ No Light found in scene!");
            }
            else
            {
                Debug.Log($"✓ Light found: {light.type} at rotation {light.transform.rotation.eulerAngles}");
            }
        }

        private static void CheckTerrain()
        {
            Debug.Log("\n--- Terrain/Ground Check ---");
            var terrain = UnityEngine.Object.FindFirstObjectByType<Terrain>();
            var plane = GameObject.Find("Plane");
            var ground = GameObject.Find("Ground");
            
            if (terrain == null && plane == null && ground == null)
            {
                Debug.LogWarning("⚠ No terrain, plane, or ground GameObject found!");
                Debug.LogWarning("   Consider adding a Plane GameObject as ground reference.");
            }
            else
            {
                if (terrain != null) Debug.Log("✓ Terrain found");
                if (plane != null) Debug.Log("✓ Plane GameObject found");
                if (ground != null) Debug.Log("✓ Ground GameObject found");
            }
        }

        private static void CheckConfigAssets()
        {
            Debug.Log("\n--- Config Assets Check ---");
            var runtimeConfig = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Space4X/Config/PureDotsRuntimeConfig.asset");
            if (runtimeConfig == null)
            {
                Debug.LogError("✗ PureDotsRuntimeConfig.asset not found!");
            }
            else
            {
                Debug.Log("✓ PureDotsRuntimeConfig.asset found");
            }
            
            var resourceTypes = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Space4X/Config/PureDotsResourceTypes.asset");
            if (resourceTypes == null)
            {
                Debug.LogWarning("⚠ PureDotsResourceTypes.asset not found - resource registry may be empty");
            }
            else
            {
                Debug.Log("✓ PureDotsResourceTypes.asset found");
            }
            
            var recipeCatalog = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Space4X/Config/ResourceRecipeCatalog.asset");
            if (recipeCatalog == null)
            {
                Debug.LogWarning("⚠ ResourceRecipeCatalog.asset not found - recipes may not work");
            }
            else
            {
                Debug.Log("✓ ResourceRecipeCatalog.asset found");
            }
        }
    }
}

