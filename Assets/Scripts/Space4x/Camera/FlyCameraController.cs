using UnityEngine;
using UCamera = UnityEngine.Camera;
using UObject = UnityEngine.Object;
using UTime = UnityEngine.Time;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Space4X.Camera
{
    /// <summary>
    /// Free-fly RTS-style camera:
    /// - WASD: horizontal move (relative to view).
    /// - Q/E: vertical move.
    /// - MMB drag: yaw / pitch.
    /// - LMB drag: pan.
    /// </summary>
    [RequireComponent(typeof(UCamera))]
    public class FlyCameraController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed      = 30f;
        [SerializeField] private float moveSpeedShift = 90f;
        [SerializeField] private float verticalSpeed  = 25f;

        [Header("Rotation")]
        [SerializeField] private float rotateSpeed    = 0.15f;
        [SerializeField] private float pitchMin       = -89f;
        [SerializeField] private float pitchMax       =  89f;

        [Header("Panning")]
        [SerializeField] private float panSpeed       = 0.2f;

        private Vector3 _lastMousePos;
        private float _yaw;
        private float _pitch;

        private void Start()
        {
            var euler = transform.eulerAngles;
            _yaw   = euler.y;
            _pitch = euler.x;
        }

        private void Update()
        {
            float deltaTime = UTime.unscaledDeltaTime; // camera should ignore sim timescale by default

            HandleMovement(deltaTime);
            HandleMouse(deltaTime);
        }

        private void HandleMovement(float dt)
        {
            float speed = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                ? moveSpeedShift
                : moveSpeed;

            float h = Input.GetAxisRaw("Horizontal"); // A/D or ←/→
            float v = Input.GetAxisRaw("Vertical");   // W/S or ↑/↓

            // Horizontal movement in camera's local space
            Vector3 move = Vector3.zero;
            move += transform.forward * v;
            move += transform.right   * h;
            move.y = 0f;
            move.Normalize();

            // Vertical movement
            float upDown = 0f;
            if (Input.GetKey(KeyCode.Q))
                upDown -= 1f;
            if (Input.GetKey(KeyCode.E))
                upDown += 1f;

            Vector3 worldMove = move * speed * dt;
            worldMove.y += upDown * verticalSpeed * dt;

            transform.position += worldMove;
        }

        private void HandleMouse(float dt)
        {
            Vector3 mousePos = Input.mousePosition;

            bool mmb = Input.GetMouseButton(2); // middle
            bool lmb = Input.GetMouseButton(0); // left

            if (!mmb && !lmb)
            {
                _lastMousePos = mousePos;
                return;
            }

            Vector3 delta = mousePos - _lastMousePos;
            _lastMousePos = mousePos;

            if (mmb)
            {
                // Rotate camera
                _yaw   += delta.x * rotateSpeed;
                _pitch -= delta.y * rotateSpeed;
                _pitch  = Mathf.Clamp(_pitch, pitchMin, pitchMax);

                Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
                transform.rotation = rot;
            }
            else if (lmb)
            {
                // Pan in camera plane
                Vector3 right = transform.right;
                Vector3 up    = Vector3.up; // world up

                Vector3 pan = (-right * delta.x + -up * delta.y) * panSpeed * dt;
                transform.position += pan;
            }
        }
    }
}

