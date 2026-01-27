using UnityEngine;
using UnityEngineCamera = UnityEngine.Camera;

namespace PureDOTS.Runtime.Camera
{
    /// <summary>
    /// CAMERA RIG CONTRACT - CameraRigApplier
    ///
    /// The single point of truth for applying camera rig state to Unity Cameras.
    /// This is the ONLY component allowed to mutate Camera.main transform.
    ///
    /// CONTRACT GUARANTEES:
    /// - Reads CameraRigState from CameraRigService
    /// - Derives Position+Rotation from canonical state and applies via SetPositionAndRotation (atomic operation)
    /// - Applies FieldOfView if specified (> 0.01f)
    /// - Runs in LateUpdate at ExecutionOrder 10000 (after all other updates)
    /// - Fallback to Camera.main if no camera attached
    ///
    /// USAGE PATTERN:
    /// 1. Attach to a GameObject with a Camera component
    /// 2. Game camera controllers call CameraRigService.Publish(state)
    /// 3. This component automatically applies the state in LateUpdate
    /// 4. Result: Camera follows the published rig state
    ///
    /// Uses frame-time (Time.deltaTime) - runs in LateUpdate.
    /// Not part of deterministic simulation / rewind.
    /// Safe utility for game projects to build cameras on.
    /// </summary>
    [RequireComponent(typeof(UnityEngineCamera))]
    [DefaultExecutionOrder(10000)]
    public sealed class CameraRigApplier : MonoBehaviour
    {
        private UnityEngineCamera _camera;

        private void Awake()
        {
            _camera = GetComponent<UnityEngineCamera>();
        }

        private void LateUpdate()
        {
            if (!CameraRigService.HasState)
            {
                return;
            }

            var state = CameraRigService.Current;
            if (state.RigType == CameraRigType.None)
            {
                return;
            }

            if (_camera == null)
            {
                _camera = UnityEngineCamera.main;
                if (_camera == null)
                {
                    return;
                }
            }

            CameraRigMath.DerivePose(in state, out var position, out var rotation);

            // Atomic position+rotation update
            _camera.transform.SetPositionAndRotation(position, rotation);

            // Optional field of view update
            if (state.FieldOfView > 0.01f)
            {
                _camera.fieldOfView = state.FieldOfView;
            }
        }
    }
}
