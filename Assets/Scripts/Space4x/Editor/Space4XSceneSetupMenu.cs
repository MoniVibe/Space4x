#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Entities;
using PureDOTS.Authoring;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Editor
{
    using Debug = UnityEngine.Debug;

    
    /// <summary>
    /// Editor menu for quickly setting up Space4X demo scenes.
    /// </summary>
    public static class Space4XSceneSetupMenu
    {
        private const string MenuRoot = "Space4X/";

        [MenuItem(MenuRoot + "Create Combat Demo Scene", false, 100)]
        public static void CreateCombatDemoScene()
        {
            // Create new scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Set up the scene
            SetupCombatDemoScene();

            // Save the scene
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Combat Demo Scene",
                "Space4X_CombatDemo",
                "unity",
                "Save the combat demo scene");

            if (!string.IsNullOrEmpty(path))
            {
                EditorSceneManager.SaveScene(scene, path);
                UnityDebug.Log($"[Space4X] Created combat demo scene at {path}");
            }
        }

        [MenuItem(MenuRoot + "Create Mining Demo Scene", false, 101)]
        public static void CreateMiningDemoScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            SetupMiningDemoScene();

            string path = EditorUtility.SaveFilePanelInProject(
                "Save Mining Demo Scene",
                "Space4X_MiningDemo",
                "unity",
                "Save the mining demo scene");

            if (!string.IsNullOrEmpty(path))
            {
                EditorSceneManager.SaveScene(scene, path);
                UnityDebug.Log($"[Space4X] Created mining demo scene at {path}");
            }
        }

        [MenuItem(MenuRoot + "Add Dev Menu to Scene", false, 200)]
        public static void AddDevMenuToScene()
        {
            var existing = Object.FindFirstObjectByType<DevMenu.Space4XDevMenuUI>();
            if (existing != null)
            {
                EditorGUIUtility.PingObject(existing);
                UnityDebug.LogWarning("[Space4X] Dev Menu already exists in scene");
                return;
            }

            var go = new GameObject("Space4X Dev Menu");
            go.AddComponent<DevMenu.Space4XDevMenuUI>();

            // Try to find or create spawn registry
            var registryGuid = AssetDatabase.FindAssets("t:Space4XDevSpawnRegistry");
            if (registryGuid.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(registryGuid[0]);
                var registry = AssetDatabase.LoadAssetAtPath<DevMenu.Space4XDevSpawnRegistry>(path);
                if (registry != null)
                {
                    var serializedObj = new SerializedObject(go.GetComponent<DevMenu.Space4XDevMenuUI>());
                    serializedObj.FindProperty("spawnRegistry").objectReferenceValue = registry;
                    serializedObj.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            else
            {
                UnityDebug.LogWarning("[Space4X] No SpawnRegistry found. Create one via Assets > Create > Space4X > Dev > Spawn Registry");
            }

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            UnityDebug.Log("[Space4X] Added Dev Menu to scene. Press F12 to toggle.");
        }

        [MenuItem(MenuRoot + "Create Default Spawn Registry", false, 201)]
        public static void CreateDefaultSpawnRegistry()
        {
            var registry = ScriptableObject.CreateInstance<DevMenu.Space4XDevSpawnRegistry>();

            string path = EditorUtility.SaveFilePanelInProject(
                "Save Spawn Registry",
                "Space4XDevSpawnRegistry",
                "asset",
                "Save the spawn registry");

            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.CreateAsset(registry, path);
                AssetDatabase.SaveAssets();
                EditorGUIUtility.PingObject(registry);
                UnityDebug.Log($"[Space4X] Created spawn registry at {path}");
            }
        }

        private static void SetupCombatDemoScene()
        {
            // Find or create the main camera
            var mainCamera = UnityEngine.Camera.main;
            if (mainCamera == null)
            {
                var cameraGo = new GameObject("Main Camera");
                mainCamera = cameraGo.AddComponent<UnityEngine.Camera>();
                cameraGo.tag = "MainCamera";
                cameraGo.AddComponent<AudioListener>();
            }

            // Position camera for top-down view
            mainCamera.transform.position = new Vector3(0, 100, -30);
            mainCamera.transform.rotation = Quaternion.Euler(70, 0, 0);
            mainCamera.orthographic = false;
            mainCamera.fieldOfView = 60;

            // Create directional light
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            lightGo.transform.rotation = Quaternion.Euler(50, -30, 0);

            // Create ECS Bootstrap
            CreateECSBootstrap("Combat Demo Bootstrap", setupCombatDemo: true);

            // Camera Bootstrap
            var cameraBootstrapGo = new GameObject("Space4X Camera Bootstrap");
            var cameraBootstrap = cameraBootstrapGo.AddComponent<Space4X.Camera.Space4XCameraBootstrap>();

            // Try to find and assign the camera prefab
            var cameraPrefabGuid = AssetDatabase.FindAssets("Space4XCamera t:Prefab");
            if (cameraPrefabGuid.Length > 0)
            {
                var prefabPath = AssetDatabase.GUIDToAssetPath(cameraPrefabGuid[0]);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab != null)
                {
                    var serializedObj = new SerializedObject(cameraBootstrap);
                    serializedObj.FindProperty("cameraPrefab").objectReferenceValue = prefab;
                    serializedObj.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // Create Dev Menu
            var devMenuGo = new GameObject("Space4X Dev Menu");
            devMenuGo.AddComponent<DevMenu.Space4XDevMenuUI>();

            // Create ground plane for reference
            var groundGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            groundGo.name = "Ground Reference";
            groundGo.transform.localScale = new Vector3(20, 1, 20);
            groundGo.GetComponent<MeshRenderer>().sharedMaterial = new Material(Shader.Find("Standard"))
            {
                color = new Color(0.1f, 0.1f, 0.15f)
            };

            UnityDebug.Log("[Space4X] Combat demo scene setup complete");
        }

        private static void SetupMiningDemoScene()
        {
            // Camera
            var mainCamera = UnityEngine.Camera.main;
            if (mainCamera == null)
            {
                var cameraGo = new GameObject("Main Camera");
                mainCamera = cameraGo.AddComponent<UnityEngine.Camera>();
                cameraGo.tag = "MainCamera";
                cameraGo.AddComponent<AudioListener>();
            }

            mainCamera.transform.position = new Vector3(0, 80, -20);
            mainCamera.transform.rotation = Quaternion.Euler(70, 0, 0);

            // Light
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(50, -30, 0);

            // ECS Bootstrap
            CreateECSBootstrap("Mining Demo Bootstrap", setupMiningDemo: true);

            // Camera Bootstrap
            var cameraBootstrapGo = new GameObject("Space4X Camera Bootstrap");
            var cameraBootstrap = cameraBootstrapGo.AddComponent<Space4X.Camera.Space4XCameraBootstrap>();

            // Try to find and assign the camera prefab
            var cameraPrefabGuid = AssetDatabase.FindAssets("Space4XCamera t:Prefab");
            if (cameraPrefabGuid.Length > 0)
            {
                var prefabPath = AssetDatabase.GUIDToAssetPath(cameraPrefabGuid[0]);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab != null)
                {
                    var serializedObj = new SerializedObject(cameraBootstrap);
                    serializedObj.FindProperty("cameraPrefab").objectReferenceValue = prefab;
                    serializedObj.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // Dev Menu
            var devMenuGo = new GameObject("Space4X Dev Menu");
            devMenuGo.AddComponent<DevMenu.Space4XDevMenuUI>();

            UnityDebug.Log("[Space4X] Mining demo scene setup complete");
        }

        private static void CreateECSBootstrap(string name, bool setupCombatDemo = false, bool setupMiningDemo = false)
        {
            var bootstrapGo = new GameObject(name);

            // Add PureDOTS config
            var pureDotsConfig = bootstrapGo.AddComponent<PureDOTS.Authoring.PureDotsConfigAuthoring>();

            // Add Spatial Partition
            var spatialPartition = bootstrapGo.AddComponent<SpatialPartitionAuthoring>();

            // Add specific demo authoring
            if (setupCombatDemo)
            {
                bootstrapGo.AddComponent<Authoring.Space4XCombatDemoAuthoring>();
            }
            else if (setupMiningDemo)
            {
                bootstrapGo.AddComponent<Registry.Space4XMiningDemoAuthoring>();
            }
        }

        [MenuItem(MenuRoot + "Validate Scene Setup", false, 300)]
        public static void ValidateSceneSetup()
        {
            int issues = 0;

            // Check for main camera
            if (UnityEngine.Camera.main == null)
            {
                UnityDebug.LogWarning("[Space4X] No main camera found in scene");
                issues++;
            }

            // Check for PureDOTS config
            var pureDotsConfig = Object.FindFirstObjectByType<PureDOTS.Authoring.PureDotsConfigAuthoring>();
            if (pureDotsConfig == null)
            {
                UnityDebug.LogWarning("[Space4X] No PureDotsConfigAuthoring found. Add one to enable ECS.");
                issues++;
            }

            // Check for spatial partition
            var spatialPartition = Object.FindFirstObjectByType<SpatialPartitionAuthoring>();
            if (spatialPartition == null)
            {
                UnityDebug.LogWarning("[Space4X] No SpatialPartitionAuthoring found. Spatial queries won't work.");
                issues++;
            }

            // Check for camera bootstrap
            var cameraBootstrap = Object.FindFirstObjectByType<Space4X.Camera.Space4XCameraBootstrap>();
            if (cameraBootstrap == null)
            {
                UnityDebug.LogWarning("[Space4X] No Camera Bootstrap in scene. Camera may not initialize properly.");
                issues++;
            }

            // Check for dev menu
            var devMenu = Object.FindFirstObjectByType<DevMenu.Space4XDevMenuUI>();
            if (devMenu == null)
            {
                UnityDebug.Log("[Space4X] No Dev Menu in scene. Add via Space4X > Add Dev Menu to Scene");
            }

            if (issues == 0)
            {
                UnityDebug.Log("[Space4X] Scene validation passed!");
            }
            else
            {
                UnityDebug.LogWarning($"[Space4X] Scene validation found {issues} issue(s)");
            }
        }
    }
}
#endif

