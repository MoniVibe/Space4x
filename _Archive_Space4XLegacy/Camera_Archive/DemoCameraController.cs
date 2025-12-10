#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace Space4X.Demo
{
    /// <summary>
    /// Lightweight WASD-style fly camera used in editor/demo builds only.
    /// Attach to an empty GameObject (or camera) inside demo scenes.
    /// </summary>
    [DisallowMultipleComponent]
    public class DemoCameraController : MonoBehaviour
    {
        [Header("Movement")]
        public float move = 20f;
        public float fast = 60f;
        public float zoom = 200f;

        [Header("Rotation")]
        public float yaw = 120f;
        public float pitch = 90f; // Reserved for future use (mouse pitch, etc.)

        private Camera cachedCamera;

        private void Awake()
        {
            cachedCamera = GetComponent<Camera>();

            if (cachedCamera == null && Camera.main == null)
            {
                cachedCamera = gameObject.AddComponent<Camera>();
            }
        }

        private void Update()
        {
            var cam = Camera.main ?? cachedCamera ?? GetComponent<Camera>();
            if (cam == null)
            {
                return;
            }

            cachedCamera = cam;

            var transform = cam.transform;
            float speed = (Input.GetKey(KeyCode.LeftShift) ? fast : move) * Time.unscaledDeltaTime;

            // Flat forward vector (ignores current pitch)
            Vector3 flatForward = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
            if (flatForward == Vector3.zero)
            {
                flatForward = Vector3.forward;
            }

            if (Input.GetKey(KeyCode.W)) transform.position += flatForward * speed;
            if (Input.GetKey(KeyCode.S)) transform.position -= flatForward * speed;
            if (Input.GetKey(KeyCode.A)) transform.position -= transform.right * speed;
            if (Input.GetKey(KeyCode.D)) transform.position += transform.right * speed;
            if (Input.GetKey(KeyCode.R)) transform.position += Vector3.up * speed;
            if (Input.GetKey(KeyCode.F)) transform.position -= Vector3.up * speed;

            if (Input.GetKey(KeyCode.Q))
            {
                transform.Rotate(0f, -yaw * Time.unscaledDeltaTime, 0f, Space.World);
            }

            if (Input.GetKey(KeyCode.E))
            {
                transform.Rotate(0f, yaw * Time.unscaledDeltaTime, 0f, Space.World);
            }

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > Mathf.Epsilon)
            {
                transform.position += transform.forward * (scroll * zoom * Time.unscaledDeltaTime);
            }
        }
    }
}
#endif

