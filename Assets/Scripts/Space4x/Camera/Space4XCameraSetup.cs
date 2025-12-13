#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityCamera = UnityEngine.Camera;

namespace Space4X.Camera
{
    using Debug = UnityEngine.Debug;

    
    /// <summary>
    /// Editor utility to create and configure the Space4X camera prefab.
    /// </summary>
    public static class Space4XCameraSetup
    {
        [MenuItem("Space4X/Camera/Create Camera Prefab")]
        public static void CreateCameraPrefab()
        {
            // Create the camera GameObject
            var cameraGo = new GameObject("Space4XCamera");
            var camera = cameraGo.AddComponent<UnityCamera>();
            cameraGo.tag = "MainCamera";
            cameraGo.AddComponent<AudioListener>();

            // Add the camera rig controller
            var rigController = cameraGo.AddComponent<Space4XCameraRigController>();
            rigController.TargetCamera = camera;

            // Position for good defaults
            cameraGo.transform.position = new Vector3(0, 60, -30);
            cameraGo.transform.rotation = Quaternion.Euler(45, 0, 0);

            // Try to find input actions
            var inputActions = FindDefaultInputActions();
            if (inputActions != null)
            {
                // Note: In a real setup, you'd configure the input action references
                // on the rig controller here
            }

            // Create prefab
            string prefabPath = "Assets/Prefabs/Space4X/Space4XCamera.prefab";
            // Ensure directory exists
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(prefabPath));
            
            PrefabUtility.SaveAsPrefabAsset(cameraGo, prefabPath);

            // Clean up the temporary GameObject
            Object.DestroyImmediate(cameraGo);

            UnityEngine.Debug.Log($"[Space4X Camera] Created camera prefab at {prefabPath}");
        }

        private static InputActionAsset FindDefaultInputActions()
        {
            // Look for input actions in common locations
            var inputActionsGuids = AssetDatabase.FindAssets("t:InputActionAsset");
            foreach (var guid in inputActionsGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
                if (asset != null && (path.Contains("Space4X") || path.Contains("Default")))
                {
                    return asset;
                }
            }
            return null;
        }
    }
}
#endif
