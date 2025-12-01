using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Mathematics;
using PureDOTS.Runtime.Camera;

namespace Space4X.CameraSystem
{
    /// <summary>
    /// Godgame-style orbit/RTS camera with cursor-based zoom and drag pan.
    /// Publishes CameraRigState to CameraRigService for DOTS consumers.
    /// </summary>
    [DisallowMultipleComponent]
    public class Space4XCameraController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 30f;
        [SerializeField] private float _verticalSpeed = 20f;
        [SerializeField] private float _panSpeedPerPixel = 0.1f;

        [Header("Rotation")]
        [SerializeField] private float _rotationSpeed = 0.2f; // degrees per pixel
        [SerializeField] private float _minPitch = -80f;
        [SerializeField] private float _maxPitch = 80f;

        [Header("Default Pose")]
        [SerializeField] private bool _resetToDefaultOnStart = true;
        [SerializeField] private Vector3 _defaultFocus = Vector3.zero;
        [SerializeField] private float _defaultDistance = 30f;
        [SerializeField] private float _defaultYawDegrees = 45f;
        [SerializeField] private float _defaultPitchDegrees = 45f;

        [Header("Zoom")]
        [SerializeField] private float _zoomSpeed = 40f;
        [SerializeField] private float _minZoomDistance = 5f;

        [Header("Debugging")]
        [SerializeField] private bool _debugLogging = false;

        // Internal state
        private float _yaw;
        private float _pitch;
        private Vector3 _position;
        private Quaternion _rotation;
        private Camera _unityCamera;
        private Transform _cameraTransform;
        private bool _loggedMissingCamera;

        // Zoom and drag state
        private Plane _groundPlane = new Plane(Vector3.up, Vector3.zero);
        private bool _isDraggingPan;

        private void Awake()
        {
            _unityCamera = Camera.main ?? Camera.current;
        }

        private void OnEnable()
        {
            ResolveCamera();

            if (_resetToDefaultOnStart)
            {
                ResetToDefaultPose();
            }
            else if (_cameraTransform != null)
            {
                var euler = _cameraTransform.rotation.eulerAngles;
                _yaw = euler.y;
                _pitch = euler.x;
                _position = _cameraTransform.position;
                _rotation = _cameraTransform.rotation;
            }
        }

        private void Update()
        {
            // If another rig is in control, skip input
            if (PureDOTS.Runtime.Camera.BW2StyleCameraController.HasActiveRig)
            {
                return;
            }

            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null)
            {
                return;
            }

            // Focus/reset
            if (kb.fKey.wasPressedThisFrame)
            {
                ResetToDefaultPose();
                return;
            }

            // Zoom and drag-pan
            HandleZoom(mouse, UnityEngine.Time.deltaTime);
            HandleDragPan(mouse, UnityEngine.Time.deltaTime);

            float dt = UnityEngine.Time.deltaTime;

            // WASD ground-plane movement
            Vector2 moveInput = Vector2.zero;
            if (kb.wKey.isPressed) moveInput.y += 1f;
            if (kb.sKey.isPressed) moveInput.y -= 1f;
            if (kb.dKey.isPressed) moveInput.x += 1f;
            if (kb.aKey.isPressed) moveInput.x -= 1f;
            if (moveInput.sqrMagnitude > 1e-4f) moveInput = moveInput.normalized;

            Quaternion yawRotation = Quaternion.Euler(0f, _yaw, 0f);
            Vector3 camForward = yawRotation * Vector3.forward;
            Vector3 camRight = yawRotation * Vector3.right;
            Vector3 worldMove = camForward * moveInput.y + camRight * moveInput.x;
            worldMove *= _moveSpeed * dt;

            // Q/E vertical move
            float vertical = 0f;
            if (kb.eKey.isPressed) vertical += 1f;
            if (kb.qKey.isPressed) vertical -= 1f;
            Vector3 verticalMove = Vector3.up * (vertical * _verticalSpeed * dt);

            _position += worldMove + verticalMove;

            // MMB rotate (yaw/pitch)
            if (mouse.middleButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                _yaw += delta.x * _rotationSpeed;
                _yaw = NormalizeDegrees(_yaw);

                _pitch -= delta.y * _rotationSpeed; // drag up looks down
                _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);
            }

            _rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            PublishCurrentState();
        }

        private void PublishCurrentState()
        {
            var state = new CameraRigState
            {
                Position = _position,
                Rotation = _rotation,
                Pitch = math.radians(_pitch),
                Yaw = math.radians(_yaw),
                Distance = _defaultDistance,
                PerspectiveMode = true,
                FieldOfView = _unityCamera != null ? _unityCamera.fieldOfView : 60f,
                RigType = CameraRigType.Space4X
            };

            CameraRigService.Publish(state);

            if (_debugLogging && UnityEngine.Time.frameCount % 120 == 0)
            {
                Debug.Log($"[Space4XCamera] Published state Pos: {_position:F1}, Yaw: {_yaw:F1}°, Pitch: {_pitch:F1}°");
            }
        }

        private void ResetToDefaultPose()
        {
            _yaw = _defaultYawDegrees;
            _pitch = _defaultPitchDegrees;

            _rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            _position = _defaultFocus - _rotation * (Vector3.forward * _defaultDistance);

            PublishCurrentState();
        }

        private void HandleZoom(Mouse mouse, float dt)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) < 0.01f || _unityCamera == null)
                return;

            float zoomAmount = scroll * _zoomSpeed * dt;
            Ray ray = _unityCamera.ScreenPointToRay(mouse.position.ReadValue());

            if (_groundPlane.Raycast(ray, out float enter))
            {
                Vector3 hit = ray.GetPoint(enter);
                Vector3 camToHit = hit - _position;
                float dist = camToHit.magnitude;
                if (dist < _minZoomDistance && zoomAmount > 0f)
                    return;

                Vector3 dir = camToHit.normalized;
                _position += dir * zoomAmount;
            }
            else
            {
                // Fallback zoom along yaw forward
                Quaternion yawRot = Quaternion.Euler(0f, _yaw, 0f);
                Vector3 forward = yawRot * Vector3.forward;
                _position += forward * zoomAmount;
            }
        }

        private void HandleDragPan(Mouse mouse, float dt)
        {
            // If button not held, reset state
            if (!mouse.leftButton.isPressed)
            {
                _isDraggingPan = false;
                return;
            }

            // First frame of drag: just mark, don't move yet (prevents jump)
            if (!_isDraggingPan)
            {
                _isDraggingPan = true;
                // Swallow the first delta to avoid jump
                return;
            }

            Vector2 delta = mouse.delta.ReadValue();
            if (delta.sqrMagnitude < 0.0001f)
                return;

            // We pan opposite to drag (drag right -> world goes right, camera moves left)
            // Only use yaw so we stay on the ground plane
            Quaternion yawRot = Quaternion.Euler(0f, _yaw, 0f);
            Vector3 right = yawRot * Vector3.right;
            Vector3 forward = yawRot * Vector3.forward;

            // Convert pixels to world movement (no dt here: delta is per-frame already)
            float scale = _panSpeedPerPixel;
            Vector3 pan =
                (-right * delta.x +   // dragging mouse right moves camera left
                 -forward * delta.y)  // dragging mouse up moves camera forward/back
                * scale;

            _position += pan;
        }

        private bool TryGetGroundHit(Vector2 screenPos, out Vector3 hitPoint)
        {
            Ray ray = _unityCamera.ScreenPointToRay(screenPos);
            if (_groundPlane.Raycast(ray, out float enter))
            {
                hitPoint = ray.GetPoint(enter);
                return true;
            }

            hitPoint = default;
            return false;
        }

        private void ResolveCamera()
        {
            if (_unityCamera != null && _cameraTransform != null)
            {
                return;
            }

            _unityCamera = Camera.main ?? Object.FindFirstObjectByType<Camera>();
            if (_unityCamera != null)
            {
                _cameraTransform = _unityCamera.transform;
                _loggedMissingCamera = false;
            }
            else if (!_loggedMissingCamera)
            {
                Debug.LogWarning("[Space4XCamera] No Camera found; controller will run headless until one appears.");
                _loggedMissingCamera = true;
            }
        }

        private static float NormalizeDegrees(float degrees)
        {
            while (degrees > 180f) degrees -= 360f;
            while (degrees < -180f) degrees += 360f;
            return degrees;
        }

        /// <summary>
        /// Export the current camera state (yaw/pitch in radians for DOTS consumers).
        /// </summary>
        public CameraRigState GetCurrentState()
        {
            return new CameraRigState
            {
                Position = _position,
                Rotation = _rotation,
                Pitch = math.radians(_pitch),
                Yaw = math.radians(_yaw),
                Distance = _defaultDistance,
                PerspectiveMode = true,
                FieldOfView = _unityCamera != null ? _unityCamera.fieldOfView : 60f,
                RigType = CameraRigType.Space4X
            };
        }

        /// <summary>
        /// Set camera state from an external source (authoring/demo bootstrap).
        /// </summary>
        public void SetState(CameraRigState state)
        {
            _position = state.Position;
            _rotation = state.Rotation;
            _yaw = math.degrees(state.Yaw);
            _pitch = math.degrees(state.Pitch);
            PublishCurrentState();
        }
    }
}
