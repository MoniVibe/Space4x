using UnityEngine;

namespace PureDOTS.Runtime.Camera
{
    /// <summary>
    /// Optional bridge for HUD/debug code that need CameraRigState without
    /// directly touching CameraRigService statics.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraRigTelemetryBridge : MonoBehaviour
    {
        private CameraRigState _latestState;
        private bool _hasState;

        /// <summary>Returns the latest published camera rig state, if available.</summary>
        public bool TryGetLatestState(out CameraRigState state)
        {
            state = _latestState;
            return _hasState;
        }

        private void OnEnable()
        {
            CameraRigService.CameraStateChanged += HandleCameraStateChanged;
            if (CameraRigService.TryGetState(out var state))
            {
                _latestState = state;
                _hasState = true;
            }
        }

        private void OnDisable()
        {
            CameraRigService.CameraStateChanged -= HandleCameraStateChanged;
        }

        private void HandleCameraStateChanged(CameraRigState state)
        {
            _latestState = state;
            _hasState = true;
        }
    }
}


