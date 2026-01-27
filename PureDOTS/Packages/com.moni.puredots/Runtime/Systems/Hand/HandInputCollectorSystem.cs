using PureDOTS.Runtime.Hand;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PureDOTS.Systems.Hand
{
    /// <summary>
    /// Collects hand input from Unity Input System and writes HandInputFrame singleton.
    /// Non-Burst system that bridges Mono input to ECS.
    /// Runs in InitializationSystemGroup to ensure input is available before simulation systems.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct HandInputCollectorSystem : ISystem
    {
        private bool _previousRmbHeld;
        private bool _previousLmbHeld;
        private bool _previousReleaseOnePressed;
        private bool _previousReleaseAllPressed;
        private bool _previousToggleThrowMode;
        private uint _sampleId;

        public void OnCreate(ref SystemState state)
        {
            // Ensure HandInputFrame singleton exists
            if (!SystemAPI.TryGetSingletonEntity<HandInputFrame>(out _))
            {
                var entity = state.EntityManager.CreateEntity(typeof(HandInputFrame));
                state.EntityManager.SetComponentData(entity, new HandInputFrame());
            }
        }

        [BurstDiscard]
        public void OnUpdate(ref SystemState state)
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;

            if (mouse == null || keyboard == null)
            {
                return;
            }

            // Get camera for raycast
            Camera camera = Camera.main;
            if (camera == null)
            {
                camera = Object.FindFirstObjectByType<Camera>();
            }

            if (camera == null)
            {
                return;
            }

            // Read mouse position
            Vector2 screenPos = mouse.position.ReadValue();
            float2 cursorScreenPos = new float2(screenPos.x, screenPos.y);

            // Convert to world ray (blittable)
            Ray ray = camera.ScreenPointToRay(screenPos);
            float3 rayOrigin = new float3(ray.origin.x, ray.origin.y, ray.origin.z);
            float3 rayDirection = new float3(ray.direction.x, ray.direction.y, ray.direction.z);
            rayDirection = math.normalizesafe(rayDirection, new float3(0f, 0f, 1f));

            // Read button states
            bool rmbPressed = mouse.rightButton.wasPressedThisFrame;
            bool rmbHeld = mouse.rightButton.isPressed;
            bool rmbReleased = mouse.rightButton.wasReleasedThisFrame;

            bool lmbPressed = mouse.leftButton.wasPressedThisFrame;
            bool lmbHeld = mouse.leftButton.isPressed;
            bool lmbReleased = mouse.leftButton.wasReleasedThisFrame;

            // Read modifiers
            bool shiftHeld = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            bool ctrlHeld = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
            bool altHeld = keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed;

            // Read hotkeys (Q/1 for release one, E/2 for release all, T for toggle throw mode)
            bool releaseOnePressed = keyboard.qKey.wasPressedThisFrame || keyboard.digit1Key.wasPressedThisFrame;
            bool releaseAllPressed = keyboard.eKey.wasPressedThisFrame || keyboard.digit2Key.wasPressedThisFrame;
            bool toggleThrowMode = keyboard.tKey.wasPressedThisFrame;

            // Read scroll delta
            Vector2 scrollValue = mouse.scroll.ReadValue();
            float scrollDelta = scrollValue.y; // Unity uses Y for vertical scroll

            // Read cancel action (Escape)
            bool cancelAction = keyboard.escapeKey.wasPressedThisFrame;

            // Write HandInputFrame singleton
            var inputFrame = new HandInputFrame
            {
                SampleId = ++_sampleId,
                CursorScreenPos = cursorScreenPos,
                RayOrigin = rayOrigin,
                RayDirection = rayDirection,
                RmbPressed = rmbPressed,
                RmbHeld = rmbHeld,
                RmbReleased = rmbReleased,
                LmbPressed = lmbPressed,
                LmbHeld = lmbHeld,
                LmbReleased = lmbReleased,
                ShiftHeld = shiftHeld,
                CtrlHeld = ctrlHeld,
                AltHeld = altHeld,
                ReleaseOnePressed = releaseOnePressed,
                ReleaseAllPressed = releaseAllPressed,
                ScrollDelta = scrollDelta,
                CancelAction = cancelAction,
                ToggleThrowMode = toggleThrowMode
            };

            SystemAPI.SetSingleton(inputFrame);

            // Update previous states for edge detection
            _previousRmbHeld = rmbHeld;
            _previousLmbHeld = lmbHeld;
            _previousReleaseOnePressed = releaseOnePressed;
            _previousReleaseAllPressed = releaseAllPressed;
            _previousToggleThrowMode = toggleThrowMode;
        }
    }
}

