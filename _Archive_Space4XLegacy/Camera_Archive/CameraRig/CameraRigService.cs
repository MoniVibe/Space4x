using UnityEngine;

namespace PureDOTS.Camera
{
    /// <summary>
    /// Central camera state manager providing a static API for camera controllers
    /// to publish their state and for appliers to consume it.
    ///
    /// This service acts as the communication bridge between camera controllers
    /// (which run in Update) and camera appliers (which run in LateUpdate).
    /// </summary>
    public static class CameraRigService
    {
        private static CameraRigState _currentState = CameraRigState.Default;
        private static bool _hasNewState = false;
        private static int _lastUpdateFrame = -1;

        /// <summary>
        /// Publish a new camera rig state. This should be called by camera controllers
        /// in their Update() method. The state will be applied to the camera in LateUpdate.
        /// </summary>
        /// <param name="state">The new camera rig state to apply</param>
        public static void SetState(CameraRigState state)
        {
            _currentState = state;
            _hasNewState = true;
            _lastUpdateFrame = Time.frameCount;
        }

        /// <summary>
        /// Get the current camera rig state. This should be called by camera appliers
        /// in their LateUpdate() method to apply the state to the actual Camera.
        /// </summary>
        /// <param name="consume">Whether to mark this state as consumed (prevent re-application)</param>
        /// <returns>The current camera rig state</returns>
        public static CameraRigState GetState(bool consume = true)
        {
            if (consume)
            {
                _hasNewState = false;
            }
            return _currentState;
        }

        /// <summary>
        /// Check if there's a new unconsumed state available.
        /// Useful for appliers to know if they need to update the camera.
        /// </summary>
        /// <returns>True if there's a new state that hasn't been consumed yet</returns>
        public static bool HasNewState()
        {
            return _hasNewState;
        }

        /// <summary>
        /// Get the frame number when the current state was last updated.
        /// Useful for debugging and ensuring state freshness.
        /// </summary>
        /// <returns>The frame count when SetState was last called</returns>
        public static int GetLastUpdateFrame()
        {
            return _lastUpdateFrame;
        }

        /// <summary>
        /// Reset the service to its default state.
        /// Useful for initialization or when switching scenes.
        /// </summary>
        public static void Reset()
        {
            _currentState = CameraRigState.Default;
            _hasNewState = false;
            _lastUpdateFrame = -1;
        }

        /// <summary>
        /// Initialize the service with a specific camera rig type.
        /// This sets up the default state with the appropriate rig type.
        /// </summary>
        /// <param name="rigType">The camera rig type to initialize with</param>
        public static void Initialize(CameraRigType rigType)
        {
            var defaultState = CameraRigState.Default;
            defaultState.RigType = rigType;
            _currentState = defaultState;
            _hasNewState = true;
            _lastUpdateFrame = Time.frameCount;
        }
    }
}
