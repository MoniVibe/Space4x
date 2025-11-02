using Godgame.Camera;
using Godgame.Interaction;
using Godgame.Interaction.Input;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Godgame.Camera
{
    /// <summary>
    /// Main camera control system implementing RTS/Free-fly and BW2-style Orbital modes.
    /// Follows PureDOTS patterns with deterministic, Burst-compatible logic where possible.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(InputReaderSystem))]
    public partial struct CameraControlSystem : ISystem
    {
        private Entity _cameraModeEntity;
        private Entity _cameraSettingsEntity;
        private Entity _cameraTransformEntity;
        private Entity _cameraTerrainEntity;
        private bool _singletonsInitialized;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<InputState>();
            
            // Create singleton entities in OnCreate (managed context, not Burst)
            InitializeSingletons(ref state);
        }

        private void InitializeSingletons(ref SystemState state)
        {
            if (_singletonsInitialized)
            {
                return;
            }

            // CameraSettings singleton
            using var settingsQuery = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<CameraSettings>());
            if (settingsQuery.IsEmptyIgnoreFilter)
            {
                _cameraSettingsEntity = state.EntityManager.CreateEntity(typeof(CameraSettings));
                state.EntityManager.SetComponentData(_cameraSettingsEntity, CameraSettings.Default);
            }
            else
            {
                _cameraSettingsEntity = settingsQuery.GetSingletonEntity();
            }

            // CameraModeState singleton
            using var modeQuery = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<CameraModeState>());
            if (modeQuery.IsEmptyIgnoreFilter)
            {
                _cameraModeEntity = state.EntityManager.CreateEntity(typeof(CameraModeState));
                state.EntityManager.SetComponentData(_cameraModeEntity, new CameraModeState
                {
                    Mode = CameraMode.RTSFreeFly,
                    JustToggled = false
                });
            }
            else
            {
                _cameraModeEntity = modeQuery.GetSingletonEntity();
            }

            // CameraTransform singleton
            using var transformQuery = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<CameraTransform>());
            if (transformQuery.IsEmptyIgnoreFilter)
            {
                _cameraTransformEntity = state.EntityManager.CreateEntity(typeof(CameraTransform));
                state.EntityManager.SetComponentData(_cameraTransformEntity, new CameraTransform
                {
                    Position = new float3(0f, 10f, -10f),
                    Rotation = quaternion.LookRotationSafe(new float3(0f, 0f, 1f), new float3(0f, 1f, 0f)),
                    DistanceFromPivot = 10f,
                    PitchAngle = 45f
                });
            }
            else
            {
                _cameraTransformEntity = transformQuery.GetSingletonEntity();
            }

            // CameraTerrainState singleton
            using var terrainQuery = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<CameraTerrainState>());
            if (terrainQuery.IsEmptyIgnoreFilter)
            {
                _cameraTerrainEntity = state.EntityManager.CreateEntity(typeof(CameraTerrainState));
                state.EntityManager.SetComponentData(_cameraTerrainEntity, default(CameraTerrainState));
            }
            else
            {
                _cameraTerrainEntity = terrainQuery.GetSingletonEntity();
            }

            _singletonsInitialized = true;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Early-out during rewind (per PureDOTS requirements)
            // Note: RewindState check would go here if PureDOTS provides it

            // Get input state
            if (!SystemAPI.TryGetSingleton<InputState>(out var inputState))
            {
                return;
            }

            // Ensure singletons are initialized (fallback if OnCreate didn't run)
            if (!_singletonsInitialized)
            {
                // Can't call managed code from Burst - this should never happen in practice
                return;
            }

            var cameraModeRW = SystemAPI.GetComponentRW<CameraModeState>(_cameraModeEntity);
            var cameraSettingsRW = SystemAPI.GetComponentRW<CameraSettings>(_cameraSettingsEntity);
            var cameraTransformRW = SystemAPI.GetComponentRW<CameraTransform>(_cameraTransformEntity);
            var cameraTerrainRW = SystemAPI.GetComponentRW<CameraTerrainState>(_cameraTerrainEntity);

            ref var cameraMode = ref cameraModeRW.ValueRW;
            ref var settings = ref cameraSettingsRW.ValueRW;
            ref var transform = ref cameraTransformRW.ValueRW;
            ref var terrainState = ref cameraTerrainRW.ValueRW;

            var deltaTime = state.WorldUnmanaged.Time.DeltaTime;

            // Handle mode toggle (with debouncing)
            if (inputState.CameraToggleMode && !cameraMode.JustToggled)
            {
                cameraMode.Mode = cameraMode.Mode == CameraMode.RTSFreeFly 
                    ? CameraMode.Orbital 
                    : CameraMode.RTSFreeFly;
                cameraMode.JustToggled = true;
            }
            else
            {
                cameraMode.JustToggled = false;
            }

            // Update camera based on current mode
            if (cameraMode.Mode == CameraMode.RTSFreeFly)
            {
                UpdateRTSFreeFlyMode(ref transform, ref settings, inputState, deltaTime);
            }
            else // Orbital mode
            {
                UpdateOrbitalMode(ref transform, ref settings, ref terrainState, inputState, deltaTime);
            }
        }

        [BurstCompile]
        private void UpdateRTSFreeFlyMode(
            ref CameraTransform transform,
            ref CameraSettings settings,
            InputState input,
            float deltaTime)
        {
            // Get camera forward/right/up vectors from rotation
            var forward = math.mul(transform.Rotation, new float3(0f, 0f, 1f));
            var right = math.mul(transform.Rotation, new float3(1f, 0f, 0f));
            var up = new float3(0f, 1f, 0f);

            // WASD movement (relative to camera rotation)
            var moveInput = input.Move;
            var horizontalMove = right * moveInput.x + forward * moveInput.y;
            transform.Position += horizontalMove * settings.MovementSpeed * deltaTime;

            // Q/E vertical movement (world space)
            transform.Position += up * input.Vertical * settings.MovementSpeed * deltaTime;

            // Mouse look rotation
            var lookDelta = input.PointerDelta;
            if (math.lengthsq(lookDelta) > 0.001f)
            {
                var rotationDelta = new float3(
                    -lookDelta.y * settings.RotationSensitivity * deltaTime,
                    lookDelta.x * settings.RotationSensitivity * deltaTime,
                    0f
                );

                var currentEuler = math.EulerXYZ(transform.Rotation);
                var newEuler = currentEuler + rotationDelta;
                
                // Clamp pitch to prevent flipping
                newEuler.x = math.clamp(newEuler.x, math.radians(-89f), math.radians(89f));
                
                transform.Rotation = quaternion.EulerXYZ(newEuler);
            }

            // Scroll wheel zoom (move forward/back in view direction)
            if (math.abs(input.Scroll) > 0.001f)
            {
                var zoomAmount = input.Scroll * settings.ZoomSpeed * deltaTime;
                transform.Position += forward * zoomAmount;
            }
        }

        [BurstCompile]
        private void UpdateOrbitalMode(
            ref CameraTransform transform,
            ref CameraSettings settings,
            ref CameraTerrainState terrainState,
            InputState input,
            float deltaTime)
        {
            // Note: This is a simplified version. Full implementation would need:
            // - MMB orbit with distance-scaled sensitivity
            // - LMB pan with grab plane
            // - Scroll zoom toward cursor
            // - Terrain collision
            // Full implementation deferred to when terrain raycast system is available

            // Basic orbital rotation around focus point
            if (input.MiddleHeld && math.lengthsq(input.PointerDelta) > 0.001f)
            {
                // Rotate around focus point
                var lookDelta = input.PointerDelta;
                var distance = math.distance(transform.Position, settings.OrbitalFocusPoint);
                
                // Apply distance-scaled sensitivity
                float sensitivity = GetDistanceScaledSensitivity(distance, settings);
                
                // Calculate rotation angles
                var yawDelta = lookDelta.x * sensitivity * settings.OrbitalRotationSpeed * deltaTime;
                var pitchDelta = -lookDelta.y * sensitivity * settings.OrbitalRotationSpeed * deltaTime;

                // Apply pitch limits
                var currentPitch = transform.PitchAngle + pitchDelta;
                currentPitch = math.clamp(currentPitch, settings.PitchMin, settings.PitchMax);
                pitchDelta = currentPitch - transform.PitchAngle;

                // Rotate camera around focus point
                var toCamera = transform.Position - settings.OrbitalFocusPoint;
                var rotationYaw = quaternion.RotateY(math.radians(yawDelta));
                var rotationPitch = quaternion.AxisAngle(new float3(1f, 0f, 0f), math.radians(pitchDelta));
                
                toCamera = math.mul(rotationYaw, toCamera);
                toCamera = math.mul(rotationPitch, toCamera);
                
                transform.Position = settings.OrbitalFocusPoint + toCamera;
                transform.Rotation = quaternion.LookRotationSafe(-toCamera, new float3(0f, 1f, 0f));
                transform.DistanceFromPivot = math.length(toCamera);
                transform.PitchAngle = currentPitch;
            }

            // Scroll zoom (toward cursor - simplified for now)
            if (math.abs(input.Scroll) > 0.001f)
            {
                var zoomAmount = input.Scroll * settings.ZoomSpeed * deltaTime;
                var toCamera = transform.Position - settings.OrbitalFocusPoint;
                var direction = math.normalize(toCamera);
                var newDistance = math.clamp(
                    transform.DistanceFromPivot - zoomAmount,
                    settings.ZoomMin,
                    settings.ZoomMax
                );
                
                transform.Position = settings.OrbitalFocusPoint + direction * newDistance;
                transform.DistanceFromPivot = newDistance;
            }
        }

        [BurstCompile]
        private float GetDistanceScaledSensitivity(float distance, CameraSettings settings)
        {
            if (distance <= 20f)
            {
                return settings.SensitivityClose;
            }
            else if (distance <= 100f)
            {
                return settings.SensitivityMid;
            }
            else
            {
                return settings.SensitivityFar;
            }
        }

    }
}

