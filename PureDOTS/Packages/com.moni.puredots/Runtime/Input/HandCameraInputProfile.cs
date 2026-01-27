using UnityEngine;
using UnityEngine.InputSystem;

namespace PureDOTS.Input
{
    [CreateAssetMenu(
        fileName = "HandCameraInputProfile",
        menuName = "PureDOTS/Input/Hand Camera Input Profile",
        order = 0)]
    public sealed class HandCameraInputProfile : ScriptableObject
    {
        [Header("Input Asset")]
        [SerializeField] InputActionAsset inputActions;
        [SerializeField] string actionMapName = "HandCamera";

        [Header("Router")]
        [SerializeField, Min(0f)] float handlerCooldownSeconds = 0.1f;
        [SerializeField, Min(0)] int hysteresisFrames = 3;
        [SerializeField] bool logTransitions;

        [Header("Masks & Raycast")]
        [SerializeField] LayerMask interactionMask = ~0;
        [SerializeField] LayerMask groundMask = ~0;
        [SerializeField] LayerMask storehouseMask = 0;
        [SerializeField] LayerMask pileMask = 0;
        [SerializeField] LayerMask draggableMask = 0;
        [SerializeField, Min(0.1f)] float maxRayDistance = 800f;

        [Header("Action Names")]
        [SerializeField] string moveActionName = "Move";
        [SerializeField] string verticalActionName = "Vertical";
        [SerializeField] string yAxisLockToggleActionName = "YAxisLockToggle";
        [SerializeField] string orbitActionName = "Orbit";
        [SerializeField] string panActionName = "Pan";
        [SerializeField] string zoomActionName = "Zoom";

        [Header("Default Bindings (Optional)")]
        [SerializeField] string defaultMoveBinding = "<Keyboard>/wasd";
        [SerializeField] string defaultVerticalBinding = "<Keyboard>/qe";
        [SerializeField] string defaultYAxisLockToggleBinding = "<Keyboard>/leftAlt";
        [SerializeField] string defaultOrbitBinding = "<Mouse>/delta";
        [SerializeField] string defaultPanBinding = "<Mouse>/delta";
        [SerializeField] string defaultZoomBinding = "<Mouse>/scroll";

        public InputActionAsset InputActions => inputActions;
        public string ActionMapName => actionMapName;
        public float HandlerCooldownSeconds => Mathf.Max(0f, handlerCooldownSeconds);
        public int HysteresisFrames => Mathf.Max(0, hysteresisFrames);
        public bool LogTransitions => logTransitions;
        public LayerMask InteractionMask => interactionMask;
        public LayerMask GroundMask => groundMask;
        public LayerMask StorehouseMask => storehouseMask;
        public LayerMask PileMask => pileMask;
        public LayerMask DraggableMask => draggableMask;
        public float MaxRayDistance => Mathf.Max(0.1f, maxRayDistance);

        public string MoveActionName => moveActionName;
        public string VerticalActionName => verticalActionName;
        public string YAxisLockToggleActionName => yAxisLockToggleActionName;
        public string OrbitActionName => orbitActionName;
        public string PanActionName => panActionName;
        public string ZoomActionName => zoomActionName;

        public string DefaultMoveBinding => defaultMoveBinding;
        public string DefaultVerticalBinding => defaultVerticalBinding;
        public string DefaultYAxisLockToggleBinding => defaultYAxisLockToggleBinding;
        public string DefaultOrbitBinding => defaultOrbitBinding;
        public string DefaultPanBinding => defaultPanBinding;
        public string DefaultZoomBinding => defaultZoomBinding;
    }
}
