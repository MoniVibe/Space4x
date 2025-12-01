using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Space4X.Demo
{
    /// <summary>
    /// IMGUI debug panel for demo controls: scenario selection, time controls, bindings, determinism overlay.
    /// Press F11 to toggle.
    /// </summary>
    public class Space4XDemoUI : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private Key toggleKey = Key.F11;

        // UI State
        private bool _isOpen;
        private Rect _windowRect = new Rect(10, 10, 400, 600);
        private Vector2 _scrollPosition;
        private string _selectedScenario = "";
        private List<string> _availableScenarios = new List<string>();
        private bool _showScenarioPanel = true;
        private bool _showTimeControls = true;
        private bool _showBindingPanel = true;
        private bool _showMetrics = true;

        // Styles
        private GUIStyle _headerStyle;
        private GUIStyle _buttonStyle;
        private bool _stylesInitialized;

        private void Start()
        {
            RefreshScenarioList();
        }

        private void Update()
        {
            if (KeyPressed(toggleKey))
            {
                _isOpen = !_isOpen;
            }
        }

        private void OnGUI()
        {
            if (!_isOpen) return;

            InitializeStyles();

            _windowRect = GUILayout.Window(
                GetInstanceID(),
                _windowRect,
                DrawWindow,
                "Space4X Demo Controls",
                GUILayout.MinWidth(400),
                GUILayout.MinHeight(600));
        }

        private static bool KeyPressed(Key key)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !Enum.IsDefined(typeof(Key), key))
                return false;

            var control = keyboard[key];
            return control != null && control.wasPressedThisFrame;
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12
            };

            _stylesInitialized = true;
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.Space(5);

            // Tabs
            GUILayout.BeginHorizontal();
            _showScenarioPanel = GUILayout.Toggle(_showScenarioPanel, "Scenarios", _buttonStyle);
            _showTimeControls = GUILayout.Toggle(_showTimeControls, "Time", _buttonStyle);
            _showBindingPanel = GUILayout.Toggle(_showBindingPanel, "Bindings", _buttonStyle);
            _showMetrics = GUILayout.Toggle(_showMetrics, "Metrics", _buttonStyle);
            if (GUILayout.Button("X", _buttonStyle, GUILayout.Width(30)))
            {
                _isOpen = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            if (_showScenarioPanel)
            {
                DrawScenarioPanel();
            }

            if (_showTimeControls)
            {
                DrawTimeControls();
            }

            if (_showBindingPanel)
            {
                DrawBindingPanel();
            }

            if (_showMetrics)
            {
                DrawMetricsPanel();
            }

            GUILayout.EndScrollView();

            // Make window draggable
            GUI.DragWindow(new Rect(0, 0, _windowRect.width, 20));
        }

        private void DrawScenarioPanel()
        {
            GUILayout.Label("Scenario Selection", _headerStyle);
            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", _buttonStyle))
            {
                RefreshScenarioList();
            }
            GUILayout.Label($"Found: {_availableScenarios.Count} scenarios");
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            foreach (var scenario in _availableScenarios)
            {
                bool isSelected = _selectedScenario == scenario;
                GUI.color = isSelected ? Color.green : Color.white;

                if (GUILayout.Button(Path.GetFileName(scenario), _buttonStyle))
                {
                    _selectedScenario = scenario;
                    LoadScenario(scenario);
                }

                GUI.color = Color.white;
            }
        }

        private void DrawTimeControls()
        {
            GUILayout.Label("Time Controls", _headerStyle);
            GUILayout.Space(10);

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                GUILayout.Label("No ECS World active", new GUIStyle(GUI.skin.label) { normal = { textColor = Color.red } });
                return;
            }

            var em = world.EntityManager;
            DemoBootstrapState? demoState = null;
            
            // Query for DemoBootstrapState singleton
            var query = em.CreateEntityQuery(typeof(DemoBootstrapState));
            if (query.CalculateEntityCount() > 0)
            {
                demoState = query.GetSingleton<DemoBootstrapState>();
            }
            query.Dispose();

            // Pause/Play
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("P - Pause/Play", _buttonStyle))
            {
                TogglePause(em);
            }
            GUILayout.EndHorizontal();

            // Step controls
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("[ - Step Back", _buttonStyle))
            {
                // TODO: Implement step back
            }
            if (GUILayout.Button("] - Step Forward", _buttonStyle))
            {
                // TODO: Implement step forward
            }
            GUILayout.EndHorizontal();

            // Speed controls
            GUILayout.BeginHorizontal();
            GUILayout.Label("Speed:");
            if (GUILayout.Button("0.5x", _buttonStyle))
            {
                SetTimeScale(em, 0.5f);
            }
            if (GUILayout.Button("1x", _buttonStyle))
            {
                SetTimeScale(em, 1.0f);
            }
            if (GUILayout.Button("2x", _buttonStyle))
            {
                SetTimeScale(em, 2.0f);
            }
            GUILayout.EndHorizontal();

            // Rewind toggle
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("R - Toggle Rewind", _buttonStyle))
            {
                ToggleRewind(em);
            }
            GUILayout.EndHorizontal();

            // Display current state
            if (demoState.HasValue)
            {
                var state = demoState.Value;
                GUILayout.Space(5);
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"Paused: {state.Paused == 1}");
                GUILayout.Label($"Time Scale: {state.TimeScale:F1}x");
                GUILayout.Label($"Rewind: {state.RewindEnabled == 1}");
                GUILayout.Label($"RNG Seed: {state.RngSeed}");
                GUILayout.EndVertical();
            }
        }

        private void DrawBindingPanel()
        {
            GUILayout.Label("Presentation Bindings", _headerStyle);
            GUILayout.Space(10);

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var em = world.EntityManager;
            DemoOptions? options = null;
            
            var query = em.CreateEntityQuery(typeof(DemoOptions));
            if (query.CalculateEntityCount() > 0)
            {
                options = query.GetSingleton<DemoOptions>();
            }
            query.Dispose();

            if (options.HasValue)
            {
                string currentBinding = options.Value.BindingsSet == 1 ? "Fancy" : "Minimal";
                GUILayout.Label($"Current: {currentBinding}");

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("B - Swap Bindings", _buttonStyle))
                {
                    SwapBindings(em);
                }
                GUILayout.EndHorizontal();
            }
        }

        private void DrawMetricsPanel()
        {
            GUILayout.Label("Determinism Overlay", _headerStyle);
            GUILayout.Space(10);

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var em = world.EntityManager;

            // System metrics
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("System Metrics", EditorStyles.boldLabel);
            GUILayout.Label($"Tick: {GetCurrentTick(em)}");
            GUILayout.Label($"FPS: {1.0f / Time.deltaTime:F1}");
            GUILayout.Label($"Fixed Tick MS: {GetFixedTickMs():F2}");
            GUILayout.Label($"Snapshot KB: {GetSnapshotKb():F2}");
            GUILayout.EndVertical();

            GUILayout.Space(10);

            // Telemetry metrics
            var telemetryQuery = em.CreateEntityQuery(typeof(TelemetryStream));
            if (telemetryQuery.CalculateEntityCount() > 0)
            {
                var telemetryEntity = telemetryQuery.GetSingletonEntity();
                if (em.HasBuffer<TelemetryMetric>(telemetryEntity))
                {
                    var telemetryStream = em.GetComponentData<TelemetryStream>(telemetryEntity);
                    var metrics = em.GetBuffer<TelemetryMetric>(telemetryEntity);
                    GUILayout.BeginVertical(GUI.skin.box);
                    GUILayout.Label("Telemetry Metrics", EditorStyles.boldLabel);
                    GUILayout.Label($"Last Tick: {telemetryStream.LastTick}");

                    // Show last 10 metrics
                    int start = math.max(0, metrics.Length - 10);
                    for (int i = start; i < metrics.Length; i++)
                    {
                        var metric = metrics[i];
                        GUILayout.Label($"{metric.Key}: {metric.Value:F2} [{metric.Unit}]");
                    }

                    GUILayout.EndVertical();
                }
            }
            telemetryQuery.Dispose();
        }

        private void RefreshScenarioList()
        {
            _availableScenarios.Clear();
            string scenariosPath = Path.Combine(Application.dataPath, "Scenarios");
            
            if (Directory.Exists(scenariosPath))
            {
                var jsonFiles = Directory.GetFiles(scenariosPath, "*.json", SearchOption.TopDirectoryOnly);
                foreach (var file in jsonFiles)
                {
                    _availableScenarios.Add(file);
                }
            }
        }

        private void LoadScenario(string scenarioPath)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                Debug.LogError("[DemoUI] No ECS World active");
                return;
            }

            var em = world.EntityManager;
            var query = em.CreateEntityQuery(typeof(DemoOptions));
            if (query.CalculateEntityCount() > 0)
            {
                var options = query.GetSingletonRW<DemoOptions>();
                options.ValueRW.ScenarioPath = new FixedString64Bytes(Path.GetFileName(scenarioPath));
                Debug.Log($"[DemoUI] Scenario selected: {options.ValueRO.ScenarioPath}");
                
                // TODO: Trigger ScenarioRunner to load the scenario
                // This would integrate with PureDOTS ScenarioRunner
            }
            query.Dispose();
        }

        private void TogglePause(EntityManager em)
        {
            var query = em.CreateEntityQuery(typeof(DemoBootstrapState));
            if (query.CalculateEntityCount() > 0)
            {
                var state = query.GetSingletonRW<DemoBootstrapState>();
                state.ValueRW.Paused = (byte)(state.ValueRO.Paused == 1 ? 0 : 1);
            }
            query.Dispose();
        }

        private void ToggleRewind(EntityManager em)
        {
            var query = em.CreateEntityQuery(typeof(DemoBootstrapState));
            if (query.CalculateEntityCount() > 0)
            {
                var state = query.GetSingletonRW<DemoBootstrapState>();
                state.ValueRW.RewindEnabled = (byte)(state.ValueRO.RewindEnabled == 1 ? 0 : 1);
            }
            query.Dispose();
        }

        private void SwapBindings(EntityManager em)
        {
            var query = em.CreateEntityQuery(typeof(DemoOptions));
            if (query.CalculateEntityCount() > 0)
            {
                var options = query.GetSingletonRW<DemoOptions>();
                options.ValueRW.BindingsSet = (byte)(options.ValueRO.BindingsSet == 1 ? 0 : 1);
            }
            query.Dispose();
        }

        private void SetTimeScale(EntityManager em, float scale)
        {
            var query = em.CreateEntityQuery(typeof(DemoBootstrapState));
            if (query.CalculateEntityCount() > 0)
            {
                var state = query.GetSingletonRW<DemoBootstrapState>();
                state.ValueRW.TimeScale = scale;
            }
            query.Dispose();
        }

        private uint GetCurrentTick(EntityManager em)
        {
            var query = em.CreateEntityQuery(typeof(PureDOTS.Runtime.Components.TimeState));
            if (query.CalculateEntityCount() > 0)
            {
                var timeState = query.GetSingleton<PureDOTS.Runtime.Components.TimeState>();
                return timeState.Tick;
            }
            query.Dispose();
            return 0;
        }

        private float GetFixedTickMs()
        {
            // TODO: Get actual fixed tick duration from system
            return Time.fixedDeltaTime * 1000f;
        }

        private float GetSnapshotKb()
        {
            // TODO: Get snapshot ring buffer usage
            return 0f;
        }

        private static class EditorStyles
        {
            public static GUIStyle boldLabel => new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
        }
    }
}

