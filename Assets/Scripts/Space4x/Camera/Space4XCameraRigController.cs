using PureDOTS.Runtime.Camera;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityCamera = UnityEngine.Camera;

namespace Space4X.Camera
{
    /// <summary>
    /// Lightweight Space4X camera rig controller that publishes CameraRigState via CameraRigService.
    /// Uses frame-time input (Input System) and avoids deterministic simulation dependencies.
    /// </summary>
    [DisallowMultipleComponent]
    public class Space4XCameraRigController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private UnityCamera targetCamera;

        public UnityCamera TargetCamera
        {
            get => targetCamera;
            set => targetCamera = value;
        }

        [Header("Input Actions (Input System)")]
        [SerializeField] private InputActionReference orbitAction;
        [SerializeField] private InputActionReference panAction;
        [SerializeField] private InputActionReference zoomAction;

        [Header("Speeds")]
        [SerializeField] private float orbitDegreesPerSecond = 120f;
        [SerializeField] private float panUnitsPerSecond = 25f;
        [SerializeField] private float zoomUnitsPerSecond = 35f;

        [Header("Limits")]
        [SerializeField] private float minDistance = 5f;
        [SerializeField] private float maxDistance = 300f;
        [SerializeField] private float minPitch = 5f;
        [SerializeField] private float maxPitch = 85f;

        [Header("State")]
        [SerializeField] private Vector3 focusPoint = Vector3.zero;
        [SerializeField] private float yawDegrees = 0f;
        [SerializeField] private float pitchDegrees = 45f;
        [SerializeField] private float distance = 60f;

        private void OnEnable()
        {
            orbitAction?.action.Enable();
            panAction?.action.Enable();
            zoomAction?.action.Enable();
        }

        private void OnDisable()
        {
            orbitAction?.action.Disable();
            panAction?.action.Disable();
            zoomAction?.action.Disable();
        }

        private void Update()
        {
            // TEMP sanity: remove after confirming horizon reacts
            // transform.Rotate(Vector3.up, 10f * Time.unscaledDeltaTime, Space.World);

            float dt = Time.deltaTime;

            var orbit = orbitAction != null ? orbitAction.action.ReadValue<Vector2>() : Vector2.zero;
            var pan = panAction != null ? panAction.action.ReadValue<Vector2>() : Vector2.zero;
            float zoom = zoomAction != null ? zoomAction.action.ReadValue<float>() : 0f;

            yawDegrees += orbit.x * orbitDegreesPerSecond * dt;
            pitchDegrees = Mathf.Clamp(pitchDegrees + orbit.y * orbitDegreesPerSecond * dt, minPitch, maxPitch);
            distance = Mathf.Clamp(distance - zoom * zoomUnitsPerSecond * dt, minDistance, maxDistance);

            var yawRotation = Quaternion.Euler(0f, yawDegrees, 0f);
            var right = yawRotation * Vector3.right;
            var forward = yawRotation * Vector3.forward;
            focusPoint += (right * pan.x + forward * pan.y) * (panUnitsPerSecond * dt);
        }

        private void LateUpdate()
        {
            var rotation = Quaternion.Euler(pitchDegrees, yawDegrees, 0f);
            var position = focusPoint - (rotation * Vector3.forward * distance);

            if (targetCamera != null)
            {
                targetCamera.transform.SetPositionAndRotation(position, rotation);
            }

            var state = new CameraRigState
            {
                Position = position,
                Rotation = rotation,
                Pitch = pitchDegrees,
                Yaw = yawDegrees,
                Distance = distance,
                PerspectiveMode = true,
                FieldOfView = targetCamera != null ? targetCamera.fieldOfView : 60f,
                RigType = CameraRigType.Space4X
            };

            CameraRigService.Publish(state);
        }
    }
}
