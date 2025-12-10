using UnityEngine;
using UnityEngine.InputSystem;

namespace Space4X.Camera
{
    /// <summary>
    /// Input wiring for the Space4X orbit rig.
    /// </summary>
    [DisallowMultipleComponent]
    public class Space4XCameraInputAuthoring : MonoBehaviour
    {
        [Header("Input Actions")]
        public InputActionReference orbitAction;
        public InputActionReference panAction;
        public InputActionReference zoomAction;

        [Header("Sensitivity")]
        public float orbitDegreesPerSecond = 120f;
        public float panUnitsPerSecond = 25f;
        public float zoomUnitsPerSecond = 35f;
    }
}



