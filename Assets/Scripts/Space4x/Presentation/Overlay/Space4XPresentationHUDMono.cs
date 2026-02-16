using System;
using System.IO;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Platform;
using PureDOTS.Runtime.Scenarios;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Presentation.Overlay
{
    /// <summary>
    /// Lightweight presentation HUD for demo capture and live explanation.
    /// Reads ECS state only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Space4XPresentationHUDMono : MonoBehaviour
    {
        private const string ScenarioPathEnv = "SPACE4X_SCENARIO_PATH";

        [SerializeField] private bool _showHud = true;
        [SerializeField] private KeyCode _toggleHudKey = KeyCode.H;
        [SerializeField] private float _refreshRateHz = 5f;
        [SerializeField] private Rect _hudRect = new Rect(12f, 12f, 520f, 260f);
        [SerializeField] private int _fontSize = 13;

        private GUIStyle _panelStyle;
        private GUIStyle _labelStyle;
        private float _nextRefreshAt;
        private HudSnapshot _snapshot;

        private struct HudSnapshot
        {
            public string ScenarioId;
            public string ScenarioPath;
            public uint Tick;
            public float DeltaTime;
            public float WorldSeconds;
            public string RewindMode;
            public int CarrierCount;
            public int MinerCount;
            public int AsteroidCount;
            public int ResourceSourceCount;
            public int StrikeCraftCount;
            public int EscortAssignmentCount;
            public int ChildTetherCount;
            public int CrewTotal;
            public int ModuleTotal;
            public string SelectedCarrier;
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleHudKey))
            {
                _showHud = !_showHud;
            }

            if (!_showHud)
            {
                return;
            }

            var now = Time.unscaledTime;
            if (now < _nextRefreshAt)
            {
                return;
            }

            _nextRefreshAt = now + (1f / Mathf.Max(1f, _refreshRateHz));
            RefreshSnapshot();
        }

        private void OnGUI()
        {
            if (!_showHud)
            {
                return;
            }

            EnsureStyles();

            GUILayout.BeginArea(_hudRect, GUIContent.none, _panelStyle);
            GUILayout.Label($"Scenario: {_snapshot.ScenarioId}", _labelStyle);
            GUILayout.Label($"Path: {_snapshot.ScenarioPath}", _labelStyle);
            GUILayout.Label($"Tick: {_snapshot.Tick}  dt: {_snapshot.DeltaTime:0.0000}  t: {_snapshot.WorldSeconds:0.00}s", _labelStyle);
            GUILayout.Label($"Rewind: {_snapshot.RewindMode}", _labelStyle);
            GUILayout.Space(6f);
            GUILayout.Label($"Counts: carriers={_snapshot.CarrierCount} miners={_snapshot.MinerCount} strike={_snapshot.StrikeCraftCount}", _labelStyle);
            GUILayout.Label($"Counts: asteroids={_snapshot.AsteroidCount} resources={_snapshot.ResourceSourceCount}", _labelStyle);
            GUILayout.Label($"Counts: escorts={_snapshot.EscortAssignmentCount + _snapshot.ChildTetherCount} (assign={_snapshot.EscortAssignmentCount}, tether={_snapshot.ChildTetherCount})", _labelStyle);
            GUILayout.Label($"Hierarchy: crew_total={_snapshot.CrewTotal} modules_total={_snapshot.ModuleTotal}", _labelStyle);
            GUILayout.Label($"Selected carrier: {_snapshot.SelectedCarrier}", _labelStyle);
            GUILayout.Space(6f);
            GUILayout.Label("Hotkeys: R hold=scrub, Space=commit, C/Esc=cancel", _labelStyle);
            GUILayout.Label("Camera: F=focus, G=follow, C=cinematic, H=toggle HUD", _labelStyle);
            GUILayout.EndArea();
        }

        private void RefreshSnapshot()
        {
            var snapshot = new HudSnapshot
            {
                ScenarioId = "n/a",
                ScenarioPath = "n/a",
                RewindMode = "n/a",
                SelectedCarrier = "none"
            };

            var envScenario = Environment.GetEnvironmentVariable(ScenarioPathEnv);
            if (!string.IsNullOrWhiteSpace(envScenario))
            {
                snapshot.ScenarioPath = TryResolvePath(envScenario);
            }

            if (!TryGetEntityManager(out var entityManager))
            {
                _snapshot = snapshot;
                return;
            }

            using (var scenarioQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ScenarioInfo>()))
            {
                if (!scenarioQuery.IsEmptyIgnoreFilter)
                {
                    var info = scenarioQuery.GetSingleton<ScenarioInfo>();
                    var scenarioId = info.ScenarioId.ToString();
                    snapshot.ScenarioId = string.IsNullOrWhiteSpace(scenarioId) ? "n/a" : scenarioId;
                    if (snapshot.ScenarioPath == "n/a")
                    {
                        snapshot.ScenarioPath = snapshot.ScenarioId;
                    }
                }
            }

            using (var timeQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TimeState>()))
            {
                if (!timeQuery.IsEmptyIgnoreFilter)
                {
                    var timeState = timeQuery.GetSingleton<TimeState>();
                    snapshot.Tick = timeState.Tick;
                    snapshot.DeltaTime = timeState.FixedDeltaTime;
                    snapshot.WorldSeconds = timeState.WorldSeconds;
                }
            }

            using (var rewindQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<RewindState>()))
            {
                if (!rewindQuery.IsEmptyIgnoreFilter)
                {
                    var rewindState = rewindQuery.GetSingleton<RewindState>();
                    snapshot.RewindMode = rewindState.Mode.ToString();
                }
            }

            snapshot.CarrierCount = Count<Carrier>(entityManager);
            snapshot.MinerCount = Count<MiningVessel>(entityManager);
            snapshot.AsteroidCount = Count<Asteroid>(entityManager);
            snapshot.ResourceSourceCount = Count<ResourceSourceState>(entityManager);
            snapshot.StrikeCraftCount = Count<StrikeCraftProfile>(entityManager);
            snapshot.EscortAssignmentCount = Count<EscortAssignment>(entityManager);
            snapshot.ChildTetherCount = Count<ChildVesselTether>(entityManager);
            snapshot.CrewTotal = SumBufferLengths<PlatformCrewMember>(entityManager);
            snapshot.ModuleTotal = SumBufferLengths<CarrierModuleSlot>(entityManager);

            if (Space4XEntityPicker.TryGetSelectedCarrier(out var selectedCarrier))
            {
                snapshot.SelectedCarrier = $"{selectedCarrier.Index}:{selectedCarrier.Version}";
            }

            _snapshot = snapshot;
        }

        private static int Count<T>(EntityManager entityManager)
            where T : unmanaged, IComponentData
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.CalculateEntityCount();
        }

        private static int SumBufferLengths<T>(EntityManager entityManager)
            where T : unmanaged, IBufferElementData
        {
            var total = 0;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<T>());
            if (query.IsEmptyIgnoreFilter)
            {
                return 0;
            }

            using var entities = query.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                if (!entityManager.HasBuffer<T>(entities[i]))
                {
                    continue;
                }

                total += entityManager.GetBuffer<T>(entities[i]).Length;
            }

            return total;
        }

        private static bool TryGetEntityManager(out EntityManager entityManager)
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                entityManager = default;
                return false;
            }

            entityManager = world.EntityManager;
            return true;
        }

        private static string TryResolvePath(string path)
        {
            try
            {
                return Path.GetFullPath(path);
            }
            catch
            {
                return path;
            }
        }

        private void EnsureStyles()
        {
            if (_panelStyle != null && _labelStyle != null)
            {
                return;
            }

            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = _fontSize,
                padding = new RectOffset(10, 10, 8, 8)
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = _fontSize
            };
            _labelStyle.normal.textColor = new Color(0.92f, 0.95f, 1f, 1f);
        }
    }
}
