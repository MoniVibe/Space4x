using UnityEngine;
using UnityEditor;
using Unity.Scenes;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Space4X.Registry;

namespace Space4X.Editor
{
    /// <summary>
    /// Creates a SubScene for Space4X_MiningDemo GameObject so entities can be baked.
    /// </summary>
    public static class FixSubScene
    {
        [MenuItem("Tools/Space4X/Fix: Create SubScene for Mining Demo")]
        public static void CreateSubSceneForMiningDemo()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                Debug.LogError("No active scene! Please open the DualMiningDemo scene first.");
                return;
            }

            // Desired SubScene path
            var subScenePath = "Assets/Scenes/Demo/Space4X_MiningDemo_SubScene.unity";
            var directory = System.IO.Path.GetDirectoryName(subScenePath);
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            // Ensure the SubScene asset exists (create empty if missing)
            var subSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(subScenePath);
            if (subSceneAsset == null)
            {
                var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                EditorSceneManager.SaveScene(newScene, subScenePath);
                subSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(subScenePath);
                EditorSceneManager.CloseScene(newScene, false);
                Debug.Log($"Created new SubScene asset at {subScenePath}");
            }

            if (subSceneAsset == null)
            {
                Debug.LogError($"Failed to locate SubScene asset at {subScenePath}");
                return;
            }

            // Ensure SubScene GameObject exists in the main scene
            var subSceneGO = GameObject.Find("Space4X_MiningDemo_SubScene");
            if (subSceneGO == null)
            {
                subSceneGO = new GameObject("Space4X_MiningDemo_SubScene");
                Undo.RegisterCreatedObjectUndo(subSceneGO, "Create SubScene GameObject");
            }

            var subSceneComponent = subSceneGO.GetComponent<SubScene>();
            if (subSceneComponent == null)
            {
                subSceneComponent = subSceneGO.AddComponent<SubScene>();
            }

            // Link SubScene component to the asset and enable auto load
            Undo.RecordObject(subSceneComponent, "Assign SubScene SceneAsset");
            subSceneComponent.SceneAsset = subSceneAsset;
            subSceneComponent.AutoLoadScene = true;
            EditorUtility.SetDirty(subSceneComponent);
            AssetDatabase.SaveAssets();

            // Optionally verify Space4X_MiningDemo exists inside the SubScene
            Scene loadedSubScene = default;
            try
            {
                loadedSubScene = EditorSceneManager.OpenScene(subScenePath, OpenSceneMode.Additive);
                var miningDemoGO = FindRootObject(loadedSubScene, "Space4X_MiningDemo");
                if (miningDemoGO == null)
                {
                    var mainSceneMiningDemo = FindRootObject(activeScene, "Space4X_MiningDemo");
                    if (mainSceneMiningDemo != null)
                    {
                        EditorSceneManager.MoveGameObjectToScene(mainSceneMiningDemo, loadedSubScene);
                        miningDemoGO = mainSceneMiningDemo;
                        Debug.Log("✓ Moved Space4X_MiningDemo GameObject into the SubScene.");
                    }
                }

                if (miningDemoGO == null)
                {
                    Debug.LogWarning("⚠ Space4X_MiningDemo GameObject not found inside the SubScene. Run 'Setup Dual Mining Demo Scene' if needed.");
                }
                else
                {
                    Debug.Log("✓ Space4X_MiningDemo exists inside the SubScene.");
                }

                EditorSceneManager.SaveScene(loadedSubScene);
            }
            finally
            {
                if (loadedSubScene.IsValid())
                {
                    EditorSceneManager.CloseScene(loadedSubScene, false);
                }
            }

            // Save the main scene
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log($"✓ Linked Space4X_MiningDemo_SubScene GameObject to asset {subScenePath}");
            Debug.Log("✓ AutoLoadScene enabled. Enter Play Mode to bake entities.");
        }

        private static GameObject FindRootObject(Scene scene, string name)
        {
            if (!scene.IsValid())
                return null;

            var roots = scene.GetRootGameObjects();
            foreach (var go in roots)
            {
                if (go.name == name)
                    return go;
            }

            return null;
        }
    }
}

