using System;
using System.IO;
using PureDOTS.Input;
using PureDOTS.Runtime.Interaction;
using Space4X.Input;
using Space4X.Registry;
using Space4X.UI;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UCamera = UnityEngine.Camera;
using UTime = UnityEngine.Time;
using CameraRigApplier = PureDOTS.Runtime.Camera.CameraRigApplier;
using Space4XCameraRigController = Space4X.Camera.Space4XCameraRigController;
using SysEnv = System.Environment;

namespace Space4X.Diagnostics
{
    /// <summary>
    /// Validates control-mode handoff contracts (camera/input/flagship ownership) with settle windows.
    /// Helps detect mode drift regressions and "wrong mode still active" bugs.
    /// </summary>
    [DefaultExecutionOrder(-9371)]
    [DisallowMultipleComponent]
    public sealed class Space4XModeTransitionContractProbe : MonoBehaviour
    {
        private const string ProbeEnabledEnv = "SPACE4X_MODE_CONTRACT_PROBE";
        private const string ProbeOutputEnv = "SPACE4X_MODE_CONTRACT_PROBE_OUT";
        private const string ProbeOutDirEnv = "SPACE4X_PROBE_OUT_DIR";
        private const string DefaultFileName = "space4x_mode_transition_contract_probe.jsonl";

        private const string KindTransition = "transition";
        private const string KindVariant = "variant";
        private const string KindHeartbeat = "heartbeat";

        [SerializeField] private bool enabledByDefault;
        [SerializeField] private Key toggleKey = Key.F10;
        [SerializeField] private float sampleIntervalSeconds = 0.05f;
        [SerializeField] private float heartbeatIntervalSeconds = 5f;
        [SerializeField] private float settleStableSeconds = 0.15f;
        [SerializeField] private float transitionTimeoutSeconds = 1.5f;
        [SerializeField] private bool echoViolationsToUnityLog = true;
        [SerializeField] private bool requireRtsComponentsToExistForRtsModes;

        private struct TransitionState
        {
            public bool Active;
            public Space4XControlMode FromMode;
            public Space4XControlMode ToMode;
            public float StartedAt;
            public float StableSinceAt;
        }

        [Serializable]
        private sealed class ProbeRecord
        {
            public string timestamp_utc = string.Empty;
            public string scene = string.Empty;
            public string kind = string.Empty;
            public string outcome = string.Empty;
            public string mode_from = string.Empty;
            public string mode_to = string.Empty;
            public string current_mode = string.Empty;
            public string hint = string.Empty;
            public int transition_count;
            public int violation_count;
            public float settle_ms;
            public int expects_rts_rig;
            public int rts_rig_enabled;
            public int rts_applier_enabled;
            public int expects_rts_bridge;
            public int rts_bridge_enabled;
            public int classic_command_enabled;
            public int has_controlled;
            public int movement_suppressed_enabled;
            public int has_target;
            public int controlled_aligned;
        }

        private string _outputPath = string.Empty;
        private bool _active;
        private float _nextSampleAt;
        private float _nextHeartbeatAt;
        private Space4XControlMode _lastKnownMode;
        private bool _hasLastKnownMode;
        private TransitionState _transition;
        private int _transitionCount;
        private int _violationCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Application.isBatchMode && !IsEnabledByEnvironment())
                return;

            if (FindAnyObjectByType<Space4XModeTransitionContractProbe>() != null)
                return;

            var go = new GameObject("Space4X Mode Transition Contract Probe");
            DontDestroyOnLoad(go);
            go.AddComponent<Space4XModeTransitionContractProbe>();
        }

        private void OnEnable()
        {
            _outputPath = ResolveOutputPath();
            _active = ResolveActiveFromEnvOrDefault();
            _nextSampleAt = 0f;
            _nextHeartbeatAt = 0f;
            _lastKnownMode = Space4XControlModeState.CurrentMode;
            _hasLastKnownMode = true;
            _transition = default;
            _transitionCount = 0;
            _violationCount = 0;

            Space4XControlModeState.ModeChanged += OnModeChanged;
            Space4XControlModeState.ModeVariantChanged += OnModeVariantChanged;
            UnityEngine.Debug.Log($"[Space4XModeTransitionContractProbe] boot active={_active} path='{_outputPath}'");
        }

        private void OnDisable()
        {
            Space4XControlModeState.ModeChanged -= OnModeChanged;
            Space4XControlModeState.ModeVariantChanged -= OnModeVariantChanged;
            _transition = default;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && toggleKey != Key.None && keyboard[toggleKey].wasPressedThisFrame)
            {
                _active = !_active;
                UnityEngine.Debug.Log($"[Space4XModeTransitionContractProbe] active={_active} path='{_outputPath}'");
                if (!_active)
                {
                    _transition = default;
                }
            }

            if (!_active || UTime.unscaledTime < _nextSampleAt)
                return;

            _nextSampleAt = UTime.unscaledTime + Mathf.Max(0.01f, sampleIntervalSeconds);
            EvaluateTransition();

            var now = UTime.unscaledTime;
            if (now >= _nextHeartbeatAt)
            {
                _nextHeartbeatAt = now + Mathf.Max(1f, heartbeatIntervalSeconds);
                var snapshot = BuildSnapshot(Space4XControlModeState.CurrentMode, "heartbeat");
                WriteRecord(snapshot, KindHeartbeat, "sample", 0f);
            }
        }

        private void OnModeChanged(Space4XControlMode mode)
        {
            var fromMode = _hasLastKnownMode ? _lastKnownMode : mode;
            _lastKnownMode = mode;
            _hasLastKnownMode = true;
            _transitionCount++;
            _transition = new TransitionState
            {
                Active = true,
                FromMode = fromMode,
                ToMode = mode,
                StartedAt = UTime.unscaledTime,
                StableSinceAt = -1f
            };
        }

        private void OnModeVariantChanged(Space4XControlMode mode, bool enabled)
        {
            if (!_active)
                return;

            var snapshot = BuildSnapshot(mode, enabled ? "variant_enabled" : "variant_disabled");
            WriteRecord(snapshot, KindVariant, enabled ? "enabled" : "disabled", 0f);
        }

        private void EvaluateTransition()
        {
            if (!_transition.Active)
                return;

            var now = UTime.unscaledTime;
            var elapsed = Mathf.Max(0f, now - _transition.StartedAt);
            var snapshot = BuildSnapshot(_transition.ToMode, "transition");
            var settled = EvaluateContractSatisfied(_transition.ToMode, snapshot, out var hint);

            if (settled)
            {
                if (_transition.StableSinceAt < 0f)
                {
                    _transition.StableSinceAt = now;
                    return;
                }

                if (now - _transition.StableSinceAt >= Mathf.Max(0.01f, settleStableSeconds))
                {
                    snapshot.hint = "settled";
                    WriteRecord(snapshot, KindTransition, "ok", elapsed * 1000f);
                    _transition = default;
                }

                return;
            }

            _transition.StableSinceAt = -1f;
            if (elapsed < Mathf.Max(0.05f, transitionTimeoutSeconds))
                return;

            _violationCount++;
            snapshot.violation_count = _violationCount;
            snapshot.hint = string.IsNullOrWhiteSpace(hint) ? "contract_not_satisfied" : hint;
            WriteRecord(snapshot, KindTransition, "violation", elapsed * 1000f);
            _transition = default;
        }

        private ProbeRecord BuildSnapshot(Space4XControlMode mode, string hint)
        {
            var camera = UCamera.main ?? FindAnyObjectByType<UCamera>();
            var rtsRig = camera != null ? camera.GetComponent<Space4XCameraRigController>() : null;
            var rtsApplier = camera != null ? camera.GetComponent<CameraRigApplier>() : null;
            var rtsBridge = FindAnyObjectByType<RtsInputBridge>();
            var classicCommands = FindAnyObjectByType<Space4XRtsClassicCommandMono>();
            var controller = camera != null ? camera.GetComponent<Space4XPlayerFlagshipController>() : null;
            var follow = camera != null ? camera.GetComponent<Space4XFollowPlayerVessel>() : null;

            var controlledEntity = Entity.Null;
            var targetEntity = Entity.Null;
            var hasControlled = controller != null && controller.TryGetControlledFlagship(out controlledEntity);
            var hasTarget = follow != null && follow.TryGetDebugTarget(out targetEntity);
            var aligned = hasControlled && hasTarget && controlledEntity == targetEntity;
            var movementSuppressedEnabled = ResolveMovementSuppressedEnabled(controlledEntity);

            var expectsRtsRig = mode == Space4XControlMode.Rts || mode == Space4XControlMode.DivineHand;
            var expectsRtsBridge = mode == Space4XControlMode.Rts;

            return new ProbeRecord
            {
                timestamp_utc = DateTime.UtcNow.ToString("o"),
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                mode_from = _transition.FromMode.ToString(),
                mode_to = _transition.ToMode.ToString(),
                current_mode = Space4XControlModeState.CurrentMode.ToString(),
                hint = hint,
                transition_count = _transitionCount,
                violation_count = _violationCount,
                expects_rts_rig = expectsRtsRig ? 1 : 0,
                rts_rig_enabled = BoolToInt(rtsRig != null && rtsRig.enabled),
                rts_applier_enabled = BoolToInt(rtsApplier != null && rtsApplier.enabled),
                expects_rts_bridge = expectsRtsBridge ? 1 : 0,
                rts_bridge_enabled = BoolToInt(rtsBridge != null && rtsBridge.enabled),
                classic_command_enabled = BoolToInt(classicCommands != null && classicCommands.enabled),
                has_controlled = BoolToInt(hasControlled),
                movement_suppressed_enabled = BoolToInt(movementSuppressedEnabled),
                has_target = BoolToInt(hasTarget),
                controlled_aligned = BoolToInt(aligned)
            };
        }

        private bool EvaluateContractSatisfied(Space4XControlMode targetMode, ProbeRecord snapshot, out string hint)
        {
            hint = string.Empty;
            var expectsRtsRig = targetMode == Space4XControlMode.Rts || targetMode == Space4XControlMode.DivineHand;
            var expectsRtsBridge = targetMode == Space4XControlMode.Rts;
            var expectsMovementSuppressed = targetMode == Space4XControlMode.CursorOrient || targetMode == Space4XControlMode.CruiseLook;

            if (Space4XControlModeState.CurrentMode != targetMode)
            {
                hint = "mode_not_committed";
                return false;
            }

            if (expectsRtsRig && requireRtsComponentsToExistForRtsModes &&
                snapshot.rts_rig_enabled == 0 && snapshot.rts_applier_enabled == 0)
            {
                hint = "rts_components_missing";
                return false;
            }

            if (snapshot.rts_rig_enabled != BoolToInt(expectsRtsRig))
            {
                hint = expectsRtsRig ? "rts_rig_not_enabled" : "rts_rig_still_enabled";
                return false;
            }

            if (snapshot.rts_applier_enabled != BoolToInt(expectsRtsRig))
            {
                hint = expectsRtsRig ? "rts_applier_not_enabled" : "rts_applier_still_enabled";
                return false;
            }

            if (snapshot.rts_bridge_enabled != BoolToInt(expectsRtsBridge))
            {
                hint = expectsRtsBridge ? "rts_bridge_not_enabled" : "rts_bridge_enabled_outside_rts";
                return false;
            }

            if (snapshot.classic_command_enabled != BoolToInt(expectsRtsBridge))
            {
                hint = expectsRtsBridge ? "classic_command_not_enabled" : "classic_command_enabled_outside_rts";
                return false;
            }

            if (snapshot.has_controlled == 0)
            {
                hint = "controlled_flagship_missing";
                return false;
            }

            if (snapshot.movement_suppressed_enabled != BoolToInt(expectsMovementSuppressed))
            {
                hint = expectsMovementSuppressed
                    ? "movement_suppressed_not_enabled_in_manual"
                    : "movement_suppressed_still_enabled_in_rts";
                return false;
            }

            if (snapshot.has_target == 1 && snapshot.controlled_aligned == 0)
            {
                hint = "camera_target_not_aligned_with_controlled";
                return false;
            }

            return true;
        }

        private static bool ResolveMovementSuppressedEnabled(Entity controlledEntity)
        {
            if (controlledEntity == Entity.Null)
                return false;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;

            var entityManager = world.EntityManager;
            if (!entityManager.Exists(controlledEntity) || !entityManager.HasComponent<MovementSuppressed>(controlledEntity))
                return false;

            return entityManager.IsComponentEnabled<MovementSuppressed>(controlledEntity);
        }

        private void WriteRecord(ProbeRecord record, string kind, string outcome, float settleMs)
        {
            record.kind = kind;
            record.outcome = outcome;
            record.settle_ms = settleMs;
            var line = JsonUtility.ToJson(record);
            if (!TryAppendLine(_outputPath, line))
            {
                return;
            }

            if (echoViolationsToUnityLog && string.Equals(outcome, "violation", StringComparison.Ordinal))
            {
                UnityEngine.Debug.LogWarning(
                    $"[Space4XModeTransitionContractProbe] outcome=violation from={record.mode_from} to={record.mode_to} current={record.current_mode} hint={record.hint} settleMs={settleMs:0.0}");
            }
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

                File.AppendAllText(path, line + SysEnv.NewLine);
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[Space4XModeTransitionContractProbe] write failed: {ex.Message}");
                return false;
            }
        }

        private static string ResolveOutputPath()
        {
            var env = SysEnv.GetEnvironmentVariable(ProbeOutputEnv);
            if (!string.IsNullOrWhiteSpace(env))
            {
                return env;
            }

            var outDir = SysEnv.GetEnvironmentVariable(ProbeOutDirEnv);
            if (!string.IsNullOrWhiteSpace(outDir))
            {
                return Path.Combine(outDir, DefaultFileName);
            }

            return Path.Combine(Application.persistentDataPath, DefaultFileName);
        }

        private bool ResolveActiveFromEnvOrDefault()
        {
            if (!TryReadEnvironmentEnabled(out var enabled))
            {
                return enabledByDefault;
            }

            return enabled;
        }

        private static bool IsEnabledByEnvironment()
        {
            return TryReadEnvironmentEnabled(out var enabled) && enabled;
        }

        private static bool TryReadEnvironmentEnabled(out bool enabled)
        {
            enabled = false;
            var env = SysEnv.GetEnvironmentVariable(ProbeEnabledEnv);
            if (string.IsNullOrWhiteSpace(env))
            {
                return false;
            }

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

        private static int BoolToInt(bool value)
        {
            return value ? 1 : 0;
        }
    }
}
