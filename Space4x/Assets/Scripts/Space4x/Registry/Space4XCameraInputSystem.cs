using PureDOTS.Systems;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Space4X.Registry
{
    /// <summary>
    /// Pure DOTS system that reads Input System actions directly and writes to camera control state.
    /// Runs early in PresentationSystemGroup to capture input before camera update.
    /// Non-Burst compatible due to Input System access.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(Space4XCameraSystem))]
    public partial struct Space4XCameraInputSystem : ISystem
    {
        private InputActionAsset cachedInputActions;
        private InputActionMap cameraActionMap;
        private InputAction panAction;
        private InputAction zoomAction;
        private InputAction rotateAction;
        private InputAction resetAction;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XCameraInputConfig>();
        }

        public void OnDestroy(ref SystemState state)
        {
            if (cameraActionMap != null)
            {
                cameraActionMap.Disable();
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<Space4XCameraInputConfig>(out var inputConfig))
            {
                return;
            }

            var inputActions = FindInputActionAsset();
            if (inputActions == null)
            {
                return;
            }

            if (cachedInputActions == null || cachedInputActions != inputActions)
            {
                InitializeInputActions(inputActions);
            }

            if (cameraActionMap == null || !cameraActionMap.enabled)
            {
                return;
            }

            var controlState = ReadInputActions(inputConfig);
            
            if (!SystemAPI.HasSingleton<Space4XCameraControlState>())
            {
                var entity = state.EntityManager.CreateEntity(typeof(Space4XCameraControlState));
                SystemAPI.SetComponent(entity, controlState);
            }
            else
            {
                var entity = SystemAPI.GetSingletonEntity<Space4XCameraControlState>();
                SystemAPI.SetComponent(entity, controlState);
            }
        }

        private InputActionAsset FindInputActionAsset()
        {
            var authoring = Object.FindFirstObjectByType<Space4XCameraInputAuthoring>();
            if (authoring != null && authoring.InputActions != null)
            {
                return authoring.InputActions;
            }
            
            return null;
        }

        private void InitializeInputActions(InputActionAsset inputActions)
        {
            if (inputActions == null)
            {
                cameraActionMap = null;
                return;
            }

            cachedInputActions = inputActions;
            cameraActionMap = inputActions.FindActionMap("Camera");
            
            if (cameraActionMap != null)
            {
                panAction = cameraActionMap.FindAction("Pan");
                zoomAction = cameraActionMap.FindAction("Zoom");
                rotateAction = cameraActionMap.FindAction("Rotate");
                resetAction = cameraActionMap.FindAction("Reset");
                cameraActionMap.Enable();
            }
        }

        private Space4XCameraControlState ReadInputActions(Space4XCameraInputConfig config)
        {
            var panValue = panAction != null ? panAction.ReadValue<Vector2>() : Vector2.zero;
            var zoomValue = zoomAction != null ? zoomAction.ReadValue<Vector2>() : Vector2.zero;
            var rotateValue = rotateAction != null ? rotateAction.ReadValue<Vector2>() : Vector2.zero;
            var resetPressed = resetAction != null && resetAction.WasPressedThisFrame();

            var rightMouseHeld = Mouse.current != null && Mouse.current.rightButton.isPressed;
            var effectiveRotateInput = (config.EnableRotation && rightMouseHeld) ? rotateValue : Vector2.zero;

            return new Space4XCameraControlState
            {
                PanInput = math.float2(panValue.x, panValue.y),
                ZoomInput = zoomValue.y != 0f ? zoomValue.y : (zoomValue.x != 0f ? zoomValue.x : 0f),
                RotateInput = math.float2(effectiveRotateInput.x, effectiveRotateInput.y),
                ResetRequested = resetPressed,
                EnablePan = config.EnablePan,
                EnableZoom = config.EnableZoom,
                EnableRotation = config.EnableRotation && rightMouseHeld
            };
        }
    }
}
