using UnityEngine;

namespace PureDOTS.Camera
{
    /// <summary>
    /// MonoBehaviour that applies the latest CameraRigState to the actual Camera.
    /// This component should be attached to the main camera GameObject.
    ///
    /// Runs in LateUpdate with a high execution order (10000) to ensure it applies
    /// camera state after all other camera controllers have run.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public class CameraRigApplier : MonoBehaviour
    {
        [Header("Camera Reference")]
        [Tooltip("The camera to apply the rig state to. If null, uses Camera.main")]
        [SerializeField] private UnityEngine.Camera _targetCamera;

        [Header("Debugging")]
        [Tooltip("Log state application for debugging")]
        [SerializeField] private bool _debugLogging = false;

        private UnityEngine.Camera _cachedCamera;
        private int _lastAppliedFrame = -1;

        private void Awake()
        {
            // Ensure we have a camera reference
            if (_targetCamera == null)
            {
                _targetCamera = GetComponent<UnityEngine.Camera>();
            }

            if (_targetCamera == null)
            {
                _targetCamera = UnityEngine.Camera.main;
            }

            _cachedCamera = _targetCamera;

            if (_cachedCamera == null)
            {
                Debug.LogError("[CameraRigApplier] No camera found! Make sure there's a Camera component on this GameObject or marked as MainCamera.");
                enabled = false;
                return;
            }
        }

        private void LateUpdate()
        {
            if (_cachedCamera == null)
            {
                return;
            }

            // Only apply if there's a new state
            if (!CameraRigService.HasNewState())
            {
                return;
            }

            var state = CameraRigService.GetState(consume: true);

            // Apply position and rotation
            _cachedCamera.transform.position = state.Position;
            _cachedCamera.transform.rotation = state.Rotation;

            // Apply field of view if it's different
            if (!Mathf.Approximately(_cachedCamera.fieldOfView, state.FieldOfView))
            {
                _cachedCamera.fieldOfView = state.FieldOfView;
            }

            _lastAppliedFrame = Time.frameCount;

            if (_debugLogging || Time.frameCount % 60 == 0) // Log every second at 60fps or when debug logging enabled
            {
                Debug.Log($"[CameraRigApplier] Applied state: pos={state.Position:F1}, rot={state.Rotation.eulerAngles:F1}");
            }
        }

        private void OnEnable()
        {
            Debug.Log($"[CameraRigApplier] Enabled on {gameObject.name}");
            if (_debugLogging)
            {
                Debug.Log("[CameraRigApplier] Debug logging enabled - will log state applications.");
            }
        }

        private void OnDisable()
        {
            if (_debugLogging)
            {
                Debug.Log("[CameraRigApplier] Disabled.");
            }
        }

        /// <summary>
        /// Get the camera this applier is controlling.
        /// </summary>
        public UnityEngine.Camera GetCamera()
        {
            return _cachedCamera;
        }

        /// <summary>
        /// Get the frame when state was last applied.
        /// </summary>
        public int GetLastAppliedFrame()
        {
            return _lastAppliedFrame;
        }

        /// <summary>
        /// Force re-cache the camera reference.
        /// Useful if the camera component was added/removed at runtime.
        /// </summary>
        public void RecacheCamera()
        {
            Awake();
        }
    }
}
