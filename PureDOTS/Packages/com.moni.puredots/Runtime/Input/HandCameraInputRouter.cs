using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

using PureDOTS.Runtime.Camera;

namespace PureDOTS.Input
{
    [DefaultExecutionOrder(-120)]
    [DisallowMultipleComponent]
    public sealed class HandCameraInputRouter : MonoBehaviour
    {
        [Header("Profile")]
        [SerializeField] HandCameraInputProfile profile;

        [Header("Input Actions")]
        [SerializeField] InputActionAsset inputActions;
        [SerializeField] string actionMapName = "HandCamera";

        [Header("Raycast")]
        [SerializeField] Camera raycastCamera;
        [SerializeField] LayerMask interactionMask = ~0;
        [SerializeField] LayerMask groundMask = ~0;
        [SerializeField] LayerMask storehouseMask = 0;
        [SerializeField] LayerMask pileMask = 0;
        [SerializeField] LayerMask draggableMask = 0;
        [SerializeField] float maxRayDistance = 800f;

        [Header("Router Behaviour")]
        [SerializeField, Tooltip("Seconds new handlers are blocked after a cancel to avoid bouncing.")] float handlerCooldownSeconds = 0.1f;
        [SerializeField, Tooltip("Frames to retain the current handler when priorities tie.")] int hysteresisFrames = 2;
        [SerializeField] bool logTransitions;

        InputActionMap _map;
        InputAction _pointerAction;
        InputAction _pointerDeltaAction;
        InputAction _leftClickAction;
        InputAction _middleClickAction;
        InputAction _rightClickAction;
        InputAction _scrollAction;

        readonly List<IRmbHandler> _handlers = new();
        IRmbHandler _activeHandler;
        float _cooldownRemaining;
        int _hysteresisRemaining;
        RmbContext _currentContext;
        Vector2 _pointerPosition;
        Vector2 _pointerDelta;
        Vector2 _scrollValue;
        bool _handHasCargo;

        static readonly RaycastHit s_emptyHit = new();

        InputActionAsset ResolvedInputActions => profile != null && profile.InputActions != null ? profile.InputActions : inputActions;
        string ResolvedActionMapName => profile != null && !string.IsNullOrEmpty(profile.ActionMapName) ? profile.ActionMapName : actionMapName;
        float ResolvedHandlerCooldown => profile != null ? profile.HandlerCooldownSeconds : handlerCooldownSeconds;
        int ResolvedHysteresisFrames => profile != null ? profile.HysteresisFrames : hysteresisFrames;
        bool ResolvedLogTransitions => profile != null ? profile.LogTransitions : logTransitions;
        LayerMask ResolvedInteractionMask => profile != null ? profile.InteractionMask : interactionMask;
        LayerMask ResolvedGroundMask => profile != null ? profile.GroundMask : groundMask;
        LayerMask ResolvedStorehouseMask => profile != null ? profile.StorehouseMask : storehouseMask;
        LayerMask ResolvedPileMask => profile != null ? profile.PileMask : pileMask;
        LayerMask ResolvedDraggableMask => profile != null ? profile.DraggableMask : draggableMask;
        float ResolvedMaxRayDistance => profile != null ? profile.MaxRayDistance : maxRayDistance;

        public RmbContext CurrentContext => _currentContext;
        public LayerMask InteractionMask => ResolvedInteractionMask;
        public LayerMask GroundMask => ResolvedGroundMask;
        public LayerMask StorehouseMask => ResolvedStorehouseMask;
        public LayerMask PileMask => ResolvedPileMask;
        public LayerMask DraggableMask => ResolvedDraggableMask;
        public float MaxRayDistance => ResolvedMaxRayDistance;
        public Vector2 PointerPosition => _pointerPosition;
        public Vector2 PointerDelta => _pointerDelta;
        public Vector2 ScrollValue => _scrollValue;

        public InputAction LeftClickAction => _leftClickAction;
        public InputAction MiddleClickAction => _middleClickAction;
        public InputAction RightClickAction => _rightClickAction;

        /// <summary>
        /// Tries to get the active router in the scene and return its current context.
        /// </summary>
        public static bool TryGetContext(out RmbContext context)
        {
            var router = FindFirstObjectByType<HandCameraInputRouter>();
            if (router != null)
            {
                context = router.CurrentContext;
                return true;
            }

            context = default;
            return false;
        }

        public void RegisterHandler(IRmbHandler handler)
        {
            if (handler == null || _handlers.Contains(handler)) return;
            _handlers.Add(handler);
            _handlers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        public void UnregisterHandler(IRmbHandler handler)
        {
            if (handler == null) return;
            _handlers.Remove(handler);
            if (_activeHandler == handler)
            {
                _activeHandler = null;
            }
        }

        public void ReportHandCargo(bool value)
        {
            _handHasCargo = value;
        }

        void Awake()
        {
            ValidateMasks();

            if (ResolvedInputActions == null)
            {
#if UNITY_EDITOR
                if (!Application.isBatchMode)
                {
                    Debug.Log($"{nameof(HandCameraInputRouter)} on {name} has no InputActionAsset assigned; a runtime fallback will be generated.", this);
                }
#endif
            }

            if (InteractionMask == 0)
            {
                Debug.LogError($"{nameof(HandCameraInputRouter)} on {name} has an empty interactionMask; configure Layers_Tags_Physics.md compliant masks.", this);
            }

            if (GroundMask == 0)
            {
#if UNITY_EDITOR
                if (!Application.isBatchMode)
                {
                    Debug.Log($"{nameof(HandCameraInputRouter)} on {name} has an empty groundMask; cursor fallbacks will use a flat Y=0 plane.", this);
                }
#endif
            }

            _hysteresisRemaining = Mathf.Max(0, ResolvedHysteresisFrames);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            ValidateMasks();
        }
#endif

        void OnEnable()
        {
            AcquireActions();
            EnableActions();
        }

        void OnDisable()
        {
            DisableActions();
            _activeHandler = null;
        }

        void Update()
        {
            if (_pointerAction == null || _rightClickAction == null) return;

            UpdateInputSamples();
            BuildContext();
            Dispatch();

            if (_cooldownRemaining > 0f)
            {
                _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - _currentContext.UnscaledDeltaTime);
            }
        }

        void AcquireActions()
        {
            var resolvedMapName = ResolvedActionMapName;
            var resolvedAsset = ResolvedInputActions;
            bool assetAutoCreated = false;

            if (resolvedAsset == null)
            {
                resolvedAsset = inputActions = ScriptableObject.CreateInstance<InputActionAsset>();
                resolvedAsset.name = $"{name}_GeneratedHandCameraInput";
                assetAutoCreated = true;
#if UNITY_EDITOR
                Debug.LogWarning($"{nameof(HandCameraInputRouter)} on {name} generated a fallback InputActionAsset. Assign a profile or asset reference to avoid this.", this);
#endif
            }

            _map = resolvedAsset.FindActionMap(resolvedMapName, throwIfNotFound: false);
            if (_map == null)
            {
                _map = new InputActionMap(resolvedMapName);
                if (assetAutoCreated)
                {
                    resolvedAsset.AddActionMap(_map);
                }
#if UNITY_EDITOR
                Debug.LogWarning($"{nameof(HandCameraInputRouter)} on {name} could not find action map '{resolvedMapName}'. A runtime fallback map was created.", this);
#endif
            }

            if (_map == null)
            {
                Debug.LogError($"{nameof(HandCameraInputRouter)} on {name} failed to acquire an InputActionMap.", this);
                return;
            }

            _pointerAction = RequireAction("PointerPosition", InputActionType.PassThrough, "<Mouse>/position");
            _pointerDeltaAction = RequireAction("PointerDelta", InputActionType.PassThrough, "<Mouse>/delta");
            _leftClickAction = RequireAction("LeftClick", InputActionType.Button, "<Mouse>/leftButton");
            _middleClickAction = RequireAction("MiddleClick", InputActionType.Button, "<Mouse>/middleButton");
            _rightClickAction = RequireAction("RightClick", InputActionType.Button, "<Mouse>/rightButton");
            _scrollAction = RequireAction("ScrollWheel", InputActionType.PassThrough, "<Mouse>/scroll");

            if (_pointerAction == null || _rightClickAction == null)
            {
                Debug.LogError($"{nameof(HandCameraInputRouter)} requires PointerPosition and RightClick actions in map '{resolvedMapName}'.", this);
            }
        }

        InputAction RequireAction(string actionName, InputActionType type, string defaultBinding)
        {
            if (_map == null)
            {
                return null;
            }

            var action = _map.FindAction(actionName, throwIfNotFound: false);
            if (action == null)
            {
                action = _map.AddAction(actionName, type);
                if (!string.IsNullOrEmpty(defaultBinding))
                {
                    action.AddBinding(defaultBinding);
                }
            }
            else if (action.bindings.Count == 0 && !string.IsNullOrEmpty(defaultBinding))
            {
                action.AddBinding(defaultBinding);
            }

            return action;
        }

        void EnableActions()
        {
            if (_map == null) return;
            _map.Enable();
        }

        void DisableActions()
        {
            if (_map == null) return;
            _map.Disable();
        }

        void UpdateInputSamples()
        {
            _pointerPosition = _pointerAction != null ? _pointerAction.ReadValue<Vector2>() : _pointerPosition;
            _pointerDelta = _pointerDeltaAction != null ? _pointerDeltaAction.ReadValue<Vector2>() : Vector2.zero;
            _scrollValue = _scrollAction != null ? _scrollAction.ReadValue<Vector2>() : Vector2.zero;
        }

        void BuildContext()
        {
            bool pointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            Camera cam = raycastCamera != null ? raycastCamera : Camera.main;
            if (cam == null)
            {
                var fallbackRay = new Ray(Vector3.zero, Vector3.forward);
                _currentContext = new RmbContext(
                    _pointerPosition,
                    fallbackRay,
                    pointerOverUI,
                    false,
                    s_emptyHit,
                    Vector3.zero,
                    -1,
                    UnityEngine.Time.deltaTime,
                    UnityEngine.Time.unscaledDeltaTime,
                    _handHasCargo,
                    false,
                    false,
                    false,
                    true);
                return;
            }

            Ray pointerRay = cam.ScreenPointToRay(_pointerPosition);

            float rayDistance = ResolvedMaxRayDistance;
            LayerMask resolvedInteractionMask = InteractionMask;
            bool hasWorldHit = Physics.Raycast(pointerRay, out RaycastHit hit, rayDistance, resolvedInteractionMask, QueryTriggerInteraction.Ignore);
            Vector3 worldPoint = hasWorldHit ? hit.point : ProjectToGround(pointerRay, rayDistance);
            int worldLayer = hasWorldHit && hit.collider != null ? hit.collider.gameObject.layer : -1;

            LayerMask resolvedStorehouseMask = ResolvedStorehouseMask;
            LayerMask resolvedPileMask = ResolvedPileMask;
            LayerMask resolvedDraggableMask = ResolvedDraggableMask;
            LayerMask resolvedGroundMask = GroundMask;

            bool hitStorehouse = hasWorldHit && resolvedStorehouseMask != 0 && ((resolvedStorehouseMask.value & (1 << worldLayer)) != 0);
            bool hitPile = hasWorldHit && resolvedPileMask != 0 && ((resolvedPileMask.value & (1 << worldLayer)) != 0);
            bool hitDraggable = hasWorldHit && resolvedDraggableMask != 0 && ((resolvedDraggableMask.value & (1 << worldLayer)) != 0);
            bool hitGround = hasWorldHit ? (resolvedGroundMask != 0 && ((resolvedGroundMask.value & (1 << worldLayer)) != 0)) : true;

            _currentContext = new RmbContext(
                _pointerPosition,
                pointerRay,
                pointerOverUI,
                hasWorldHit,
                hasWorldHit ? hit : s_emptyHit,
                worldPoint,
                worldLayer,
                UnityEngine.Time.deltaTime,
                UnityEngine.Time.unscaledDeltaTime,
                _handHasCargo,
                hitStorehouse,
                hitPile,
                hitDraggable,
                hitGround);
        }

        Vector3 ProjectToGround(Ray ray, float rayDistance)
        {
            LayerMask resolvedGroundMask = GroundMask;
            if (resolvedGroundMask != 0 && Physics.Raycast(ray, out var hit, rayDistance, resolvedGroundMask, QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }

            var groundPlane = new Plane(Vector3.up, Vector3.zero);
            return groundPlane.Raycast(ray, out float enter) ? ray.GetPoint(Mathf.Min(enter, rayDistance)) : ray.GetPoint(10f);
        }

        void Dispatch()
        {
            if (_rightClickAction == null) return;

            bool pressedThisFrame = _rightClickAction.WasPressedThisFrame();
            bool releasedThisFrame = _rightClickAction.WasReleasedThisFrame();
            bool isPressed = _rightClickAction.IsPressed();
            int resolvedHysteresis = ResolvedHysteresisFrames;
            bool shouldLog = ResolvedLogTransitions;

            if (_currentContext.PointerOverUI)
            {
                if (_activeHandler != null)
                {
                    _activeHandler.OnRmb(_currentContext, RmbPhase.Canceled);
                    if (shouldLog)
                    {
                        Debug.Log($"[RMB] {_activeHandler.GetType().Name} canceled (UI override).", this);
                    }
                    _activeHandler = null;
                }

                _cooldownRemaining = 0f;
                _hysteresisRemaining = Mathf.Max(0, resolvedHysteresis);
                return;
            }

            PruneHandlerList();
            var candidate = SelectCandidate();
            bool activeValid = _activeHandler != null && _activeHandler.CanHandle(_currentContext);

            if (candidate != null && _activeHandler != null && candidate != _activeHandler)
            {
                if (candidate.Priority == _activeHandler.Priority && activeValid)
                {
                    candidate = _activeHandler;
                }
            }

            if (!activeValid)
            {
                if (_activeHandler != null && _hysteresisRemaining > 0)
                {
                    candidate ??= _activeHandler;
                    _hysteresisRemaining--;
                }
            }
            else
            {
                _hysteresisRemaining = Mathf.Max(0, resolvedHysteresis);
            }

            if (pressedThisFrame && _cooldownRemaining <= 0f)
            {
                if (candidate != null)
                {
                    SwitchActiveHandler(candidate, RmbPhase.Started);
                }
            }

            if (isPressed && _activeHandler != null)
            {
                // Upgrade to higher priority if necessary
                if (candidate != null && candidate != _activeHandler && candidate.Priority > _activeHandler.Priority)
                {
                    SwitchActiveHandler(candidate, RmbPhase.Started);
                }

                _activeHandler.OnRmb(_currentContext, RmbPhase.Performed);
            }

            if (releasedThisFrame && _activeHandler != null)
            {
                _activeHandler.OnRmb(_currentContext, RmbPhase.Canceled);
                if (shouldLog)
                {
                    Debug.Log($"[RMB] {_activeHandler.GetType().Name} canceled.", this);
                }

                _activeHandler = null;
                _cooldownRemaining = ResolvedHandlerCooldown;
                _hysteresisRemaining = Mathf.Max(0, resolvedHysteresis);
            }

            if (!isPressed && candidate == null && _hysteresisRemaining > 0)
            {
                _hysteresisRemaining--;
            }
        }

        IRmbHandler SelectCandidate()
        {
            if (_cooldownRemaining > 0f) return null;

            foreach (var handler in _handlers)
            {
                if (handler == null) continue;
                if (handler.CanHandle(_currentContext))
                {
                    return handler;
                }
            }
            return null;
        }

        void SwitchActiveHandler(IRmbHandler nextHandler, RmbPhase startPhase)
        {
            if (nextHandler == null) return;
            if (_activeHandler != null)
            {
                _activeHandler.OnRmb(_currentContext, RmbPhase.Canceled);
                if (ResolvedLogTransitions)
                {
                    Debug.Log($"[RMB] {_activeHandler.GetType().Name} canceled (pre-empted).", this);
                }
            }

            _activeHandler = nextHandler;
            _activeHandler.OnRmb(_currentContext, startPhase);
            if (ResolvedLogTransitions)
            {
                Debug.Log($"[RMB] {_activeHandler.GetType().Name} started.", this);
            }

            _hysteresisRemaining = Mathf.Max(0, ResolvedHysteresisFrames);
        }

        void PruneHandlerList()
        {
            for (int i = _handlers.Count - 1; i >= 0; i--)
            {
                if (_handlers[i] == null)
                {
                    _handlers.RemoveAt(i);
                }
            }
        }

        void ValidateMasks()
        {
            var resolvedInteraction = InteractionMask;
            var resolvedGround = GroundMask;
            var resolvedStorehouse = ResolvedStorehouseMask;
            var resolvedPile = ResolvedPileMask;
            var resolvedDraggable = ResolvedDraggableMask;

            if (resolvedInteraction == 0)
            {
                return;
            }

            void WarnIfNotSubset(string label, LayerMask mask)
            {
                if (mask == 0) return;
                if ((mask.value & ~resolvedInteraction.value) != 0)
                {
                    Debug.LogWarning($"{nameof(HandCameraInputRouter)} on {name}: {label} mask is not a subset of InteractionMask; pointer queries may miss expected colliders.", this);
                }
            }

            WarnIfNotSubset("Ground", resolvedGround);
            WarnIfNotSubset("Storehouse", resolvedStorehouse);
            WarnIfNotSubset("Pile", resolvedPile);
            WarnIfNotSubset("Draggable", resolvedDraggable);
        }
    }
}
