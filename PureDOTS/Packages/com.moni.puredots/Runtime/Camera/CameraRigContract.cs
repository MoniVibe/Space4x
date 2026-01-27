using UnityEngine;

namespace PureDOTS.Runtime.Camera
{
    /// <summary>
    /// CAMERA RIG CONTRACT - Usage Pattern for Game Projects
    ///
    /// This file demonstrates the standard pattern for game projects to implement
    /// camera controllers using the PureDOTS camera rig service.
    ///
    /// KEY PRINCIPLES:
    /// 1. Game code owns input interpretation and game flavor
    /// 2. PureDOTS owns the generic math that evolves CameraRigState
    /// 3. CameraRigService is the single source of truth for rig state
    /// 4. CameraRigApplier is the single point that mutates Camera.main
    ///
    /// Uses frame-time (Time.deltaTime).
    /// Not part of deterministic simulation / rewind.
    /// Safe utility for game projects to build cameras on.
    /// </summary>
    public static class CameraRigContract
    {
        /// <summary>
        /// EXAMPLE USAGE PATTERN - RTS Camera Controller
        ///
        /// This is how Space4X and Godgame should structure their camera controllers.
        /// </summary>
        public class ExampleRtsCameraController : MonoBehaviour
        {
            // Game-specific input interpretation
            [Header("Game-Specific Input")]
            [SerializeField] private float panSpeed = 10f;
            [SerializeField] private float rotateSpeed = 2f;
            [SerializeField] private float zoomSpeed = 5f;

            // Current camera state (game-specific)
            private Vector3 _position = new Vector3(0, 25, -30);
            private float _yaw;
            private float _pitch = 40f;
            private float _distance = 30f;

            private void Update()
            {
                // 1. Read game-specific input
                Vector2 panInput = new Vector2(UnityEngine.Input.GetAxis("Horizontal"), UnityEngine.Input.GetAxis("Vertical"));
                Vector2 rotateInput = new Vector2(UnityEngine.Input.GetAxis("Mouse X"), UnityEngine.Input.GetAxis("Mouse Y"));
                float zoomInput = UnityEngine.Input.GetAxis("Mouse ScrollWheel");

                // 2. Update game-specific camera state
                _position += transform.right * panInput.x * panSpeed * UnityEngine.Time.deltaTime;
                _position += transform.forward * panInput.y * panSpeed * UnityEngine.Time.deltaTime;

                if (UnityEngine.Input.GetMouseButton(1)) // Right mouse button held
                {
                    _yaw += rotateInput.x * rotateSpeed;
                    _pitch = Mathf.Clamp(_pitch - rotateInput.y * rotateSpeed, -80f, 80f);
                }

                _distance = Mathf.Clamp(_distance - zoomInput * zoomSpeed, 5f, 200f);

                // 3. Compute new camera transform (game-specific math)
                Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
                Vector3 offset = rotation * Vector3.back * _distance;
                Vector3 cameraPosition = _position + offset;

                // 4. Create CameraRigState (PureDOTS contract)
                CameraRigState rigState = new CameraRigState
                {
                    Focus = _position,
                    Pitch = _pitch,
                    Yaw = _yaw,
                    Roll = 0f,
                    Distance = _distance,
                    Mode = CameraRigMode.Orbit,
                    PerspectiveMode = true,
                    FieldOfView = 60f,
                    RigType = CameraRigType.Space4X // or Godgame
                };

                // 5. Publish to CameraRigService (PureDOTS contract)
                CameraRigService.Publish(rigState);

                // CameraRigApplier will automatically apply this in LateUpdate
            }
        }

        /// <summary>
        /// CONTRACT SUMMARY
        ///
        /// Game Code Responsibilities:
        /// - Interpret game-specific input (WASD, mouse, gamepad, touch)
        /// - Implement game-specific camera behaviors (bounds, modes, transitions)
        /// - Maintain canonical camera state (Focus/Yaw/Pitch/Distance)
        /// - Fill CameraRigState struct with the canonical state and publish it
        /// - Call CameraRigService.Publish(state) when state changes
        ///
        /// PureDOTS Responsibilities:
        /// - Store authoritative CameraRigState (CameraRigService)
        /// - Apply CameraRigState to Camera.main (CameraRigApplier)
        /// - Provide reusable camera behaviors (BW2StyleCameraController)
        /// - Ensure no conflicts between multiple camera rigs
        ///
        /// Separation Benefits:
        /// - Game code stays game-specific and flexible
        /// - PureDOTS provides stable, reusable camera infrastructure
        /// - Clear contract prevents accidental coupling
        /// - Easy to test camera logic in isolation
        /// </summary>
    }
}
