using UnityEngine;
using UnityEngine.InputSystem;
using UnityCamera = UnityEngine.Camera;

namespace Space4X.Camera
{
    /// <summary>
    /// Bootstrap script that ensures a Space4X camera exists at runtime.
    /// Creates the camera prefab if no main camera exists.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class Space4XCameraBootstrap : MonoBehaviour
    {
        [Header("Camera Setup")]
        [SerializeField]
        private GameObject cameraPrefab;

        [Header("Default Input Actions")]
        [SerializeField]
        private InputActionAsset defaultInputActions;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootstrapExistsOnLoad()
        {
#if UNITY_EDITOR
            if (Object.FindFirstObjectByType<Space4XCameraBootstrap>() != null)
                return;

            var bootstrapGo = new GameObject("Space4X Camera Bootstrap (Runtime)");
            bootstrapGo.AddComponent<Space4XCameraBootstrap>();
            UnityEngine.Object.DontDestroyOnLoad(bootstrapGo);
#endif
        }

        private void Awake()
        {
#if UNITY_EDITOR
            EnsureCameraExists();
#endif
        }

#if UNITY_EDITOR
        private void EnsureCameraExists()
        {
            // If we already have a main camera, find or create rig controller for it
            if (UnityCamera.main != null)
            {
                var mainCamera = UnityCamera.main;
                var rigController = Object.FindFirstObjectByType<Space4XCameraRigController>();

                if (rigController == null)
                {
                    // Create a rig controller GameObject and attach it
                    var rigGo = new GameObject("Space4X Camera Rig Controller");
                    rigController = rigGo.AddComponent<Space4XCameraRigController>();
                    Debug.Log("[Space4X Camera] Created rig controller for existing main camera");
                }

                rigController.TargetCamera = mainCamera;
                Debug.Log("[Space4X Camera] Assigned existing main camera to rig controller");
                return;
            }

            if (cameraPrefab != null)
            {
                // Instantiate the camera prefab
                var cameraInstance = Instantiate(cameraPrefab);
                cameraInstance.name = "Space4X Main Camera";

                // Find the camera component and rig controller
                var cameraComponent = cameraInstance.GetComponentInChildren<UnityCamera>();
                var rigController = cameraInstance.GetComponentInChildren<Space4XCameraRigController>();

                if (cameraComponent != null && rigController != null)
                {
                    rigController.TargetCamera = cameraComponent;
                    Debug.Log("[Space4X Camera] Created camera from prefab and assigned to rig controller");
                }
                else
                {
                    Debug.LogWarning("[Space4X Camera] Camera prefab instantiated but missing Camera or Space4XCameraRigController components");
                }
            }
            else
            {
                // Fallback: create a basic camera setup
                Debug.LogWarning("[Space4X Camera] No camera prefab assigned, creating fallback camera");

                var cameraGo = new GameObject("Space4X Main Camera");
                var camera = cameraGo.AddComponent<UnityCamera>();
                cameraGo.tag = "MainCamera";
                cameraGo.AddComponent<AudioListener>();

                // Add the camera controller
                var controller = cameraGo.AddComponent<Space4XCameraRigController>();
                controller.TargetCamera = camera;

                // Position for a good default view
                cameraGo.transform.position = new Vector3(0, 60, -30);
                cameraGo.transform.rotation = Quaternion.Euler(45, 0, 0);

                Debug.Log("[Space4X Camera] Created fallback camera setup");
            }
        }
#endif
    }
}
