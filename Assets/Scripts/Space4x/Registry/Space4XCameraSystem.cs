using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Burst;
using UnityEngine;

namespace Space4X.Registry
{
    /// <summary>
    /// DOTS system that reads camera control input and updates the Camera GameObject transform.
    /// Runs in PresentationSystemGroup to align with PureDOTS update order.
    /// Non-Burst compatible due to Unity GameObject/Transform access.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XCameraInputSystem))]
    public partial struct Space4XCameraSystem : ISystem
    {
        private bool _loggedSystemReady;
        private bool _loggedMissingState;
        private bool _loggedMissingConfig;
        private bool _loggedMissingCamera;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstDiscard]
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingleton<RewindState>(out var rewind) && rewind.Mode == RewindMode.Playback)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<Space4XCameraControlState>(out var controlState))
            {
                controlState = new Space4XCameraControlState
                {
                    EnablePan = true,
                    EnableZoom = true,
                    EnableVerticalMove = true,
                    EnableRotation = false
                };
            }

            // Initialize persistent state if it doesn't exist
            if (!SystemAPI.HasSingleton<Space4XCameraPersistentState>())
            {
                var persistentEntity = state.EntityManager.CreateEntity(typeof(Space4XCameraPersistentState));
                SystemAPI.SetComponent(persistentEntity, new Space4XCameraPersistentState
                {
                    VerticalMoveCameraRelative = false // Default to world Y-axis mode
                });
            }

            if (!TryEnsureCameraState(ref state, out var cameraStateEntity, out var cameraState))
            {
                if (!_loggedMissingState)
                {
                    Debug.LogWarning("[Space4XCameraSystem] Space4XCameraState singleton not found. Move the Main Camera into the SubScene or run Setup Dual Mining Demo again.");
                    _loggedMissingState = true;
                }
                return;
            }
            _loggedMissingState = false;

            // Handle toggle vertical mode
            if (controlState.ToggleVerticalModeRequested)
            {
                var persistentEntity = SystemAPI.GetSingletonEntity<Space4XCameraPersistentState>();
                var persistentState = SystemAPI.GetComponent<Space4XCameraPersistentState>(persistentEntity);
                persistentState.VerticalMoveCameraRelative = !persistentState.VerticalMoveCameraRelative;
                SystemAPI.SetComponent(persistentEntity, persistentState);
                Debug.Log($"Vertical movement mode: {(persistentState.VerticalMoveCameraRelative ? "Camera-relative" : "World Y-axis")}");
            }

            if (!SystemAPI.TryGetSingleton<Space4XCameraConfig>(out var config))
            {
                if (!TryEnsureCameraConfig(ref state, out config))
                {
                    if (!_loggedMissingConfig)
                    {
                        Debug.LogWarning("[Space4XCameraSystem] Space4XCameraConfig singleton not found. Assign a camera profile or run the setup tool.");
                        _loggedMissingConfig = true;
                    }
                    return;
                }
            }
            _loggedMissingConfig = false;

            if (!_loggedSystemReady)
            {
                Debug.Log("[Space4XCameraSystem] System is running and ready to drive the camera.");
                _loggedSystemReady = true;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var deltaTime = timeState.FixedDeltaTime > 0f ? timeState.FixedDeltaTime : UnityEngine.Time.deltaTime;

            var camera = GetCamera();
            if (camera == null)
            {
                if (UnityEngine.Time.frameCount % 120 == 0 && !_loggedMissingCamera)
                {
                    Debug.LogWarning("[Space4XCameraSystem] No Camera found! Camera movement will not work.");
                    _loggedMissingCamera = true;
                }
                return;
            }
            _loggedMissingCamera = false;
            
            // Debug logging (log rotation too, and log more frequently when rotating)
            if (UnityEngine.Time.frameCount % 60 == 0 && (math.lengthsq(controlState.PanInput) > 0f || math.abs(controlState.ZoomInput) > 0f || math.lengthsq(controlState.RotateInput) > 0f))
            {
                var rotationEuler = math.degrees(math.EulerXYZ(cameraState.Rotation));
                Debug.Log($"[Space4XCameraSystem] Input - Pan: {controlState.PanInput}, Zoom: {controlState.ZoomInput}, Rotate: {controlState.RotateInput}, Pos: {cameraState.Position}, Rot: ({rotationEuler.x:F1}, {rotationEuler.y:F1}, {rotationEuler.z:F1})");
            }

            var focusPoint = cameraState.FocusPoint;
            var targetRotation = cameraState.Rotation;
            var targetZoomDistance = cameraState.ZoomDistance;

            // Extract horizontal plane rotation (Y-axis only) for consistent panning
            var euler = math.EulerXYZ(targetRotation);
            var horizontalRotation = quaternion.EulerXYZ(0f, euler.y, 0f);

            if (controlState.ResetRequested)
            {
                targetRotation = cameraState.InitialRotation;
                targetZoomDistance = math.distance(cameraState.InitialPosition, cameraState.FocusPoint);
                var forward = math.mul(targetRotation, math.float3(0f, 0f, 1f));
                focusPoint = cameraState.InitialPosition + forward * targetZoomDistance;
                horizontalRotation = quaternion.EulerXYZ(0f, math.EulerXYZ(targetRotation).y, 0f);
            }
            else
            {
                if (controlState.EnablePan && math.lengthsq(controlState.PanInput) > 0f)
                {
                    var panSpeed = config.PanSpeed * deltaTime;
                    var panInput = controlState.PanInput * panSpeed;

                    // Use horizontal rotation only for panning to maintain consistent plane orientation
                    var right = math.mul(horizontalRotation, math.float3(1f, 0f, 0f));
                    var forward = math.mul(horizontalRotation, math.float3(0f, 0f, 1f));

                    var worldPan = right * panInput.x + forward * panInput.y;
                    focusPoint += worldPan;

                    if (config.UsePanBounds)
                    {
                        focusPoint = math.clamp(focusPoint, config.PanBoundsMin, config.PanBoundsMax);
                    }
                }

                if (controlState.EnableVerticalMove && math.abs(controlState.VerticalMoveInput) > 0f)
                {
                    var persistentState = SystemAPI.GetSingleton<Space4XCameraPersistentState>();
                    var verticalSpeed = config.VerticalMoveSpeed * deltaTime;
                    var verticalDelta = controlState.VerticalMoveInput * verticalSpeed;

                    if (persistentState.VerticalMoveCameraRelative)
                    {
                        var cameraUp = math.mul(targetRotation, math.float3(0f, 1f, 0f));
                        focusPoint += cameraUp * verticalDelta;
                    }
                    else
                    {
                        focusPoint.y += verticalDelta;
                    }

                    if (config.UsePanBounds)
                    {
                        focusPoint = math.clamp(focusPoint, config.PanBoundsMin, config.PanBoundsMax);
                    }
                }

                if (controlState.EnableRotation && math.lengthsq(controlState.RotateInput) > 0f)
                {
                    var rotateInput = controlState.RotateInput;
                    // Invert Y axis for pitch (mouse Y up should pitch camera up, not down)
                    var yawDelta = rotateInput.x * math.radians(config.RotationSpeed) * deltaTime;
                    var pitchDelta = -rotateInput.y * math.radians(config.RotationSpeed) * deltaTime; // Inverted for natural feel

                    euler = math.EulerXYZ(targetRotation);
                    var newYaw = euler.y + yawDelta;
                    var newPitch = euler.x + pitchDelta;
                    
                    // Clamp pitch to prevent flipping beyond limits
                    newPitch = math.clamp(newPitch, config.PitchMin, config.PitchMax);
                    
                    // Normalize yaw to [-PI, PI] range to prevent drift while maintaining continuity
                    // This prevents issues when rotating multiple times around
                    while (newYaw > math.PI) newYaw -= math.PI * 2f;
                    while (newYaw < -math.PI) newYaw += math.PI * 2f;

                    targetRotation = quaternion.EulerXYZ(newPitch, newYaw, 0f);
                    // Update horizontal rotation for next frame's panning
                    horizontalRotation = quaternion.EulerXYZ(0f, newYaw, 0f);
                }

                if (controlState.EnableZoom && math.abs(controlState.ZoomInput) > 0f)
                {
                    var zoomDelta = controlState.ZoomInput * config.ZoomSpeed * deltaTime;
                    targetZoomDistance = math.clamp(targetZoomDistance - zoomDelta, config.ZoomMinDistance, config.ZoomMaxDistance);
                }
            }

            var targetForward = math.mul(targetRotation, math.float3(0f, 0f, 1f));
            var targetPosition = focusPoint - targetForward * targetZoomDistance;

            float3 finalPosition;
            quaternion finalRotation;
            float finalZoom;
            float3 finalFocus;

            if (config.Smoothing > 0f)
            {
                var smoothingFactor = 1f - math.pow(1f - config.Smoothing, deltaTime * 60f);
                finalRotation = math.slerp(cameraState.Rotation, targetRotation, smoothingFactor);
                finalZoom = math.lerp(cameraState.ZoomDistance, targetZoomDistance, smoothingFactor);
                finalFocus = math.lerp(cameraState.FocusPoint, focusPoint, smoothingFactor);
                var forward = math.mul(finalRotation, math.float3(0f, 0f, 1f));
                finalPosition = finalFocus - forward * finalZoom;
            }
            else
            {
                finalPosition = targetPosition;
                finalRotation = targetRotation;
                finalZoom = targetZoomDistance;
                finalFocus = focusPoint;
            }

            var updatedState = cameraState;
            updatedState.Position = finalPosition;
            updatedState.Rotation = finalRotation;
            updatedState.ZoomDistance = finalZoom;
            updatedState.FocusPoint = finalFocus;

            if (config.UsePanBounds)
            {
                updatedState.FocusPoint = math.clamp(updatedState.FocusPoint, config.PanBoundsMin, config.PanBoundsMax);
                var clampedForward = math.mul(updatedState.Rotation, math.float3(0f, 0f, 1f));
                updatedState.Position = updatedState.FocusPoint - clampedForward * updatedState.ZoomDistance;
            }

            SystemAPI.SetComponent(cameraStateEntity, updatedState);

            ApplyTransformToCamera(camera, updatedState);
        }

        [BurstDiscard]
        private Camera GetCamera()
        {
            // Don't cache - always get fresh reference since struct systems can't hold managed references
            var camera = Camera.main;
            if (camera == null)
            {
                camera = Object.FindFirstObjectByType<Camera>();
            }
            return camera;
        }

        [BurstDiscard]
        private void ApplyTransformToCamera(Camera camera, Space4XCameraState state)
        {
            if (camera == null)
            {
                return;
            }

            camera.transform.position = state.Position;
            camera.transform.rotation = state.Rotation;
        }

        private bool TryEnsureCameraState(ref SystemState state, out Entity stateEntity, out Space4XCameraState cameraState)
        {
            if (SystemAPI.TryGetSingletonEntity<Space4XCameraState>(out stateEntity))
            {
                cameraState = SystemAPI.GetComponent<Space4XCameraState>(stateEntity);
                return true;
            }

            Space4XCameraAuthoring authoring = null;
            try
            {
                authoring = Object.FindFirstObjectByType<Space4XCameraAuthoring>();
            }
            catch
            {
                // ignored – FindFirstObjectByType may throw during shutdown
            }

            if (authoring == null)
            {
                cameraState = default;
                stateEntity = Entity.Null;
                return false;
            }

            cameraState = authoring.BuildInitialState();
            stateEntity = state.EntityManager.CreateEntity(typeof(Space4XCameraState));
            state.EntityManager.SetComponentData(stateEntity, cameraState);
            Debug.Log("[Space4XCameraSystem] Created Space4XCameraState singleton at runtime from authoring component.");

            return true;
        }

        private bool TryEnsureCameraConfig(ref SystemState state, out Space4XCameraConfig config)
        {
            if (SystemAPI.TryGetSingleton<Space4XCameraConfig>(out config))
            {
                return true;
            }

            Space4XCameraAuthoring authoring = null;
            try
            {
                authoring = Object.FindFirstObjectByType<Space4XCameraAuthoring>();
            }
            catch
            {
                // ignored
            }

            if (authoring == null)
            {
                config = default;
                return false;
            }

            config = authoring.BuildConfigData();
            var entity = state.EntityManager.CreateEntity(typeof(Space4XCameraConfig));
            state.EntityManager.SetComponentData(entity, config);
            Debug.Log("[Space4XCameraSystem] Created Space4XCameraConfig singleton at runtime from authoring component.");
            return true;
        }
    }
}

