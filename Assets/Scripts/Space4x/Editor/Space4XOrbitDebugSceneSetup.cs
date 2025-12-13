#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Space4X.Editor
{
    using Debug = UnityEngine.Debug;

    
    /// <summary>
    /// Editor utility to set up the Space4X_OrbitDebug scene with camera and light.
    /// </summary>
    public static class Space4XOrbitDebugSceneSetup
    {
        [MenuItem("Space4X/Setup Orbit Debug Scene")]
        public static void SetupOrbitDebugScene()
        {
            // Create or load the scene
            var scenePath = "Assets/Scenes/Space4X_OrbitDebug.unity";
            var scene = EditorSceneManager.GetSceneByPath(scenePath);
            
            if (!scene.IsValid())
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                // Save the scene
                EditorSceneManager.SaveScene(scene, scenePath);
            }
            else
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            // Clear existing objects (except camera and light if they exist)
            var rootObjects = scene.GetRootGameObjects();
            foreach (var obj in rootObjects)
            {
                if (obj.name != "Main Camera" && obj.name != "Directional Light")
                {
                    Object.DestroyImmediate(obj);
                }
            }

            // Find or create Main Camera
            GameObject cameraObj = GameObject.Find("Main Camera");
            if (cameraObj == null)
            {
                cameraObj = new GameObject("Main Camera");
                cameraObj.AddComponent<UnityEngine.Camera>();
                cameraObj.tag = "MainCamera";
            }

            // Set camera position and rotation (looking at origin from (-15, 21, -15))
            cameraObj.transform.position = new Vector3(-15f, 21f, -15f);
            cameraObj.transform.LookAt(Vector3.zero);
            
            // Set camera settings
            var camera = cameraObj.GetComponent<UnityEngine.Camera>();
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1000f;

            // Find or create Directional Light
            GameObject lightObj = GameObject.Find("Directional Light");
            if (lightObj == null)
            {
                lightObj = new GameObject("Directional Light");
                var light = lightObj.AddComponent<Light>();
                light.type = LightType.Directional;
            }

            lightObj.transform.position = new Vector3(0f, 3f, 0f);
            lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Mark scene as dirty and save
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[Space4XOrbitDebugSceneSetup] Scene setup complete. Camera at (-15, 21, -15) looking at origin.");
        }
    }
}
#endif

