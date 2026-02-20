using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interaction;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// Fixed-step processor for player flagship flight input intent.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PureDOTS.Systems.ResourceSystemGroup))]
    [UpdateBefore(typeof(VesselMovementSystem))]
    public partial struct Space4XPlayerFlagshipInputSystem : ISystem
    {
        private const uint MaxTickSubsteps = 256u;
        private ComponentLookup<VesselMovement> _movementLookup;
        private ComponentLookup<MovementCommand> _movementCommandLookup;
        private ComponentLookup<MovementSuppressed> _movementSuppressedLookup;
        private EntityQuery _diagnosticsQuery;
        private Entity _diagnosticsEntity;
        private uint _lastProcessedTick;
        private uint _maxBacklogObserved;
        private byte _hasLastProcessedTick;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            _movementLookup = state.GetComponentLookup<VesselMovement>(false);
            _movementCommandLookup = state.GetComponentLookup<MovementCommand>(false);
            _movementSuppressedLookup = state.GetComponentLookup<MovementSuppressed>(true);
            _diagnosticsQuery = state.GetEntityQuery(ComponentType.ReadWrite<PlayerFlagshipInputTickDiagnostics>());
            if (_diagnosticsQuery.IsEmptyIgnoreFilter)
            {
                _diagnosticsEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(_diagnosticsEntity, new PlayerFlagshipInputTickDiagnostics
                {
                    Tick = 0u,
                    TickDeltaObserved = 0u,
                    TickStepsProcessed = 0u,
                    TickBacklog = 0u,
                    MaxBacklogObserved = 0u,
                    FixedDeltaTime = 0f,
                    SpeedMultiplier = 1f
                });
            }
            else
            {
                _diagnosticsEntity = _diagnosticsQuery.GetSingletonEntity();
            }
            _lastProcessedTick = 0u;
            _maxBacklogObserved = 0u;
            _hasLastProcessedTick = 0;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                WriteDiagnostics(ref state, in timeState, 0u, 0u, 0u);
                _hasLastProcessedTick = 0;
                return;
            }

            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                WriteDiagnostics(ref state, in timeState, 0u, 0u, 0u);
                _hasLastProcessedTick = 0;
                return;
            }

            // Keep flight integration locked to simulation ticks (never render-frame cadence).
            if (_hasLastProcessedTick == 0)
            {
                _lastProcessedTick = timeState.Tick;
                WriteDiagnostics(ref state, in timeState, 0u, 0u, 0u);
                _hasLastProcessedTick = 1;
                return;
            }

            if (timeState.Tick <= _lastProcessedTick)
            {
                WriteDiagnostics(ref state, in timeState, 0u, 0u, 0u);
                return;
            }

            var tickDelta = timeState.Tick - _lastProcessedTick;
            if (tickDelta == 0u)
            {
                WriteDiagnostics(ref state, in timeState, 0u, 0u, 0u);
                return;
            }

            // Never drop simulation ticks; process in bounded batches and carry backlog to subsequent updates.
            var tickStepCount = math.min(MaxTickSubsteps, tickDelta);
            _lastProcessedTick += tickStepCount;
            var tickBacklog = tickDelta - tickStepCount;
            WriteDiagnostics(ref state, in timeState, tickDelta, tickStepCount, tickBacklog);

            // Integrate each missed tick as fixed substeps instead of one collapsed large dt to reduce visible stutter.
            var dt = math.max(0f, timeState.FixedDeltaTime);
            if (dt <= 0f)
            {
                return;
            }

            _movementLookup.Update(ref state);
            _movementCommandLookup.Update(ref state);
            _movementSuppressedLookup.Update(ref state);

            foreach (var (inputRef, transformRef, profileRef, runtimeRef, entity) in SystemAPI
                         .Query<RefRW<PlayerFlagshipFlightInput>, RefRW<LocalTransform>, RefRO<ShipFlightProfile>, RefRW<ShipFlightRuntimeState>>()
                         .WithAll<PlayerFlagshipTag>()
                         .WithEntityAccess())
            {
                var input = inputRef.ValueRO;
                var profile = profileRef.ValueRO.Sanitized();
                var transform = transformRef.ValueRO;
                var runtime = runtimeRef.ValueRO;

                if (input.ToggleDampenersRequested != 0)
                {
                    runtime.InertialDampenersEnabled = runtime.InertialDampenersEnabled == 0 ? (byte)1 : (byte)0;
                    input.ToggleDampenersRequested = 0;
                }

                var movementEnabled = input.MovementEnabled != 0;
                var hasMovement = _movementLookup.HasComponent(entity);
                var movementSuppressed = _movementSuppressedLookup.HasComponent(entity) &&
                                         _movementSuppressedLookup.IsComponentEnabled(entity);

                if (!movementEnabled)
                {
                    // RTS handoff path: when flagship movement suppression is disabled,
                    // leave movement/intent ownership to AI/order systems.
                    if (!movementSuppressed)
                    {
                        input.ToggleDampenersRequested = 0;
                        inputRef.ValueRW = input;
                        continue;
                    }

                    runtime.VelocityWorld = float3.zero;
                    runtime.AngularSpeedRadians = 0f;
                    runtime.ForwardThrottle = 0f;
                    runtime.StrafeThrottle = 0f;
                    runtime.VerticalThrottle = 0f;
                    runtimeRef.ValueRW = runtime;

                    if (hasMovement)
                    {
                        var movement = _movementLookup[entity];
                        movement.Velocity = float3.zero;
                        movement.CurrentSpeed = 0f;
                        movement.IsMoving = 0;
                        _movementLookup[entity] = movement;
                    }

                    UpdateMovementCommand(entity, transform.Position);
                    inputRef.ValueRW = input;
                    continue;
                }

                var velocity = runtime.VelocityWorld;
                var angularSpeed = math.max(0f, runtime.AngularSpeedRadians);
                var forwardThrottle = math.clamp(runtime.ForwardThrottle, -1f, 1f);
                var strafeThrottle = math.clamp(runtime.StrafeThrottle, -1f, 1f);
                var verticalThrottle = math.clamp(runtime.VerticalThrottle, -1f, 1f);
                var boost = input.BoostPressed != 0 ? math.max(1f, profile.BoostMultiplier) : 1f;
                var forwardInput = math.clamp(input.Forward, -1f, 1f);
                var strafeInput = math.clamp(input.Strafe, -1f, 1f);
                var verticalInput = math.clamp(input.Vertical, -1f, 1f);
                var rollInput = math.clamp(input.Roll, -1f, 1f);
                var fighterSteeringMode = input.FighterSteeringMode != 0;
                var turnNorm = math.saturate(profile.CursorTurnSharpness / 12f);
                // Keep ship turn response profile-driven instead of camera-sensitivity-driven.
                var maxAngularSpeed = math.radians(math.lerp(8f, 24f, turnNorm));
                var angularAcceleration = math.radians(math.lerp(24f, 90f, turnNorm));
                // Allow larger heading error so camera motion does not visually force 1:1 ship parity.
                var maxCursorLeadAngle = math.radians(math.lerp(28f, 170f, turnNorm));
                var speedReference = math.max(0.1f, profile.MaxForwardSpeed * boost);
                var substepCount = (int)tickStepCount;
                for (var step = 0; step < substepCount; step++)
                {
                    var speedNorm = math.saturate(math.length(velocity) / speedReference);
                    var turnAuthority = math.lerp(1f, 0.45f, speedNorm);
                    var maxAngularSpeedStep = maxAngularSpeed * turnAuthority;
                    var angularAccelerationStep = angularAcceleration * turnAuthority;
                    var shipForward = math.normalizesafe(math.mul(transform.Rotation, new float3(0f, 0f, 1f)), new float3(0f, 0f, 1f));
                    var shipRight = math.normalizesafe(math.mul(transform.Rotation, new float3(1f, 0f, 0f)), new float3(1f, 0f, 0f));
                    var shipUp = math.normalizesafe(math.mul(transform.Rotation, new float3(0f, 1f, 0f)), new float3(0f, 1f, 0f));

                    if (input.CursorSteeringActive != 0 &&
                        math.lengthsq(input.CursorLookDirection) > 1e-6f)
                    {
                        var lookDirection = math.normalizesafe(input.CursorLookDirection, shipForward);
                        if (!fighterSteeringMode)
                        {
                            lookDirection = ClampDirectionLead(shipForward, lookDirection, maxCursorLeadAngle);
                        }

                        var upDirection = math.normalizesafe(input.CursorUpDirection, shipUp);
                        var desired = quaternion.LookRotationSafe(lookDirection, upDirection);
                        transform.Rotation = RotateTowardsWithInertia(
                            transform.Rotation,
                            desired,
                            ref angularSpeed,
                            maxAngularSpeedStep,
                            angularAccelerationStep,
                            dt);
                    }

                    shipForward = math.normalizesafe(math.mul(transform.Rotation, new float3(0f, 0f, 1f)), shipForward);
                    shipRight = math.normalizesafe(math.mul(transform.Rotation, new float3(1f, 0f, 0f)), shipRight);
                    shipUp = math.normalizesafe(math.mul(transform.Rotation, new float3(0f, 1f, 0f)), shipUp);

                    var translationForward = shipForward;
                    var translationRight = shipRight;
                    var translationUp = shipUp;
                    if (input.TranslationBasisOverride != 0 &&
                        math.lengthsq(input.TranslationForward) > 1e-6f)
                    {
                        translationForward = math.normalizesafe(input.TranslationForward, shipForward);
                        var translationUpHint = math.normalizesafe(input.TranslationUp, shipUp);
                        translationRight = math.cross(translationUpHint, translationForward);
                        if (math.lengthsq(translationRight) <= 1e-6f)
                        {
                            translationRight = math.cross(shipUp, translationForward);
                        }

                        translationRight = math.normalizesafe(translationRight, shipRight);
                        translationUp = math.normalizesafe(math.cross(translationForward, translationRight), shipUp);
                    }

                    var forwardResponse = math.abs(forwardInput) > 0.001f
                        ? ResolveThrottleResponse(
                            forwardInput >= 0f ? profile.ForwardAcceleration : profile.ReverseAcceleration,
                            forwardInput >= 0f ? profile.MaxForwardSpeed : profile.MaxReverseSpeed)
                        : ResolveThrottleResponse(profile.DampenerDeceleration, math.max(profile.MaxForwardSpeed, profile.MaxReverseSpeed));
                    var strafeResponse = math.abs(strafeInput) > 0.001f
                        ? ResolveThrottleResponse(profile.StrafeAcceleration, profile.MaxStrafeSpeed)
                        : ResolveThrottleResponse(profile.DampenerDeceleration, profile.MaxStrafeSpeed);
                    var verticalResponse = math.abs(verticalInput) > 0.001f
                        ? ResolveThrottleResponse(profile.VerticalAcceleration, profile.MaxVerticalSpeed)
                        : ResolveThrottleResponse(profile.DampenerDeceleration, profile.MaxVerticalSpeed);

                    forwardThrottle = MoveTowards(forwardThrottle, forwardInput, forwardResponse * dt);
                    strafeThrottle = MoveTowards(strafeThrottle, strafeInput, strafeResponse * dt);
                    verticalThrottle = MoveTowards(verticalThrottle, verticalInput, verticalResponse * dt);

                    if (math.abs(forwardThrottle) > 0.001f)
                    {
                        var acceleration = forwardThrottle >= 0f ? profile.ForwardAcceleration : profile.ReverseAcceleration;
                        velocity += translationForward * (forwardThrottle * math.max(0f, acceleration) * boost * dt);
                    }

                    if (math.abs(strafeThrottle) > 0.001f)
                    {
                        velocity += translationRight * (strafeThrottle * math.max(0f, profile.StrafeAcceleration) * boost * dt);
                    }

                    if (math.abs(verticalThrottle) > 0.001f)
                    {
                        velocity += translationUp * (verticalThrottle * math.max(0f, profile.VerticalAcceleration) * boost * dt);
                    }

                    var localVelocityX = math.dot(velocity, translationRight);
                    var localVelocityY = math.dot(velocity, translationUp);
                    var localVelocityZ = math.dot(velocity, translationForward);
                    localVelocityZ = math.clamp(localVelocityZ, -math.max(0f, profile.MaxReverseSpeed) * boost, math.max(0f, profile.MaxForwardSpeed) * boost);
                    localVelocityX = math.clamp(localVelocityX, -math.max(0f, profile.MaxStrafeSpeed) * boost, math.max(0f, profile.MaxStrafeSpeed) * boost);
                    localVelocityY = math.clamp(localVelocityY, -math.max(0f, profile.MaxVerticalSpeed) * boost, math.max(0f, profile.MaxVerticalSpeed) * boost);
                    velocity = translationRight * localVelocityX + translationUp * localVelocityY + translationForward * localVelocityZ;

                    if (input.RetroBrakePressed != 0)
                    {
                        velocity = MoveTowardsVector(velocity, float3.zero, math.max(0f, profile.RetroBrakeAcceleration) * boost * dt);
                        forwardThrottle = MoveTowards(forwardThrottle, 0f, forwardResponse * dt);
                        strafeThrottle = MoveTowards(strafeThrottle, 0f, strafeResponse * dt);
                        verticalThrottle = MoveTowards(verticalThrottle, 0f, verticalResponse * dt);
                    }
                    else if (runtime.InertialDampenersEnabled != 0)
                    {
                        if (math.abs(strafeInput) < 0.001f && math.abs(strafeThrottle) < 0.02f)
                        {
                            localVelocityX = MoveTowards(localVelocityX, 0f, math.max(0f, profile.DampenerDeceleration) * dt);
                        }

                        if (math.abs(verticalInput) < 0.001f && math.abs(verticalThrottle) < 0.02f)
                        {
                            localVelocityY = MoveTowards(localVelocityY, 0f, math.max(0f, profile.DampenerDeceleration) * dt);
                        }

                        if (math.abs(forwardInput) < 0.001f && math.abs(forwardThrottle) < 0.02f)
                        {
                            localVelocityZ = MoveTowards(localVelocityZ, 0f, math.max(0f, profile.DampenerDeceleration) * dt);
                        }

                        velocity = translationRight * localVelocityX + translationUp * localVelocityY + translationForward * localVelocityZ;
                    }
                    else
                    {
                        var drag = math.max(0f, profile.PassiveDriftDrag);
                        if (drag > 0f)
                        {
                            velocity *= math.exp(-drag * dt);
                        }
                    }

                    if (input.AutoAlignToTranslation != 0 && input.CursorSteeringActive == 0)
                    {
                        // Mode 2 default: only reorient while movement input is actively held.
                        // (WASD + vertical keys)
                        var desiredTravel = translationForward * forwardInput +
                                            translationRight * strafeInput +
                                            translationUp * verticalInput;
                        if (math.lengthsq(desiredTravel) > 1e-4f)
                        {
                            desiredTravel = math.normalizesafe(desiredTravel, shipForward);
                            var desiredRotation = quaternion.LookRotationSafe(desiredTravel, translationUp);
                            transform.Rotation = RotateTowardsWithInertia(
                                transform.Rotation,
                                desiredRotation,
                                ref angularSpeed,
                                maxAngularSpeedStep,
                                angularAccelerationStep,
                                dt);

                            shipForward = math.normalizesafe(math.mul(transform.Rotation, new float3(0f, 0f, 1f)), shipForward);
                        }
                        else
                        {
                            angularSpeed = MoveTowards(angularSpeed, 0f, angularAccelerationStep * dt);
                        }
                    }
                    else if (input.CursorSteeringActive == 0)
                    {
                        angularSpeed = MoveTowards(angularSpeed, 0f, angularAccelerationStep * dt);
                    }

                    transform.Position += velocity * dt;

                    if (math.abs(rollInput) > 0.001f)
                    {
                        var rollRadians = math.radians(profile.RollSpeedDegrees * rollInput * dt);
                        var rollDelta = quaternion.AxisAngle(shipForward, rollRadians);
                        transform.Rotation = math.normalize(math.mul(rollDelta, transform.Rotation));
                    }
                }

                runtime.VelocityWorld = velocity;
                runtime.AngularSpeedRadians = angularSpeed;
                runtime.ForwardThrottle = forwardThrottle;
                runtime.StrafeThrottle = strafeThrottle;
                runtime.VerticalThrottle = verticalThrottle;
                runtimeRef.ValueRW = runtime;
                transformRef.ValueRW = transform;

                if (hasMovement)
                {
                    var movement = _movementLookup[entity];
                    if (movementSuppressed)
                    {
                        movement.Velocity = float3.zero;
                        movement.CurrentSpeed = 0f;
                        movement.IsMoving = 0;
                    }
                    else
                    {
                        movement.Velocity = velocity;
                        movement.CurrentSpeed = math.length(velocity);
                        movement.IsMoving = movement.CurrentSpeed > 0.001f ? (byte)1 : (byte)0;
                    }
                    _movementLookup[entity] = movement;
                }

                UpdateMovementCommand(entity, transform.Position);
                inputRef.ValueRW = input;
            }
        }

        private void WriteDiagnostics(ref SystemState state, in TimeState timeState, uint tickDeltaObserved, uint tickStepsProcessed, uint tickBacklog)
        {
            _maxBacklogObserved = math.max(_maxBacklogObserved, tickBacklog);

            if (_diagnosticsQuery.IsEmptyIgnoreFilter)
            {
                _diagnosticsEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(_diagnosticsEntity, new PlayerFlagshipInputTickDiagnostics());
            }

            if (_diagnosticsEntity == Entity.Null ||
                !state.EntityManager.Exists(_diagnosticsEntity) ||
                !state.EntityManager.HasComponent<PlayerFlagshipInputTickDiagnostics>(_diagnosticsEntity))
            {
                if (_diagnosticsQuery.IsEmptyIgnoreFilter)
                {
                    _diagnosticsEntity = state.EntityManager.CreateEntity();
                    state.EntityManager.AddComponentData(_diagnosticsEntity, new PlayerFlagshipInputTickDiagnostics());
                }
                else
                {
                    _diagnosticsEntity = _diagnosticsQuery.GetSingletonEntity();
                }
            }

            state.EntityManager.SetComponentData(_diagnosticsEntity, new PlayerFlagshipInputTickDiagnostics
            {
                Tick = timeState.Tick,
                TickDeltaObserved = tickDeltaObserved,
                TickStepsProcessed = tickStepsProcessed,
                TickBacklog = tickBacklog,
                MaxBacklogObserved = _maxBacklogObserved,
                FixedDeltaTime = timeState.FixedDeltaTime,
                SpeedMultiplier = timeState.CurrentSpeedMultiplier
            });
        }

        private void UpdateMovementCommand(Entity entity, float3 position)
        {
            if (!_movementCommandLookup.HasComponent(entity))
            {
                return;
            }

            var command = _movementCommandLookup[entity];
            command.TargetPosition = position;
            command.ArrivalThreshold = 0.5f;
            _movementCommandLookup[entity] = command;
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            if (current < target)
            {
                return math.min(current + maxDelta, target);
            }

            return math.max(current - maxDelta, target);
        }

        private static float3 MoveTowardsVector(float3 current, float3 target, float maxDelta)
        {
            var delta = target - current;
            var distance = math.length(delta);
            if (distance <= maxDelta || distance <= 0.0001f)
            {
                return target;
            }

            return current + delta / distance * maxDelta;
        }

        private static float ResolveThrottleResponse(float acceleration, float maxSpeed)
        {
            var safeSpeed = math.max(0.1f, maxSpeed);
            var response = math.max(0.15f, math.max(0.01f, acceleration) / safeSpeed);
            return math.clamp(response, 0.15f, 3f);
        }

        private static float3 ClampDirectionLead(float3 currentForward, float3 desiredForward, float maxLeadRadians)
        {
            var current = math.normalizesafe(currentForward, new float3(0f, 0f, 1f));
            var desired = math.normalizesafe(desiredForward, current);
            maxLeadRadians = math.max(0f, maxLeadRadians);

            var dot = math.clamp(math.dot(current, desired), -1f, 1f);
            var angle = math.acos(dot);
            if (angle <= maxLeadRadians || angle <= 1e-5f)
            {
                return desired;
            }

            var t = math.saturate(maxLeadRadians / angle);
            return math.normalizesafe(math.lerp(current, desired, t), current);
        }

        private static quaternion RotateTowardsWithInertia(
            quaternion current,
            quaternion desired,
            ref float angularSpeed,
            float maxAngularSpeed,
            float angularAcceleration,
            float dt)
        {
            var currentNorm = math.normalize(current);
            var desiredNorm = math.normalize(desired);
            var dot = math.clamp(math.dot(currentNorm, desiredNorm), -1f, 1f);
            var angle = math.acos(math.min(1f, math.abs(dot))) * 2f;
            if (angle <= 1e-4f)
            {
                angularSpeed = MoveTowards(angularSpeed, 0f, angularAcceleration * dt);
                return desiredNorm;
            }

            maxAngularSpeed = math.max(0f, maxAngularSpeed);
            angularAcceleration = math.max(0f, angularAcceleration);
            var desiredSpeed = math.min(maxAngularSpeed, angle / math.max(1e-4f, dt));
            angularSpeed = MoveTowards(angularSpeed, desiredSpeed, angularAcceleration * dt);
            angularSpeed = math.clamp(angularSpeed, 0f, maxAngularSpeed);
            var step = math.min(angle, angularSpeed * dt);
            var t = math.saturate(step / angle);
            return math.normalize(math.slerp(currentNorm, desiredNorm, t));
        }
    }
}
