using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Registry
{
    /// <summary>
    /// DOTS system that reads camera control input and updates the Camera GameObject transform.
    /// Runs in PresentationSystemGroup to align with PureDOTS update order.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct Space4XCameraSystem : ISystem
    {
        private Camera cachedCamera;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XCameraControlState>();
            state.RequireForUpdate<Space4XCameraState>();
            state.RequireForUpdate<Space4XCameraConfig>();
            state.RequireForUpdate<TimeState>();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingleton<RewindState>(out var rewind) && rewind.Mode == RewindMode.Playback)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<Space4XCameraControlState>(out var controlState))
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<Space4XCameraState>(out var cameraState))
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<Space4XCameraConfig>(out var config))
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var deltaTime = timeState.FixedDeltaTime;

            var camera = GetCamera();
            if (camera == null)
            {
                return;
            }

            var updatedState = cameraState;
            var updatedPosition = cameraState.Position;
            var updatedRotation = cameraState.Rotation;
            var updatedZoomDistance = cameraState.ZoomDistance;

            if (controlState.ResetRequested)
            {
                updatedPosition = cameraState.InitialPosition;
                updatedRotation = cameraState.InitialRotation;
                updatedZoomDistance = Vector3.Distance(cameraState.InitialPosition, cameraState.FocusPoint);
            }
            else
            {
                if (controlState.EnablePan && math.lengthsq(controlState.PanInput) > 0f)
                {
                    var panDirection = math.normalize(controlState.PanInput);
                    var panSpeed = config.PanSpeed * deltaTime;
                    var panDelta = panDirection * panSpeed;

                    var right = math.mul(updatedRotation, math.float3(1f, 0f, 0f));
                    var forward = math.mul(updatedRotation, math.float3(0f, 0f, 1f));
                    forward.y = 0f;
                    forward = math.normalize(forward);

                    var worldPan = right * panDelta.x + forward * panDelta.y;
                    updatedPosition += worldPan;

                    if (config.UsePanBounds)
                    {
                        updatedPosition = math.clamp(updatedPosition, config.PanBoundsMin, config.PanBoundsMax);
                    }
                }

                if (controlState.EnableZoom && math.abs(controlState.ZoomInput) > 0f)
                {
                    var zoomDelta = controlState.ZoomInput * config.ZoomSpeed * deltaTime;
                    updatedZoomDistance = math.clamp(updatedZoomDistance - zoomDelta, config.ZoomMinDistance, config.ZoomMaxDistance);
                }

                if (controlState.EnableRotation && math.lengthsq(controlState.RotateInput) > 0f)
                {
                    var rotateInput = controlState.RotateInput;
                    var yawDelta = rotateInput.x * config.RotationSpeed * math.radians(1f) * deltaTime;
                    var pitchDelta = rotateInput.y * config.RotationSpeed * math.radians(1f) * deltaTime;

                    var euler = math.EulerXYZ(updatedRotation);
                    euler.y += yawDelta;
                    euler.x += pitchDelta;
                    euler.x = math.clamp(euler.x, config.PitchMin, config.PitchMax);

                    updatedRotation = quaternion.EulerXYZ(euler);
                }
            }

            if (config.Smoothing > 0f)
            {
                var smoothingFactor = 1f - math.pow(1f - config.Smoothing, deltaTime * 60f);
                updatedPosition = math.lerp(cameraState.Position, updatedPosition, smoothingFactor);
                updatedRotation = math.slerp(cameraState.Rotation, updatedRotation, smoothingFactor);
                updatedZoomDistance = math.lerp(cameraState.ZoomDistance, updatedZoomDistance, smoothingFactor);
            }

            updatedState.Position = updatedPosition;
            updatedState.Rotation = updatedRotation;
            updatedState.ZoomDistance = updatedZoomDistance;

            var cameraStateEntity = SystemAPI.GetSingletonEntity<Space4XCameraState>();
            SystemAPI.SetComponent(cameraStateEntity, updatedState);

            ApplyTransformToCamera(camera, updatedState);
        }

        private Camera GetCamera()
        {
            if (cachedCamera == null)
            {
                cachedCamera = Camera.main;
                if (cachedCamera == null)
                {
                    cachedCamera = Object.FindFirstObjectByType<Camera>();
                }
            }

            return cachedCamera;
        }

        private void ApplyTransformToCamera(Camera camera, Space4XCameraState state)
        {
            if (camera == null)
            {
                return;
            }

            camera.transform.position = state.Position;
            camera.transform.rotation = state.Rotation;
        }
    }
}

