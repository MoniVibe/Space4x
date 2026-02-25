using System;
using System.IO;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interaction;
using PureDOTS.Runtime.Physics;
using Space4X.Presentation;
using Space4X.Registry;
using Space4X.Runtime;
using Space4X.UI;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using UCamera = UnityEngine.Camera;
using UTime = UnityEngine.Time;
using SystemEnv = System.Environment;

namespace Space4X.Diagnostics
{
    /// <summary>
    /// Detects and logs suspicious flagship movement/parity glitches at runtime.
    /// Intended for debugging stutter by classifying likely source (simulation vs presentation).
    /// </summary>
    [DefaultExecutionOrder(-9380)]
    [DisallowMultipleComponent]
    public sealed class Space4XMovementGlitchProbe : MonoBehaviour
    {
        private const string ProbeEnabledEnv = "SPACE4X_MOVEMENT_GLITCH_PROBE";
        private const string ProbeOutputEnv = "SPACE4X_MOVEMENT_GLITCH_PROBE_OUT";
        private const string ProbeOutDirEnv = "SPACE4X_PROBE_OUT_DIR";
        private const string ProbeEchoEnv = "SPACE4X_MOVEMENT_GLITCH_PROBE_ECHO";
        private const string DefaultFileName = "space4x_movement_glitches.jsonl";

        private const string KindTransformParityGap = "transform_parity_gap";
        private const string KindTickCatchupJump = "tick_catchup_jump";
        private const string KindPresentationMismatch = "presentation_step_mismatch";
        private const string KindUnexpectedJump = "unexpected_position_jump";
        private const string KindHighSpeedNoInterp = "high_speed_no_interpolation";
        private const string KindStallThenImpulse = "stall_then_impulse";
        private const string KindHeartbeat = "heartbeat";

        [SerializeField] private bool enabledByDefault = true;
        [SerializeField] private bool echoToUnityLog = false;
        [SerializeField] private Key toggleKey = Key.F8;
        [SerializeField] private float sampleIntervalSeconds = 0f;
        [SerializeField] private float transformParityGapThreshold = 0.2f;
        [SerializeField] private float baseJumpThreshold = 0.4f;
        [SerializeField] private float expectedStepJumpMultiplier = 3f;
        [SerializeField] private float stallMinRuntimeSpeed = 1.5f;
        [SerializeField] private float stallStepToExpectedRatio = 0.6f;
        [SerializeField] private int stallMinConsecutiveSamples = 3;
        [SerializeField] private float burstStepMultiplierAfterStall = 1.6f;
        [SerializeField] private float stallBurstMinStep = 0.08f;
        [SerializeField] private int collisionRecentTickWindow = 3;
        [SerializeField] private bool heartbeatEnabled = true;
        [SerializeField] private float heartbeatIntervalSeconds = 1f;
        [SerializeField] private float perKindCooldownSeconds = 0.2f;

        private string _outputPath = string.Empty;
        private bool _active;
        private float _nextSampleAt;
        private bool _hasBaseline;
        private Entity _baselineTarget;
        private Vector3 _baselineLocalPosition;
        private Vector3 _baselineWorldPosition;
        private uint _baselineTick;
        private float _baselineSampleAt;

        private float _lastTransformParityEmitAt = float.NegativeInfinity;
        private float _lastTickCatchupEmitAt = float.NegativeInfinity;
        private float _lastPresentationMismatchEmitAt = float.NegativeInfinity;
        private float _lastUnexpectedJumpEmitAt = float.NegativeInfinity;
        private float _lastHighSpeedNoInterpEmitAt = float.NegativeInfinity;
        private float _lastStallThenImpulseEmitAt = float.NegativeInfinity;
        private float _nextHeartbeatAt;
        private int _stallSampleCount;
        private float _stallPeakExpectedStep;
        private float _stallPeakRuntimeSpeed;

        [Serializable]
        private sealed class GlitchRecord
        {
            public string timestamp_utc = string.Empty;
            public string scene = string.Empty;
            public string kind = string.Empty;
            public string likely_source = string.Empty;
            public string mode = string.Empty;
            public string entity = "Entity.Null";
            public string note = string.Empty;
            public uint tick;
            public uint tick_delta;
            public float fixed_dt;
            public float speed_multiplier;
            public float interpolation_alpha;
            public uint input_backlog;
            public uint input_observed_delta;
            public uint input_processed_steps;
            public float frame_dt;
            public float velocity_speed;
            public float expected_step;
            public float local_step;
            public float world_step;
            public float ltw_gap;
            public int follow_interp_cfg;
            public int follow_interp_active;
            public int has_movement_suppressed;
            public int has_orbit_anchor;
            public int has_orbit_state;
            public int has_rogue_orbit;
            public int has_micro_impulse;
            public int has_frame_membership;
            public int has_frame_driven;
            public int has_pose_snapshot;
            public int has_render_frame_scale;
            public int has_band_scale;
            public int has_requires_physics;
            public int has_collision_buffer;
            public int has_physics_velocity;
            public float physics_linear_speed;
            public int collision_events_count;
            public int collision_recent_count;
            public int collision_recent_collision_count;
            public int collision_recent_trigger_count;
            public float collision_max_impulse;
            public float input_magnitude;
            public int stall_sample_count;
            public float stall_peak_expected_step;
            public float stall_peak_runtime_speed;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Application.isBatchMode && !IsEnabledByEnvironment())
                return;

            if (FindAnyObjectByType<Space4XMovementGlitchProbe>() != null)
                return;

            var go = new GameObject("Space4X Movement Glitch Probe");
            DontDestroyOnLoad(go);
            go.AddComponent<Space4XMovementGlitchProbe>();
        }

        private void OnEnable()
        {
            _outputPath = ResolveOutputPath();
            _active = ResolveActiveFromEnvOrDefault();
            echoToUnityLog = ResolveEchoFromEnvOrDefault();
            _nextSampleAt = 0f;
            _nextHeartbeatAt = 0f;
            ResetBaseline();
            ResetStallTracking();
            UnityEngine.Debug.Log($"[Space4XMovementGlitchProbe] boot active={_active} echo={echoToUnityLog} path='{_outputPath}'");
        }

        private void LateUpdate()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && toggleKey != Key.None && keyboard[toggleKey].wasPressedThisFrame)
            {
                _active = !_active;
                UnityEngine.Debug.Log($"[Space4XMovementGlitchProbe] active={_active} path='{_outputPath}'");
                if (!_active)
                {
                    ResetBaseline();
                }
            }

            if (!_active)
                return;

            if (sampleIntervalSeconds > 0f && UTime.unscaledTime < _nextSampleAt)
                return;

            _nextSampleAt = sampleIntervalSeconds > 0f
                ? UTime.unscaledTime + Mathf.Max(0.01f, sampleIntervalSeconds)
                : UTime.unscaledTime;

            TryDetectAndLogGlitch();
        }

        private void TryDetectAndLogGlitch()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                ResetBaseline();
                return;
            }

            var em = world.EntityManager;
            var camera = UCamera.main ?? FindAnyObjectByType<UCamera>();
            if (camera == null)
            {
                if (!TryResolveFallbackTarget(em, out var fallbackNoCamera, out var controlledNoCamera))
                {
                    ResetBaseline();
                    return;
                }

                ProcessTarget(
                    em,
                    fallbackNoCamera,
                    controlledNoCamera,
                    followInterpCfg: false,
                    followInterpActive: false);
                return;
            }

            var follow = camera.GetComponent<Space4XFollowPlayerVessel>();
            var controller = camera.GetComponent<Space4XPlayerFlagshipController>();

            var target = Entity.Null;
            var hasControlledFlagship = false;
            if (controller != null && controller.TryGetControlledFlagship(out var controlled))
            {
                target = controlled;
                hasControlledFlagship = true;
            }
            else if (follow != null && follow.TryGetDebugTarget(out var followTarget))
            {
                target = followTarget;
            }

            if (target == Entity.Null)
            {
                if (!TryResolveFallbackTarget(em, out target, out hasControlledFlagship))
                {
                    ResetBaseline();
                    return;
                }
            }

            var followInterpCfg = follow != null && follow.DebugInterpolateControlledFlagshipPose;
            var followInterpActive = follow != null && follow.DebugControlledPoseInterpolationActive;
            ProcessTarget(em, target, hasControlledFlagship, followInterpCfg, followInterpActive);
        }

        private void ProcessTarget(
            EntityManager em,
            Entity target,
            bool hasControlledFlagship,
            bool followInterpCfg,
            bool followInterpActive)
        {
            if (!em.Exists(target))
            {
                ResetBaseline();
                return;
            }

            if (!TryGetPoseSamples(em, target, out var localPosition, out var worldPosition, out var hasLocal, out var hasWorld))
            {
                ResetBaseline();
                return;
            }

            var now = UTime.unscaledTime;
            if (!_hasBaseline || _baselineTarget != target)
            {
                ResetStallTracking();
                SeedBaseline(target, localPosition, worldPosition, 0u, now);
                return;
            }

            var hasTimeState = TryGetTimeState(em, out var timeState, out var interpolationAlpha);
            var currentTick = hasTimeState ? timeState.Tick : 0u;
            var tickDelta = hasTimeState && currentTick >= _baselineTick
                ? currentTick - _baselineTick
                : 0u;

            var frameDt = Mathf.Max(1e-4f, now - _baselineSampleAt);
            var localStep = Vector3.Distance(localPosition, _baselineLocalPosition);
            var worldStep = Vector3.Distance(worldPosition, _baselineWorldPosition);
            var ltwGap = hasLocal && hasWorld
                ? Vector3.Distance(localPosition, worldPosition)
                : 0f;

            var velocitySpeed = 0f;
            if (em.HasComponent<VesselMovement>(target))
            {
                var movement = em.GetComponentData<VesselMovement>(target);
                velocitySpeed = movement.CurrentSpeed > 0f ? movement.CurrentSpeed : math.length(movement.Velocity);
            }
            else if (em.HasComponent<ShipFlightRuntimeState>(target))
            {
                var runtime = em.GetComponentData<ShipFlightRuntimeState>(target);
                velocitySpeed = math.length(runtime.VelocityWorld);
            }

            if (velocitySpeed <= 1e-4f && em.HasComponent<ShipFlightRuntimeState>(target))
            {
                var runtime = em.GetComponentData<ShipFlightRuntimeState>(target);
                velocitySpeed = math.length(runtime.VelocityWorld);
            }

            var hasRequiresPhysics = em.HasComponent<RequiresPhysics>(target);
            var hasPhysicsVelocity = em.HasComponent<Unity.Physics.PhysicsVelocity>(target);
            var physicsLinearSpeed = hasPhysicsVelocity
                ? math.length(em.GetComponentData<Unity.Physics.PhysicsVelocity>(target).Linear)
                : 0f;
            var hasCollisionBuffer = em.HasBuffer<PhysicsCollisionEventElement>(target);
            var collisionStats = hasCollisionBuffer
                ? ReadCollisionStats(em.GetBuffer<PhysicsCollisionEventElement>(target), currentTick, collisionRecentTickWindow)
                : default;

            var hasPlayerInputData = em.HasComponent<PlayerFlagshipFlightInput>(target);
            var inputMagnitude = 0f;
            var inputMovementEnabled = false;
            if (hasPlayerInputData)
            {
                var input = em.GetComponentData<PlayerFlagshipFlightInput>(target);
                inputMagnitude = math.sqrt(input.Forward * input.Forward +
                                           input.Strafe * input.Strafe +
                                           input.Vertical * input.Vertical +
                                           input.Roll * input.Roll);
                inputMovementEnabled = input.MovementEnabled != 0;
            }

            var effectiveSpeed = math.max(velocitySpeed, physicsLinearSpeed);
            var expectedStep = Mathf.Max(effectiveSpeed * frameDt, 0f);
            var jumpThreshold = Mathf.Max(baseJumpThreshold, (expectedStep * Mathf.Max(1f, expectedStepJumpMultiplier)) + 0.08f);

            var hasInputDiagnostics = TryGetInputTickDiagnostics(em, out var inputDiagnostics);
            var inputBacklog = hasInputDiagnostics ? inputDiagnostics.TickBacklog : 0u;
            var inputObservedDelta = hasInputDiagnostics ? inputDiagnostics.TickDeltaObserved : 0u;
            var inputProcessed = hasInputDiagnostics ? inputDiagnostics.TickStepsProcessed : 0u;

            var speedMultiplier = hasTimeState ? timeState.CurrentSpeedMultiplier : 1f;
            var mode = Space4XControlModeState.CurrentMode.ToString();
            var hasMovementSuppressed = em.HasComponent<MovementSuppressed>(target) && em.IsComponentEnabled<MovementSuppressed>(target);
            var hasOrbitAnchor = em.HasComponent<Space4XOrbitAnchor>(target);
            var hasOrbitState = em.HasComponent<Space4XOrbitAnchorState>(target);
            var hasRogueOrbit = em.HasComponent<Space4XRogueOrbitTag>(target);
            var hasMicroImpulse = em.HasComponent<Space4XMicroImpulseTag>(target);
            var hasFrameMembership = em.HasComponent<Space4XFrameMembership>(target);
            var hasFrameDriven = em.HasComponent<Space4XFrameDrivenTransformTag>(target);
            var hasPoseSnapshot = em.HasComponent<SimPoseSnapshot>(target);
            var presentationPositionRemapped = IsPresentationPositionRemapped(em, target, out var hasRenderFrameScale, out var hasBandScale);
            var snapshotDrivenRenderPhaseMismatch = hasControlledFlagship &&
                                                   hasPoseSnapshot &&
                                                   hasMovementSuppressed &&
                                                   !followInterpActive &&
                                                   ltwGap <= Mathf.Max(0.5f, transformParityGapThreshold * 4f);
            var stallSamplesForRecord = _stallSampleCount;
            var stallPeakExpectedForRecord = _stallPeakExpectedStep;
            var stallPeakSpeedForRecord = _stallPeakRuntimeSpeed;
            var burstAfterStall = false;
            var burstThreshold = 0f;
            var stallSampleMin = Mathf.Max(2, stallMinConsecutiveSamples);
            var stallRatio = Mathf.Clamp(stallStepToExpectedRatio, 0.02f, 0.95f);
            var stallCandidate = inputMovementEnabled &&
                                 inputMagnitude > 0.15f &&
                                 effectiveSpeed >= Mathf.Max(0.2f, stallMinRuntimeSpeed) &&
                                 expectedStep >= 0.015f &&
                                 localStep <= expectedStep * stallRatio;

            if (stallCandidate)
            {
                _stallSampleCount += 1;
                _stallPeakExpectedStep = math.max(_stallPeakExpectedStep, expectedStep);
                _stallPeakRuntimeSpeed = math.max(_stallPeakRuntimeSpeed, effectiveSpeed);
                stallSamplesForRecord = _stallSampleCount;
                stallPeakExpectedForRecord = _stallPeakExpectedStep;
                stallPeakSpeedForRecord = _stallPeakRuntimeSpeed;
            }
            else if (_stallSampleCount > 0)
            {
                stallSamplesForRecord = _stallSampleCount;
                stallPeakExpectedForRecord = _stallPeakExpectedStep;
                stallPeakSpeedForRecord = _stallPeakRuntimeSpeed;

                if (_stallSampleCount >= stallSampleMin)
                {
                    var minBurstStep = Mathf.Max(0.01f, stallBurstMinStep);
                    burstThreshold = Mathf.Max(
                        minBurstStep,
                        _stallPeakExpectedStep * Mathf.Max(1.05f, burstStepMultiplierAfterStall));
                    burstAfterStall = localStep >= burstThreshold;
                }

                ResetStallTracking();
            }

            var kind = string.Empty;
            var source = string.Empty;
            var note = string.Empty;

            if (burstAfterStall)
            {
                kind = KindStallThenImpulse;
                source = collisionStats.RecentCollisionCount > 0
                    ? "collision_response_or_contact_resolution"
                    : hasRequiresPhysics
                        ? "physics_sync_or_dual_writer"
                        : "simulation_writer_conflict_or_tick_phase";
                note = $"stallSamples={stallSamplesForRecord} burstStep={localStep:0.000} burstThreshold={burstThreshold:0.000} recentCollisions={collisionStats.RecentCollisionCount} maxImpulse={collisionStats.MaxImpulse:0.000}";
            }
            else if (!presentationPositionRemapped &&
                !snapshotDrivenRenderPhaseMismatch &&
                hasLocal && hasWorld &&
                ltwGap > Mathf.Max(0.01f, transformParityGapThreshold))
            {
                kind = KindTransformParityGap;
                source = "presentation_or_transform_sync";
                note = $"ltw gap {ltwGap:0.000} > {transformParityGapThreshold:0.000}";
            }
            else if (localStep > jumpThreshold && (tickDelta > 1u || inputBacklog > 0u))
            {
                kind = KindTickCatchupJump;
                source = "simulation_tick_catchup";
                note = $"step {localStep:0.000} with tickDelta={tickDelta} backlog={inputBacklog}";
            }
            else if (!presentationPositionRemapped &&
                     !snapshotDrivenRenderPhaseMismatch &&
                     hasLocal && hasWorld &&
                     worldStep > (localStep * 1.8f + 0.03f) && ltwGap > 0.08f)
            {
                kind = KindPresentationMismatch;
                source = "presentation_transform_pipeline";
                note = $"worldStep {worldStep:0.000} vs localStep {localStep:0.000}";
            }
            else if (localStep > jumpThreshold && tickDelta <= 1u && inputBacklog == 0u)
            {
                kind = KindUnexpectedJump;
                source = followInterpActive
                    ? "simulation_writer_conflict_or_teleport"
                    : "presentation_interpolation_or_dual_writer";
                note = $"step {localStep:0.000} > jumpThreshold {jumpThreshold:0.000}";
            }
            else if (speedMultiplier > 1.05f &&
                     !followInterpActive &&
                     localStep > Mathf.Max(baseJumpThreshold, expectedStep * 2.2f + 0.15f))
            {
                kind = KindHighSpeedNoInterp;
                source = "presentation_interpolation_disabled";
                note = $"speed={speedMultiplier:0.00} step={localStep:0.000}";
            }

            if (string.IsNullOrEmpty(kind) &&
                heartbeatEnabled &&
                now >= _nextHeartbeatAt)
            {
                kind = KindHeartbeat;
                source = "probe_alive";
                note = "no_glitch_detected";
                _nextHeartbeatAt = now + Mathf.Max(0.1f, heartbeatIntervalSeconds);
            }

            if (!string.IsNullOrEmpty(kind) && ShouldEmit(kind, now))
            {
                var record = new GlitchRecord
                {
                    timestamp_utc = DateTime.UtcNow.ToString("o"),
                    scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                    kind = kind,
                    likely_source = source,
                    mode = mode,
                    entity = FormatEntity(target),
                    note = note,
                    tick = currentTick,
                    tick_delta = tickDelta,
                    fixed_dt = hasTimeState ? timeState.FixedDeltaTime : 0f,
                    speed_multiplier = speedMultiplier,
                    interpolation_alpha = interpolationAlpha,
                    input_backlog = inputBacklog,
                    input_observed_delta = inputObservedDelta,
                    input_processed_steps = inputProcessed,
                    frame_dt = frameDt,
                    velocity_speed = effectiveSpeed,
                    expected_step = expectedStep,
                    local_step = localStep,
                    world_step = worldStep,
                    ltw_gap = ltwGap,
                    follow_interp_cfg = followInterpCfg ? 1 : 0,
                    follow_interp_active = followInterpActive ? 1 : 0,
                    has_movement_suppressed = hasMovementSuppressed ? 1 : 0,
                    has_orbit_anchor = hasOrbitAnchor ? 1 : 0,
                    has_orbit_state = hasOrbitState ? 1 : 0,
                    has_rogue_orbit = hasRogueOrbit ? 1 : 0,
                    has_micro_impulse = hasMicroImpulse ? 1 : 0,
                    has_frame_membership = hasFrameMembership ? 1 : 0,
                    has_frame_driven = hasFrameDriven ? 1 : 0,
                    has_pose_snapshot = hasPoseSnapshot ? 1 : 0,
                    has_render_frame_scale = hasRenderFrameScale ? 1 : 0,
                    has_band_scale = hasBandScale ? 1 : 0,
                    has_requires_physics = hasRequiresPhysics ? 1 : 0,
                    has_collision_buffer = hasCollisionBuffer ? 1 : 0,
                    has_physics_velocity = hasPhysicsVelocity ? 1 : 0,
                    physics_linear_speed = physicsLinearSpeed,
                    collision_events_count = collisionStats.EventCount,
                    collision_recent_count = collisionStats.RecentCount,
                    collision_recent_collision_count = collisionStats.RecentCollisionCount,
                    collision_recent_trigger_count = collisionStats.RecentTriggerCount,
                    collision_max_impulse = collisionStats.MaxImpulse,
                    input_magnitude = inputMagnitude,
                    stall_sample_count = stallSamplesForRecord,
                    stall_peak_expected_step = stallPeakExpectedForRecord,
                    stall_peak_runtime_speed = stallPeakSpeedForRecord
                };

                var line = JsonUtility.ToJson(record);
                if (TryAppendLine(_outputPath, line) && echoToUnityLog)
                {
                    UnityEngine.Debug.LogWarning($"[Space4XMovementGlitchProbe] {line}");
                }
            }

            SeedBaseline(target, localPosition, worldPosition, currentTick, now);
        }

        private static bool TryResolveFallbackTarget(EntityManager em, out Entity target, out bool hasControlledFlagship)
        {
            target = Entity.Null;
            hasControlledFlagship = false;

            using (var flagshipQuery = em.CreateEntityQuery(
                       ComponentType.ReadOnly<PlayerFlagshipTag>(),
                       ComponentType.ReadOnly<LocalTransform>()))
            {
                if (!flagshipQuery.IsEmptyIgnoreFilter)
                {
                    using var entities = flagshipQuery.ToEntityArray(Allocator.Temp);
                    for (var i = 0; i < entities.Length; i++)
                    {
                        var candidate = entities[i];
                        if (!em.Exists(candidate))
                        {
                            continue;
                        }

                        target = candidate;
                        hasControlledFlagship = true;
                        return true;
                    }
                }
            }

            using (var flightQuery = em.CreateEntityQuery(
                       ComponentType.ReadOnly<ShipFlightRuntimeState>(),
                       ComponentType.ReadOnly<LocalTransform>()))
            {
                if (!flightQuery.IsEmptyIgnoreFilter)
                {
                    using var entities = flightQuery.ToEntityArray(Allocator.Temp);
                    for (var i = 0; i < entities.Length; i++)
                    {
                        var candidate = entities[i];
                        if (!em.Exists(candidate))
                        {
                            continue;
                        }

                        target = candidate;
                        hasControlledFlagship = em.HasComponent<PlayerFlagshipTag>(candidate);
                        return true;
                    }
                }
            }

            using (var movementQuery = em.CreateEntityQuery(
                       ComponentType.ReadOnly<VesselMovement>(),
                       ComponentType.ReadOnly<LocalTransform>()))
            {
                if (!movementQuery.IsEmptyIgnoreFilter)
                {
                    using var entities = movementQuery.ToEntityArray(Allocator.Temp);
                    for (var i = 0; i < entities.Length; i++)
                    {
                        var candidate = entities[i];
                        if (!em.Exists(candidate))
                        {
                            continue;
                        }

                        target = candidate;
                        hasControlledFlagship = em.HasComponent<PlayerFlagshipTag>(candidate);
                        return true;
                    }
                }
            }

            return false;
        }

        private bool ShouldEmit(string kind, float now)
        {
            var cooldown = Mathf.Max(0.05f, perKindCooldownSeconds);
            float lastEmitAt;
            switch (kind)
            {
                case KindTransformParityGap:
                    lastEmitAt = _lastTransformParityEmitAt;
                    break;
                case KindTickCatchupJump:
                    lastEmitAt = _lastTickCatchupEmitAt;
                    break;
                case KindPresentationMismatch:
                    lastEmitAt = _lastPresentationMismatchEmitAt;
                    break;
                case KindUnexpectedJump:
                    lastEmitAt = _lastUnexpectedJumpEmitAt;
                    break;
                case KindHighSpeedNoInterp:
                    lastEmitAt = _lastHighSpeedNoInterpEmitAt;
                    break;
                case KindStallThenImpulse:
                    lastEmitAt = _lastStallThenImpulseEmitAt;
                    break;
                case KindHeartbeat:
                    return true;
                default:
                    return false;
            }

            if (now - lastEmitAt < cooldown)
                return false;

            switch (kind)
            {
                case KindTransformParityGap:
                    _lastTransformParityEmitAt = now;
                    break;
                case KindTickCatchupJump:
                    _lastTickCatchupEmitAt = now;
                    break;
                case KindPresentationMismatch:
                    _lastPresentationMismatchEmitAt = now;
                    break;
                case KindUnexpectedJump:
                    _lastUnexpectedJumpEmitAt = now;
                    break;
                case KindHighSpeedNoInterp:
                    _lastHighSpeedNoInterpEmitAt = now;
                    break;
                case KindStallThenImpulse:
                    _lastStallThenImpulseEmitAt = now;
                    break;
            }

            return true;
        }

        private static bool TryGetPoseSamples(
            EntityManager em,
            Entity target,
            out Vector3 localPosition,
            out Vector3 worldPosition,
            out bool hasLocal,
            out bool hasWorld)
        {
            localPosition = Vector3.zero;
            worldPosition = Vector3.zero;
            hasLocal = false;
            hasWorld = false;

            if (em.HasComponent<LocalTransform>(target))
            {
                var local = em.GetComponentData<LocalTransform>(target);
                localPosition = new Vector3(local.Position.x, local.Position.y, local.Position.z);
                hasLocal = true;
            }

            if (em.HasComponent<LocalToWorld>(target))
            {
                var localToWorld = em.GetComponentData<LocalToWorld>(target);
                worldPosition = new Vector3(localToWorld.Position.x, localToWorld.Position.y, localToWorld.Position.z);
                hasWorld = true;
            }

            if (!hasLocal && !hasWorld)
                return false;

            if (!hasLocal)
                localPosition = worldPosition;

            if (!hasWorld)
                worldPosition = localPosition;

            return true;
        }

        private static bool TryGetTimeState(EntityManager em, out TimeState timeState, out float interpolationAlpha)
        {
            timeState = default;
            interpolationAlpha = 0f;

            using var timeQuery = em.CreateEntityQuery(ComponentType.ReadOnly<TimeState>());
            if (timeQuery.IsEmptyIgnoreFilter)
                return false;

            timeState = timeQuery.GetSingleton<TimeState>();

            using var interpolationQuery = em.CreateEntityQuery(ComponentType.ReadOnly<FixedStepInterpolationState>());
            if (!interpolationQuery.IsEmptyIgnoreFilter)
            {
                interpolationAlpha = interpolationQuery.GetSingleton<FixedStepInterpolationState>().Alpha;
            }

            return true;
        }

        private static bool TryGetInputTickDiagnostics(EntityManager em, out PlayerFlagshipInputTickDiagnostics diagnostics)
        {
            diagnostics = default;
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerFlagshipInputTickDiagnostics>());
            if (query.IsEmptyIgnoreFilter)
                return false;

            diagnostics = query.GetSingleton<PlayerFlagshipInputTickDiagnostics>();
            return true;
        }

        private readonly struct CollisionStats
        {
            public readonly int EventCount;
            public readonly int RecentCount;
            public readonly int RecentCollisionCount;
            public readonly int RecentTriggerCount;
            public readonly float MaxImpulse;

            public CollisionStats(
                int eventCount,
                int recentCount,
                int recentCollisionCount,
                int recentTriggerCount,
                float maxImpulse)
            {
                EventCount = eventCount;
                RecentCount = recentCount;
                RecentCollisionCount = recentCollisionCount;
                RecentTriggerCount = recentTriggerCount;
                MaxImpulse = maxImpulse;
            }
        }

        private static CollisionStats ReadCollisionStats(
            DynamicBuffer<PhysicsCollisionEventElement> events,
            uint currentTick,
            int recentTickWindow)
        {
            var eventCount = events.Length;
            var recentCount = 0;
            var recentCollisionCount = 0;
            var recentTriggerCount = 0;
            var maxImpulse = 0f;
            var tickWindow = math.max(0, recentTickWindow);

            for (var i = 0; i < events.Length; i++)
            {
                var evt = events[i];
                maxImpulse = math.max(maxImpulse, evt.Impulse);

                var isRecent = false;
                if (tickWindow <= 0)
                {
                    isRecent = evt.Tick == currentTick;
                }
                else if (currentTick >= evt.Tick)
                {
                    isRecent = (currentTick - evt.Tick) <= (uint)tickWindow;
                }

                if (!isRecent)
                {
                    continue;
                }

                recentCount += 1;
                if (evt.EventType == PhysicsCollisionEventType.Collision)
                {
                    recentCollisionCount += 1;
                }
                else
                {
                    recentTriggerCount += 1;
                }
            }

            return new CollisionStats(
                eventCount,
                recentCount,
                recentCollisionCount,
                recentTriggerCount,
                maxImpulse);
        }

        private static bool IsPresentationPositionRemapped(
            EntityManager em,
            Entity target,
            out bool hasRenderFrameScale,
            out bool hasBandScale)
        {
            hasRenderFrameScale = false;
            hasBandScale = false;

            if (em.HasComponent<Space4XOrbitalBandState>(target))
            {
                var band = em.GetComponentData<Space4XOrbitalBandState>(target);
                hasBandScale = band.InBand != 0 &&
                               band.AnchorFrame != Entity.Null &&
                               math.abs(band.PresentationScale - 1f) > 0.0001f;
            }

            using var configQuery = em.CreateEntityQuery(ComponentType.ReadOnly<Space4XRenderFrameConfig>());
            if (!configQuery.IsEmptyIgnoreFilter)
            {
                var renderFrameConfig = configQuery.GetSingleton<Space4XRenderFrameConfig>();
                if (renderFrameConfig.Enabled != 0)
                {
                    using var stateQuery = em.CreateEntityQuery(ComponentType.ReadOnly<Space4XRenderFrameState>());
                    if (!stateQuery.IsEmptyIgnoreFilter)
                    {
                        var renderFrameState = stateQuery.GetSingleton<Space4XRenderFrameState>();
                        hasRenderFrameScale = renderFrameState.AnchorFrame != Entity.Null &&
                                              math.abs(renderFrameState.Scale - 1f) > 0.0001f;
                    }
                }
            }

            return hasRenderFrameScale || hasBandScale;
        }

        private static bool TryAppendLine(string path, string line)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.AppendAllText(path, line + SystemEnv.NewLine);
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[Space4XMovementGlitchProbe] write failed: {ex.Message}");
                return false;
            }
        }

        private static string ResolveOutputPath()
        {
            var env = SystemEnv.GetEnvironmentVariable(ProbeOutputEnv);
            if (!string.IsNullOrWhiteSpace(env))
                return env;

            var outDir = SystemEnv.GetEnvironmentVariable(ProbeOutDirEnv);
            if (!string.IsNullOrWhiteSpace(outDir))
                return Path.Combine(outDir, DefaultFileName);

            return Path.Combine(Application.persistentDataPath, DefaultFileName);
        }

        private bool ResolveActiveFromEnvOrDefault()
        {
            if (!TryReadEnvironmentEnabled(out var enabled))
                return enabledByDefault;

            return enabled;
        }

        private bool ResolveEchoFromEnvOrDefault()
        {
            if (!TryReadEnvironmentFlag(ProbeEchoEnv, out var enabled))
                return echoToUnityLog;

            return enabled;
        }

        private static bool IsEnabledByEnvironment()
        {
            return TryReadEnvironmentEnabled(out var enabled) && enabled;
        }

        private static bool TryReadEnvironmentEnabled(out bool enabled)
        {
            return TryReadEnvironmentFlag(ProbeEnabledEnv, out enabled);
        }

        private static bool TryReadEnvironmentFlag(string envVarName, out bool enabled)
        {
            enabled = false;
            var env = SystemEnv.GetEnvironmentVariable(envVarName);
            if (string.IsNullOrWhiteSpace(env))
                return false;

            var token = env.Trim().ToLowerInvariant();
            if (token == "1" || token == "true" || token == "yes" || token == "on")
            {
                enabled = true;
                return true;
            }

            if (token == "0" || token == "false" || token == "no" || token == "off")
            {
                enabled = false;
                return true;
            }

            return false;
        }

        private void SeedBaseline(Entity target, Vector3 localPosition, Vector3 worldPosition, uint tick, float sampleAt)
        {
            _hasBaseline = true;
            _baselineTarget = target;
            _baselineLocalPosition = localPosition;
            _baselineWorldPosition = worldPosition;
            _baselineTick = tick;
            _baselineSampleAt = sampleAt;
        }

        private void ResetBaseline()
        {
            _hasBaseline = false;
            _baselineTarget = Entity.Null;
            _baselineLocalPosition = Vector3.zero;
            _baselineWorldPosition = Vector3.zero;
            _baselineTick = 0u;
            _baselineSampleAt = 0f;
            ResetStallTracking();
        }

        private void ResetStallTracking()
        {
            _stallSampleCount = 0;
            _stallPeakExpectedStep = 0f;
            _stallPeakRuntimeSpeed = 0f;
        }

        private static string FormatEntity(Entity entity)
        {
            return entity == Entity.Null
                ? "Entity.Null"
                : $"Entity({entity.Index}:{entity.Version})";
        }
    }
}
