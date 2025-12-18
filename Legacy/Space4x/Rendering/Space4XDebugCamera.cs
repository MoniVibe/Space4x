using UnityEngine;
using UnityEngine.InputSystem;

namespace Space4X.Rendering
{
    [DisallowMultipleComponent]
    public sealed class Space4XDebugCamera : MonoBehaviour
    {
        [Header("Target world position of pinned debug RenderKey")]
        public Vector3 targetPosition = new Vector3(0f, 0f, 20f);

        [Header("Initial camera offset from target")]
        public Vector3 offset = new Vector3(0f, 10f, -20f);

        [Header("Simple orbit controls")]
        public float orbitSpeed = 120f;
        public float zoomSpeed = 20f;
        public float minDistance = 5f;
        public float maxDistance = 100f;

        float _distance;
        float _yaw;
        float _pitch;

        void Start()
        {
            _distance = offset.magnitude;
            var dir = offset.normalized;

            _pitch = Mathf.Asin(dir.y) * Mathf.Rad2Deg;
            _yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

            UpdateCameraTransform();
        }

        void LateUpdate()
        {
            if (Mouse.current == null) return;

            float dt = Time.deltaTime;

            if (Mouse.current.rightButton.isPressed)
            {
                float dx = Mouse.current.delta.x.ReadValue() * 0.1f; // Scale down delta
                float dy = -Mouse.current.delta.y.ReadValue() * 0.1f;

                _yaw += dx * orbitSpeed * dt;
                _pitch += dy * orbitSpeed * dt;
                _pitch = Mathf.Clamp(_pitch, -80f, 80f);
            }

            float scroll = Mouse.current.scroll.ReadValue().y / 120f; // Normalize scroll
            if (Mathf.Abs(scroll) > 0.001f)
            {
                _distance -= scroll * zoomSpeed * dt;
                _distance = Mathf.Clamp(_distance, minDistance, maxDistance);
            }

            UpdateCameraTransform();
        }

        void UpdateCameraTransform()
        {
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 camPos = targetPosition + rot * new Vector3(0f, 0f, -_distance);
            transform.position = camPos;
            transform.rotation = Quaternion.LookRotation(targetPosition - camPos, Vector3.up);
        }
    }
}
