using UnityEngine;

namespace Space4X.DebugTools
{
    [RequireComponent(typeof(UnityEngine.Camera))]
    public class Space4XDebugLockCamera : MonoBehaviour
    {
        public Vector3 targetPosition = new Vector3(0f, 0f, 20f);
        public float cameraDistance = 20f;
        public float height = 10f;

        void LateUpdate()
        {
            // Simple: place camera at a fixed offset and look at targetPosition
            var offset = new Vector3(0f, height, -cameraDistance);
            transform.position = targetPosition + offset;
            transform.rotation = Quaternion.LookRotation(targetPosition - transform.position, Vector3.up);
        }
    }
}
