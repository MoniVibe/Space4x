using System.Collections.Generic;
using System.Text;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Streaming;
using PureDOTS.Runtime.Telemetry;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace PureDOTS.Debugging
{
    /// <summary>
    /// Opt-in MonoBehaviour bridge that reads DebugDisplayData singleton and updates Unity UI.
    /// Attach this to a Canvas GameObject in playmode builds for runtime debug visualization.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    [DisallowMultipleComponent]
    public sealed class DebugDisplayReader : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Text component displaying time state (optional)")]
        public Text timeStateText;
        
        [Tooltip("Text component displaying rewind state (optional)")]
        public Text rewindStateText;

        [Tooltip("Text component displaying tick/command log summary (optional)")]
        public Text tickLogText;
        
        [Tooltip("Text component displaying villager count (optional)")]
        public Text villagerCountText;
        
        [Tooltip("Text component displaying resource totals (optional)")]
        public Text resourceTotalText;

        [Tooltip("Text component displaying registry summary (optional)")]
        public Text registryStateText;

        [Tooltip("Text component displaying registry health headline (optional)")]
        public Text registryHealthHeadlineText;

        [Tooltip("Text component displaying registry health alerts (optional)")]
        public Text registryHealthAlertText;

        [Tooltip("Text component displaying spatial grid state (optional)")]
        public Text spatialStateText;

        [Tooltip("Text component displaying streaming summary (optional)")]
        public Text streamingStateText;

        [Tooltip("Text component displaying pooling summary (optional)")]
        public Text poolingStateText;

        [Tooltip("Text component displaying sunlight diagnostics (optional)")]
        public Text sunlightStateText;

        [Tooltip("Text component displaying frame timing summary (optional)")]
        public Text frameTimingText;

        [Tooltip("Text component displaying allocation diagnostics (optional)")]
        public Text allocationStateText;

        [Tooltip("Text component displaying replay capture summary (optional)")]
        public Text replayStateText;

        [Header("Telemetry")]
        [Tooltip("Capture ECS telemetry metrics and expose them through this bridge (optional)")]
        public bool captureTelemetry = true;

        [Tooltip("Text component displaying condensed telemetry metrics (optional)")]
        public Text telemetrySummaryText;

        [Header("Update Settings")]
        [Tooltip("Update frequency in seconds (0 = every frame)")]
        public float updateInterval = 0.1f;

        [Tooltip("Start with HUD visible")]
        public bool startVisible = true;

        private World _world;
        private float _lastUpdateTime;
        private Canvas _canvas;
        private EntityQuery _commandQuery;
        private EntityQuery _debugDataQuery;
        private EntityQuery _telemetryQuery;
        private EntityQuery _streamingCoordinatorQuery;
        private EntityQuery _presentationQueueQuery;
        private bool _hasCommandQuery;
        private bool _hasDebugDataQuery;
        private bool _hasTelemetryQuery;
        private bool _hasStreamingCoordinatorQuery;
        private bool _hasPresentationQueueQuery;
        private readonly List<TelemetryMetricSnapshot> _telemetrySnapshots = new List<TelemetryMetricSnapshot>(16);
        private uint _telemetryVersion;
        private StringBuilder _telemetryBuilder;

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            InitializeWorld();
        }

        private void OnEnable()
        {
            // Reinitialize on world reload
            InitializeWorld();
        }

        public IReadOnlyList<TelemetryMetricSnapshot> LatestTelemetry => _telemetrySnapshots;

        private void InitializeWorld()
        {
            var newWorld = World.DefaultGameObjectInjectionWorld;
            
            if (newWorld == null || !newWorld.IsCreated)
            {
                if (_world != null)
                {
                    Debug.LogWarning("DebugDisplayReader lost connection to DefaultGameObjectInjectionWorld.", this);
                }
                if (_hasCommandQuery)
                {
                    _commandQuery.Dispose();
                    _hasCommandQuery = false;
                }
                if (_hasDebugDataQuery)
                {
                    _debugDataQuery.Dispose();
                    _hasDebugDataQuery = false;
                }
                if (_hasTelemetryQuery)
                {
                    _telemetryQuery.Dispose();
                    _hasTelemetryQuery = false;
                }
                if (_hasStreamingCoordinatorQuery)
                {
                    _streamingCoordinatorQuery.Dispose();
                    _hasStreamingCoordinatorQuery = false;
                }
                if (_hasPresentationQueueQuery)
                {
                    _presentationQueueQuery.Dispose();
                    _hasPresentationQueueQuery = false;
                }
                _world = null;
                enabled = false;
                return;
            }

            // Dispose old queries if world changed
            if (_world != null && _world != newWorld)
            {
                if (_hasCommandQuery)
                {
                    _commandQuery.Dispose();
                    _hasCommandQuery = false;
                }
                if (_hasDebugDataQuery)
                {
                    _debugDataQuery.Dispose();
                    _hasDebugDataQuery = false;
                }
                if (_hasTelemetryQuery)
                {
                    _telemetryQuery.Dispose();
                    _hasTelemetryQuery = false;
                }
                if (_hasStreamingCoordinatorQuery)
                {
                    _streamingCoordinatorQuery.Dispose();
                    _hasStreamingCoordinatorQuery = false;
                }
                if (_hasPresentationQueueQuery)
                {
                    _presentationQueueQuery.Dispose();
                    _hasPresentationQueueQuery = false;
                }
            }

            _world = newWorld;

            // Cache queries for new world
            var entityManager = _world.EntityManager;
            _commandQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<DebugCommandSingletonTag>());
            _debugDataQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<DebugDisplayData>());
            _telemetryQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TelemetryStream>());
            _streamingCoordinatorQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<StreamingCoordinator>());
            _presentationQueueQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PresentationCommandQueue>());
            _hasCommandQuery = true;
            _hasDebugDataQuery = true;
            _hasTelemetryQuery = true;
            _hasStreamingCoordinatorQuery = true;
            _hasPresentationQueueQuery = true;
            _telemetrySnapshots.Clear();
            _telemetryVersion = 0;

            // Set initial visibility
            if (_canvas != null)
            {
                _canvas.enabled = startVisible;
            }
        }

        private void OnDestroy()
        {
            // Dispose cached queries (EntityQuery is a struct, check IsCreated)
            if (_hasCommandQuery)
            {
                _commandQuery.Dispose();
                _hasCommandQuery = false;
            }
            if (_hasDebugDataQuery)
            {
                _debugDataQuery.Dispose();
                _hasDebugDataQuery = false;
            }
            if (_hasTelemetryQuery)
            {
                _telemetryQuery.Dispose();
                _hasTelemetryQuery = false;
            }
            if (_hasStreamingCoordinatorQuery)
            {
                _streamingCoordinatorQuery.Dispose();
                _hasStreamingCoordinatorQuery = false;
            }
            if (_hasPresentationQueueQuery)
            {
                _presentationQueueQuery.Dispose();
                _hasPresentationQueueQuery = false;
            }
        }

        private void Update()
        {
            if (_world == null || !_world.IsCreated)
            {
                return;
            }

            // Throttle updates for performance
            if (updateInterval > 0f && Time.time - _lastUpdateTime < updateInterval)
            {
                return;
            }

            _lastUpdateTime = Time.time;

            // Check for debug commands
            ProcessDebugCommands();

            // Update UI from ECS singleton
            UpdateUI();
            UpdateTelemetrySnapshot();
        }

        private void ProcessDebugCommands()
        {
            if (_commandQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var entity = _commandQuery.GetSingletonEntity();
            var entityManager = _world.EntityManager;
            if (!entityManager.HasBuffer<DebugCommand>(entity))
            {
                return;
            }

            var commands = entityManager.GetBuffer<DebugCommand>(entity);
            
            for (int i = 0; i < commands.Length; i++)
            {
                var cmd = commands[i];
                switch (cmd.Type)
                {
                    case DebugCommand.CommandType.ToggleHUD:
                        if (_canvas != null)
                        {
                            _canvas.enabled = !_canvas.enabled;
                        }
                        break;
                    case DebugCommand.CommandType.ShowHUD:
                        if (_canvas != null)
                        {
                            _canvas.enabled = true;
                        }
                        break;
                    case DebugCommand.CommandType.HideHUD:
                        if (_canvas != null)
                        {
                            _canvas.enabled = false;
                        }
                        break;
                    case DebugCommand.CommandType.ClearStreamingCooldowns:
                        IssueClearStreamingCooldowns(entityManager);
                        break;
                    case DebugCommand.CommandType.ReloadPresentation:
                        IssuePresentationReload(entityManager);
                        break;
                }
            }

            // Clear processed commands
            commands.Clear();
        }

        private void IssueClearStreamingCooldowns(EntityManager entityManager)
        {
            if (!_hasStreamingCoordinatorQuery || _streamingCoordinatorQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var coordinator = _streamingCoordinatorQuery.GetSingletonEntity();
            if (!entityManager.HasComponent<StreamingDebugControl>(coordinator))
            {
                return;
            }

            var control = entityManager.GetComponentData<StreamingDebugControl>(coordinator);
            control.ClearCooldowns = true;
            entityManager.SetComponentData(coordinator, control);
        }

        private void IssuePresentationReload(EntityManager entityManager)
        {
            if (!_hasPresentationQueueQuery || _presentationQueueQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var queueEntity = _presentationQueueQuery.GetSingletonEntity();
            if (!entityManager.HasComponent<PresentationReloadCommand>(queueEntity))
            {
                entityManager.AddComponentData(queueEntity, new PresentationReloadCommand { RequestId = 1 });
            }
            else
            {
                var cmd = entityManager.GetComponentData<PresentationReloadCommand>(queueEntity);
                cmd.RequestId++;
                entityManager.SetComponentData(queueEntity, cmd);
            }
        }

        private void UpdateUI()
        {
            if (_debugDataQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var debugData = _debugDataQuery.GetSingleton<DebugDisplayData>();

            // Update UI elements if assigned
            if (timeStateText != null)
            {
                timeStateText.text = debugData.TimeStateText.ToString();
            }

            if (rewindStateText != null)
            {
                rewindStateText.text = debugData.RewindStateText.ToString();
            }

            if (tickLogText != null)
            {
                tickLogText.text = debugData.TickLogText.ToString();
            }

            if (villagerCountText != null)
            {
                villagerCountText.text = $"Villagers: {debugData.VillagerCount}";
            }

            if (resourceTotalText != null)
            {
                resourceTotalText.text = $"Resources: {debugData.TotalResourcesStored:F1}";
            }

            if (registryStateText != null)
            {
                registryStateText.text = debugData.RegistryStateText.ToString();
            }

            if (registryHealthHeadlineText != null)
            {
                registryHealthHeadlineText.text = debugData.RegistryHealthHeadline.ToString();
            }

            if (registryHealthAlertText != null)
            {
                registryHealthAlertText.text = debugData.RegistryHealthAlerts.ToString();
            }

            if (spatialStateText != null)
            {
                spatialStateText.text = debugData.SpatialStateText.ToString();
            }

            if (poolingStateText != null)
            {
                poolingStateText.text = debugData.PoolingStateText.ToString();
            }

            if (sunlightStateText != null)
            {
                sunlightStateText.text = debugData.SunlightStateText.ToString();
            }

            if (streamingStateText != null)
            {
                streamingStateText.text = debugData.StreamingStateText.ToString();
            }

            if (frameTimingText != null)
            {
                frameTimingText.text = debugData.FrameTimingText.ToString();
            }

            if (allocationStateText != null)
            {
                allocationStateText.text = debugData.AllocationStateText.ToString();
            }

            if (replayStateText != null)
            {
                replayStateText.text = debugData.ReplayStateText.ToString();
            }
        }

        private void UpdateTelemetrySnapshot()
        {
            if (!captureTelemetry)
            {
                if (_telemetrySnapshots.Count > 0)
                {
                    _telemetrySnapshots.Clear();
                }

                _telemetryVersion = 0;

                if (telemetrySummaryText != null && !string.IsNullOrEmpty(telemetrySummaryText.text))
                {
                    telemetrySummaryText.text = string.Empty;
                }

                return;
            }

            if (!_hasTelemetryQuery || _world == null || !_world.IsCreated)
            {
                return;
            }

            var entityManager = _world.EntityManager;

            if (_telemetryQuery.IsEmptyIgnoreFilter)
            {
                if (_telemetrySnapshots.Count > 0)
                {
                    _telemetrySnapshots.Clear();
                    _telemetryVersion = 0;
                }

                if (telemetrySummaryText != null)
                {
                    telemetrySummaryText.text = string.Empty;
                }

                return;
            }

            var telemetryEntity = _telemetryQuery.GetSingletonEntity();
            if (!entityManager.HasComponent<TelemetryStream>(telemetryEntity))
            {
                return;
            }

            var stream = entityManager.GetComponentData<TelemetryStream>(telemetryEntity);
            if (_telemetryVersion == stream.Version)
            {
                return;
            }

            _telemetryVersion = stream.Version;
            _telemetrySnapshots.Clear();

            if (!entityManager.HasBuffer<TelemetryMetric>(telemetryEntity))
            {
                if (telemetrySummaryText != null)
                {
                    telemetrySummaryText.text = string.Empty;
                }

                return;
            }

            var buffer = entityManager.GetBuffer<TelemetryMetric>(telemetryEntity);
            for (int i = 0; i < buffer.Length; i++)
            {
                var metric = buffer[i];
                _telemetrySnapshots.Add(new TelemetryMetricSnapshot(metric.Key.ToString(), metric.Value, metric.Unit));
            }

            if (telemetrySummaryText != null)
            {
                if (_telemetryBuilder == null)
                {
                    _telemetryBuilder = new StringBuilder(128);
                }
                else
                {
                    _telemetryBuilder.Clear();
                }

                for (int i = 0; i < _telemetrySnapshots.Count; i++)
                {
                    if (i > 0)
                    {
                        _telemetryBuilder.Append(" | ");
                    }

                    var snapshot = _telemetrySnapshots[i];
                    _telemetryBuilder.Append(snapshot.Key);
                    _telemetryBuilder.Append(": ");
                    _telemetryBuilder.Append(snapshot.Value.ToString("0.##"));
                }

                telemetrySummaryText.text = _telemetryBuilder.ToString();
            }
        }

        public readonly struct TelemetryMetricSnapshot
        {
            public readonly string Key;
            public readonly float Value;
            public readonly TelemetryMetricUnit Unit;

            public TelemetryMetricSnapshot(string key, float value, TelemetryMetricUnit unit)
            {
                Key = key;
                Value = value;
                Unit = unit;
            }
        }

        /// <summary>
        /// Public API for toggling HUD visibility from other MonoBehaviour scripts.
        /// </summary>
        public void ToggleHUD()
        {
            if (_canvas != null)
            {
                _canvas.enabled = !_canvas.enabled;
            }
        }

        /// <summary>
        /// Public API for showing HUD.
        /// </summary>
        public void ShowHUD()
        {
            if (_canvas != null)
            {
                _canvas.enabled = true;
            }
        }

        /// <summary>
        /// Public API for hiding HUD.
        /// </summary>
        public void HideHUD()
        {
            if (_canvas != null)
            {
                _canvas.enabled = false;
            }
        }
    }
}
