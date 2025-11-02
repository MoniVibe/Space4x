using Godgame.Interaction;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Godgame.Interaction.Input
{
    /// <summary>
    /// Reads Unity Input System Actions and populates InputState singleton component.
    /// Uses Unity Input System package exclusively (no legacy Input Manager).
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct InputReaderSystem : ISystem
    {
        private InputActionAsset _inputActions;
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _cameraVerticalAction;
        private InputAction _cameraToggleModeAction;
        private bool _initialized;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<InputState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Initialize input actions on first update
            if (!_initialized)
            {
                InitializeInputActions();
                _initialized = true;
            }

            if (_inputActions == null)
            {
                return;
            }

            // Get or create InputState singleton
            var inputStateEntity = SystemAPI.GetSingletonEntity<InputState>();
            var inputStateRW = SystemAPI.GetComponentRW<InputState>(inputStateEntity);
            ref var inputState = ref inputStateRW.ValueRW;

            // Read mouse position and delta
            var mouse = Mouse.current;
            if (mouse != null)
            {
                inputState.PointerPos = mouse.position.ReadValue();
                
                // Read Look action for mouse delta
                if (_lookAction != null)
                {
                    var lookDelta = _lookAction.ReadValue<Vector2>();
                    inputState.PointerDelta = new float2(lookDelta.x, lookDelta.y);
                }
                else
                {
                    inputState.PointerDelta = float2.zero;
                }

                // Read scroll wheel
                var scroll = mouse.scroll.ReadValue();
                inputState.Scroll = scroll.y;

                // Read mouse button states
                inputState.PrimaryHeld = mouse.leftButton.isPressed;
                inputState.SecondaryHeld = mouse.rightButton.isPressed;
                inputState.MiddleHeld = mouse.middleButton.isPressed;
            }
            else
            {
                inputState.PointerPos = float2.zero;
                inputState.PointerDelta = float2.zero;
                inputState.Scroll = 0f;
                inputState.PrimaryHeld = false;
                inputState.SecondaryHeld = false;
                inputState.MiddleHeld = false;
            }

            // Read Move action (WASD)
            if (_moveAction != null)
            {
                var moveValue = _moveAction.ReadValue<Vector2>();
                inputState.Move = new float2(moveValue.x, moveValue.y);
            }
            else
            {
                inputState.Move = float2.zero;
            }

            // Read CameraVertical action (Q/E)
            if (_cameraVerticalAction != null)
            {
                inputState.Vertical = _cameraVerticalAction.ReadValue<float>();
            }
            else
            {
                inputState.Vertical = 0f;
            }

            // Read CameraToggleMode action (button press)
            if (_cameraToggleModeAction != null)
            {
                inputState.CameraToggleMode = _cameraToggleModeAction.WasPressedThisFrame();
            }
            else
            {
                inputState.CameraToggleMode = false;
            }

            // Read keyboard for ThrowModifier (check if Shift is held)
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                inputState.ThrowModifier = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            }
            else
            {
                inputState.ThrowModifier = false;
            }
        }

        private void InitializeInputActions()
        {
            // Try multiple methods to load InputActionAsset
            InputActionAsset inputActionsAsset = null;

            // Method 1: Try Resources.Load
            inputActionsAsset = Resources.Load<InputActionAsset>("InputSystem_Actions");

            // Method 2: Try loading from Assets path (Editor only)
#if UNITY_EDITOR
            if (inputActionsAsset == null)
            {
                inputActionsAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            }
#endif

            // Method 3: Try project-wide actions if available
            if (inputActionsAsset == null && UnityEngine.InputSystem.InputSystem.actions != null)
            {
                inputActionsAsset = UnityEngine.InputSystem.InputSystem.actions;
            }

            if (inputActionsAsset == null)
            {
                Debug.LogWarning("[InputReaderSystem] InputActionAsset not found. Please ensure InputSystem_Actions.inputactions exists in Assets folder or Resources folder. Input will not be read.");
                return;
            }

            _inputActions = inputActionsAsset;
            
            // Enable action maps
            var playerMap = _inputActions.FindActionMap("Player");
            if (playerMap != null)
            {
                playerMap.Enable();
                _moveAction = playerMap.FindAction("Move");
                _lookAction = playerMap.FindAction("Look");
            }

            // Enable Camera action map
            var cameraMap = _inputActions.FindActionMap("Camera");
            if (cameraMap != null)
            {
                cameraMap.Enable();
                _cameraVerticalAction = cameraMap.FindAction("CameraVertical");
                _cameraToggleModeAction = cameraMap.FindAction("CameraToggleMode");
            }
        }

        public void OnDestroy(ref SystemState state)
        {
            if (_inputActions != null)
            {
                _inputActions.Disable();
            }
        }
    }
}

