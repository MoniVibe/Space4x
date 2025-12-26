using PureDOTS.Runtime.Camera;
using PureDOTS.Runtime.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UCamera = UnityEngine.Camera;
using UTime = UnityEngine.Time;

namespace Space4X.Camera
{
    /// <summary>
    /// Lightweight camera controller used when no authored rig exists.
    /// Provides WASD/QE translation, RMB orbit, MMB pan, and scroll zoom.
    /// Publishes state via CameraRigService so DOTS systems can consume it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Space4XCameraPlaceholder : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 45f;
        [SerializeField] private float verticalSpeed = 25f;
        [SerializeField] private float fastMultiplier = 2.5f;

        [Header("Orbit / Pan / Zoom")]
        [SerializeField] private float orbitSensitivity = 0.2f;
        [SerializeField] private float panSensitivity = 0.02f;
        [SerializeField] private float zoomUnitsPerNotch = 40f;
        [SerializeField] private float minDistance = 5f;
        [SerializeField] private float maxDistance = 300f;

        [Header("Defaults")]
        [SerializeField] private Vector3 defaultFocus = new Vector3(80f, 0f, 20f);
        [SerializeField] private float defaultDistance = 140f;
        [SerializeField] private float defaultYaw = 315f;
        [SerializeField] private float defaultPitch = 35f;

        private UCamera _camera;
        private Vector3 _focus;
        private float _distance;
        private float _yaw;
        private float _pitch;
        private readonly Plane _groundPlane = new Plane(Vector3.up, Vector3.zero);

        private void Awake()
        {
            if (!RuntimeMode.IsRenderingEnabled)
            {
                enabled = false;
                return;
            }

            _camera = UCamera.main ?? GetComponent<UCamera>();
            if (_camera == null)
            {
                _camera = gameObject.AddComponent<UCamera>();
            }
            if (GetComponent<AudioListener>() == null)
            {
                gameObject.AddComponent<AudioListener>();
            }
            if (GetComponent<CameraRigApplier>() == null)
            {
                gameObject.AddComponent<CameraRigApplier>();
            }

            ResetPose();
            ApplyTransform();
        }

        private void ResetPose()
        {
            _focus = defaultFocus;
            _distance = Mathf.Clamp(defaultDistance, minDistance, maxDistance);
            _yaw = defaultYaw;
            _pitch = Mathf.Clamp(defaultPitch, 5f, 85f);
        }

        private void Update()
        {
            if (!RuntimeMode.IsRenderingEnabled || _camera == null)
            {
                return;
            }

            float dt = UTime.unscaledDeltaTime;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            Vector3 horizontalMove = Vector3.zero;
            float verticalInput = 0f;
            if (keyboard != null)
            {
                var yawRot = Quaternion.Euler(0f, _yaw, 0f);
                var forward = yawRot * Vector3.forward;
                var right = yawRot * Vector3.right;

                if (keyboard.wKey.isPressed) horizontalMove += forward;
                if (keyboard.sKey.isPressed) horizontalMove -= forward;
                if (keyboard.dKey.isPressed) horizontalMove += right;
                if (keyboard.aKey.isPressed) horizontalMove -= right;

                if (keyboard.eKey.isPressed) verticalInput += 1f;
                if (keyboard.qKey.isPressed) verticalInput -= 1f;

                if (keyboard.leftShiftKey.isPressed)
                {
                    horizontalMove *= fastMultiplier;
                }
            }

            if (horizontalMove.sqrMagnitude > 0.001f)
            {
                _focus += moveSpeed * dt * horizontalMove.normalized;
            }

            if (Mathf.Abs(verticalInput) > 0.001f)
            {
                _focus += Vector3.up * (verticalSpeed * dt * Mathf.Sign(verticalInput));
            }

            if (mouse != null)
            {
                if (mouse.rightButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    _yaw += delta.x * orbitSensitivity;
                    _pitch = Mathf.Clamp(_pitch - delta.y * orbitSensitivity, 5f, 85f);
                }

                if (mouse.middleButton.isPressed)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    var yawRot = Quaternion.Euler(0f, _yaw, 0f);
                    var right = yawRot * Vector3.right;
                    var forward = yawRot * Vector3.forward;
                    _focus -= (right * delta.x + forward * delta.y) * panSensitivity;
                }

                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    float notches = scroll / 120f;
                    _distance = Mathf.Clamp(_distance - notches * zoomUnitsPerNotch, minDistance, maxDistance);
                }
            }

            ApplyTransform();
            PublishState();
        }

        private void ApplyTransform()
        {
            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            var position = _focus - rotation * Vector3.forward * _distance;
            _camera.transform.SetPositionAndRotation(position, rotation);
        }

        private void PublishState()
        {
            var state = new CameraRigState
            {
                Focus = _focus,
                Pitch = _pitch,
                Yaw = _yaw,
                Roll = 0f,
                Distance = _distance,
                Mode = CameraRigMode.Orbit,
                PerspectiveMode = true,
                FieldOfView = _camera.fieldOfView,
                RigType = CameraRigType.Space4X
            };

            CameraRigService.Publish(state);
        }
    }
}

