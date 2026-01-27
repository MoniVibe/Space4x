using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Unity.Entities;
using Unity.Mathematics;
using PureDOTS.Input;

namespace PureDOTS.Input
{
    /// <summary>
    /// MonoBehaviour bridge that accumulates Unity Input System events per frame
    /// and exposes snapshots for DOTS systems to consume once per tick.
    /// Handles focus lost, cursor lock, and multi-tick catch-up.
    /// </summary>
    [DefaultExecutionOrder(-120)]
    [DisallowMultipleComponent]
    public sealed class InputSnapshotBridge : MonoBehaviour
    {
        [Header("Input Actions")]
        [SerializeField] InputActionAsset inputActions;
        [SerializeField] string actionMapName = "HandCamera";

        [Header("Raycast")]
        [SerializeField] Camera raycastCamera;
        [SerializeField] LayerMask groundMask = ~0;
        [SerializeField] float maxRayDistance = 800f;
        [SerializeField] float fallbackHeight = 12f;

        [Header("Configuration")]
        [SerializeField, Tooltip("Maximum edge events to buffer per frame")]
        int maxEdgeEventsPerFrame = 16;

        private InputActionMap _map;
        private InputAction _pointerAction;
        private InputAction _leftClickAction;
        private InputAction _middleClickAction;
        private InputAction _rightClickAction;
        private InputAction _scrollAction;
        private InputAction _moveAction;
        private InputAction _verticalMoveAction;
        private InputAction _toggleYAxisAction;
        private InputAction _queueModifierAction;
        private InputAction _releaseSingleAction;
        private InputAction _releaseAllAction;
        private InputAction _toggleThrowModeAction;
        private InputAction _rewindAction;
        private InputAction _timeSlowerAction;
        private InputAction _timeFasterAction;
        private InputAction _pauseAction;

        // Accumulated state for current frame
        private struct FrameInputSnapshot
        {
            public float2 PointerPosition;
            public float2 PointerDelta;
            public float3 CursorWorldPosition;
            public float3 AimDirection;
            public byte PrimaryHeld;
            public byte SecondaryHeld;
            public float ThrowCharge;
            public byte PointerOverUI;
            public byte AppHasFocus;

            // Camera
            public float2 OrbitDelta;
            public float2 PanDelta;
            public float ZoomDelta;
            public float2 CameraMove;
            public float VerticalMove;
            public byte ToggleYAxisTriggered;
            public byte YAxisUnlocked;

            // Hand advanced controls
            public byte QueueModifierHeld;
            public byte ReleaseSingleTriggered;
            public byte ReleaseAllTriggered;
            public byte ToggleThrowModeTriggered;
            public byte ThrowModeIsSlingshot;

            // Time controls
            public byte RewindHeld;
            public byte RewindPressedThisFrame;
            public byte RewindSpeedLevel;
            public byte EnterGhostPreview;
            public byte StepDownTriggered;
            public byte StepUpTriggered;
            public byte PauseToggleTriggered;

            // Cursor raycast data (for devtools)
            public UnityEngine.Ray CursorRay;
            public bool HasRaycastHit;
            public UnityEngine.RaycastHit RaycastHit;
            public byte ModifierKeys; // Bit flags: Shift=1, Ctrl=2, Alt=4

            // Edge events (clear each frame)
            public List<HandInputEdge> HandEdges;
            public List<CameraInputEdge> CameraEdges;
        }

        private FrameInputSnapshot _currentSnapshot;
        private FrameInputSnapshot _lastFlushedSnapshot;
        private bool _lastPrimaryState;
        private bool _lastSecondaryState;
        private bool _lastMiddleState;
        private float2 _lastPointerPosition;
        private float _accumulatedThrowCharge;
        private bool _appHadFocusLastFrame;
        private bool _yAxisUnlocked;
        private bool _pendingYAxisToggle;
        private bool _throwModeSlingshot = true;
        private bool _pendingThrowModeToggle;
        private bool _pendingReleaseSingle;
        private bool _pendingReleaseAll;
        private bool _rewindHeld;
        private byte _rewindSpeedLevel;
        private float _rewindHoldTimer;
        private bool _rewindPressedThisFrame;
        private bool _enterGhostPreview;
        private bool _stepDownTriggered;
        private bool _stepUpTriggered;
        private bool _pauseToggleTriggered;

        // Ring buffer for pointer deltas (accumulate between DOTS ticks)
        private readonly Queue<float2> _pointerDeltaRing = new Queue<float2>(8);
        private float2 _accumulatedPointerDelta;

        void Awake()
        {
            _currentSnapshot = new FrameInputSnapshot
            {
                HandEdges = new List<HandInputEdge>(maxEdgeEventsPerFrame),
                CameraEdges = new List<CameraInputEdge>(maxEdgeEventsPerFrame)
            };
            _lastFlushedSnapshot = _currentSnapshot;
        }

        void OnEnable()
        {
            AcquireActions();
            EnableActions();
            _appHadFocusLastFrame = Application.isFocused;
        }

        void OnDisable()
        {
            DisableActions();
        }

        void Update()
        {
            if (_map == null || !_map.enabled)
            {
                return;
            }

            // Reset snapshot for this frame
            _currentSnapshot.HandEdges.Clear();
            _currentSnapshot.CameraEdges.Clear();
            _currentSnapshot.PointerDelta = float2.zero;
            _currentSnapshot.OrbitDelta = float2.zero;
            _currentSnapshot.PanDelta = float2.zero;
            _currentSnapshot.ZoomDelta = 0f;
            _currentSnapshot.CameraMove = float2.zero;
            _currentSnapshot.VerticalMove = 0f;
            _currentSnapshot.ToggleYAxisTriggered = 0;
            _currentSnapshot.QueueModifierHeld = 0;
            _currentSnapshot.ReleaseSingleTriggered = 0;
            _currentSnapshot.ReleaseAllTriggered = 0;
            _currentSnapshot.ToggleThrowModeTriggered = 0;
            _currentSnapshot.ThrowModeIsSlingshot = (byte)(_throwModeSlingshot ? 1 : 0);
            _currentSnapshot.RewindHeld = 0;
            _currentSnapshot.RewindPressedThisFrame = 0;
            _currentSnapshot.RewindSpeedLevel = _rewindSpeedLevel;
            _currentSnapshot.EnterGhostPreview = 0;
            _currentSnapshot.StepDownTriggered = 0;
            _currentSnapshot.StepUpTriggered = 0;
            _currentSnapshot.PauseToggleTriggered = 0;

            // Check app focus
            bool appHasFocus = Application.isFocused;
            _currentSnapshot.AppHasFocus = appHasFocus ? (byte)1 : (byte)0;

            // Handle focus lost: synthesize Up events for held buttons
            if (!appHasFocus && _appHadFocusLastFrame)
            {
                SynthesizeFocusLostEvents();
            }
            _appHadFocusLastFrame = appHasFocus;

            if (!appHasFocus)
            {
                // Don't accumulate input when app doesn't have focus
                return;
            }

            if (_pendingYAxisToggle)
            {
                _yAxisUnlocked = !_yAxisUnlocked;
                _currentSnapshot.ToggleYAxisTriggered = 1;
                _pendingYAxisToggle = false;
            }
            _currentSnapshot.YAxisUnlocked = (byte)(_yAxisUnlocked ? 1 : 0);

            if (_pendingThrowModeToggle)
            {
                _throwModeSlingshot = !_throwModeSlingshot;
                _currentSnapshot.ToggleThrowModeTriggered = 1;
                _pendingThrowModeToggle = false;
            }
            _currentSnapshot.ThrowModeIsSlingshot = (byte)(_throwModeSlingshot ? 1 : 0);

            // Poll current input state (not callbacks to avoid GC)
            PollInputState();

            // Accumulate pointer delta for ring buffer
            var currentPointer = _pointerAction?.ReadValue<Vector2>() ?? Vector2.zero;
            if (_lastPointerPosition.x != 0 || _lastPointerPosition.y != 0)
            {
                var delta = new float2(currentPointer.x - _lastPointerPosition.x, currentPointer.y - _lastPointerPosition.y);
                _accumulatedPointerDelta += delta;
                _pointerDeltaRing.Enqueue(delta);
                if (_pointerDeltaRing.Count > 8)
                {
                    _pointerDeltaRing.Dequeue();
                }
            }
            _lastPointerPosition = new float2(currentPointer.x, currentPointer.y);
            _currentSnapshot.PointerPosition = _lastPointerPosition;

            // Detect edge events
            DetectEdgeEvents();

            // Compute cursor world position
            ComputeCursorWorldPosition(out _currentSnapshot.CursorWorldPosition, out _currentSnapshot.AimDirection);

            // Update cursor raycast for devtools
            if (raycastCamera != null)
            {
                var screenPos = new Vector3(_currentSnapshot.PointerPosition.x, _currentSnapshot.PointerPosition.y, 0f);
                _currentSnapshot.CursorRay = raycastCamera.ScreenPointToRay(screenPos);
                _currentSnapshot.HasRaycastHit = Physics.Raycast(_currentSnapshot.CursorRay, out _currentSnapshot.RaycastHit, maxRayDistance, groundMask);
            }
            else
            {
                _currentSnapshot.HasRaycastHit = false;
            }

            // Update modifier keys via Input System (legacy Input is disabled)
            _currentSnapshot.ModifierKeys = 0;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if ((keyboard.leftShiftKey?.isPressed ?? false) || (keyboard.rightShiftKey?.isPressed ?? false))
                {
                    _currentSnapshot.ModifierKeys |= 1;
                }

                if ((keyboard.leftCtrlKey?.isPressed ?? false) || (keyboard.rightCtrlKey?.isPressed ?? false))
                {
                    _currentSnapshot.ModifierKeys |= 2;
                }

                if ((keyboard.leftAltKey?.isPressed ?? false) || (keyboard.rightAltKey?.isPressed ?? false))
                {
                    _currentSnapshot.ModifierKeys |= 4;
                }
            }

            // Update continuous state
            _currentSnapshot.PrimaryHeld = _lastPrimaryState ? (byte)1 : (byte)0;
            _currentSnapshot.SecondaryHeld = _lastSecondaryState ? (byte)1 : (byte)0;
            _currentSnapshot.ThrowCharge = _accumulatedThrowCharge;
            _currentSnapshot.PointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject() ? (byte)1 : (byte)0;
        }

        /// <summary>
        /// Gets current cursor raycast hit data for devtools.
        /// </summary>
        public void GetCursorHit(out UnityEngine.Ray ray, out bool hasHit, out UnityEngine.RaycastHit hit, out byte modifierKeys)
        {
            ray = _currentSnapshot.CursorRay;
            hasHit = _currentSnapshot.HasRaycastHit;
            hit = _currentSnapshot.RaycastHit;
            modifierKeys = _currentSnapshot.ModifierKeys;
        }

        /// <summary>
        /// Flushes accumulated input snapshot to ECS once per DOTS tick.
        /// Called by CopyInputToEcsSystem.
        /// </summary>
        public void FlushSnapshotToEcs(EntityManager entityManager, Entity handEntity, Entity cameraEntity, Entity timeControlEntity, uint currentTick)
        {
            // Flush hand input
            if (handEntity != Entity.Null && entityManager.Exists(handEntity))
            {
                // Get PlayerId from hand entity if present
                byte playerId = 0;
                if (entityManager.HasComponent<PlayerId>(handEntity))
                {
                    playerId = entityManager.GetComponentData<PlayerId>(handEntity).Value;
                }

                var handInput = new DivineHandInput
                {
                    SampleTick = currentTick,
                    PlayerId = playerId,
                    PointerPosition = _currentSnapshot.PointerPosition,
                    PointerDelta = _accumulatedPointerDelta, // Use accumulated delta
                    CursorWorldPosition = _currentSnapshot.CursorWorldPosition,
                    AimDirection = _currentSnapshot.AimDirection,
                    PrimaryHeld = _currentSnapshot.PrimaryHeld,
                    SecondaryHeld = _currentSnapshot.SecondaryHeld,
                    ThrowCharge = _currentSnapshot.ThrowCharge,
                    PointerOverUI = _currentSnapshot.PointerOverUI,
                    AppHasFocus = _currentSnapshot.AppHasFocus,
                    QueueModifierHeld = _currentSnapshot.QueueModifierHeld,
                    ReleaseSingleTriggered = _currentSnapshot.ReleaseSingleTriggered,
                    ReleaseAllTriggered = _currentSnapshot.ReleaseAllTriggered,
                    ToggleThrowModeTriggered = _currentSnapshot.ToggleThrowModeTriggered,
                    ThrowModeIsSlingshot = _currentSnapshot.ThrowModeIsSlingshot
                };
                entityManager.SetComponentData(handEntity, handInput);

                // Flush edge buffer
                if (entityManager.HasBuffer<HandInputEdge>(handEntity))
                {
                    var edges = entityManager.GetBuffer<HandInputEdge>(handEntity);
                    edges.Clear();
                    foreach (var edge in _currentSnapshot.HandEdges)
                    {
                        var edgeWithTick = edge;
                        edgeWithTick.Tick = currentTick;
                        edges.Add(edgeWithTick);
                    }
                }
                else
                {
                    var edges = entityManager.AddBuffer<HandInputEdge>(handEntity);
                    foreach (var edge in _currentSnapshot.HandEdges)
                    {
                        var edgeWithTick = edge;
                        edgeWithTick.Tick = currentTick;
                        edges.Add(edgeWithTick);
                    }
                }

                // Reset accumulated delta after flush
                _accumulatedPointerDelta = float2.zero;
            }

            // Flush camera input
            if (cameraEntity != Entity.Null && entityManager.Exists(cameraEntity))
            {
                // Get PlayerId from camera entity if present
                byte cameraPlayerId = 0;
                if (entityManager.HasComponent<PlayerId>(cameraEntity))
                {
                    cameraPlayerId = entityManager.GetComponentData<PlayerId>(cameraEntity).Value;
                }

                var cameraInput = new CameraInputState
                {
                    SampleTick = currentTick,
                    PlayerId = cameraPlayerId,
                    OrbitDelta = _currentSnapshot.OrbitDelta,
                    PanDelta = _currentSnapshot.PanDelta,
                    ZoomDelta = _currentSnapshot.ZoomDelta,
                    PointerPosition = _currentSnapshot.PointerPosition,
                    PointerOverUI = _currentSnapshot.PointerOverUI,
                    AppHasFocus = _currentSnapshot.AppHasFocus,
                    MoveInput = _currentSnapshot.CameraMove,
                    VerticalMove = _currentSnapshot.VerticalMove,
                    YAxisUnlocked = _currentSnapshot.YAxisUnlocked,
                    ToggleYAxisTriggered = _currentSnapshot.ToggleYAxisTriggered
                };
                entityManager.SetComponentData(cameraEntity, cameraInput);

                // Flush edge buffer
                if (entityManager.HasBuffer<CameraInputEdge>(cameraEntity))
                {
                    var edges = entityManager.GetBuffer<CameraInputEdge>(cameraEntity);
                    edges.Clear();
                    foreach (var edge in _currentSnapshot.CameraEdges)
                    {
                        var edgeWithTick = edge;
                        edgeWithTick.Tick = currentTick;
                        edges.Add(edgeWithTick);
                    }
                }
                else
                {
                    var edges = entityManager.AddBuffer<CameraInputEdge>(cameraEntity);
                    foreach (var edge in _currentSnapshot.CameraEdges)
                    {
                        var edgeWithTick = edge;
                        edgeWithTick.Tick = currentTick;
                        edges.Add(edgeWithTick);
                    }
                }
            }

            if (timeControlEntity != Entity.Null && entityManager.Exists(timeControlEntity))
            {
                var timeInput = new TimeControlInputState
                {
                    SampleTick = currentTick,
                    RewindHeld = _currentSnapshot.RewindHeld,
                    RewindPressedThisFrame = _currentSnapshot.RewindPressedThisFrame,
                    RewindSpeedLevel = _currentSnapshot.RewindSpeedLevel,
                    EnterGhostPreview = _currentSnapshot.EnterGhostPreview,
                    StepDownTriggered = _currentSnapshot.StepDownTriggered,
                    StepUpTriggered = _currentSnapshot.StepUpTriggered,
                    PauseToggleTriggered = _currentSnapshot.PauseToggleTriggered
                };
                entityManager.SetComponentData(timeControlEntity, timeInput);
            }

            _lastFlushedSnapshot = _currentSnapshot;
        }

        private void AcquireActions()
        {
            if (inputActions != null)
            {
                _map = inputActions.FindActionMap(actionMapName, throwIfNotFound: false);
                if (_map == null)
                {
                    _map = inputActions.AddActionMap(actionMapName);
                }
            }
            else
            {
                _map = new InputActionMap(actionMapName);
            }

            if (_map == null)
            {
                Debug.LogWarning($"{nameof(InputSnapshotBridge)} could not initialize InputActionMap '{actionMapName}'.");
                return;
            }

            _pointerAction = RequireAction("Pointer", InputActionType.PassThrough);
            if (_pointerAction != null && _pointerAction.bindings.Count == 0)
            {
                _pointerAction.AddBinding("<Mouse>/position");
            }

            _leftClickAction = RequireAction("LeftClick", InputActionType.Button);
            if (_leftClickAction != null && _leftClickAction.bindings.Count == 0)
            {
                _leftClickAction.AddBinding("<Mouse>/leftButton");
            }

            _middleClickAction = RequireAction("MiddleClick", InputActionType.Button);
            if (_middleClickAction != null && _middleClickAction.bindings.Count == 0)
            {
                _middleClickAction.AddBinding("<Mouse>/middleButton");
            }

            _rightClickAction = RequireAction("RightClick", InputActionType.Button);
            if (_rightClickAction != null && _rightClickAction.bindings.Count == 0)
            {
                _rightClickAction.AddBinding("<Mouse>/rightButton");
            }

            _scrollAction = RequireAction("Scroll", InputActionType.PassThrough);
            if (_scrollAction != null && _scrollAction.bindings.Count == 0)
            {
                _scrollAction.AddBinding("<Mouse>/scroll");
            }

            _moveAction = RequireAction("Move", InputActionType.Value);
            Ensure2DVectorBindings(_moveAction);

            _verticalMoveAction = RequireAction("VerticalMove", InputActionType.Value);
            EnsureAxisBindings(_verticalMoveAction, "<Keyboard>/z", "<Keyboard>/x");

            _toggleYAxisAction = RequireAction("ToggleYAxis", InputActionType.Button);
            EnsureBindingIfEmpty(_toggleYAxisAction, "<Keyboard>/y");
            if (_toggleYAxisAction != null)
            {
                _toggleYAxisAction.performed += _ => _pendingYAxisToggle = true;
            }

            _queueModifierAction = RequireAction("QueueModifier", InputActionType.Button);
            EnsureBindingIfEmpty(_queueModifierAction, "<Keyboard>/shift");

            _releaseSingleAction = RequireAction("ReleaseSingle", InputActionType.Button);
            EnsureBindingIfEmpty(_releaseSingleAction, "<Keyboard>/q");
            if (_releaseSingleAction != null)
            {
                _releaseSingleAction.performed += _ => _pendingReleaseSingle = true;
            }

            _releaseAllAction = RequireAction("ReleaseAll", InputActionType.Button);
            EnsureBindingIfEmpty(_releaseAllAction, "<Keyboard>/e");
            if (_releaseAllAction != null)
            {
                _releaseAllAction.performed += _ => _pendingReleaseAll = true;
            }

            _toggleThrowModeAction = RequireAction("ToggleThrowMode", InputActionType.Button);
            EnsureBindingIfEmpty(_toggleThrowModeAction, "<Keyboard>/t");
            if (_toggleThrowModeAction != null)
            {
                _toggleThrowModeAction.performed += _ => _pendingThrowModeToggle = true;
            }

            _rewindAction = RequireAction("Rewind", InputActionType.Button);
            EnsureBindingIfEmpty(_rewindAction, "<Keyboard>/r");
            if (_rewindAction != null)
            {
                _rewindAction.started += OnRewindStarted;
                _rewindAction.canceled += OnRewindCanceled;
            }

            _timeSlowerAction = RequireAction("TimeSlower", InputActionType.Button);
            EnsureBindingIfEmpty(_timeSlowerAction, "<Keyboard>/[");
            if (_timeSlowerAction != null)
            {
                _timeSlowerAction.performed += _ => _stepDownTriggered = true;
            }

            _timeFasterAction = RequireAction("TimeFaster", InputActionType.Button);
            EnsureBindingIfEmpty(_timeFasterAction, "<Keyboard>/]");
            if (_timeFasterAction != null)
            {
                _timeFasterAction.performed += _ => _stepUpTriggered = true;
            }

            _pauseAction = RequireAction("PauseResume", InputActionType.Button);
            EnsureBindingIfEmpty(_pauseAction, "<Keyboard>/space");
            if (_pauseAction != null)
            {
                _pauseAction.performed += _ => _pauseToggleTriggered = true;
            }
        }

        private void EnableActions()
        {
            _map?.Enable();
        }

        private void DisableActions()
        {
            _map?.Disable();
        }

        private InputAction RequireAction(string actionName, InputActionType type)
        {
            if (_map == null)
            {
                return null;
            }

            var action = _map.FindAction(actionName, throwIfNotFound: false);
            if (action == null)
            {
                action = _map.AddAction(actionName, type: type);
            }
            return action;
        }

        private static void EnsureBindingIfEmpty(InputAction action, string binding)
        {
            if (action == null || action.bindings.Count > 0 || string.IsNullOrEmpty(binding))
            {
                return;
            }
            action.AddBinding(binding);
        }

        private static void Ensure2DVectorBindings(InputAction action)
        {
            if (action == null || action.bindings.Count > 0)
            {
                return;
            }

            var composite = action.AddCompositeBinding("2DVector");
            composite.With("Up", "<Keyboard>/w");
            composite.With("Up", "<Keyboard>/upArrow");
            composite.With("Down", "<Keyboard>/s");
            composite.With("Down", "<Keyboard>/downArrow");
            composite.With("Left", "<Keyboard>/a");
            composite.With("Left", "<Keyboard>/leftArrow");
            composite.With("Right", "<Keyboard>/d");
            composite.With("Right", "<Keyboard>/rightArrow");
        }

        private static void EnsureAxisBindings(InputAction action, string positiveBinding, string negativeBinding)
        {
            if (action == null || action.bindings.Count > 0)
            {
                return;
            }

            if (!string.IsNullOrEmpty(positiveBinding))
            {
                action.AddBinding(positiveBinding).WithProcessor("scale(factor=1)");
            }
            if (!string.IsNullOrEmpty(negativeBinding))
            {
                action.AddBinding(negativeBinding).WithProcessor("scale(factor=-1)");
            }
        }

        private void OnRewindStarted(InputAction.CallbackContext _)
        {
            _rewindHeld = true;
            _rewindHoldTimer = 0f;
            _rewindSpeedLevel = 1;
            _rewindPressedThisFrame = true;
            _enterGhostPreview = false;
        }

        private void OnRewindCanceled(InputAction.CallbackContext _)
        {
            _rewindHeld = false;
            _enterGhostPreview = true;
            _rewindSpeedLevel = 0;
            _rewindHoldTimer = 0f;
        }

        private void PollInputState()
        {
            // Poll held states
            _lastPrimaryState = _leftClickAction?.IsPressed() ?? false;
            _lastSecondaryState = _rightClickAction?.IsPressed() ?? false;
            _lastMiddleState = _middleClickAction?.IsPressed() ?? false;

            // Accumulate throw charge while holding
            if (_lastSecondaryState)
            {
                _accumulatedThrowCharge += Time.deltaTime;
            }
            else
            {
                _accumulatedThrowCharge = 0f;
            }

            if (_rewindHeld)
            {
                _rewindHoldTimer += Time.deltaTime;
                float level = 1f + math.floor(_rewindHoldTimer / 0.4f);
                _rewindSpeedLevel = (byte)math.clamp(level, 1f, 4f);
            }
            else
            {
                _rewindHoldTimer = 0f;
            }

            // Poll scroll
            var scrollValue = _scrollAction?.ReadValue<Vector2>() ?? Vector2.zero;
            _currentSnapshot.ZoomDelta = scrollValue.y; // Unity Input System uses Y for scroll

            var moveValue = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
            _currentSnapshot.CameraMove = new float2(moveValue.x, moveValue.y);
            _currentSnapshot.VerticalMove = _verticalMoveAction != null ? _verticalMoveAction.ReadValue<float>() : 0f;
            _currentSnapshot.QueueModifierHeld = (byte)((_queueModifierAction?.IsPressed() ?? false) ? 1 : 0);
            _currentSnapshot.ReleaseSingleTriggered = (byte)(_pendingReleaseSingle ? 1 : 0);
            _currentSnapshot.ReleaseAllTriggered = (byte)(_pendingReleaseAll ? 1 : 0);
            _currentSnapshot.RewindHeld = (byte)(_rewindHeld ? 1 : 0);
            _currentSnapshot.RewindPressedThisFrame = (byte)(_rewindPressedThisFrame ? 1 : 0);
            _currentSnapshot.EnterGhostPreview = (byte)(_enterGhostPreview ? 1 : 0);
            _currentSnapshot.RewindSpeedLevel = _rewindSpeedLevel;
            _currentSnapshot.StepDownTriggered = (byte)(_stepDownTriggered ? 1 : 0);
            _currentSnapshot.StepUpTriggered = (byte)(_stepUpTriggered ? 1 : 0);
            _currentSnapshot.PauseToggleTriggered = (byte)(_pauseToggleTriggered ? 1 : 0);

            // Clear one-shot flags after publishing
            _pendingReleaseSingle = false;
            _pendingReleaseAll = false;
            _rewindPressedThisFrame = false;
            _enterGhostPreview = false;
            _stepDownTriggered = false;
            _stepUpTriggered = false;
            _pauseToggleTriggered = false;
        }

        private void DetectEdgeEvents()
        {
            // Detect primary button edges
            bool currentPrimary = _leftClickAction?.IsPressed() ?? false;
            if (currentPrimary && !_lastPrimaryState)
            {
                _currentSnapshot.HandEdges.Add(new HandInputEdge
                {
                    Button = InputButton.Primary,
                    Kind = InputEdgeKind.Down,
                    PointerPosition = _currentSnapshot.PointerPosition
                });
            }
            else if (!currentPrimary && _lastPrimaryState)
            {
                _currentSnapshot.HandEdges.Add(new HandInputEdge
                {
                    Button = InputButton.Primary,
                    Kind = InputEdgeKind.Up,
                    PointerPosition = _currentSnapshot.PointerPosition
                });
            }

            // Detect secondary button edges
            bool currentSecondary = _rightClickAction?.IsPressed() ?? false;
            if (currentSecondary && !_lastSecondaryState)
            {
                _currentSnapshot.HandEdges.Add(new HandInputEdge
                {
                    Button = InputButton.Secondary,
                    Kind = InputEdgeKind.Down,
                    PointerPosition = _currentSnapshot.PointerPosition
                });
            }
            else if (!currentSecondary && _lastSecondaryState)
            {
                _currentSnapshot.HandEdges.Add(new HandInputEdge
                {
                    Button = InputButton.Secondary,
                    Kind = InputEdgeKind.Up,
                    PointerPosition = _currentSnapshot.PointerPosition
                });
            }

            // Detect middle button edges (camera orbit)
            bool currentMiddle = _middleClickAction?.IsPressed() ?? false;
            if (currentMiddle && !_lastMiddleState)
            {
                _currentSnapshot.CameraEdges.Add(new CameraInputEdge
                {
                    Button = InputButton.Middle,
                    Kind = InputEdgeKind.Down,
                    PointerPosition = _currentSnapshot.PointerPosition
                });
            }
            else if (!currentMiddle && _lastMiddleState)
            {
                _currentSnapshot.CameraEdges.Add(new CameraInputEdge
                {
                    Button = InputButton.Middle,
                    Kind = InputEdgeKind.Up,
                    PointerPosition = _currentSnapshot.PointerPosition
                });
            }

            // Update last states
            _lastPrimaryState = currentPrimary;
            _lastSecondaryState = currentSecondary;
            _lastMiddleState = currentMiddle;
        }

        private void SynthesizeFocusLostEvents()
        {
            // Synthesize Up events for any held buttons to prevent stuck drags
            if (_lastPrimaryState)
            {
                _currentSnapshot.HandEdges.Add(new HandInputEdge
                {
                    Button = InputButton.Primary,
                    Kind = InputEdgeKind.Up,
                    PointerPosition = _currentSnapshot.PointerPosition
                });
            }
            if (_lastSecondaryState)
            {
                _currentSnapshot.HandEdges.Add(new HandInputEdge
                {
                    Button = InputButton.Secondary,
                    Kind = InputEdgeKind.Up,
                    PointerPosition = _currentSnapshot.PointerPosition
                });
            }
            if (_lastMiddleState)
            {
                _currentSnapshot.CameraEdges.Add(new CameraInputEdge
                {
                    Button = InputButton.Middle,
                    Kind = InputEdgeKind.Up,
                    PointerPosition = _currentSnapshot.PointerPosition
                });
            }

            _lastPrimaryState = false;
            _lastSecondaryState = false;
            _lastMiddleState = false;
            _accumulatedThrowCharge = 0f;
            _rewindHeld = false;
            _rewindSpeedLevel = 0;
            _pendingReleaseSingle = false;
            _pendingReleaseAll = false;
            _currentSnapshot.QueueModifierHeld = 0;
        }

        private void ComputeCursorWorldPosition(out float3 worldPos, out float3 aimDirection)
        {
            if (raycastCamera == null)
            {
                worldPos = new float3(0f, fallbackHeight, 0f);
                aimDirection = new float3(0f, -1f, 0f);
                return;
            }

            var pointerPos = _currentSnapshot.PointerPosition;
            var ray = raycastCamera.ScreenPointToRay(new Vector3(pointerPos.x, pointerPos.y, 0f));

            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, groundMask))
            {
                worldPos = new float3(hit.point.x, hit.point.y, hit.point.z);
                aimDirection = math.normalize(new float3(ray.direction.x, ray.direction.y, ray.direction.z));
            }
            else
            {
                // Fallback: project onto plane at fallback height
                var t = (fallbackHeight - ray.origin.y) / ray.direction.y;
                worldPos = new float3(
                    ray.origin.x + ray.direction.x * t,
                    fallbackHeight,
                    ray.origin.z + ray.direction.z * t);
                aimDirection = math.normalize(new float3(ray.direction.x, ray.direction.y, ray.direction.z));
            }
        }
    }
}
