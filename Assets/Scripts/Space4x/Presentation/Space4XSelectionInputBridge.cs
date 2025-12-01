using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Space4X.Presentation
{
    /// <summary>
    /// MonoBehaviour that bridges Input System selection actions to ECS singleton components.
    /// Runs every frame to capture input before presentation systems.
    /// </summary>
    public class Space4XSelectionInputBridge : MonoBehaviour
    {
        [Header("Input Action References")]
        [Tooltip("Reference to the Input Action Asset")]
        public InputActionAsset InputActions;

        [Header("Debug")]
        [Tooltip("Log input events")]
        public bool DebugLog = false;

        private InputActionMap _playerMap;
        private InputAction _clickAction;
        private InputAction _boxSelectAction;
        private InputAction _shiftAction;
        private InputAction _deselectAction;
        private InputAction _cycleFleetsAction;
        private InputAction _toggleOverlaysAction;
        private InputAction _debugViewAction;

        private World _world;
        private EntityQuery _selectionInputQuery;
        private EntityQuery _commandInputQuery;

        private bool _initialized;
        private bool _boxStarted;
        private float2 _boxStartPosition;

        private void Awake()
        {
            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null)
            {
                Debug.LogWarning("[Space4XSelectionInputBridge] No default world found.");
                return;
            }

            _selectionInputQuery = _world.EntityManager.CreateEntityQuery(typeof(SelectionInput));
            _commandInputQuery = _world.EntityManager.CreateEntityQuery(typeof(CommandInput));
        }

        private void OnEnable()
        {
            InitializeInputActions();
        }

        private void OnDisable()
        {
            DisableInputActions();
        }

        private void InitializeInputActions()
        {
            if (InputActions == null)
            {
                // Try to find input actions asset
                InputActions = Resources.Load<InputActionAsset>("Space4X_Input");
                if (InputActions == null)
                {
                    // Create default input actions
                    InputActions = CreateDefaultInputActions();
                }
            }

            if (InputActions == null)
            {
                Debug.LogError("[Space4XSelectionInputBridge] No Input Action Asset found.");
                return;
            }

            _playerMap = InputActions.FindActionMap("Player");
            if (_playerMap == null)
            {
                _playerMap = InputActions.FindActionMap("Space4X_Player");
            }
            if (_playerMap == null && InputActions.actionMaps.Count > 0)
            {
                _playerMap = InputActions.actionMaps[0];
            }

            if (_playerMap == null)
            {
                Debug.LogError("[Space4XSelectionInputBridge] No Player action map found.");
                return;
            }

            // Get or create actions
            _clickAction = _playerMap.FindAction("Selection_Click") ?? _playerMap.FindAction("Click");
            _boxSelectAction = _playerMap.FindAction("Selection_Box") ?? _playerMap.FindAction("BoxSelect");
            _shiftAction = _playerMap.FindAction("Shift") ?? _playerMap.FindAction("MultiSelect");
            _deselectAction = _playerMap.FindAction("Selection_Deselect") ?? _playerMap.FindAction("Deselect");
            _cycleFleetsAction = _playerMap.FindAction("Command_CycleFleets") ?? _playerMap.FindAction("CycleFleets");
            _toggleOverlaysAction = _playerMap.FindAction("Command_ToggleOverlays") ?? _playerMap.FindAction("ToggleOverlays");
            _debugViewAction = _playerMap.FindAction("Command_DebugView") ?? _playerMap.FindAction("DebugView");

            _playerMap.Enable();
            _initialized = true;

            if (DebugLog)
            {
                Debug.Log("[Space4XSelectionInputBridge] Input actions initialized.");
            }
        }

        private void DisableInputActions()
        {
            if (_playerMap != null)
            {
                _playerMap.Disable();
            }
        }

        private InputActionAsset CreateDefaultInputActions()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = new InputActionMap("Player");

            // Click action
            var click = map.AddAction("Selection_Click", InputActionType.Button);
            click.AddBinding("<Mouse>/leftButton");

            // Shift action for multi-select
            var shift = map.AddAction("Shift", InputActionType.Button);
            shift.AddBinding("<Keyboard>/leftShift");
            shift.AddBinding("<Keyboard>/rightShift");

            // Deselect action
            var deselect = map.AddAction("Selection_Deselect", InputActionType.Button);
            deselect.AddBinding("<Mouse>/rightButton");
            deselect.AddBinding("<Keyboard>/escape");

            // Cycle fleets
            var cycleFleets = map.AddAction("Command_CycleFleets", InputActionType.Button);
            cycleFleets.AddBinding("<Keyboard>/tab");

            // Toggle overlays
            var toggleOverlays = map.AddAction("Command_ToggleOverlays", InputActionType.Button);
            toggleOverlays.AddBinding("<Keyboard>/o");

            // Debug view
            var debugView = map.AddAction("Command_DebugView", InputActionType.Button);
            debugView.AddBinding("<Keyboard>/f1");

            asset.AddActionMap(map);
            return asset;
        }

        private void Update()
        {
            if (!_initialized || _world == null || !_world.IsCreated)
            {
                return;
            }

            UpdateSelectionInput();
            UpdateCommandInput();
        }

        private void UpdateSelectionInput()
        {
            var selectionInput = new SelectionInput();

            // Get mouse position normalized to screen
            Vector2 mousePos = Mouse.current?.position.ReadValue() ?? Vector2.zero;
            float2 normalizedPos = new float2(
                mousePos.x / Screen.width,
                mousePos.y / Screen.height
            );

            selectionInput.ClickPosition = normalizedPos;

            // Click detection
            bool clickPressed = _clickAction?.WasPressedThisFrame() ?? false;
            bool clickHeld = _clickAction?.IsPressed() ?? false;

            selectionInput.ClickPressed = clickPressed;
            selectionInput.ClickHeld = clickHeld;

            // Box selection
            if (clickPressed && !_boxStarted)
            {
                _boxStarted = true;
                _boxStartPosition = normalizedPos;
            }

            if (_boxStarted)
            {
                selectionInput.BoxStart = _boxStartPosition;
                selectionInput.BoxEnd = normalizedPos;

                // Check if it's actually a box (dragged more than threshold)
                float dragDistance = math.distance(_boxStartPosition, normalizedPos);
                selectionInput.BoxActive = dragDistance > 0.02f; // 2% of screen
            }

            if (!clickHeld && _boxStarted)
            {
                _boxStarted = false;
            }

            // Shift for multi-select
            selectionInput.ShiftHeld = _shiftAction?.IsPressed() ?? false;

            // Deselect
            selectionInput.DeselectRequested = _deselectAction?.WasPressedThisFrame() ?? false;

            // Write to ECS
            WriteSelectionInput(selectionInput);

            if (DebugLog && (clickPressed || selectionInput.DeselectRequested))
            {
                Debug.Log($"[Space4XSelectionInputBridge] Click: {clickPressed}, Deselect: {selectionInput.DeselectRequested}, Pos: {normalizedPos}");
            }
        }

        private void UpdateCommandInput()
        {
            // Check for right-click (command issue)
            bool rightClickPressed = Mouse.current?.rightButton.wasPressedThisFrame ?? false;
            Vector2 mousePos = Mouse.current?.position.ReadValue() ?? Vector2.zero;

            var commandInput = new CommandInput
            {
                CycleFleetsPressed = _cycleFleetsAction?.WasPressedThisFrame() ?? false,
                ToggleOverlaysPressed = _toggleOverlaysAction?.WasPressedThisFrame() ?? false,
                DebugViewPressed = _debugViewAction?.WasPressedThisFrame() ?? false,
                IssueMoveCommand = rightClickPressed, // Right-click issues move command for now
                IssueAttackCommand = false, // Would need target detection
                IssueMineCommand = false, // Would need asteroid detection
                CancelCommand = false,
                CommandTargetPosition = float3.zero, // Would convert screen to world
                CommandTargetEntity = Entity.Null
            };

            WriteCommandInput(commandInput);

            if (DebugLog && (commandInput.CycleFleetsPressed || commandInput.ToggleOverlaysPressed || commandInput.DebugViewPressed))
            {
                Debug.Log($"[Space4XSelectionInputBridge] CycleFleets: {commandInput.CycleFleetsPressed}, ToggleOverlays: {commandInput.ToggleOverlaysPressed}, DebugView: {commandInput.DebugViewPressed}");
            }
        }

        private void WriteSelectionInput(SelectionInput input)
        {
            if (_selectionInputQuery.IsEmptyIgnoreFilter)
            {
                var entity = _world.EntityManager.CreateEntity(typeof(SelectionInput));
                _world.EntityManager.SetComponentData(entity, input);
            }
            else
            {
                var entity = _selectionInputQuery.GetSingletonEntity();
                _world.EntityManager.SetComponentData(entity, input);
            }
        }

        private void WriteCommandInput(CommandInput input)
        {
            if (_commandInputQuery.IsEmptyIgnoreFilter)
            {
                var entity = _world.EntityManager.CreateEntity(typeof(CommandInput));
                _world.EntityManager.SetComponentData(entity, input);
            }
            else
            {
                var entity = _commandInputQuery.GetSingletonEntity();
                _world.EntityManager.SetComponentData(entity, input);
            }
        }
    }
}

