#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using PureDOTS.Rendering;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using UTime = UnityEngine.Time;

namespace Space4X.Diagnostics
{
    /// <summary>
    /// Tracks renderable entity population and emits bounded lifecycle telemetry (heartbeat + drop events).
    /// Helps diagnose "disappearing entity" regressions without flooding logs.
    /// </summary>
    [DefaultExecutionOrder(-9375)]
    [DisallowMultipleComponent]
    public sealed class Space4XEntityVisibilityProbe : MonoBehaviour
    {
        private const string ProbeEnabledEnv = "SPACE4X_ENTITY_VISIBILITY_PROBE";
        private const string ProbeOutputEnv = "SPACE4X_ENTITY_VISIBILITY_PROBE_OUT";
        private const string DefaultFileName = "space4x_entity_visibility_probe.jsonl";

        [SerializeField] private bool enabledByDefault;
        [SerializeField] private Key toggleKey = Key.F6;
        [SerializeField] private float sampleIntervalSeconds = 0.5f;
        [SerializeField] private float heartbeatIntervalSeconds = 5f;
        [SerializeField] private int dropThresholdAbsolute = 2;
        [SerializeField] private float dropThresholdFractionFromPeak = 0.2f;
        [SerializeField] private float eventCooldownSeconds = 2f;
        [SerializeField] private bool echoEventsToUnityLog = true;

        private string _outputPath = string.Empty;
        private bool _active;
        private float _nextSampleAt;
        private float _nextHeartbeatAt;
        private float _lastEventAt = float.NegativeInfinity;
        private Snapshot _previous;
        private Snapshot _peak;
        private bool _hasBaseline;

        [Serializable]
        private sealed class ProbeRecord
        {
            public string timestamp_utc = string.Empty;
            public string scene = string.Empty;
            public string kind = string.Empty; // heartbeat | drop | flagship_lost | flagship_non_renderable
            public string event_name = string.Empty;
            public string hint = string.Empty;
            public int carrier;
            public int miner;
            public int asteroid;
            public int material_mesh;
            public int local_to_world;
            public int render_bounds;
            public int mesh_presenter;
            public int flagship_count;
            public int flagship_renderable;
            public int peak_carrier;
            public int peak_miner;
            public int peak_asteroid;
            public int peak_material_mesh;
            public int drop_carrier_from_prev;
            public int drop_miner_from_prev;
            public int drop_asteroid_from_prev;
            public int drop_material_mesh_from_prev;
            public int drop_carrier_from_peak;
            public int drop_miner_from_peak;
            public int drop_asteroid_from_peak;
            public int drop_material_mesh_from_peak;
        }

        private struct Snapshot
        {
            public int Carrier;
            public int Miner;
            public int Asteroid;
            public int MaterialMesh;
            public int LocalToWorld;
            public int RenderBounds;
            public int MeshPresenter;
            public int FlagshipCount;
            public int FlagshipRenderable;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Application.isBatchMode && !IsEnabledByEnvironment())
                return;

            if (FindAnyObjectByType<Space4XEntityVisibilityProbe>() != null)
                return;

            var go = new GameObject("Space4X Entity Visibility Probe");
            DontDestroyOnLoad(go);
            go.AddComponent<Space4XEntityVisibilityProbe>();
        }

        private void OnEnable()
        {
            _outputPath = ResolveOutputPath();
            _active = ResolveActiveFromEnvOrDefault();
            _nextSampleAt = 0f;
            _nextHeartbeatAt = 0f;
            _lastEventAt = float.NegativeInfinity;
            _previous = default;
            _peak = default;
            _hasBaseline = false;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && toggleKey != Key.None && keyboard[toggleKey].wasPressedThisFrame)
            {
                _active = !_active;
                Debug.Log($"[Space4XEntityVisibilityProbe] active={_active} path='{_outputPath}'");
                if (!_active)
                {
                    _hasBaseline = false;
                    _previous = default;
                    _peak = default;
                }
            }

            if (!_active || UTime.unscaledTime < _nextSampleAt)
                return;

            _nextSampleAt = UTime.unscaledTime + Mathf.Max(0.05f, sampleIntervalSeconds);
            SampleAndRecord();
        }

        private void SampleAndRecord()
        {
            if (!TryBuildSnapshot(out var snapshot))
            {
                return;
            }

            if (!_hasBaseline)
            {
                _previous = snapshot;
                _peak = snapshot;
                _hasBaseline = true;
            }

            _peak = MaxSnapshot(_peak, snapshot);
            var now = UTime.unscaledTime;
            var canEmitEvent = now >= _lastEventAt + Mathf.Max(0.05f, eventCooldownSeconds);

            if (canEmitEvent)
            {
                if (_previous.FlagshipCount > 0 && snapshot.FlagshipCount == 0)
                {
                    EmitEvent("flagship_lost", "Controlled flagship tag disappeared from world.", snapshot, _previous, _peak);
                    _lastEventAt = now;
                }
                else if (snapshot.FlagshipCount > 0 && snapshot.FlagshipRenderable == 0)
                {
                    EmitEvent("flagship_non_renderable", BuildFlagshipHint(snapshot), snapshot, _previous, _peak);
                    _lastEventAt = now;
                }
                else if (TryResolveDropEvent(snapshot, _previous, _peak, out var eventName, out var hint))
                {
                    EmitEvent(eventName, hint, snapshot, _previous, _peak);
                    _lastEventAt = now;
                }
            }

            if (now >= _nextHeartbeatAt)
            {
                _nextHeartbeatAt = now + Mathf.Max(1f, heartbeatIntervalSeconds);
                WriteRecord(new ProbeRecord
                {
                    timestamp_utc = DateTime.UtcNow.ToString("o"),
                    scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                    kind = "heartbeat",
                    event_name = "state",
                    hint = "sample",
                    carrier = snapshot.Carrier,
                    miner = snapshot.Miner,
                    asteroid = snapshot.Asteroid,
                    material_mesh = snapshot.MaterialMesh,
                    local_to_world = snapshot.LocalToWorld,
                    render_bounds = snapshot.RenderBounds,
                    mesh_presenter = snapshot.MeshPresenter,
                    flagship_count = snapshot.FlagshipCount,
                    flagship_renderable = snapshot.FlagshipRenderable,
                    peak_carrier = _peak.Carrier,
                    peak_miner = _peak.Miner,
                    peak_asteroid = _peak.Asteroid,
                    peak_material_mesh = _peak.MaterialMesh,
                    drop_carrier_from_prev = Math.Max(0, _previous.Carrier - snapshot.Carrier),
                    drop_miner_from_prev = Math.Max(0, _previous.Miner - snapshot.Miner),
                    drop_asteroid_from_prev = Math.Max(0, _previous.Asteroid - snapshot.Asteroid),
                    drop_material_mesh_from_prev = Math.Max(0, _previous.MaterialMesh - snapshot.MaterialMesh),
                    drop_carrier_from_peak = Math.Max(0, _peak.Carrier - snapshot.Carrier),
                    drop_miner_from_peak = Math.Max(0, _peak.Miner - snapshot.Miner),
                    drop_asteroid_from_peak = Math.Max(0, _peak.Asteroid - snapshot.Asteroid),
                    drop_material_mesh_from_peak = Math.Max(0, _peak.MaterialMesh - snapshot.MaterialMesh)
                });
            }

            _previous = snapshot;
        }

        private void EmitEvent(string eventName, string hint, in Snapshot snapshot, in Snapshot previous, in Snapshot peak)
        {
            var record = new ProbeRecord
            {
                timestamp_utc = DateTime.UtcNow.ToString("o"),
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                kind = "event",
                event_name = eventName,
                hint = hint,
                carrier = snapshot.Carrier,
                miner = snapshot.Miner,
                asteroid = snapshot.Asteroid,
                material_mesh = snapshot.MaterialMesh,
                local_to_world = snapshot.LocalToWorld,
                render_bounds = snapshot.RenderBounds,
                mesh_presenter = snapshot.MeshPresenter,
                flagship_count = snapshot.FlagshipCount,
                flagship_renderable = snapshot.FlagshipRenderable,
                peak_carrier = peak.Carrier,
                peak_miner = peak.Miner,
                peak_asteroid = peak.Asteroid,
                peak_material_mesh = peak.MaterialMesh,
                drop_carrier_from_prev = Math.Max(0, previous.Carrier - snapshot.Carrier),
                drop_miner_from_prev = Math.Max(0, previous.Miner - snapshot.Miner),
                drop_asteroid_from_prev = Math.Max(0, previous.Asteroid - snapshot.Asteroid),
                drop_material_mesh_from_prev = Math.Max(0, previous.MaterialMesh - snapshot.MaterialMesh),
                drop_carrier_from_peak = Math.Max(0, peak.Carrier - snapshot.Carrier),
                drop_miner_from_peak = Math.Max(0, peak.Miner - snapshot.Miner),
                drop_asteroid_from_peak = Math.Max(0, peak.Asteroid - snapshot.Asteroid),
                drop_material_mesh_from_peak = Math.Max(0, peak.MaterialMesh - snapshot.MaterialMesh)
            };

            WriteRecord(record);
            if (echoEventsToUnityLog)
            {
                Debug.LogWarning(
                    $"[Space4XEntityVisibilityProbe] event={eventName} hint='{hint}' carrier={snapshot.Carrier} miner={snapshot.Miner} asteroid={snapshot.Asteroid} materialMesh={snapshot.MaterialMesh} meshPresenter={snapshot.MeshPresenter} localToWorld={snapshot.LocalToWorld} flagshipCount={snapshot.FlagshipCount} flagshipRenderable={snapshot.FlagshipRenderable}");
            }
        }

        private static Snapshot MaxSnapshot(in Snapshot a, in Snapshot b)
        {
            return new Snapshot
            {
                Carrier = Math.Max(a.Carrier, b.Carrier),
                Miner = Math.Max(a.Miner, b.Miner),
                Asteroid = Math.Max(a.Asteroid, b.Asteroid),
                MaterialMesh = Math.Max(a.MaterialMesh, b.MaterialMesh),
                LocalToWorld = Math.Max(a.LocalToWorld, b.LocalToWorld),
                RenderBounds = Math.Max(a.RenderBounds, b.RenderBounds),
                MeshPresenter = Math.Max(a.MeshPresenter, b.MeshPresenter),
                FlagshipCount = Math.Max(a.FlagshipCount, b.FlagshipCount),
                FlagshipRenderable = Math.Max(a.FlagshipRenderable, b.FlagshipRenderable)
            };
        }

        private bool TryResolveDropEvent(in Snapshot current, in Snapshot previous, in Snapshot peak, out string eventName, out string hint)
        {
            eventName = string.Empty;
            hint = string.Empty;

            if (IsDropEvent(previous.Carrier, current.Carrier, peak.Carrier))
            {
                eventName = "carrier_drop";
                hint = BuildDropHint("Carrier", previous.Carrier, current.Carrier, peak.Carrier, current);
                return true;
            }

            if (IsDropEvent(previous.Miner, current.Miner, peak.Miner))
            {
                eventName = "miner_drop";
                hint = BuildDropHint("MiningVessel", previous.Miner, current.Miner, peak.Miner, current);
                return true;
            }

            if (IsDropEvent(previous.Asteroid, current.Asteroid, peak.Asteroid))
            {
                eventName = "asteroid_drop";
                hint = BuildDropHint("Asteroid", previous.Asteroid, current.Asteroid, peak.Asteroid, current);
                return true;
            }

            if (IsDropEvent(previous.MaterialMesh, current.MaterialMesh, peak.MaterialMesh))
            {
                eventName = "renderable_drop";
                hint = BuildDropHint("MaterialMeshInfo", previous.MaterialMesh, current.MaterialMesh, peak.MaterialMesh, current);
                return true;
            }

            return false;
        }

        private bool IsDropEvent(int previous, int current, int peak)
        {
            if (current >= previous)
                return false;

            var dropFromPrev = previous - current;
            var dropFromPeak = Math.Max(0, peak - current);
            var fractionDrop = peak > 0 ? (float)dropFromPeak / peak : 0f;

            if (dropFromPrev >= Math.Max(1, dropThresholdAbsolute))
                return true;

            return dropFromPeak >= 1 && fractionDrop >= Mathf.Clamp01(dropThresholdFractionFromPeak);
        }

        private static string BuildDropHint(string label, int previous, int current, int peak, in Snapshot snapshot)
        {
            var dropPrev = Math.Max(0, previous - current);
            var dropPeak = Math.Max(0, peak - current);

            var structuralHint = "entity_lifecycle_or_despawn";
            if (snapshot.MaterialMesh < snapshot.LocalToWorld)
            {
                structuralHint = "render_components_missing_or_not_applied";
            }
            else if (snapshot.MeshPresenter < snapshot.MaterialMesh)
            {
                structuralHint = "mesh_presenter_disabled_or_missing";
            }

            return $"{label} drop prev={dropPrev} peakDrop={dropPeak} hint={structuralHint}";
        }

        private static string BuildFlagshipHint(in Snapshot snapshot)
        {
            if (snapshot.FlagshipCount <= 0)
                return "flagship_missing";

            if (snapshot.MaterialMesh <= 0)
                return "no_renderables_available";

            if (snapshot.MeshPresenter <= 0)
                return "mesh_presenter_missing";

            if (snapshot.LocalToWorld <= 0)
                return "local_to_world_missing";

            return "flagship_render_components_missing_or_disabled";
        }

        private bool TryBuildSnapshot(out Snapshot snapshot)
        {
            snapshot = default;

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
                return false;

            var em = world.EntityManager;
            snapshot.Carrier = Count<Carrier>(em);
            snapshot.Miner = Count<MiningVessel>(em);
            snapshot.Asteroid = Count<Asteroid>(em);
            snapshot.MaterialMesh = Count<MaterialMeshInfo>(em);
            snapshot.LocalToWorld = Count<LocalToWorld>(em);
            snapshot.RenderBounds = Count<RenderBounds>(em);
            snapshot.MeshPresenter = Count<MeshPresenter>(em);

            using var flagshipQuery = em.CreateEntityQuery(ComponentType.ReadOnly<PlayerFlagshipTag>());
            snapshot.FlagshipCount = flagshipQuery.CalculateEntityCount();
            snapshot.FlagshipRenderable = 0;
            if (snapshot.FlagshipCount > 0)
            {
                using var entities = flagshipQuery.ToEntityArray(Allocator.Temp);
                var flagship = entities[0];
                if (em.Exists(flagship))
                {
                    var hasMeshInfo = em.HasComponent<MaterialMeshInfo>(flagship);
                    var hasPresenter = em.HasComponent<MeshPresenter>(flagship);
                    var hasLocalToWorld = em.HasComponent<LocalToWorld>(flagship);
                    var presenterEnabled = hasPresenter && em.IsComponentEnabled<MeshPresenter>(flagship);
                    snapshot.FlagshipRenderable = hasMeshInfo && hasLocalToWorld && presenterEnabled ? 1 : 0;
                }
            }

            return true;
        }

        private static int Count<T>(EntityManager em) where T : unmanaged, IComponentData
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.CalculateEntityCount();
        }

        private void WriteRecord(ProbeRecord record)
        {
            var jsonLine = JsonUtility.ToJson(record);
            if (!TryAppendLine(_outputPath, jsonLine))
                return;
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

                File.AppendAllText(path, line + Environment.NewLine);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Space4XEntityVisibilityProbe] write failed: {ex.Message}");
                return false;
            }
        }

        private static string ResolveOutputPath()
        {
            var env = Environment.GetEnvironmentVariable(ProbeOutputEnv);
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
            var env = Environment.GetEnvironmentVariable(ProbeEnabledEnv);
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
    }
}
#endif
