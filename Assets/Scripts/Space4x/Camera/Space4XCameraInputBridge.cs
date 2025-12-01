using UnityEngine;
using UnityEngine.InputSystem;
namespace Space4X.CameraSystem
{
    /// <summary>
    /// MonoBehaviour responsible for capturing Space4X camera input and exposing a snapshot each frame.
    /// This bridge reads from Unity's Input System (new or legacy) and provides a clean API
    /// for camera controllers to consume input state.
    /// </summary>
    public class Space4XCameraInputBridge : MonoBehaviour
    {
        /// <summary>
        /// Input snapshot structure containing all camera input state for a frame.
        /// </summary>
        public struct Snapshot
        {
            /// <summary>
            /// Pan input as a 2D vector (X = left/right, Y = forward/back)
            /// </summary>
            public Vector2 Pan;

            /// <summary>
            /// Vertical movement input (Q/E keys)
            /// </summary>
            public float Vertical;

            /// <summary>
            /// Rotation input as mouse delta when RMB is held
            /// </summary>
            public Vector2 Rotate;

            /// <summary>
            /// Zoom input from scroll wheel (positive = zoom out, negative = zoom in)
            /// </summary>
            public float Zoom;

            /// <summary>
            /// Whether reset was requested (R key)
            /// </summary>
            public bool ResetRequested;

            /// <summary>
            /// Whether perspective mode toggle was requested (T key, one-shot)
            /// </summary>
            public bool TogglePerspectiveMode;

            /// <summary>
            /// Speed multiplier based on shift keys (1.0 = normal, 3.0 = fast)
            /// </summary>
            public float SpeedMultiplier;

            /// <summary>
            /// Frame count when this snapshot was taken
            /// </summary>
            public int Frame;
        }

        private static Space4XCameraInputBridge _instance;
        private static Snapshot _latestSnapshot;
        private static bool _hasSnapshotThisFrame;

        [Header("Sensitivity")]
        [Tooltip("Mouse sensitivity for camera rotation")]
        [SerializeField] private float _mouseSensitivity = 1f;
        [Tooltip("Scroll sensitivity for zoom")]
        [SerializeField] private float _scrollSensitivity = 1f;
        [Tooltip("Normal movement speed multiplier")]
        [SerializeField] private float _normalSpeed = 1f;
        [Tooltip("Fast movement speed multiplier when Shift is held")]
        [SerializeField] private float _fastSpeed = 3f;

        // Tracking for mouse state
        private bool _wasRightMouseHeld;
        private Vector2 _lastMousePosition;

        [Header("Debugging")]
        [Tooltip("Enable debug logging")]
        [SerializeField] private bool _debugLogging = false;

        private void Awake()
        {
            // Singleton pattern - ensure only one instance exists
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[Space4XCameraInputBridge] Multiple instances detected, destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            _instance = this;

            // Ensure this object persists across scene loads
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }

            Debug.Log("[Space4XCameraInputBridge] Initialized and ready to capture input.");
        }

        private void Update()
        {
            UpdateSnapshot();
        }

        private void UpdateSnapshot()
        {
            var snapshot = new Snapshot
            {
                Pan = Vector2.zero,
                Vertical = 0f,
                Rotate = Vector2.zero,
                Zoom = 0f,
                ResetRequested = false,
                TogglePerspectiveMode = false,
                SpeedMultiplier = 1f,
                Frame = Time.frameCount
            };

            // ---------- KEYBOARD (Input System → legacy) ----------

            Keyboard kb = null;
            try { kb = Keyboard.current; } catch { }

            if (kb != null)
            {
                if (kb.wKey.isPressed) snapshot.Pan.y += 1f;
                if (kb.sKey.isPressed) snapshot.Pan.y -= 1f;
                if (kb.aKey.isPressed) snapshot.Pan.x -= 1f;
                if (kb.dKey.isPressed) snapshot.Pan.x += 1f;

                if (kb.qKey.isPressed) snapshot.Vertical -= 1f;
                if (kb.eKey.isPressed) snapshot.Vertical += 1f;

                if (kb.tKey.wasPressedThisFrame) snapshot.TogglePerspectiveMode = true;

                if (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed)
                    snapshot.SpeedMultiplier = 3f;
            }
            else
            {
                // Legacy keyboard fallback
                if (Input.GetKey(KeyCode.W)) snapshot.Pan.y += 1f;
                if (Input.GetKey(KeyCode.S)) snapshot.Pan.y -= 1f;
                if (Input.GetKey(KeyCode.A)) snapshot.Pan.x -= 1f;
                if (Input.GetKey(KeyCode.D)) snapshot.Pan.x += 1f;

                if (Input.GetKey(KeyCode.Q)) snapshot.Vertical -= 1f;
                if (Input.GetKey(KeyCode.E)) snapshot.Vertical += 1f;

                if (Input.GetKeyDown(KeyCode.T)) snapshot.TogglePerspectiveMode = true;

                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    snapshot.SpeedMultiplier = 3f;
            }

            // ---------- MOUSE (Input System → legacy) ----------

            Mouse mouse = null;
            try { mouse = Mouse.current; } catch { }

            if (mouse != null)
            {
                // Scroll (zoom)
                var scroll = mouse.scroll.ReadValue();
                snapshot.Zoom -= scroll.y; // scroll down = zoom in

                // MMB rotation
                if (mouse.middleButton.isPressed)
                {
                    var delta = mouse.delta.ReadValue();
                    snapshot.Rotate += delta;
                }
            }
            else
            {
                // Legacy mouse fallback
                snapshot.Zoom -= Input.mouseScrollDelta.y;

                if (Input.GetMouseButton(2)) // MMB held
                {
                    var delta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
                    snapshot.Rotate += delta;
                }
            }

            // ---------- Store snapshot ----------

            _latestSnapshot = snapshot;
            _hasSnapshotThisFrame = true;
        }

        /// <summary>
        /// Try to get the current input snapshot.
        /// </summary>
        /// <param name="snapshot">The snapshot to populate</param>
        /// <returns>True if a snapshot is available</returns>
        public static bool TryGetSnapshot(out Snapshot snapshot)
        {
            if (!_hasSnapshotThisFrame)
            {
                snapshot = default;
                return false;
            }

            snapshot = _latestSnapshot;
            return true;
        }

        /// <summary>
        /// Mark the current snapshot as consumed.
        /// </summary>
        public static void ConsumeSnapshot()
        {
            _hasSnapshotThisFrame = false;
        }

        private bool CheckNewInputSystemAvailable()
        {
            // Check if new Input System is available by trying to access Keyboard.current
            try
            {
                var keyboard = Keyboard.current;
                return keyboard != null;
            }
            catch
            {
                return false;
            }
        }

        private Vector2 GetWASDInputNew()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return Vector2.zero;

            Vector2 pan = Vector2.zero;

            if (keyboard.wKey.isPressed) pan.y += 1f;
            if (keyboard.sKey.isPressed) pan.y -= 1f;
            if (keyboard.aKey.isPressed) pan.x -= 1f;
            if (keyboard.dKey.isPressed) pan.x += 1f;

            return pan.normalized; // Normalize to prevent faster diagonal movement
        }

        private Vector2 GetWASDInputLegacy()
        {
            Vector2 pan = Vector2.zero;

            if (Input.GetKey(KeyCode.W)) pan.y += 1f;
            if (Input.GetKey(KeyCode.S)) pan.y -= 1f;
            if (Input.GetKey(KeyCode.A)) pan.x -= 1f;
            if (Input.GetKey(KeyCode.D)) pan.x += 1f;

            return pan.normalized;
        }

        private float GetVerticalInput(bool useNewInput)
        {
            if (useNewInput)
            {
                var keyboard = Keyboard.current;
                if (keyboard == null) return 0f;

                float vertical = 0f;
                if (keyboard.qKey.isPressed) vertical -= 1f; // Q = down
                if (keyboard.eKey.isPressed) vertical += 1f; // E = up

                return vertical;
            }
            else
            {
                float vertical = 0f;
                if (Input.GetKey(KeyCode.Q)) vertical -= 1f;
                if (Input.GetKey(KeyCode.E)) vertical += 1f;

                return vertical;
            }
        }

        private Vector2 GetRotationInput(bool useNewInput)
        {
            if (useNewInput)
            {
                var mouse = Mouse.current;
                if (mouse == null) return Vector2.zero;

                bool rightMouseHeld = mouse.rightButton.isPressed;

                if (rightMouseHeld)
                {
                    Vector2 mouseDelta = mouse.delta.ReadValue() * _mouseSensitivity;

                    // Update tracking
                    _wasRightMouseHeld = true;
                    _lastMousePosition = mouse.position.ReadValue();

                    return mouseDelta;
                }
                else
                {
                    _wasRightMouseHeld = false;
                    return Vector2.zero;
                }
            }
            else
            {
                if (Input.GetMouseButton(1)) // Right mouse button
                {
                    Vector2 mouseDelta = new Vector2(
                        Input.GetAxis("Mouse X"),
                        Input.GetAxis("Mouse Y")
                    ) * _mouseSensitivity;

                    _wasRightMouseHeld = true;
                    return mouseDelta;
                }
                else
                {
                    _wasRightMouseHeld = false;
                    return Vector2.zero;
                }
            }
        }

        private float GetZoomInput(bool useNewInput)
        {
            if (useNewInput)
            {
                var mouse = Mouse.current;
                if (mouse == null) return 0f;

                // Scroll delta - invert so scrolling down (negative) zooms in (positive zoom input)
                float scrollY = mouse.scroll.ReadValue().y;
                return -scrollY * _scrollSensitivity;
            }
            else
            {
                float scrollY = Input.mouseScrollDelta.y;
                return -scrollY * _scrollSensitivity;
            }
        }

        private bool GetResetInput(bool useNewInput)
        {
            if (useNewInput)
            {
                var keyboard = Keyboard.current;
                return keyboard != null && keyboard.rKey.wasPressedThisFrame;
            }
            else
            {
                return Input.GetKeyDown(KeyCode.R);
            }
        }

        private bool GetTPress(bool useNewInput)
        {
            if (useNewInput)
            {
                var keyboard = Keyboard.current;
                return keyboard != null && keyboard.tKey.isPressed;
            }
            else
            {
                return Input.GetKey(KeyCode.T);
            }
        }

        private float GetSpeedMultiplier(bool useNewInput)
        {
            bool shiftHeld = false;

            if (useNewInput)
            {
                var keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    shiftHeld = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
                }
            }
            else
            {
                shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            }

            return shiftHeld ? _fastSpeed : _normalSpeed;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                Debug.Log("[Space4XCameraInputBridge] Destroyed.");
            }
        }

        /// <summary>
        /// Get the current instance (useful for debugging)
        /// </summary>
        public static Space4XCameraInputBridge Instance => _instance;
    }
}
