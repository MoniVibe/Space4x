using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Presentation
{
    /// <summary>
    /// MonoBehaviour that displays on-screen debug panel with presentation metrics and controls.
    /// </summary>
    public class Space4XDebugPanel : MonoBehaviour
    {
        [Header("Display Settings")]
        [Tooltip("Show debug panel")]
        public bool ShowPanel = true;

        [Tooltip("Panel position (normalized 0-1)")]
        public Vector2 PanelPosition = new Vector2(10f, 10f);

        [Tooltip("Panel size")]
        public Vector2 PanelSize = new Vector2(300f, 400f);

        [Header("Display Options")]
        [Tooltip("Show entity counts")]
        public bool ShowEntityCounts = true;

        [Tooltip("Show LOD distribution")]
        public bool ShowLODDistribution = true;

        [Tooltip("Show performance metrics")]
        public bool ShowPerformanceMetrics = true;

        [Tooltip("Show selected entity info")]
        public bool ShowSelectedEntityInfo = true;

        private World _world;
        private EntityQuery _metricsQuery;
        private EntityQuery _selectionStateQuery;
        private EntityQuery _lodConfigQuery;
        private EntityQuery _densityConfigQuery;
        private Space4XPresentationMetricsLogger _metricsLogger;
        private Space4XScenarioLoader _scenarioLoader;
        private bool _initialized;

        private void Awake()
        {
            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null)
            {
                enabled = false;
                return;
            }

            _metricsQuery = _world.EntityManager.CreateEntityQuery(typeof(PresentationMetrics));
            _selectionStateQuery = _world.EntityManager.CreateEntityQuery(typeof(SelectionState));
            _lodConfigQuery = _world.EntityManager.CreateEntityQuery(typeof(PresentationLODConfig));
            _densityConfigQuery = _world.EntityManager.CreateEntityQuery(typeof(RenderDensityConfig));

            // Find metrics logger and scenario loader
            _metricsLogger = FindFirstObjectByType<Space4XPresentationMetricsLogger>();
            _scenarioLoader = FindFirstObjectByType<Space4XScenarioLoader>();
        }

        private void OnGUI()
        {
            if (!ShowPanel || _world == null || !_world.IsCreated)
            {
                return;
            }

            if (!_initialized)
            {
                _initialized = true;
            }

            // Create debug panel window
            Rect panelRect = new Rect(PanelPosition.x, PanelPosition.y, PanelSize.x, PanelSize.y);
            GUILayout.Window(12345, panelRect, DrawDebugPanel, "Space4X Debug Panel");
        }

        private void DrawDebugPanel(int windowID)
        {
            GUILayout.BeginVertical();

            // Scenario name
            if (_scenarioLoader != null)
            {
                GUILayout.Label($"=== Scenario: {_scenarioLoader.GetScenarioName()} ===", GUI.skin.box);
            }

            // Entity counts
            if (ShowEntityCounts)
            {
                GUILayout.Label("=== Entity Counts ===", GUI.skin.box);
                DrawEntityCounts();
            }

            // LOD distribution
            if (ShowLODDistribution)
            {
                GUILayout.Label("=== LOD Distribution ===", GUI.skin.box);
                DrawLODDistribution();
            }

            // Performance metrics
            if (ShowPerformanceMetrics)
            {
                GUILayout.Label("=== Performance ===", GUI.skin.box);
                DrawPerformanceMetrics();
            }

            // Selected entity info
            if (ShowSelectedEntityInfo)
            {
                GUILayout.Label("=== Selection ===", GUI.skin.box);
                DrawSelectedEntityInfo();
            }

            // Debug mode toggles
            GUILayout.Label("=== Debug Modes ===", GUI.skin.box);
            DrawDebugModeToggles();

            // Render density control
            GUILayout.Label("=== Render Density ===", GUI.skin.box);
            DrawRenderDensityControl();

            // LOD threshold controls
            GUILayout.Label("=== LOD Thresholds ===", GUI.skin.box);
            DrawLODThresholdControls();

            // Metrics logging control
            GUILayout.Label("=== Metrics Logging ===", GUI.skin.box);
            DrawMetricsLoggingControl();

            GUILayout.EndVertical();

            // Make window draggable
            GUI.DragWindow();
        }

        private void DrawEntityCounts()
        {
            if (_metricsQuery.IsEmptyIgnoreFilter)
            {
                GUILayout.Label("No metrics available");
                return;
            }

            var metrics = _metricsQuery.GetSingleton<PresentationMetrics>();

            GUILayout.Label($"Total Presentation Entities: {metrics.TotalPresentationEntities}");
            GUILayout.Label($"Visible Entities: {metrics.VisibleEntities}");
            GUILayout.Label($"Carriers: {metrics.VisibleCarriers}");
            GUILayout.Label($"Crafts: {metrics.VisibleCrafts}");
            GUILayout.Label($"Asteroids: {metrics.VisibleAsteroids}");
        }

        private void DrawLODDistribution()
        {
            if (_metricsQuery.IsEmptyIgnoreFilter)
            {
                GUILayout.Label("No metrics available");
                return;
            }

            var metrics = _metricsQuery.GetSingleton<PresentationMetrics>();

            GUILayout.Label($"FullDetail: {metrics.FullDetailCount}");
            GUILayout.Label($"ReducedDetail: {metrics.ReducedDetailCount}");
            GUILayout.Label($"Impostor: {metrics.ImpostorCount}");
            GUILayout.Label($"Hidden: {metrics.HiddenCount}");

            if (_lodConfigQuery.IsEmptyIgnoreFilter == false)
            {
                var lodConfig = _lodConfigQuery.GetSingleton<PresentationLODConfig>();
                GUILayout.Label($"LOD Thresholds:");
                GUILayout.Label($"  FullDetail: 0-{lodConfig.FullDetailMaxDistance:F0}");
                GUILayout.Label($"  ReducedDetail: {lodConfig.FullDetailMaxDistance:F0}-{lodConfig.ReducedDetailMaxDistance:F0}");
                GUILayout.Label($"  Impostor: {lodConfig.ReducedDetailMaxDistance:F0}-{lodConfig.ImpostorMaxDistance:F0}");
            }
        }

        private void DrawPerformanceMetrics()
        {
            if (_metricsQuery.IsEmptyIgnoreFilter)
            {
                GUILayout.Label("No metrics available");
                return;
            }

            var metrics = _metricsQuery.GetSingleton<PresentationMetrics>();

            GUILayout.Label($"Frame Time: {metrics.PresentationFrameTimeMs:F2} ms");
            GUILayout.Label($"Render Density: {metrics.CurrentRenderDensity:P0}");

            if (_densityConfigQuery.IsEmptyIgnoreFilter == false)
            {
                var densityConfig = _densityConfigQuery.GetSingleton<RenderDensityConfig>();
                GUILayout.Label($"Auto-Adjust: {densityConfig.AutoAdjust}");
            }
        }

        private void DrawSelectedEntityInfo()
        {
            if (_selectionStateQuery.IsEmptyIgnoreFilter)
            {
                GUILayout.Label("No selection state");
                return;
            }

            var selectionState = _selectionStateQuery.GetSingleton<SelectionState>();

            GUILayout.Label($"Selected Count: {selectionState.SelectedCount}");
            GUILayout.Label($"Selection Type: {selectionState.Type}");

            if (selectionState.PrimarySelected != Entity.Null)
            {
                GUILayout.Label($"Primary: Entity {selectionState.PrimarySelected.Index}");
            }
        }

        private void DrawDebugModeToggles()
        {
            // Get or create debug overlay config
            var debugOverlayQuery = _world.EntityManager.CreateEntityQuery(typeof(DebugOverlayConfig));
            if (debugOverlayQuery.IsEmptyIgnoreFilter)
            {
                var entity = _world.EntityManager.CreateEntity();
                _world.EntityManager.AddComponentData(entity, new DebugOverlayConfig());
            }

            var debugConfig = debugOverlayQuery.GetSingleton<DebugOverlayConfig>();

            bool showLOD = GUILayout.Toggle(debugConfig.ShowLODVisualization, "Show LOD Colors");
            bool showFleetCentroids = GUILayout.Toggle(debugConfig.ShowFactionZones, "Show Fleet Centroids");
            bool showResourceFields = GUILayout.Toggle(debugConfig.ShowResourceFields, "Show Resource Fields");
            bool showMetrics = GUILayout.Toggle(debugConfig.ShowMetrics, "Show Metrics");
            bool showInspector = GUILayout.Toggle(debugConfig.ShowInspector, "Show Inspector");

            if (showLOD != debugConfig.ShowLODVisualization ||
                showFleetCentroids != debugConfig.ShowFactionZones ||
                showResourceFields != debugConfig.ShowResourceFields ||
                showMetrics != debugConfig.ShowMetrics ||
                showInspector != debugConfig.ShowInspector)
            {
                var entity = debugOverlayQuery.GetSingletonEntity();
                _world.EntityManager.SetComponentData(entity, new DebugOverlayConfig
                {
                    ShowResourceFields = showResourceFields,
                    ShowFactionZones = showFleetCentroids,
                    ShowDebugPaths = debugConfig.ShowDebugPaths,
                    ShowLODVisualization = showLOD,
                    ShowMetrics = showMetrics,
                    ShowInspector = showInspector
                });
            }

            // Sim freeze toggle
            var timeStateQuery = _world.EntityManager.CreateEntityQuery(typeof(PureDOTS.Runtime.Components.TimeState));
            if (!timeStateQuery.IsEmptyIgnoreFilter)
            {
                GUILayout.Space(10f);
                if (GUILayout.Button("Freeze Sim"))
                {
                    // Toggle sim freeze - would need to pause TimeState
                    // This is a placeholder - actual implementation would pause the sim
                    Debug.Log("[Space4XDebugPanel] Sim freeze toggle - not yet implemented");
                }
            }
        }

        private void DrawRenderDensityControl()
        {
            if (_densityConfigQuery.IsEmptyIgnoreFilter)
            {
                GUILayout.Label("No render density config");
                return;
            }

            var densityConfig = _densityConfigQuery.GetSingleton<RenderDensityConfig>();
            float currentDensity = densityConfig.Density;

            GUILayout.Label($"Current Density: {currentDensity:P0}");
            
            // Calculate "Render 1 in N crafts"
            int renderN = currentDensity > 0.001f ? (int)(1f / currentDensity) : 999;
            GUILayout.Label($"Render 1 in {renderN} crafts");
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("-0.1"))
            {
                var newDensity = math.max(0f, currentDensity - 0.1f);
                UpdateRenderDensity(newDensity);
            }
            if (GUILayout.Button("+0.1"))
            {
                var newDensity = math.min(1f, currentDensity + 0.1f);
                UpdateRenderDensity(newDensity);
            }
            if (GUILayout.Button("0.5"))
            {
                UpdateRenderDensity(0.5f);
            }
            if (GUILayout.Button("1.0"))
            {
                UpdateRenderDensity(1f);
            }
            GUILayout.EndHorizontal();

            float newDensitySlider = GUILayout.HorizontalSlider(currentDensity, 0f, 1f);
            if (math.abs(newDensitySlider - currentDensity) > 0.01f)
            {
                UpdateRenderDensity(newDensitySlider);
            }
        }

        private void UpdateRenderDensity(float density)
        {
            if (_densityConfigQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var entity = _densityConfigQuery.GetSingletonEntity();
            var config = _world.EntityManager.GetComponentData<RenderDensityConfig>(entity);
            config.Density = density;
            _world.EntityManager.SetComponentData(entity, config);
        }

        private void DrawLODThresholdControls()
        {
            if (_lodConfigQuery.IsEmptyIgnoreFilter)
            {
                GUILayout.Label("No LOD config");
                return;
            }

            var entity = _lodConfigQuery.GetSingletonEntity();
            var lodConfig = _world.EntityManager.GetComponentData<PresentationLODConfig>(entity);

            // FullDetail Max Distance
            GUILayout.BeginHorizontal();
            GUILayout.Label($"FullDetail: 0-{lodConfig.FullDetailMaxDistance:F0}", GUILayout.Width(150));
            float newFullDetail = GUILayout.HorizontalSlider(lodConfig.FullDetailMaxDistance, 0f, 200f);
            if (math.abs(newFullDetail - lodConfig.FullDetailMaxDistance) > 0.5f)
            {
                lodConfig.FullDetailMaxDistance = newFullDetail;
                _world.EntityManager.SetComponentData(entity, lodConfig);
            }
            GUILayout.EndHorizontal();

            // ReducedDetail Max Distance
            GUILayout.BeginHorizontal();
            GUILayout.Label($"ReducedDetail: {lodConfig.FullDetailMaxDistance:F0}-{lodConfig.ReducedDetailMaxDistance:F0}", GUILayout.Width(150));
            float newReducedDetail = GUILayout.HorizontalSlider(lodConfig.ReducedDetailMaxDistance, lodConfig.FullDetailMaxDistance, 1000f);
            if (math.abs(newReducedDetail - lodConfig.ReducedDetailMaxDistance) > 0.5f)
            {
                lodConfig.ReducedDetailMaxDistance = newReducedDetail;
                _world.EntityManager.SetComponentData(entity, lodConfig);
            }
            GUILayout.EndHorizontal();

            // Impostor Max Distance
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Impostor: {lodConfig.ReducedDetailMaxDistance:F0}-{lodConfig.ImpostorMaxDistance:F0}", GUILayout.Width(150));
            float newImpostor = GUILayout.HorizontalSlider(lodConfig.ImpostorMaxDistance, lodConfig.ReducedDetailMaxDistance, 5000f);
            if (math.abs(newImpostor - lodConfig.ImpostorMaxDistance) > 0.5f)
            {
                lodConfig.ImpostorMaxDistance = newImpostor;
                _world.EntityManager.SetComponentData(entity, lodConfig);
            }
            GUILayout.EndHorizontal();

            // Reset button
            if (GUILayout.Button("Reset to Defaults"))
            {
                var defaultConfig = PresentationLODConfig.Default;
                _world.EntityManager.SetComponentData(entity, defaultConfig);
            }
        }

        private void DrawMetricsLoggingControl()
        {
            if (_metricsLogger == null)
            {
                GUILayout.Label("Metrics logger not found");
                return;
            }

            bool currentState = _metricsLogger.EnableLogging;
            bool newState = GUILayout.Toggle(currentState, "Enable Metrics Logging");

            if (newState != currentState)
            {
                _metricsLogger.EnableLogging = newState;
                if (newState)
                {
                    Debug.Log($"[Space4XDebugPanel] Metrics logging enabled. Log file: {_metricsLogger.LogFilePath}");
                }
                else
                {
                    Debug.Log("[Space4XDebugPanel] Metrics logging disabled.");
                }
            }

            if (newState)
            {
                GUILayout.Label($"Logging to: {_metricsLogger.LogFilePath}");
                GUILayout.Label($"Interval: Every {_metricsLogger.LogIntervalFrames} frames");
            }
        }
    }
}

