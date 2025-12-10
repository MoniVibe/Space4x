using UnityEngine;

namespace Space4X.Camera
{
    /// <summary>
    /// Orbit rig configuration carrier for the Space4X camera.
    /// </summary>
    [DisallowMultipleComponent]
    public class Space4XCameraAuthoring : MonoBehaviour
    {
        [Header("Camera")]
        public UnityEngine.Camera targetCamera;

        [Header("Orbit Defaults")]
        public Vector3 focusPoint = Vector3.zero;
        public float yawDegrees = 0f;
        public float pitchDegrees = 45f;
        public float distance = 60f;

        [Header("Limits")]
        public float minDistance = 5f;
        public float maxDistance = 300f;
        public float minPitch = 5f;
        public float maxPitch = 85f;
    }
}



