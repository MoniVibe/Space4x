using UnityEngine;

namespace Space4X.CameraSystem
{
    /// <summary>
    /// Bootstrap component that ensures the Space4X camera input bridge is initialized early.
    /// Add this to a GameObject that loads early in your scene (like the main camera or a bootstrap object).
    /// </summary>
    public class Space4XCameraBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeOnLoad()
        {
            // Force creation of input bridge singleton during scene loading
            // This ensures it exists before any camera controllers try to use it
            Space4XCameraInputBridge.TryGetSnapshot(out _);
        }

        private void Awake()
        {
            // Also ensure it's created in Awake if the runtime initialize didn't work
            Space4XCameraInputBridge.TryGetSnapshot(out _);
        }
    }
}
