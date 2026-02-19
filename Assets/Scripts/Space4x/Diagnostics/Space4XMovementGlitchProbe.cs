#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interaction;
using Space4X.Presentation;
using Space4X.Registry;
using Space4X.Runtime;
using Space4X.UI;
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
        private const string DefaultFileName = "space4x_movement_glitches.jsonl";

        private const string KindTransformParityGap = "transform_parity_gap";
        private const string KindTickCatchupJump = "tick_catchup_jump";
        private const string KindPresentationMismatch = "presentation_step_mismatch";
        private const string KindUnexpectedJump = "unexpected_position_jump";
        private const string KindHighSpeedNoInterp = "high_speed_no_interpolation";

        [SerializeField] private bool enabledByDefault;
        [SerializeField] private bool echoToUnityLog = false;
        [SerializeField] private Key toggleKey = Key.F8;
        [SerializeField] private float sampleIntervalSeconds = 0f;
        [SerializeField] private float transformParityGapThreshold = 0.2f;
        [SerializeField] private float baseJumpThreshold = 0.4f;
        [SerializeField] private float expectedStepJumpMultiplier = 3f;
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
            _nextSampleAt = 0f;
            ResetBaseline();
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
            var camera = UCamera.main ?? FindAnyObjectByType<UCamera>();
            if (camera == null)
            {
                ResetBaseline();
                return;
            }

            var follow = camera.GetComponent<Space4XFollowPlayerVessel>();
            var controller = camera.GetComponent<Space4XPlayerFlagshipController>();

            var target = Entity.Null;
            if (controller != null && controller.TryGetControlledFlagship(out var controlled))
            {
                target = controlled;
            }
            else if (follow != null && follow.TryGetDebugTarget(out var followTarget))
            {
                target = followTarget;
            }

            if (target == Entity.Null)
            {
                ResetBaseline();
                return;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                ResetBaseline();
                return;
            }

            var em = world.EntityManager;
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

            var expectedStep = Mathf.Max(velocitySpeed * frameDt, 0f);
            var jumpThreshold = Mathf.Max(baseJumpThreshold, (expectedStep * Mathf.Max(1f, expectedStepJumpMultiplier)) + 0.08f);

            var hasInputDiagnostics = TryGetInputTickDiagnostics(em, out var inputDiagnostics);
            var inputBacklog = hasInputDiagnostics ? inputDiagnostics.TickBacklog : 0u;
            var inputObservedDelta = hasInputDiagnostics ? inputDiagnostics.TickDeltaObserved : 0u;
            var inputProcessed = hasInputDiagnostics ? inputDiagnostics.TickStepsProcessed : 0u;

            var followInterpCfg = follow != null && follow.DebugInterpolateControlledFlagshipPose;
            var followInterpActive = follow != null && follow.DebugControlledPoseInterpolationActive;
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

            var kind = string.Empty;
            var source = string.Empty;
            var note = string.Empty;

            if (!presentationPositionRemapped &&
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
                    velocity_speed = velocitySpeed,
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
                    has_band_scale = hasBandScale ? 1 : 0
                };

                var line = JsonUtility.ToJson(record);
                if (TryAppendLine(_outputPath, line) && echoToUnityLog)
                {
                    UnityEngine.Debug.LogWarning($"[Space4XMovementGlitchProbe] {line}");
                }
            }

            SeedBaseline(target, localPosition, worldPosition, currentTick, now);
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

            return Path.Combine(Application.persistentDataPath, DefaultFileName);
        }

        private bool ResolveActiveFromEnvOrDefault()
        {
            if (!TryReadEnvironmentEnabled(out var enabled))
                return enabledByDefault;

            return enabled;
        }

        private static bool IsEnabledByEnvironment()
        {
            return TryReadEnvironmentEnabled(out var enabled) && enabled;
        }

        private static bool TryReadEnvironmentEnabled(out bool enabled)
        {
            enabled = false;
            var env = SystemEnv.GetEnvironmentVariable(ProbeEnabledEnv);
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
        }

        private static string FormatEntity(Entity entity)
        {
            return entity == Entity.Null
                ? "Entity.Null"
                : $"Entity({entity.Index}:{entity.Version})";
        }
    }
}
#endif
