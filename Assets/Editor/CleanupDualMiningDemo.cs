using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using PureDOTS.Authoring;

namespace Space4X.Editor
{
    /// <summary>
    /// Cleans up duplicate GameObjects in the Dual Mining Demo scene and assigns missing assets.
    /// </summary>
    public static class CleanupDualMiningDemo
    {
        [MenuItem("Tools/Space4X/Cleanup: Remove Duplicate Objects")]
        public static void RemoveDuplicates()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("No active scene loaded!");
                return;
            }

            Debug.Log("=== Cleaning up duplicate objects ===\n");

            int removedCount = 0;

            // Find all Godgame_MiningDemo objects
            var godgameObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            var godgameDemos = new System.Collections.Generic.List<GameObject>();
            foreach (var obj in godgameObjects)
            {
                if (obj.name == "Godgame_MiningDemo")
                {
                    godgameDemos.Add(obj);
                }
            }

            Debug.Log($"Found {godgameDemos.Count} 'Godgame_MiningDemo' objects");
            if (godgameDemos.Count > 1)
            {
                // Keep the first one, remove the rest
                for (int i = 1; i < godgameDemos.Count; i++)
                {
                    Debug.Log($"  Removing duplicate: {godgameDemos[i].name} at {godgameDemos[i].transform.position}");
                    Object.DestroyImmediate(godgameDemos[i]);
                    removedCount++;
                }
                Debug.Log($"  Kept first instance at {godgameDemos[0].transform.position}");
            }

            // Find all Space4X_MiningDemo objects (excluding those in SubScenes)
            var space4xDemos = new System.Collections.Generic.List<GameObject>();
            foreach (var obj in godgameObjects)
            {
                if (obj.name == "Space4X_MiningDemo" && string.IsNullOrEmpty(obj.scene.path))
                {
                    // This is in the main scene, not a SubScene
                    space4xDemos.Add(obj);
                }
            }

            Debug.Log($"Found {space4xDemos.Count} 'Space4X_MiningDemo' objects in main scene");
            if (space4xDemos.Count > 1)
            {
                // Keep the first one, remove the rest
                for (int i = 1; i < space4xDemos.Count; i++)
                {
                    Debug.Log($"  Removing duplicate: {space4xDemos[i].name} at {space4xDemos[i].transform.position}");
                    Object.DestroyImmediate(space4xDemos[i]);
                    removedCount++;
                }
                Debug.Log($"  Kept first instance at {space4xDemos[0].transform.position}");
            }

            // Check for duplicate SpatialPartition objects
            var spatialObjects = new System.Collections.Generic.List<GameObject>();
            foreach (var obj in godgameObjects)
            {
                if (obj.name == "SpatialPartition")
                {
                    spatialObjects.Add(obj);
                }
            }

            Debug.Log($"Found {spatialObjects.Count} 'SpatialPartition' objects");
            if (spatialObjects.Count > 1)
            {
                // Keep the first one, remove the rest
                for (int i = 1; i < spatialObjects.Count; i++)
                {
                    Debug.Log($"  Removing duplicate: {spatialObjects[i].name}");
                    Object.DestroyImmediate(spatialObjects[i]);
                    removedCount++;
                }
            }

            // Check for duplicate PureDotsConfig objects
            var configObjects = new System.Collections.Generic.List<GameObject>();
            foreach (var obj in godgameObjects)
            {
                if (obj.name == "PureDotsConfig")
                {
                    configObjects.Add(obj);
                }
            }

            Debug.Log($"Found {configObjects.Count} 'PureDotsConfig' objects");
            if (configObjects.Count > 1)
            {
                // Keep the first one, remove the rest
                for (int i = 1; i < configObjects.Count; i++)
                {
                    Debug.Log($"  Removing duplicate: {configObjects[i].name}");
                    Object.DestroyImmediate(configObjects[i]);
                    removedCount++;
                }
            }

            // Check for duplicate MiningVisualManifest objects
            var visualManifests = new System.Collections.Generic.List<GameObject>();
            foreach (var obj in godgameObjects)
            {
                if (obj.name == "MiningVisualManifest")
                {
                    visualManifests.Add(obj);
                }
            }

            Debug.Log($"Found {visualManifests.Count} 'MiningVisualManifest' objects");
            if (visualManifests.Count > 1)
            {
                // Keep the first one, remove the rest (only one should exist per scene)
                for (int i = 1; i < visualManifests.Count; i++)
                {
                    Debug.Log($"  Removing duplicate: {visualManifests[i].name}");
                    Object.DestroyImmediate(visualManifests[i]);
                    removedCount++;
                }
            }

            // After cleanup, ensure remaining objects have their assets assigned
            EnsureAssetsAssigned();

            if (removedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log($"\n✓ Cleanup complete! Removed {removedCount} duplicate object(s).");
                Debug.Log("Please save the scene (Ctrl+S) to persist the changes.");
            }
            else
            {
                Debug.Log("\n✓ No duplicates found. Scene is clean!");
            }
        }

        private static void EnsureAssetsAssigned()
        {
            Debug.Log("\n=== Ensuring assets are assigned ===");

            // Ensure PureDotsConfig has runtime config assigned
            var configObjects = GameObject.FindObjectsByType<PureDotsConfigAuthoring>(FindObjectsSortMode.None);
            var runtimeConfig = AssetDatabase.LoadAssetAtPath<PureDotsRuntimeConfig>("Assets/Space4X/Config/PureDotsRuntimeConfig.asset");
            
            foreach (var configAuthoring in configObjects)
            {
                if (configAuthoring.config == null && runtimeConfig != null)
                {
                    configAuthoring.config = runtimeConfig;
                    EditorUtility.SetDirty(configAuthoring);
                    Debug.Log($"✓ Assigned PureDotsRuntimeConfig to {configAuthoring.gameObject.name}");
                }
                else if (configAuthoring.config == null)
                {
                    Debug.LogWarning($"⚠ {configAuthoring.gameObject.name} has no config assigned and PureDotsRuntimeConfig.asset not found!");
                }
            }

            // Ensure SpatialPartitionAuthoring has profile assigned
            var spatialObjects = GameObject.FindObjectsByType<SpatialPartitionAuthoring>(FindObjectsSortMode.None);
            var spatialProfile = AssetDatabase.LoadAssetAtPath<SpatialPartitionProfile>("Assets/Space4X/Config/DefaultSpatialPartitionProfile.asset");
            
            foreach (var spatialAuthoring in spatialObjects)
            {
                if (spatialAuthoring.profile == null && spatialProfile != null)
                {
                    spatialAuthoring.profile = spatialProfile;
                    EditorUtility.SetDirty(spatialAuthoring);
                    Debug.Log($"✓ Assigned DefaultSpatialPartitionProfile to {spatialAuthoring.gameObject.name}");
                }
                else if (spatialAuthoring.profile == null)
                {
                    Debug.LogWarning($"⚠ {spatialAuthoring.gameObject.name} has no profile assigned and DefaultSpatialPartitionProfile.asset not found!");
                }
            }
        }
    }
}

