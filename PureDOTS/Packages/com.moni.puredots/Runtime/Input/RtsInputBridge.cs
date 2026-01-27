using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace PureDOTS.Input
{
    /// <summary>
    /// MonoBehaviour bridge that handles RTS-style input (selection, orders, control groups, etc.)
    /// and emits events to ECS buffers on the singleton RTS input entity.
    /// Works alongside InputSnapshotBridge for camera/hand input.
    /// </summary>
    [DefaultExecutionOrder(-119)] // Run after InputSnapshotBridge
    [DisallowMultipleComponent]
    public sealed class RtsInputBridge : MonoBehaviour
    {
        [Header("Selection")]
        [SerializeField] private float clickThresholdPixels = 4f;
        [SerializeField] private float doubleClickMaxTime = 0.3f;
        [SerializeField] private float doubleClickMaxDistPixels = 4f;

        [Header("Raycast")]
        [SerializeField] private Camera raycastCamera;
        [SerializeField] private LayerMask selectionMask = ~0;

        public Camera RaycastCamera
        {
            get => raycastCamera;
            set => raycastCamera = value;
        }

        public LayerMask SelectionMask
        {
            get => selectionMask;
            set => selectionMask = value;
        }

        private enum PointerMode
        {
            None,
            SelectionDrag
        }

        private PointerMode _pointerMode = PointerMode.None;
        private Vector2 _lmbDownScreenPos;
        private float _lastLmbUpTime;
        private Vector2 _lastLmbUpPos;
        private float _lastRmbUpTime;
        private Vector2 _lastRmbUpPos;

        private EntityManager _em;
        private Entity _rtsInputEntity;
        private Entity _godHandCommandStreamEntity;
        private bool _rtsInputEntityInitialized;

        private InputActionMap _map;
        private InputAction _leftClickAction;
        private InputAction _rightClickAction;
        private InputAction _ctrlAction;
        private InputAction _shiftAction;
        private InputAction _altAction;
        private InputAction[] _numberActions = new InputAction[10];
        private InputAction _spaceAction;
        private InputAction _rAction;
        private InputAction _tAction;
        private InputAction _cAction;
        private InputAction _zAction;
        private InputAction _f5Action;
        private InputAction _f6Action;

        private bool _lastLeftClickState;
        private bool _lastRightClickState;
        private bool _pendingSpaceToggle;
        private bool _pendingRToggle;
        private bool _pendingTToggle;
        private bool _pendingCToggle;
        private bool _pendingZToggle;
        private bool _pendingF5Toggle;
        private bool _pendingF6Toggle;
        private bool[] _pendingNumberPresses = new bool[10];

        private void Awake()
        {
            if (raycastCamera == null)
            {
                raycastCamera = Camera.main;
            }
        }

        private void OnEnable()
        {
            AcquireActions();
            EnableActions();
        }

        private void OnDisable()
        {
            DisableActions();
        }

        private void Update()
        {
            if (_map == null || !_map.enabled)
            {
                return;
            }

            EnsureRtsInputEntity();

            if (_rtsInputEntity == Entity.Null)
            {
                return;
            }

            EnsureGodHandCommandStream();

            if (_godHandCommandStreamEntity == Entity.Null)
            {
                return;
            }

            HandleMouseButtons();
            HandleKeyboard();
        }

        private void EnsureRtsInputEntity()
        {
            if (_rtsInputEntityInitialized)
            {
                return;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            _em = world.EntityManager;

            // Find or create singleton RTS input entity
            using (var query = _em.CreateEntityQuery(ComponentType.ReadOnly<RtsInputSingletonTag>()))
            {
                if (!query.IsEmptyIgnoreFilter)
                {
                    _rtsInputEntity = query.GetSingletonEntity();
                }
                else
                {
                    _rtsInputEntity = _em.CreateEntity(typeof(RtsInputSingletonTag));
                }
            }

            // Ensure all buffers exist
            EnsureBuffer<SelectionClickEvent>();
            EnsureBuffer<SelectionBoxEvent>();
            EnsureBuffer<RightClickEvent>();
            EnsureBuffer<ControlGroupInputEvent>();
            EnsureBuffer<TimeControlInputEvent>();
            EnsureBuffer<CameraFocusEvent>();
            EnsureBuffer<CameraRequestEvent>();
            EnsureBuffer<RockBreakEvent>();
            EnsureBuffer<SaveLoadCommandEvent>();

            _rtsInputEntityInitialized = true;
        }

        private void EnsureGodHandCommandStream()
        {
            if (_em == null)
            {
                _godHandCommandStreamEntity = Entity.Null;
                return;
            }

            if (_godHandCommandStreamEntity != Entity.Null &&
                _em.Exists(_godHandCommandStreamEntity) &&
                _em.HasBuffer<GodHandCommandEvent>(_godHandCommandStreamEntity))
            {
                return;
            }

            _godHandCommandStreamEntity = GodHandCommandStreamUtility.EnsureStream(_em);
        }

        private void EnsureBuffer<T>() where T : unmanaged, IBufferElementData
        {
            if (!_em.HasBuffer<T>(_rtsInputEntity))
            {
                _em.AddBuffer<T>(_rtsInputEntity);
            }
        }

        private void HandleMouseButtons()
        {
            Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

            HandleLmb(mousePos);
            HandleRmb(mousePos);
        }

        private void HandleLmb(Vector2 mousePos)
        {
            bool shiftDown = (_shiftAction?.IsPressed() ?? false);

            bool currentLeftClick = _leftClickAction?.IsPressed() ?? false;

            if (currentLeftClick && !_lastLeftClickState)
            {
                // LMB down
                if (IsPointerOverUI())
                {
                    return;
                }

                _lmbDownScreenPos = mousePos;
                _pointerMode = PointerMode.SelectionDrag;
            }

            if (currentLeftClick)
            {
                // LMB held (selection drag only)
            }

            if (!currentLeftClick && _lastLeftClickState)
            {
                // LMB up
                if (_pointerMode == PointerMode.SelectionDrag)
                {
                    Vector2 delta = mousePos - _lmbDownScreenPos;
                    float sqrMag = delta.sqrMagnitude;
                    float clickThreshSqr = clickThresholdPixels * clickThresholdPixels;

                    float timeNow = Time.unscaledTime;
                    bool isDoubleClick = (timeNow - _lastLmbUpTime) <= doubleClickMaxTime &&
                                         (mousePos - _lastLmbUpPos).sqrMagnitude <= (doubleClickMaxDistPixels * doubleClickMaxDistPixels);

                    if (isDoubleClick)
                    {
                        HandleLmbDoubleClick(mousePos);
                    }
                    else
                    {
                        if (sqrMag < clickThreshSqr)
                        {
                            // Single click selection
                            EmitSelectionClick(mousePos, shiftDown);
                        }
                        else
                        {
                            // Box selection
                            EmitSelectionBox(_lmbDownScreenPos, mousePos, shiftDown);
                        }
                    }

                    _lastLmbUpTime = timeNow;
                    _lastLmbUpPos = mousePos;
                    _pointerMode = PointerMode.None;
                }
            }

            _lastLeftClickState = currentLeftClick;
        }

        private void HandleRmb(Vector2 mousePos)
        {
            bool shiftDown = (_shiftAction?.IsPressed() ?? false);
            bool ctrlDown = (_ctrlAction?.IsPressed() ?? false);
            bool currentRightClick = _rightClickAction?.IsPressed() ?? false;

            if (!currentRightClick && _lastRightClickState)
            {
                // RMB up
                if (IsPointerOverUI())
                {
                    return;
                }

                float timeNow = Time.unscaledTime;
                bool isDoubleClick = (timeNow - _lastRmbUpTime) <= doubleClickMaxTime &&
                                     (mousePos - _lastRmbUpPos).sqrMagnitude <= (doubleClickMaxDistPixels * doubleClickMaxDistPixels);

                if (isDoubleClick)
                {
                    HandleRmbDoubleClick(mousePos);
                }
                else
                {
                    EmitRightClick(mousePos, shiftDown, ctrlDown);
                }

                _lastRmbUpTime = timeNow;
                _lastRmbUpPos = mousePos;
            }

            _lastRightClickState = currentRightClick;
        }

        private void HandleLmbDoubleClick(Vector2 screenPos)
        {
            if (raycastCamera == null)
            {
                return;
            }

            Ray ray = raycastCamera.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 800f, selectionMask))
            {
                var buffer = _em.GetBuffer<CameraFocusEvent>(_rtsInputEntity);
                buffer.Add(new CameraFocusEvent
                {
                    WorldPosition = hit.point,
                    HitEntity = Entity.Null, // ECS will resolve if needed
                    PlayerId = 0
                });
            }
        }

        private void HandleRmbDoubleClick(Vector2 screenPos)
        {
            if (raycastCamera == null)
            {
                return;
            }

            Ray ray = raycastCamera.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 800f, selectionMask))
            {
                var buffer = _em.GetBuffer<RockBreakEvent>(_rtsInputEntity);
                buffer.Add(new RockBreakEvent
                {
                    RockEntity = Entity.Null, // ECS will resolve if needed
                    HitPosition = hit.point,
                    PlayerId = 0
                });
            }
        }

        private void EmitSelectionClick(Vector2 screenPos, bool shiftDown)
        {
            var buffer = _em.GetBuffer<SelectionClickEvent>(_rtsInputEntity);
            buffer.Add(new SelectionClickEvent
            {
                ScreenPos = new float2(screenPos.x, screenPos.y),
                Mode = shiftDown ? SelectionClickMode.Toggle : SelectionClickMode.Replace,
                PlayerId = 0
            });
        }

        private void EmitSelectionBox(Vector2 start, Vector2 end, bool shiftDown)
        {
            Vector2 min = Vector2.Min(start, end);
            Vector2 max = Vector2.Max(start, end);
            var buffer = _em.GetBuffer<SelectionBoxEvent>(_rtsInputEntity);
            buffer.Add(new SelectionBoxEvent
            {
                ScreenMin = new float2(min.x, min.y),
                ScreenMax = new float2(max.x, max.y),
                Mode = shiftDown ? SelectionBoxMode.AdditiveToggle : SelectionBoxMode.Replace,
                PlayerId = 0
            });
        }

        private void EmitRightClick(Vector2 screenPos, bool shiftDown, bool ctrlDown)
        {
            var buffer = _em.GetBuffer<RightClickEvent>(_rtsInputEntity);
            buffer.Add(new RightClickEvent
            {
                ScreenPos = new float2(screenPos.x, screenPos.y),
                Queue = (byte)(shiftDown ? 1 : 0),
                Ctrl = (byte)(ctrlDown ? 1 : 0),
                PlayerId = 0
            });
        }

        private void HandleKeyboard()
        {
            HandleControlGroups();
            HandleTimeControls();
            HandleGodHand();
            HandleSaveLoad();
        }

        private void HandleControlGroups()
        {
            bool ctrlDown = (_ctrlAction?.IsPressed() ?? false);
            bool shiftDown = (_shiftAction?.IsPressed() ?? false);

            for (int i = 0; i <= 9; i++)
            {
                if (_pendingNumberPresses[i])
                {
                    var buffer = _em.GetBuffer<ControlGroupInputEvent>(_rtsInputEntity);

                    if (ctrlDown)
                    {
                        buffer.Add(new ControlGroupInputEvent
                        {
                            Number = (byte)i,
                            Save = 1,
                            Additive = (byte)(shiftDown ? 1 : 0),
                            Recall = 0,
                            PlayerId = 0
                        });
                    }
                    else
                    {
                        buffer.Add(new ControlGroupInputEvent
                        {
                            Number = (byte)i,
                            Save = 0,
                            Additive = 0,
                            Recall = 1,
                            PlayerId = 0
                        });
                    }

                    _pendingNumberPresses[i] = false;
                }
            }
        }

        private void HandleTimeControls()
        {
            bool shiftDown = (_shiftAction?.IsPressed() ?? false);

            // Space = toggle pause
            if (_pendingSpaceToggle)
            {
                var buffer = _em.GetBuffer<TimeControlInputEvent>(_rtsInputEntity);
                buffer.Add(new TimeControlInputEvent
                {
                    Kind = TimeControlCommandKind.TogglePause,
                    FloatParam = 0f,
                    IntParam = 0,
                    PlayerId = 0
                });
                _pendingSpaceToggle = false;
            }

            // R = enter rewind
            if (_pendingRToggle)
            {
                var buffer = _em.GetBuffer<TimeControlInputEvent>(_rtsInputEntity);
                buffer.Add(new TimeControlInputEvent
                {
                    Kind = TimeControlCommandKind.EnterRewind,
                    FloatParam = 0f,
                    IntParam = 0,
                    PlayerId = 0
                });
                _pendingRToggle = false;
            }

            // Shift + number = simulation speed
            if (shiftDown)
            {
                for (int i = 1; i <= 9; i++)
                {
                    if (_pendingNumberPresses[i])
                    {
                        float speed = i switch
                        {
                            1 => 0.25f,
                            2 => 0.5f,
                            3 => 1f,
                            4 => 2f,
                            5 => 4f,
                            _ => 1f
                        };

                        var buffer = _em.GetBuffer<TimeControlInputEvent>(_rtsInputEntity);
                        buffer.Add(new TimeControlInputEvent
                        {
                            Kind = TimeControlCommandKind.SetScale,
                            FloatParam = speed,
                            IntParam = 0,
                            PlayerId = 0
                        });

                        _pendingNumberPresses[i] = false;
                    }
                }
            }
        }

        private void HandleGodHand()
        {
            if (_godHandCommandStreamEntity == Entity.Null || !_em.Exists(_godHandCommandStreamEntity))
            {
                return;
            }

            var buffer = _em.GetBuffer<GodHandCommandEvent>(_godHandCommandStreamEntity);

            // T = toggle throw mode
            if (_pendingTToggle)
            {
                buffer.Add(new GodHandCommandEvent
                {
                    Kind = GodHandCommandKind.ToggleThrowMode,
                    PlayerId = 0
                });
                _pendingTToggle = false;
            }

            // C = launch next queued
            if (_pendingCToggle)
            {
                buffer.Add(new GodHandCommandEvent
                {
                    Kind = GodHandCommandKind.LaunchNextQueued,
                    PlayerId = 0
                });
                _pendingCToggle = false;
            }

            // Z = launch all queued
            if (_pendingZToggle)
            {
                buffer.Add(new GodHandCommandEvent
                {
                    Kind = GodHandCommandKind.LaunchAllQueued,
                    PlayerId = 0
                });
                _pendingZToggle = false;
            }
        }

        private void HandleSaveLoad()
        {
            if (_pendingF5Toggle)
            {
                var buffer = _em.GetBuffer<SaveLoadCommandEvent>(_rtsInputEntity);
                buffer.Add(new SaveLoadCommandEvent
                {
                    Kind = SaveLoadCommandKind.QuickSave,
                    PlayerId = 0
                });
                _pendingF5Toggle = false;
            }

            if (_pendingF6Toggle)
            {
                var buffer = _em.GetBuffer<SaveLoadCommandEvent>(_rtsInputEntity);
                buffer.Add(new SaveLoadCommandEvent
                {
                    Kind = SaveLoadCommandKind.QuickLoad,
                    PlayerId = 0
                });
                _pendingF6Toggle = false;
            }
        }

        private bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private void AcquireActions()
        {
            // Try to reuse InputSnapshotBridge's action map if available
            var snapshotBridge = FindFirstObjectByType<InputSnapshotBridge>();
            if (snapshotBridge != null && snapshotBridge.GetComponent<HandCameraInputRouter>() != null)
            {
                var router = snapshotBridge.GetComponent<HandCameraInputRouter>();
                // Try to get the map from the router's profile
                // For now, create our own map
            }

            _map = new InputActionMap("RtsInput");

            _leftClickAction = RequireAction("LeftClick", InputActionType.Button, "<Mouse>/leftButton");
            _rightClickAction = RequireAction("RightClick", InputActionType.Button, "<Mouse>/rightButton");
            _ctrlAction = RequireAction("Ctrl", InputActionType.Button, "<Keyboard>/ctrl");
            _shiftAction = RequireAction("Shift", InputActionType.Button, "<Keyboard>/shift");
            _altAction = RequireAction("Alt", InputActionType.Button, "<Keyboard>/alt");

            for (int i = 0; i <= 9; i++)
            {
                string keyName = i == 0 ? "0" : i.ToString();
                _numberActions[i] = RequireAction($"Number{i}", InputActionType.Button, $"<Keyboard>/{keyName}");
                if (_numberActions[i] != null)
                {
                    _numberActions[i].performed += _ => _pendingNumberPresses[i] = true;
                }
            }

            _spaceAction = RequireAction("Space", InputActionType.Button, "<Keyboard>/space");
            if (_spaceAction != null)
            {
                _spaceAction.performed += _ => _pendingSpaceToggle = true;
            }

            _rAction = RequireAction("R", InputActionType.Button, "<Keyboard>/r");
            if (_rAction != null)
            {
                _rAction.performed += _ => _pendingRToggle = true;
            }

            _tAction = RequireAction("T", InputActionType.Button, "<Keyboard>/t");
            if (_tAction != null)
            {
                _tAction.performed += _ => _pendingTToggle = true;
            }

            _cAction = RequireAction("C", InputActionType.Button, "<Keyboard>/c");
            if (_cAction != null)
            {
                _cAction.performed += _ => _pendingCToggle = true;
            }

            _zAction = RequireAction("Z", InputActionType.Button, "<Keyboard>/z");
            if (_zAction != null)
            {
                _zAction.performed += _ => _pendingZToggle = true;
            }

            _f5Action = RequireAction("F5", InputActionType.Button, "<Keyboard>/f5");
            if (_f5Action != null)
            {
                _f5Action.performed += _ => _pendingF5Toggle = true;
            }

            _f6Action = RequireAction("F6", InputActionType.Button, "<Keyboard>/f6");
            if (_f6Action != null)
            {
                _f6Action.performed += _ => _pendingF6Toggle = true;
            }
        }

        private InputAction RequireAction(string actionName, InputActionType type, string defaultBinding)
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

        private void EnableActions()
        {
            _map?.Enable();
        }

        private void DisableActions()
        {
            _map?.Disable();
        }
    }
}
