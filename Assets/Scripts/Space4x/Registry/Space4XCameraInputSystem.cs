using PureDOTS.Runtime.Hybrid;
using PureDOTS.Systems;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Burst;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Space4X.Registry
{
    /// <summary>
    /// Pure DOTS system that reads Input System actions directly and writes to camera control state.
    /// Runs early in PresentationSystemGroup to capture input before camera update.
    /// Non-Burst compatible due to Input System access.
    /// </summary>
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    public partial struct Space4XCameraInputSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstDiscard]
        public void OnUpdate(ref SystemState state)
        {
            if (!HybridControlCoordinator.Space4XInputEnabled)
            {
                DisableCameraInputs();
                ZeroOutCameraControlState(ref state);
                return;
            }

            if (!TryEnsureConfig(ref state, out var inputConfig))
            {
                return;
            }
            
            // Log once that system is running
            if (UnityEngine.Time.frameCount == 1)
            {
                Debug.Log("[Space4XCameraInputSystem] System is running and looking for input...");
            }

            var inputActions = FindInputActionAsset();
            if (inputActions == null)
            {
                return;
            }

            // Always get fresh references - don't cache Unity objects in struct systems
            InputActionMap cameraActionMap = null;
            InputAction panAction = null;
            InputAction zoomAction = null;
            InputAction verticalMoveAction = null;
            InputAction rotateAction = null;
            InputAction resetAction = null;
            InputAction toggleVerticalModeAction = null;

            try
            {
                cameraActionMap = inputActions.FindActionMap("Camera");
                if (cameraActionMap != null)
                {
                    panAction = cameraActionMap.FindAction("Pan");
                    zoomAction = cameraActionMap.FindAction("Zoom");
                    verticalMoveAction = cameraActionMap.FindAction("VerticalMove");
                    rotateAction = cameraActionMap.FindAction("Rotate");
                    resetAction = cameraActionMap.FindAction("Reset");
                    toggleVerticalModeAction = cameraActionMap.FindAction("ToggleVerticalMode");

                    if (!cameraActionMap.enabled)
                    {
                        cameraActionMap.Enable();
                        Debug.Log("[Space4XCameraInputSystem] Enabled Camera action map");
                    }
                    
                    // Ensure the InputActionAsset itself is enabled
                    if (!inputActions.enabled)
                    {
                        inputActions.Enable();
                        Debug.Log("[Space4XCameraInputSystem] Enabled InputActionAsset");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Space4XCameraInputSystem: Error initializing input actions: {ex.Message}");
                return;
            }

            if (cameraActionMap == null)
            {
                if (UnityEngine.Time.frameCount % 300 == 0)
                {
                    Debug.LogWarning("[Space4XCameraInputSystem] Camera action map not found! Check InputActionAsset has 'Camera' action map.");
                }
                return;
            }
            
            if (panAction == null || zoomAction == null || verticalMoveAction == null || rotateAction == null || resetAction == null || toggleVerticalModeAction == null)
            {
                if (UnityEngine.Time.frameCount % 300 == 0)
                {
                    Debug.LogWarning($"[Space4XCameraInputSystem] Missing actions - Pan: {panAction != null}, Zoom: {zoomAction != null}, VerticalMove: {verticalMoveAction != null}, Rotate: {rotateAction != null}, Reset: {resetAction != null}, ToggleVerticalMode: {toggleVerticalModeAction != null}");
                }
                return;
            }

            var controlState = ReadInputActions(inputConfig, panAction, zoomAction, verticalMoveAction, rotateAction, resetAction, toggleVerticalModeAction);
            
            // Debug logging (include rotation input)
            if (UnityEngine.Time.frameCount % 60 == 0 && (math.lengthsq(controlState.PanInput) > 0f || math.abs(controlState.ZoomInput) > 0f || math.abs(controlState.VerticalMoveInput) > 0f || math.lengthsq(controlState.RotateInput) > 0f))
            {
                Debug.Log($"[Space4XCameraInputSystem] Input - Pan: {controlState.PanInput}, Zoom: {controlState.ZoomInput}, Vertical: {controlState.VerticalMoveInput}, Rotate: {controlState.RotateInput}, RotationEnabled: {controlState.EnableRotation}");
            }
            
            if (!SystemAPI.HasSingleton<Space4XCameraControlState>())
            {
                var entity = state.EntityManager.CreateEntity(typeof(Space4XCameraControlState));
                SystemAPI.SetComponent(entity, controlState);
                Debug.Log("[Space4XCameraInputSystem] Created Space4XCameraControlState singleton");
            }
            else
            {
                var entity = SystemAPI.GetSingletonEntity<Space4XCameraControlState>();
                SystemAPI.SetComponent(entity, controlState);
            }
        }

        [BurstDiscard]
        private InputActionAsset FindInputActionAsset()
        {
            try
            {
                var authoring = Object.FindFirstObjectByType<Space4XCameraInputAuthoring>();
                if (authoring != null)
                {
                    var actions = authoring.InputActions;
                    if (actions != null)
                    {
                        return actions;
                    }
                    else
                    {
                        Debug.LogWarning("Space4XCameraInputSystem: InputActionAsset is null on authoring component.");
                    }
                }
                else
                {
                    Debug.LogWarning("Space4XCameraInputSystem: Space4XCameraInputAuthoring component not found in scene.");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Space4XCameraInputSystem: Error finding InputActionAsset: {ex.Message}");
            }
            
            return null;
        }

        [BurstDiscard]
        private Space4XCameraControlState ReadInputActions(
            Space4XCameraInputConfig config,
            InputAction panAction,
            InputAction zoomAction,
            InputAction verticalMoveAction,
            InputAction rotateAction,
            InputAction resetAction,
            InputAction toggleVerticalModeAction)
        {
            Vector2 panValue = Vector2.zero;
            float zoomScalar = 0f;
            float verticalMoveValue = 0f;
            Vector2 rotateValue = Vector2.zero;
            bool resetPressed = false;
            bool toggleVerticalModePressed = false;

            try
            {
                panValue = ReadVector2Safe(panAction);
                zoomScalar = ReadScalarSafe(zoomAction);
                verticalMoveValue = ReadScalarSafe(verticalMoveAction);
                rotateValue = ReadVector2Safe(rotateAction);
                resetPressed = resetAction.WasPressedThisFrame();
                toggleVerticalModePressed = toggleVerticalModeAction.WasPressedThisFrame();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Space4XCameraInputSystem: Error reading input values: {ex.Message}");
            }

            bool rightMouseHeld = false;
            try
            {
                rightMouseHeld = Mouse.current != null && Mouse.current.rightButton.isPressed;
                
                // Lock cursor when RMB is held to prevent cursor position resolution issues
                if (rightMouseHeld && config.RequireRightMouseForRotation)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                else if (Cursor.lockState == CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }
            catch
            {
                // Mouse may be unavailable
            }

            // Allow rotation with mouse movement OR right-click + mouse movement (more flexible)
            var effectiveRotateInput = config.EnableRotation ? rotateValue : Vector2.zero;
            // If rotation is enabled but requires right mouse, check that condition
            bool rotationEnabled = config.EnableRotation && (!config.RequireRightMouseForRotation || rightMouseHeld);

            return new Space4XCameraControlState
            {
                PanInput = math.float2(panValue.x, panValue.y),
                ZoomInput = zoomScalar,
                VerticalMoveInput = verticalMoveValue,
                RotateInput = rotationEnabled ? math.float2(effectiveRotateInput.x, effectiveRotateInput.y) : float2.zero,
                ResetRequested = resetPressed,
                ToggleVerticalModeRequested = toggleVerticalModePressed,
                EnablePan = config.EnablePan,
                EnableZoom = config.EnableZoom,
                EnableVerticalMove = config.EnableVerticalMove,
                EnableRotation = rotationEnabled
            };
        }

        private static Vector2 ReadVector2Safe(InputAction action)
        {
            if (action == null)
            {
                return Vector2.zero;
            }

            try
            {
                return action.ReadValue<Vector2>();
            }
            catch (System.Exception)
            {
            }

            try
            {
                var scalar = action.ReadValue<float>();
                return new Vector2(scalar, 0f);
            }
            catch (System.Exception)
            {
            }

            return Vector2.zero;
        }

        private static float ReadScalarSafe(InputAction action)
        {
            if (action == null)
            {
                return 0f;
            }

            try
            {
                return action.ReadValue<float>();
            }
            catch (System.Exception)
            {
            }

            try
            {
                var value = action.ReadValue<Vector2>();
                return Mathf.Abs(value.y) >= Mathf.Abs(value.x) ? value.y : value.x;
            }
            catch (System.Exception)
            {
            }

            return 0f;
        }

        private void DisableCameraInputs()
        {
            try
            {
                var authoring = Object.FindFirstObjectByType<Space4XCameraInputAuthoring>();
                if (authoring?.InputActions == null)
                {
                    return;
                }

                var cameraMap = authoring.InputActions.FindActionMap("Camera", throwIfNotFound: false);
                if (cameraMap != null && cameraMap.enabled)
                {
                    cameraMap.Disable();
                }

                if (authoring.InputActions.enabled)
                {
                    authoring.InputActions.Disable();
                }
            }
            catch
            {
                // Intentionally ignored; disabling is best effort only.
            }
        }

        private void ZeroOutCameraControlState(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<Space4XCameraControlState>())
            {
                return;
            }

            var entity = SystemAPI.GetSingletonEntity<Space4XCameraControlState>();
            SystemAPI.SetComponent(entity, default(Space4XCameraControlState));
        }

        private bool TryEnsureConfig(ref SystemState state, out Space4XCameraInputConfig config)
        {
            if (SystemAPI.TryGetSingleton<Space4XCameraInputConfig>(out config))
            {
                return true;
            }

            Space4XCameraInputAuthoring authoring = null;
            try
            {
                authoring = Object.FindFirstObjectByType<Space4XCameraInputAuthoring>();
            }
            catch
            {
                // ignore - FindFirstObjectByType can throw when quitting
            }

            if (authoring == null)
            {
                if (UnityEngine.Time.frameCount == 1)
                {
                    Debug.LogWarning("[Space4XCameraInputSystem] Space4XCameraInputAuthoring not found. Camera input config missing.");
                }
                config = default;
                return false;
            }

            if (authoring.InputActions == null)
            {
                if (UnityEngine.Time.frameCount % 300 == 0)
                {
                    Debug.LogWarning("[Space4XCameraInputSystem] InputActionAsset is null on authoring component. Assign an InputActionAsset.");
                }
                config = default;
                return false;
            }

            config = authoring.BuildConfigData();

            var entity = state.EntityManager.CreateEntity(typeof(Space4XCameraInputConfig));
            state.EntityManager.SetComponentData(entity, config);
            Debug.Log("[Space4XCameraInputSystem] Created Space4XCameraInputConfig singleton at runtime from authoring component.");
            return true;
        }
    }
}
