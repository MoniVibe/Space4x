using UnityEngine;
using UnityEngine.InputSystem;

namespace PureDOTS.Input
{
    /// <summary>
    /// Semantic profile describing camera input actions and fallback bindings.
    /// </summary>
    [CreateAssetMenu(
        fileName = "CameraInputProfile",
        menuName = "PureDOTS/Input/Camera Input Profile",
        order = 1)]
    public sealed class CameraInputProfile : ScriptableObject
    {
        [Header("Input Asset")]
        [Tooltip("InputActionAsset containing camera actions. If null, defaults will be generated at runtime.")]
        [SerializeField] private InputActionAsset inputActions;

        [Tooltip("Name of the action map that contains camera actions (e.g. 'Camera').")]
        [SerializeField] private string actionMapName = "Camera";

        [Header("Semantic Action Names")]
        [SerializeField] private string moveActionName = "Move";
        [SerializeField] private string verticalActionName = "Vertical";
        [SerializeField] private string orbitActionName = "Orbit";
        [SerializeField] private string panActionName = "Pan";
        [SerializeField] private string zoomActionName = "Zoom";
        [SerializeField] private string focusActionName = "Focus";
        [SerializeField] private string yAxisLockToggleActionName = "YAxisLockToggle";

        [Header("Default Bindings (Used when action is missing)")]
        [SerializeField] private string defaultMoveBinding = "2DVector(mode=2)";
        [SerializeField] private string defaultVerticalBinding = "<Keyboard>/e";
        [SerializeField] private string defaultVerticalNegativeBinding = "<Keyboard>/q";
        [SerializeField] private string defaultOrbitBinding = "<Mouse>/delta";
        [SerializeField] private string defaultPanBinding = "<Mouse>/delta";
        [SerializeField] private string defaultZoomBinding = "<Mouse>/scroll/y";
        [SerializeField] private string defaultFocusBinding = "<Keyboard>/f";
        [SerializeField] private string defaultYAxisLockToggleBinding = "<Keyboard>/y";

        public InputActionAsset InputActions => inputActions;
        public string ActionMapName => actionMapName;
        public string MoveActionName => moveActionName;
        public string VerticalActionName => verticalActionName;
        public string OrbitActionName => orbitActionName;
        public string PanActionName => panActionName;
        public string ZoomActionName => zoomActionName;
        public string FocusActionName => focusActionName;
        public string YAxisLockToggleActionName => yAxisLockToggleActionName;

        public string DefaultMoveBinding => defaultMoveBinding;
        public string DefaultVerticalBinding => defaultVerticalBinding;
        public string DefaultVerticalNegativeBinding => defaultVerticalNegativeBinding;
        public string DefaultOrbitBinding => defaultOrbitBinding;
        public string DefaultPanBinding => defaultPanBinding;
        public string DefaultZoomBinding => defaultZoomBinding;
        public string DefaultFocusBinding => defaultFocusBinding;
        public string DefaultYAxisLockToggleBinding => defaultYAxisLockToggleBinding;
    }
}


