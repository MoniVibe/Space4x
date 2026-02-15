using PureDOTS.Runtime.Core;
using UnityEngine;
using PureDOTS.Runtime.Camera;
using UCamera = UnityEngine.Camera;
using UObject = UnityEngine.Object;
using UDebug = UnityEngine.Debug;

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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootstrapExistsOnLoad()
        {
            if (!RuntimeMode.IsRenderingEnabled)
                return;

            // Check if we already have a bootstrap or a rig controller
            if (UObject.FindFirstObjectByType<Space4XCameraBootstrap>() != null || 
                UObject.FindFirstObjectByType<Space4XCameraRigController>() != null)
                return;

            var bootstrapGo = new GameObject("Space4X Camera Bootstrap (Runtime)");
            bootstrapGo.AddComponent<Space4XCameraBootstrap>();
            UObject.DontDestroyOnLoad(bootstrapGo);
        }

        private void Awake()
        {
            if (!RuntimeMode.IsRenderingEnabled)
                return;
            EnsureCameraExists();
        }

        private void EnsureCameraExists()
        {
            // If we already have a main camera, wire it up and bail.
            if (UCamera.main != null)
            {
                var mainCamera = UCamera.main;
                ConfigureCamera(mainCamera.gameObject);
                return;
            }

            if (cameraPrefab != null)
            {
                // Instantiate the camera prefab
                var cameraInstance = Instantiate(cameraPrefab);
                cameraInstance.name = "Space4X Main Camera";

                // Find the camera component (required)
                var cameraComponent = cameraInstance.GetComponentInChildren<UCamera>();
                if (cameraComponent == null)
                {
                    UDebug.LogWarning("[Space4X Camera] Camera prefab instantiated without a Camera component.");
                }
                else
                {
                    ConfigureCamera(cameraComponent.gameObject);
                    UObject.DontDestroyOnLoad(cameraInstance);
                }
                return;
            }

            // Fallback: create a basic camera setup
            UDebug.LogWarning("[Space4X Camera] No camera prefab assigned, creating fallback camera");

            var cameraGo = new GameObject("Space4X Main Camera");
            cameraGo.tag = "MainCamera";
            cameraGo.AddComponent<UCamera>();
            ConfigureCamera(cameraGo);
            UObject.DontDestroyOnLoad(cameraGo);
            UDebug.Log("[Space4X Camera] Created fallback camera setup");
        }

        private void ConfigureCamera(GameObject cameraGameObject)
        {
            if (cameraGameObject == null)
            {
                return;
            }

            if (!cameraGameObject.TryGetComponent(out UCamera unityCamera))
            {
                unityCamera = cameraGameObject.AddComponent<UCamera>();
            }

            if (!cameraGameObject.CompareTag("MainCamera"))
            {
                cameraGameObject.tag = "MainCamera";
            }

            if (cameraGameObject.GetComponent<AudioListener>() == null)
            {
                cameraGameObject.AddComponent<AudioListener>();
            }

            if (cameraGameObject.GetComponent<CameraRigApplier>() == null)
            {
                cameraGameObject.AddComponent<CameraRigApplier>();
            }

            var rigController = cameraGameObject.GetComponent<Space4XCameraRigController>();
            if (rigController == null)
            {
                rigController = cameraGameObject.AddComponent<Space4XCameraRigController>();
                rigController.TargetCamera = unityCamera;
                UDebug.Log("[Space4X Camera] Added Space4XCameraRigController fallback.");
            }
            else if (rigController.TargetCamera == null)
            {
                rigController.TargetCamera = unityCamera;
            }

            if (cameraGameObject.GetComponent<Space4XCameraHudOverlay>() == null)
            {
                cameraGameObject.AddComponent<Space4XCameraHudOverlay>();
            }

            var placeholder = cameraGameObject.GetComponent<Space4XCameraPlaceholder>();
            if (placeholder != null)
            {
                Destroy(placeholder);
            }

            if (cameraGameObject.transform.position == Vector3.zero)
            {
                cameraGameObject.transform.position = new Vector3(0f, 60f, -30f);
                cameraGameObject.transform.rotation = Quaternion.Euler(35f, 315f, 0f);
            }
        }
    }
}
