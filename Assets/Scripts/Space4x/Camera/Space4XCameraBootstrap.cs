using PureDOTS.Runtime.Core;
using UnityEngine;
using UnityCamera = UnityEngine.Camera;
using PureDOTS.Runtime.Camera;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Camera
{
    using Debug = UnityEngine.Debug;

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
            if (RuntimeMode.IsHeadless)
                return;

            // Check if we already have a bootstrap or a rig controller
            if (Object.FindFirstObjectByType<Space4XCameraBootstrap>() != null || 
                Object.FindFirstObjectByType<Space4XCameraRigController>() != null)
                return;

            var bootstrapGo = new GameObject("Space4X Camera Bootstrap (Runtime)");
            bootstrapGo.AddComponent<Space4XCameraBootstrap>();
            UnityEngine.Object.DontDestroyOnLoad(bootstrapGo);
        }

        private void Awake()
        {
            if (RuntimeMode.IsHeadless)
                return;
            EnsureCameraExists();
        }

        private void EnsureCameraExists()
        {
            // If we already have a main camera, find or create rig controller for it
            if (UnityCamera.main != null)
            {
                var mainCamera = UnityCamera.main;
                AttachPlaceholder(mainCamera.gameObject);
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
                    if (cameraComponent.GetComponent<CameraRigApplier>() == null)
                    {
                        cameraComponent.gameObject.AddComponent<CameraRigApplier>();
                    }
                    UnityDebug.Log("[Space4X Camera] Created camera from prefab and assigned to rig controller");
                }
                else
                {
                    UnityDebug.LogWarning("[Space4X Camera] Camera prefab instantiated but missing Camera or Space4XCameraRigController components");
                }
            }
            else
            {
                // Fallback: create a basic camera setup
                UnityDebug.LogWarning("[Space4X Camera] No camera prefab assigned, creating fallback camera");

                var cameraGo = new GameObject("Space4X Main Camera");
                var camera = cameraGo.AddComponent<UnityCamera>();
                cameraGo.tag = "MainCamera";
                AttachPlaceholder(cameraGo);
                UnityDebug.Log("[Space4X Camera] Created fallback camera setup");
            }
        }

        private void AttachPlaceholder(GameObject cameraGameObject)
        {
            if (cameraGameObject == null)
            {
                return;
            }

            if (cameraGameObject.GetComponent<AudioListener>() == null)
            {
                cameraGameObject.AddComponent<AudioListener>();
            }

            if (cameraGameObject.GetComponent<CameraRigApplier>() == null)
            {
                cameraGameObject.AddComponent<CameraRigApplier>();
            }

            if (cameraGameObject.GetComponent<Space4XCameraPlaceholder>() == null)
            {
                cameraGameObject.AddComponent<Space4XCameraPlaceholder>();
            }

            if (cameraGameObject.transform.position == Vector3.zero)
            {
                cameraGameObject.transform.position = new Vector3(0f, 60f, -30f);
                cameraGameObject.transform.rotation = Quaternion.Euler(35f, 315f, 0f);
            }
        }
    }
}
