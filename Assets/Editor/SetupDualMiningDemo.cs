using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Unity.Scenes;
using Space4X.Registry;
using PureDOTS.Authoring;
using PureDOTS.Runtime.Spatial;

namespace Space4X.Editor
{
    /// <summary>
    /// Sets up a dual mining demo scene showing both Space4X and Godgame mining behaviors side by side.
    /// Space4X side: Vessels mine asteroids and return to carriers
    /// Godgame side: Villagers gather resources and deposit in storehouses
    /// </summary>
    public static class SetupDualMiningDemo
    {
        [MenuItem("Tools/Space4X/Setup Dual Mining Demo Scene")]
        public static void SetupScene()
        {
            // Ensure directory exists first
            EnsureDirectoryExists("Assets/Scenes/Demo");
            
            var scenePath = "Assets/Scenes/Demo/DualMiningDemo.unity";
            
            // Check if there's a nested folder issue (DualMiningDemo.unity as folder)
            // This happens when Unity creates a folder instead of a file (MCP bug or Unity bug)
            var directoryPath = System.IO.Path.Combine(Application.dataPath.Replace("Assets", ""), "Assets/Scenes/Demo/DualMiningDemo.unity");
            if (System.IO.Directory.Exists(directoryPath))
            {
                Debug.LogError("Found DualMiningDemo.unity as a DIRECTORY - this prevents saving!");
                Debug.LogError($"Path: {directoryPath}");
                Debug.LogError("\nAUTO-DELETING nested directory to fix the issue...");
                
                try
                {
                    // Try to delete the nested directory
                    System.IO.Directory.Delete(directoryPath, true);
                    AssetDatabase.Refresh();
                    
                    // Also clean up the meta file
                    var metaPath = directoryPath + ".meta";
                    if (System.IO.File.Exists(metaPath))
                    {
                        System.IO.File.Delete(metaPath);
                    }
                    
                    AssetDatabase.Refresh();
                    Debug.Log("✓ Successfully deleted nested directory. Continuing with setup...");
                }
                catch (UnauthorizedAccessException ex)
                {
                    Debug.LogError($"✗ Access denied when deleting directory: {ex.Message}");
                    Debug.LogError("\nSOLUTION:");
                    Debug.LogError("1. Close ALL Unity instances");
                    Debug.LogError("2. Close File Explorer if it has that folder open");
                    Debug.LogError($"3. Manually delete this folder: {directoryPath}");
                    Debug.LogError("4. Then run this menu item again");
                    Debug.LogError("\nOr use this PowerShell command (run as Administrator if needed):");
                    Debug.LogError($"  Remove-Item -Path \"{directoryPath}\" -Recurse -Force");
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"✗ Failed to delete directory: {ex.Message}");
                    Debug.LogError($"Please manually delete: {directoryPath}");
                    Debug.LogError("Then run this menu item again.");
                    return;
                }
            }
            
            // Check if scene already exists and is loaded
            var existingScene = EditorSceneManager.GetSceneByPath(scenePath);
            Scene sceneToSave;
            
            // Check if the target file path exists and is locked (do this after checking for scene)
            var sceneFilePath = System.IO.Path.Combine(Application.dataPath.Replace("Assets", ""), scenePath);
            if (System.IO.File.Exists(sceneFilePath))
            {
                // Check if file is writeable
                try
                {
                    var fileInfo = new System.IO.FileInfo(sceneFilePath);
                    if (fileInfo.IsReadOnly)
                    {
                        Debug.LogWarning($"Scene file exists and is READ-ONLY: {scenePath}");
                        Debug.LogWarning("Attempting to make it writable...");
                        fileInfo.IsReadOnly = false;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Could not check file permissions: {ex.Message}");
                }
            }
            
            if (existingScene.IsValid() && existingScene.isLoaded)
            {
                // Use existing scene
                Debug.Log("Using existing scene...");
                sceneToSave = existingScene;
                EditorSceneManager.SetActiveScene(sceneToSave);
                
                // Still setup the scene hierarchy if needed (won't duplicate if already exists)
                SetupSceneRoot();
                SetupSpace4XSide();
                SetupGodgameSide();
                SetupSharedRendering();
            }
            else
            {
                // Create new scene
                Debug.Log("Creating new scene...");
                sceneToSave = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                
                // Setup the scene root hierarchy
                SetupSceneRoot();
                
                // Setup Space4X side (left side)
                SetupSpace4XSide();
                
                // Setup Godgame side (right side) 
                SetupGodgameSide();
                
                // Setup shared camera and lighting
                SetupSharedRendering();
            }
            
            // Try to save the scene
            bool saveResult = false;
            try
            {
                if (sceneToSave.path == scenePath || (existingScene.IsValid() && !string.IsNullOrEmpty(sceneToSave.path)))
                {
                    // Scene already has the correct path, save without path parameter
                    saveResult = EditorSceneManager.SaveScene(sceneToSave);
                }
                else
                {
                    // New scene needs a path - try saving with confirm dialog disabled
                    saveResult = EditorSceneManager.SaveScene(sceneToSave, scenePath, false);
                }
            }
            catch (System.UnauthorizedAccessException ex)
            {
                Debug.LogError($"Access denied when trying to save scene: {ex.Message}");
                Debug.LogWarning("Possible causes:");
                Debug.LogWarning("1. The scene file is open in another Unity instance");
                Debug.LogWarning("2. The file has read-only permissions");
                Debug.LogWarning("3. Another process is locking the file");
                Debug.LogWarning("\nSolution: Close all Unity instances, check file permissions, and try again.");
                Debug.LogWarning("Or manually save the scene using Ctrl+S after setup completes.");
                return;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error saving scene: {ex.Message}");
                Debug.LogWarning("The scene setup completed, but saving failed. You can manually save using Ctrl+S.");
                return;
            }
            
            if (saveResult)
            {
                Debug.Log($"=== Dual Mining Demo Scene Created ===");
                Debug.Log($"Scene saved to: {sceneToSave.path}");
                Debug.Log($"Left side (X < 0): Space4X vessels mining asteroids → carriers");
                Debug.Log($"Right side (X > 0): Godgame villagers gathering → storehouses");
                Debug.Log($"Make sure both projects have their config assets set up!");
            }
            else
            {
                Debug.LogWarning($"Scene setup completed but could not save to: {scenePath}");
                Debug.LogWarning("The scene is dirty and ready to save. Press Ctrl+S to save manually.");
                Debug.LogWarning("This may be due to:");
                Debug.LogWarning("- File permissions issue");
                Debug.LogWarning("- Scene already open in another Unity instance");
                Debug.LogWarning("- File system lock");
                Debug.LogWarning($"Current scene path: {sceneToSave.path}, Dirty: {sceneToSave.isDirty}");
            }
        }

        private static void SetupSceneRoot()
        {
            // Find or create main scene objects
            // NOTE: These can be separate from Space4X_MiningDemo, but Space4X_MiningDemo also needs them
            
            // Check for duplicates first
            var allConfigs = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            var configObjects = new System.Collections.Generic.List<GameObject>();
            foreach (var obj in allConfigs)
            {
                if (obj.name == "PureDotsConfig")
                {
                    configObjects.Add(obj);
                }
            }
            
            GameObject bootstrapObj;
            if (configObjects.Count > 0)
            {
                bootstrapObj = configObjects[0];
                Debug.Log($"Found existing PureDotsConfig, reusing it. ({configObjects.Count - 1} duplicate(s) should be cleaned up)");
            }
            else
            {
                bootstrapObj = new GameObject("PureDotsConfig");
            }
            
            var configAuthoring = bootstrapObj.GetComponent<PureDotsConfigAuthoring>();
            if (configAuthoring == null)
            {
                configAuthoring = bootstrapObj.AddComponent<PureDotsConfigAuthoring>();
            }
            
            // Assign runtime config (Space4X config path)
            var runtimeConfig = AssetDatabase.LoadAssetAtPath<PureDOTS.Authoring.PureDotsRuntimeConfig>("Assets/Space4X/Config/PureDotsRuntimeConfig.asset");
            if (runtimeConfig != null)
            {
                // PureDotsConfigAuthoring has a public 'config' field, not 'runtimeConfig'
                configAuthoring.config = runtimeConfig;
                EditorUtility.SetDirty(configAuthoring);
                Debug.Log($"Assigned runtime config to PureDotsConfig");
            }
            else
            {
                Debug.LogWarning("Could not find PureDotsRuntimeConfig.asset at Assets/Space4X/Config/PureDotsRuntimeConfig.asset");
            }

            // Create spatial partition - check for duplicates
            var spatialObjects = new System.Collections.Generic.List<GameObject>();
            foreach (var obj in allConfigs)
            {
                if (obj.name == "SpatialPartition")
                {
                    spatialObjects.Add(obj);
                }
            }
            
            GameObject spatialObj;
            if (spatialObjects.Count > 0)
            {
                spatialObj = spatialObjects[0];
                Debug.Log($"Found existing SpatialPartition, reusing it. ({spatialObjects.Count - 1} duplicate(s) should be cleaned up)");
            }
            else
            {
                spatialObj = new GameObject("SpatialPartition");
            }
            var spatialAuthoring = spatialObj.GetComponent<SpatialPartitionAuthoring>();
            if (spatialAuthoring == null)
            {
                spatialAuthoring = spatialObj.AddComponent<SpatialPartitionAuthoring>();
            }
            
            // Assign spatial partition profile if not assigned
            if (spatialAuthoring.profile == null)
            {
                var profile = AssetDatabase.LoadAssetAtPath<PureDOTS.Authoring.SpatialPartitionProfile>("Assets/Space4X/Config/DefaultSpatialPartitionProfile.asset");
                if (profile != null)
                {
                    spatialAuthoring.profile = profile;
                    EditorUtility.SetDirty(spatialAuthoring);
                    Debug.Log("Assigned DefaultSpatialPartitionProfile to SpatialPartition");
                }
                else
                {
                    Debug.LogWarning("Could not find DefaultSpatialPartitionProfile.asset at Assets/Space4X/Config/DefaultSpatialPartitionProfile.asset");
                }
            }
        }

        private static void SetupSpace4XSide()
        {
            Debug.Log("Setting up Space4X mining demo (left side, X < 0)...");
            
            // Create SubScene for Space4X entities (required for Baker to run!)
            var subSceneGO = GameObject.Find("Space4X_MiningDemo_SubScene");
            SubScene subScene = null;
            if (subSceneGO == null)
            {
                subSceneGO = new GameObject("Space4X_MiningDemo_SubScene");
                subScene = subSceneGO.AddComponent<SubScene>();
                
                // Create a new scene for the subscene
                var subScenePath = "Assets/Scenes/Demo/Space4X_MiningDemo_SubScene.unity";
                EnsureDirectoryExists("Assets/Scenes/Demo");
                var subSceneAsset = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                EditorSceneManager.SaveScene(subSceneAsset, subScenePath);
                
                // Assign the scene asset to the SubScene component
                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(subScenePath);
                if (sceneAsset != null)
                {
                    var subSceneSerialized = new SerializedObject(subScene);
                    var sceneAssetProp = subSceneSerialized.FindProperty("m_SceneAsset");
                    if (sceneAssetProp != null)
                    {
                        sceneAssetProp.objectReferenceValue = sceneAsset;
                        subSceneSerialized.ApplyModifiedProperties();
                    }
                    // Enable auto-load
                    subScene.AutoLoadScene = true;
                    EditorUtility.SetDirty(subScene);
                }
                
                Debug.Log($"Created SubScene at {subScenePath}");
            }
            else
            {
                subScene = subSceneGO.GetComponent<SubScene>();
            }
            
            // Check for existing Space4X_MiningDemo in main scene first (should be cleaned up)
            var existingInMainScene = GameObject.Find("Space4X_MiningDemo");
            if (existingInMainScene != null && !string.IsNullOrEmpty(existingInMainScene.scene.path))
            {
                Debug.LogWarning($"Found Space4X_MiningDemo in main scene at {existingInMainScene.transform.position}. It should be in the SubScene!");
                Debug.LogWarning("This object will be moved to the SubScene or you should run Cleanup first.");
            }
            
            // Find existing root or create new one INSIDE the SubScene scene
            GameObject space4xRoot = null;
            if (subScene != null && subScene.SceneAsset != null)
            {
                var targetScene = EditorSceneManager.GetSceneByPath(AssetDatabase.GetAssetPath(subScene.SceneAsset));
                if (targetScene.IsValid())
                {
                    // Look for Space4X_MiningDemo in the SubScene
                    var rootObjects = targetScene.GetRootGameObjects();
                    foreach (var root in rootObjects)
                    {
                        if (root.name == "Space4X_MiningDemo")
                        {
                            space4xRoot = root;
                            break;
                        }
                    }
                }
            }
            
            if (space4xRoot == null)
            {
                // Try finding it anywhere (might be in main scene)
                space4xRoot = GameObject.Find("Space4X_MiningDemo");
            }
            
            if (space4xRoot == null)
            {
                space4xRoot = new GameObject("Space4X_MiningDemo");
                space4xRoot.transform.position = new Vector3(-50f, 0f, 0f);
                
                // Move it to the SubScene if we have one
                if (subScene != null && subScene.SceneAsset != null)
                {
                    var targetScene = EditorSceneManager.GetSceneByPath(AssetDatabase.GetAssetPath(subScene.SceneAsset));
                    if (targetScene.IsValid())
                    {
                        EditorSceneManager.MoveGameObjectToScene(space4xRoot, targetScene);
                        Debug.Log("Created and moved Space4X_MiningDemo to SubScene");
                    }
                }
            }
            else
            {
                Debug.Log($"Found existing Space4X_MiningDemo at {space4xRoot.transform.position}");
                space4xRoot.transform.position = new Vector3(-50f, 0f, 0f);
                
                // Ensure it's in the SubScene
                if (subScene != null && subScene.SceneAsset != null)
                {
                    var targetScene = EditorSceneManager.GetSceneByPath(AssetDatabase.GetAssetPath(subScene.SceneAsset));
                    if (targetScene.IsValid() && space4xRoot.scene != targetScene)
                    {
                        EditorSceneManager.MoveGameObjectToScene(space4xRoot, targetScene);
                        Debug.Log("Moved existing Space4X_MiningDemo to SubScene");
                    }
                }
            }

            // IMPORTANT: Space4XMiningDemoAuthoring requires PureDotsConfigAuthoring and SpatialPartitionAuthoring
            // Add them to the same GameObject (RequireComponent ensures this)
            var configAuthoring = space4xRoot.GetComponent<PureDotsConfigAuthoring>();
            if (configAuthoring == null)
            {
                configAuthoring = space4xRoot.AddComponent<PureDotsConfigAuthoring>();
            }
            
            // Assign runtime config if not already assigned
            if (configAuthoring.config == null)
            {
                var runtimeConfig = AssetDatabase.LoadAssetAtPath<PureDOTS.Authoring.PureDotsRuntimeConfig>("Assets/Space4X/Config/PureDotsRuntimeConfig.asset");
                if (runtimeConfig != null)
                {
                    configAuthoring.config = runtimeConfig;
                    EditorUtility.SetDirty(configAuthoring);
                    Debug.Log("Assigned PureDotsRuntimeConfig to Space4X_MiningDemo");
                }
                else
                {
                    Debug.LogWarning("Could not find PureDotsRuntimeConfig.asset - PureDotsConfigAuthoring will not bake correctly");
                }
            }
            
            var spatialAuthoring = space4xRoot.GetComponent<SpatialPartitionAuthoring>();
            if (spatialAuthoring == null)
            {
                spatialAuthoring = space4xRoot.AddComponent<SpatialPartitionAuthoring>();
            }
            
            // Assign spatial partition profile if not assigned
            if (spatialAuthoring.profile == null)
            {
                var profile = AssetDatabase.LoadAssetAtPath<PureDOTS.Authoring.SpatialPartitionProfile>("Assets/Space4X/Config/DefaultSpatialPartitionProfile.asset");
                if (profile != null)
                {
                    spatialAuthoring.profile = profile;
                    EditorUtility.SetDirty(spatialAuthoring);
                    Debug.Log("Assigned DefaultSpatialPartitionProfile to Space4X_MiningDemo SpatialPartition");
                }
                else
                {
                    Debug.LogWarning("Could not find DefaultSpatialPartitionProfile.asset - SpatialPartitionAuthoring will not bake correctly");
                }
            }

            // Add Space4XMiningDemoAuthoring component (or get existing)
            // This will only work if the required components are present
            var authoring = space4xRoot.GetComponent<Space4XMiningDemoAuthoring>();
            if (authoring == null)
            {
                authoring = space4xRoot.AddComponent<Space4XMiningDemoAuthoring>();
            }
            
            if (authoring == null)
            {
                Debug.LogError("Failed to create Space4XMiningDemoAuthoring component!");
                Debug.LogError("Make sure PureDotsConfigAuthoring and SpatialPartitionAuthoring exist on the same GameObject!");
                return;
            }
            
            Debug.Log("Space4X_MiningDemo GameObject setup complete with all required components");
            
            // Configure via SerializedObject to set up carriers, vessels, and asteroids
            var serialized = new SerializedObject(authoring);
            
            if (serialized == null)
            {
                Debug.LogError("Failed to create SerializedObject for Space4XMiningDemoAuthoring!");
                return;
            }
            
            // Set up a carrier at origin (relative to space4xRoot)
            var carriersProp = serialized.FindProperty("carriers");
            if (carriersProp == null)
            {
                Debug.LogError("Could not find 'carriers' property on Space4XMiningDemoAuthoring! Check serialization.");
                serialized.ApplyModifiedProperties();
                return;
            }
            
            carriersProp.arraySize = 1;
            var carrier = carriersProp.GetArrayElementAtIndex(0);
            if (carrier != null)
            {
                var carrierIdProp = carrier.FindPropertyRelative("carrierId");
                var speedProp = carrier.FindPropertyRelative("speed");
                var patrolCenterProp = carrier.FindPropertyRelative("patrolCenter");
                var patrolRadiusProp = carrier.FindPropertyRelative("patrolRadius");
                var waitTimeProp = carrier.FindPropertyRelative("waitTime");
                var positionProp = carrier.FindPropertyRelative("position");
                
                if (carrierIdProp != null) carrierIdProp.stringValue = "CARRIER-1";
                if (speedProp != null) speedProp.floatValue = 5f;
                if (patrolCenterProp != null) patrolCenterProp.vector3Value = Vector3.zero;
                if (patrolRadiusProp != null) patrolRadiusProp.floatValue = 50f;
                if (waitTimeProp != null) waitTimeProp.floatValue = 2f;
                if (positionProp != null) positionProp.vector3Value = Vector3.zero;
            }
            else
            {
                Debug.LogError("Failed to get carrier array element at index 0!");
            }

            // Set up mining vessels
            var vesselsProp = serialized.FindProperty("miningVessels");
            if (vesselsProp == null)
            {
                Debug.LogWarning("Could not find 'miningVessels' property, skipping vessel setup.");
            }
            else
            {
                vesselsProp.arraySize = 3;
                for (int i = 0; i < 3; i++)
                {
                    var vessel = vesselsProp.GetArrayElementAtIndex(i);
                    if (vessel != null)
                    {
                        var vesselIdProp = vessel.FindPropertyRelative("vesselId");
                        var speedProp = vessel.FindPropertyRelative("speed");
                        var efficiencyProp = vessel.FindPropertyRelative("miningEfficiency");
                        var capacityProp = vessel.FindPropertyRelative("cargoCapacity");
                        var carrierIdProp = vessel.FindPropertyRelative("carrierId");
                        var positionProp = vessel.FindPropertyRelative("position");
                        
                        if (vesselIdProp != null) vesselIdProp.stringValue = $"MINER-{i + 1}";
                        if (speedProp != null) speedProp.floatValue = 10f;
                        if (efficiencyProp != null) efficiencyProp.floatValue = 0.8f;
                        if (capacityProp != null) capacityProp.floatValue = 100f;
                        if (carrierIdProp != null) carrierIdProp.stringValue = "CARRIER-1";
                        
                        if (positionProp != null)
                        {
                            float angle = i * (360f / 3f) * Mathf.Deg2Rad;
                            positionProp.vector3Value = new Vector3(
                                Mathf.Cos(angle) * 10f,
                                0f,
                                Mathf.Sin(angle) * 10f
                            );
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to get vessel array element at index {i}!");
                    }
                }
            }

            // Set up asteroids
            var asteroidsProp = serialized.FindProperty("asteroids");
            if (asteroidsProp == null)
            {
                Debug.LogWarning("Could not find 'asteroids' property, skipping asteroid setup.");
            }
            else
            {
                asteroidsProp.arraySize = 4;
                for (int i = 0; i < 4; i++)
                {
                    var asteroid = asteroidsProp.GetArrayElementAtIndex(i);
                    if (asteroid != null)
                    {
                        var asteroidIdProp = asteroid.FindPropertyRelative("asteroidId");
                        var resourceTypeProp = asteroid.FindPropertyRelative("resourceType");
                        var amountProp = asteroid.FindPropertyRelative("resourceAmount");
                        var maxAmountProp = asteroid.FindPropertyRelative("maxResourceAmount");
                        var rateProp = asteroid.FindPropertyRelative("miningRate");
                        var positionProp = asteroid.FindPropertyRelative("position");
                        
                        if (asteroidIdProp != null) asteroidIdProp.stringValue = $"ASTEROID-{i + 1}";
                        if (resourceTypeProp != null) resourceTypeProp.enumValueIndex = 0; // Minerals
                        if (amountProp != null) amountProp.floatValue = 500f;
                        if (maxAmountProp != null) maxAmountProp.floatValue = 500f;
                        if (rateProp != null) rateProp.floatValue = 10f;
                        
                        if (positionProp != null)
                        {
                            float angle = i * (360f / 4f) * Mathf.Deg2Rad;
                            positionProp.vector3Value = new Vector3(
                                Mathf.Cos(angle) * 30f,
                                0f,
                                Mathf.Sin(angle) * 30f
                            );
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to get asteroid array element at index {i}!");
                    }
                }
            }

            // Visual settings
            var visualsProp = serialized.FindProperty("visuals");
            if (visualsProp != null)
            {
                var carrierPrimitiveProp = visualsProp.FindPropertyRelative("carrierPrimitive");
                var carrierScaleProp = visualsProp.FindPropertyRelative("carrierScale");
                var carrierColorProp = visualsProp.FindPropertyRelative("carrierColor");
                
                var vesselPrimitiveProp = visualsProp.FindPropertyRelative("miningVesselPrimitive");
                var vesselScaleProp = visualsProp.FindPropertyRelative("miningVesselScale");
                var vesselColorProp = visualsProp.FindPropertyRelative("miningVesselColor");
                
                var asteroidPrimitiveProp = visualsProp.FindPropertyRelative("asteroidPrimitive");
                var asteroidScaleProp = visualsProp.FindPropertyRelative("asteroidScale");
                var asteroidColorProp = visualsProp.FindPropertyRelative("asteroidColor");
                
                // Use default colors, but make sure they're set
                if (carrierPrimitiveProp != null) carrierPrimitiveProp.enumValueIndex = 1; // Capsule
                if (carrierScaleProp != null) carrierScaleProp.floatValue = 3f;
                if (carrierColorProp != null) carrierColorProp.colorValue = new Color(0.35f, 0.4f, 0.62f, 1f);
                
                if (vesselPrimitiveProp != null) vesselPrimitiveProp.enumValueIndex = 2; // Cylinder
                if (vesselScaleProp != null) vesselScaleProp.floatValue = 1.2f;
                if (vesselColorProp != null) vesselColorProp.colorValue = new Color(0.25f, 0.52f, 0.84f, 1f);
                
                if (asteroidPrimitiveProp != null) asteroidPrimitiveProp.enumValueIndex = 0; // Sphere
                if (asteroidScaleProp != null) asteroidScaleProp.floatValue = 2.25f;
                if (asteroidColorProp != null) asteroidColorProp.colorValue = new Color(0.52f, 0.43f, 0.34f, 1f);
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(authoring);
            
            Debug.Log($"Space4X setup complete. Carriers: {carriersProp.arraySize}, Vessels: {(vesselsProp != null ? vesselsProp.arraySize : 0)}, Asteroids: {(asteroidsProp != null ? asteroidsProp.arraySize : 0)}");
            Debug.LogWarning("IMPORTANT: Entities are created via Baker at runtime. Make sure:");
            Debug.LogWarning("1. Space4XMiningDemoAuthoring component exists on Space4X_MiningDemo GameObject");
            Debug.LogWarning("2. PureDotsConfigAuthoring and SpatialPartitionAuthoring are also on this GameObject");
            Debug.LogWarning("3. The scene must have PureDotsRuntimeConfig assigned in PureDotsConfig");
            Debug.LogWarning("4. ⚠ CRITICAL: Space4X_MiningDemo GameObject MUST be inside a SubScene!");
            Debug.LogWarning("   Right-click in Hierarchy → New Sub Scene → From Selection (select Space4X_MiningDemo)");
        }

        private static void SetupGodgameSide()
        {
            Debug.Log("Setting up Godgame mining demo (right side, X > 0)...");
            
            // Check if Godgame_MiningDemo already exists to avoid duplicates
            var existingGodgame = GameObject.Find("Godgame_MiningDemo");
            GameObject godgameRoot;
            if (existingGodgame != null)
            {
                Debug.Log("Godgame_MiningDemo already exists, reusing it.");
                godgameRoot = existingGodgame;
                godgameRoot.transform.position = new Vector3(50f, 0f, 0f);
            }
            else
            {
                // Create a parent object for Godgame entities
                godgameRoot = new GameObject("Godgame_MiningDemo");
                godgameRoot.transform.position = new Vector3(50f, 0f, 0f);
            }

            // Use PureDOTS authoring components (available in Space4X project)
            // Create storehouse at origin (relative to godgameRoot)
            var storehouseObj = new GameObject("Storehouse_1");
            storehouseObj.transform.SetParent(godgameRoot.transform);
            storehouseObj.transform.localPosition = Vector3.zero;
            var storehouseAuthoring = storehouseObj.AddComponent<PureDOTS.Authoring.StorehouseAuthoring>();
            
            if (storehouseAuthoring != null)
            {
                // Configure storehouse with biomass capacity
                storehouseAuthoring.inputRate = 50f;
                storehouseAuthoring.outputRate = 15f;
                storehouseAuthoring.shredRate = 0f;
                storehouseAuthoring.maxShredQueueSize = 8;
                
                // Add biomass capacity
                storehouseAuthoring.capacities = new System.Collections.Generic.List<PureDOTS.Authoring.StorehouseCapacityEntry>
                {
                    new PureDOTS.Authoring.StorehouseCapacityEntry
                    {
                        resourceTypeId = "biomass",
                        maxCapacity = 500f
                    }
                };
                EditorUtility.SetDirty(storehouseAuthoring);
            }

            // Create resource nodes (trees) around the storehouse using ResourceSourceAuthoring
            for (int i = 0; i < 5; i++)
            {
                var resourceObj = new GameObject($"ResourceNode_{i + 1}");
                resourceObj.transform.SetParent(godgameRoot.transform);
                float angle = i * (360f / 5f) * Mathf.Deg2Rad;
                resourceObj.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * 15f,
                    0f,
                    Mathf.Sin(angle) * 15f
                );
                
                var resourceAuthoring = resourceObj.AddComponent<PureDOTS.Authoring.ResourceSourceAuthoring>();
                if (resourceAuthoring != null)
                {
                    resourceAuthoring.resourceTypeId = "biomass";
                    resourceAuthoring.initialUnits = 100f;
                    resourceAuthoring.gatherRatePerWorker = 5f;
                    resourceAuthoring.maxSimultaneousWorkers = 3;
                    resourceAuthoring.infinite = false;
                    resourceAuthoring.respawns = true;
                    resourceAuthoring.respawnSeconds = 60f;
                    EditorUtility.SetDirty(resourceAuthoring);
                }
            }

            // Create villagers using PureDOTS VillagerAuthoring
            for (int i = 0; i < 3; i++)
            {
                var villagerObj = new GameObject($"Villager_{i + 1}");
                villagerObj.transform.SetParent(godgameRoot.transform);
                float angle = i * (360f / 3f) * Mathf.Deg2Rad;
                villagerObj.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * 5f,
                    0f,
                    Mathf.Sin(angle) * 5f
                );
                
                var villagerAuthoring = villagerObj.AddComponent<PureDOTS.Authoring.VillagerAuthoring>();
                if (villagerAuthoring != null)
                {
                    villagerAuthoring.villagerId = i + 1;
                    villagerAuthoring.factionId = 0;
                    villagerAuthoring.initialJob = PureDOTS.Runtime.Components.VillagerJob.JobType.Gatherer;
                    villagerAuthoring.initialDiscipline = PureDOTS.Runtime.Components.VillagerDisciplineType.Forester;
                    villagerAuthoring.baseSpeed = 3f;
                    villagerAuthoring.startAvailableForJobs = true;
                    EditorUtility.SetDirty(villagerAuthoring);
                }
            }

            Debug.Log("Godgame side setup complete with storehouse, resource nodes, and villagers using PureDOTS authoring components.");
        }

        private static void SetupSharedRendering()
        {
            // Setup camera to view both sides
            var camera = Camera.main;
            if (camera != null)
            {
                camera.transform.position = new Vector3(0f, 30f, -50f);
                camera.transform.LookAt(new Vector3(0f, 0f, 0f));
                
                // Add camera input authoring (need to get GameObject, not Camera reference)
                var cameraObj = camera.gameObject;
                var inputAuthoring = cameraObj.GetComponent<Space4XCameraInputAuthoring>();
                if (inputAuthoring == null)
                {
                    inputAuthoring = cameraObj.AddComponent<Space4XCameraInputAuthoring>();
                }
                
                // Assign input actions
                var inputAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/InputSystem_Actions.inputactions");
                if (inputAsset != null)
                {
                    var serialized = new SerializedObject(inputAuthoring);
                    var prop = serialized.FindProperty("inputActions");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = inputAsset;
                        serialized.ApplyModifiedProperties();
                    }
                }

                // Add camera state authoring
                var cameraAuthoring = cameraObj.GetComponent<Space4XCameraAuthoring>();
                if (cameraAuthoring == null)
                {
                    cameraAuthoring = cameraObj.AddComponent<Space4XCameraAuthoring>();
                }
            }

            // Setup lighting
            var light = UnityEngine.Object.FindFirstObjectByType<Light>();
            if (light == null)
            {
                var lightObj = new GameObject("Directional Light");
                light = lightObj.AddComponent<Light>();
                light.type = LightType.Directional;
                lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
            
            // Create a ground plane for visual reference
            var ground = GameObject.Find("Ground");
            if (ground == null)
            {
                ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Ground";
                ground.transform.localScale = new Vector3(20f, 1f, 20f); // Large plane
                ground.transform.position = new Vector3(0f, 0f, 0f);
                
                // Make it green (terrain color)
                var renderer = ground.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var material = new Material(Shader.Find("Standard"));
                    material.color = new Color(0.2f, 0.6f, 0.3f, 1f); // Green terrain color
                    renderer.material = material;
                }
                
                Debug.Log("Created Ground plane for visual reference");
            }
            
            // Create MiningVisualManifest GameObject for visual representation
            var visualManifest = GameObject.Find("MiningVisualManifest");
            if (visualManifest == null)
            {
                visualManifest = new GameObject("MiningVisualManifest");
                var manifestAuthoring = visualManifest.AddComponent<PureDOTS.Authoring.MiningVisualManifestAuthoring>();
                Debug.Log("Created MiningVisualManifest GameObject for visual representation");
            }
        }

        private static void EnsureDirectoryExists(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parts = path.Split('/');
                var parent = "";
                foreach (var part in parts)
                {
                    var current = parent == "" ? part : $"{parent}/{part}";
                    if (!AssetDatabase.IsValidFolder(current))
                    {
                        AssetDatabase.CreateFolder(parent == "" ? "Assets" : parent, part);
                    }
                    parent = current;
                }
            }
        }
    }
}

