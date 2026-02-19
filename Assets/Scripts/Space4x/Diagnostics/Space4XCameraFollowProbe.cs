#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using Space4X.UI;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using UCamera = UnityEngine.Camera;
using UTime = UnityEngine.Time;

namespace Space4X.Diagnostics
{
    /// <summary>
    /// Optional structured camera follow probe for automated nuisance filtering.
    /// Emits JSONL samples with alignment and orientation drift metrics.
    /// </summary>
    [DefaultExecutionOrder(-9390)]
    [DisallowMultipleComponent]
    public sealed class Space4XCameraFollowProbe : MonoBehaviour
    {
        private const string ProbeEnabledEnv = "SPACE4X_CAMERA_PROBE";
        private const string ProbeOutputEnv = "SPACE4X_CAMERA_PROBE_OUT";
        private const string DefaultFileName = "space4x_camera_follow_probe.jsonl";

        [SerializeField] private bool enabledByDefault;
        [SerializeField] private Key toggleKey = Key.F7;
        [SerializeField] private float sampleIntervalSeconds = 0.25f;
        [SerializeField] private bool echoToUnityLog;

        private string _outputPath = string.Empty;
        private bool _active;
        private float _nextSampleAt;
        private bool _hasSpinBaseline;
        private float _lastCameraYaw;
        private float _lastSampleAt;

        [Serializable]
        private sealed class ProbeSample
        {
            public string timestamp_utc = string.Empty;
            public string scene = string.Empty;
            public string mode = string.Empty;
            public bool has_controlled;
            public bool has_target;
            public string controlled = "Entity.Null";
            public string target = "Entity.Null";
            public bool aligned;
            public bool has_target_transform;
            public float yaw_delta_deg = -1f;
            public float camera_spin_deg_s;
            public float camera_roll_deg;
            public float camera_x;
            public float camera_y;
            public float camera_z;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Application.isBatchMode && !IsEnabledByEnvironment())
                return;

            if (FindFirstObjectByType<Space4XCameraFollowProbe>() != null)
                return;

            var go = new GameObject("Space4X Camera Follow Probe");
            DontDestroyOnLoad(go);
            go.AddComponent<Space4XCameraFollowProbe>();
        }

        private void OnEnable()
        {
            _outputPath = ResolveOutputPath();
            _active = ResolveActiveFromEnvOrDefault();
            _nextSampleAt = 0f;
            _hasSpinBaseline = false;
            _lastCameraYaw = 0f;
            _lastSampleAt = 0f;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && toggleKey != Key.None && keyboard[toggleKey].wasPressedThisFrame)
            {
                _active = !_active;
                UnityEngine.Debug.Log($"[Space4XCameraFollowProbe] active={_active} path='{_outputPath}'");
            }

            if (!_active)
                return;

            if (UTime.unscaledTime < _nextSampleAt)
                return;

            _nextSampleAt = UTime.unscaledTime + math.max(0.05f, sampleIntervalSeconds);
            TryWriteSample();
        }

        private void TryWriteSample()
        {
            var camera = UCamera.main ?? FindFirstObjectByType<UCamera>();
            if (camera == null)
                return;

            var follow = camera.GetComponent<Space4XFollowPlayerVessel>();
            var controller = camera.GetComponent<Space4XPlayerFlagshipController>();

            var controlledEntity = Entity.Null;
            var targetEntity = Entity.Null;
            var hasControlled = controller != null && controller.TryGetControlledFlagship(out controlledEntity);
            var hasTarget = follow != null && follow.TryGetDebugTarget(out targetEntity);
            var aligned = hasControlled && hasTarget && controlledEntity == targetEntity;

            var sample = new ProbeSample
            {
                timestamp_utc = global::System.DateTime.UtcNow.ToString("o"),
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                mode = Space4XControlModeState.CurrentMode.ToString(),
                has_controlled = hasControlled,
                has_target = hasTarget,
                controlled = FormatEntity(hasControlled ? controlledEntity : Entity.Null),
                target = FormatEntity(hasTarget ? targetEntity : Entity.Null),
                aligned = aligned,
                camera_roll_deg = Mathf.DeltaAngle(0f, camera.transform.eulerAngles.z),
                camera_x = camera.transform.position.x,
                camera_y = camera.transform.position.y,
                camera_z = camera.transform.position.z
            };

            if (hasTarget && TryComputeYawDelta(camera, targetEntity, out var yawDelta))
            {
                sample.has_target_transform = true;
                sample.yaw_delta_deg = yawDelta;
            }

            if (_hasSpinBaseline)
            {
                var dt = math.max(1e-4f, UTime.unscaledTime - _lastSampleAt);
                var delta = Mathf.DeltaAngle(_lastCameraYaw, camera.transform.eulerAngles.y);
                sample.camera_spin_deg_s = math.abs(delta) / dt;
            }
            else
            {
                sample.camera_spin_deg_s = 0f;
                _hasSpinBaseline = true;
            }

            _lastCameraYaw = camera.transform.eulerAngles.y;
            _lastSampleAt = UTime.unscaledTime;

            var jsonLine = JsonUtility.ToJson(sample);
            if (!TryAppendLine(_outputPath, jsonLine))
            {
                return;
            }

            if (echoToUnityLog)
            {
                UnityEngine.Debug.Log($"[Space4XCameraFollowProbe] {jsonLine}");
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

                File.AppendAllText(path, line + global::System.Environment.NewLine);
                return true;
            }
            catch (global::System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[Space4XCameraFollowProbe] write failed: {ex.Message}");
                return false;
            }
        }

        private static string ResolveOutputPath()
        {
            var env = global::System.Environment.GetEnvironmentVariable(ProbeOutputEnv);
            if (!string.IsNullOrWhiteSpace(env))
            {
                return env;
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
            var env = global::System.Environment.GetEnvironmentVariable(ProbeEnabledEnv);
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

        private static bool TryComputeYawDelta(UCamera camera, Entity targetEntity, out float yawDeltaDeg)
        {
            yawDeltaDeg = 0f;
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return false;
            }

            var em = world.EntityManager;
            if (!em.Exists(targetEntity) || !em.HasComponent<LocalTransform>(targetEntity))
            {
                return false;
            }

            var localTransform = em.GetComponentData<LocalTransform>(targetEntity);
            var shipForward3 = math.mul(localTransform.Rotation, new float3(0f, 0f, 1f));
            var cameraForward3 = (float3)camera.transform.forward;

            var shipForward2 = new Vector2(shipForward3.x, shipForward3.z);
            var cameraForward2 = new Vector2(cameraForward3.x, cameraForward3.z);
            if (shipForward2.sqrMagnitude < 1e-6f || cameraForward2.sqrMagnitude < 1e-6f)
            {
                return false;
            }

            shipForward2.Normalize();
            cameraForward2.Normalize();
            yawDeltaDeg = Mathf.Abs(Vector2.SignedAngle(cameraForward2, shipForward2));
            return true;
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
