using Unity.Entities;
using UnityEngine;
using Space4X.Registry;

namespace Space4X.Demo
{
    /// <summary>
    /// Ensures a camera input authoring exists at runtime (demo/dev only), instantiating a prefab if needed.
    /// Also ensures the Space4XCameraInputSystem MonoBehaviour component exists.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class EnsureCameraInputAuthoringRuntimeSystem : SystemBase
    {
        private bool _done;

        protected override void OnUpdate()
        {
            if (_done) return;

#if UNITY_2022_2_OR_NEWER
            var authoring = Object.FindFirstObjectByType<Space4XCameraInputAuthoring>(FindObjectsInactive.Include);
#else
            var authorings = Object.FindObjectsOfType<Space4XCameraInputAuthoring>(true);
            var authoring = authorings.Length > 0 ? authorings[0] : null;
#endif
            if (!authoring)
            {
                var prefab = Resources.Load<GameObject>("Space4X_Input");
                if (prefab)
                {
                    var go = Object.Instantiate(prefab);
                    go.name = "Space4X_Input";
                    Object.DontDestroyOnLoad(go);
                    Debug.Log("[EnsureCameraInputAuthoring] Instantiated Space4X_Input prefab for camera input.");
                }
                else
                {
                    Debug.LogWarning("[EnsureCameraInputAuthoring] Resources/Space4X_Input.prefab not found.");
                }
            }
            else
            {
                Debug.Log("[EnsureCameraInputAuthoring] Found existing Space4XCameraInputAuthoring; skipping prefab instantiation.");
            }

            // Ensure Space4XCameraInputSystem MonoBehaviour exists
            var inputSystem = Object.FindFirstObjectByType<Space4XCameraInputSystem>();
            if (!inputSystem)
            {
                var go = new GameObject("Space4XCameraInputSystem");
                go.AddComponent<Space4XCameraInputSystem>();
                Object.DontDestroyOnLoad(go);
                Debug.Log("[EnsureCameraInputAuthoring] Created Space4XCameraInputSystem MonoBehaviour.");
            }

            _done = true;
            Enabled = false;
        }
    }
}
