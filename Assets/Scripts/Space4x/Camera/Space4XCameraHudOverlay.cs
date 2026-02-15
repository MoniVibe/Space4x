using PureDOTS.Runtime.Components;
using Space4X.BattleSlice;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Space4X.Camera
{
    /// <summary>
    /// Minimal runtime overlay for demo capture: alive counts, time/tick, and optional digest.
    /// Presentation-only; reads ECS state but does not mutate simulation data.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Space4XCameraHudOverlay : MonoBehaviour
    {
        [SerializeField] private bool showHud = true;
        [SerializeField] private Key toggleHudKey = Key.H;
        [SerializeField] private bool showDeterminismDigest = true;
        [SerializeField] private Rect hudRect = new Rect(12f, 12f, 420f, 140f);
        [SerializeField] private int fontSize = 14;
        [SerializeField] private float refreshRateHz = 10f;

        private World _ecsWorld;
        private EntityQuery _tickQuery;
        private bool _tickQueryValid;
        private EntityQuery _battleMetricsQuery;
        private bool _battleMetricsQueryValid;
        private EntityQuery _battleFighterQuery;
        private bool _battleFighterQueryValid;
        private Space4XCameraRigController _rigController;
        private GUIStyle _panelStyle;
        private GUIStyle _labelStyle;
        private float _nextRefreshTime;
        private HudSnapshot _snapshot;

        private struct HudSnapshot
        {
            public int Side0Alive;
            public int Side1Alive;
            public uint Tick;
            public float WorldSeconds;
            public uint Digest;
            public bool HasDigest;
        }

        private void OnEnable()
        {
            _rigController = GetComponent<Space4XCameraRigController>();
        }

        private void OnDisable()
        {
            DisposeQuery(ref _tickQuery, ref _tickQueryValid);
            DisposeQuery(ref _battleMetricsQuery, ref _battleMetricsQueryValid);
            DisposeQuery(ref _battleFighterQuery, ref _battleFighterQueryValid);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[toggleHudKey].wasPressedThisFrame)
            {
                showHud = !showHud;
            }

            if (!showHud)
            {
                return;
            }

            if (Time.unscaledTime < _nextRefreshTime)
            {
                return;
            }

            _nextRefreshTime = Time.unscaledTime + 1f / Mathf.Max(1f, refreshRateHz);
            RefreshSnapshot();
        }

        private void OnGUI()
        {
            if (!showHud)
            {
                return;
            }

            EnsureStyles();
            GUILayout.BeginArea(hudRect, GUIContent.none, _panelStyle);
            GUILayout.Label($"Alive  side0={_snapshot.Side0Alive}  side1={_snapshot.Side1Alive}", _labelStyle);
            GUILayout.Label($"Tick   {_snapshot.Tick}    Time {_snapshot.WorldSeconds:0.0}s", _labelStyle);

            if (showDeterminismDigest && _snapshot.HasDigest)
            {
                GUILayout.Label($"Digest {_snapshot.Digest}", _labelStyle);
            }
            else if (showDeterminismDigest)
            {
                GUILayout.Label("Digest n/a", _labelStyle);
            }

            var mode = _rigController != null && _rigController.IsCinematicActive
                ? "Cinematic"
                : (_rigController != null && _rigController.IsFollowSelectedActive ? "Follow" : "Manual");
            GUILayout.Label($"Mode   {mode}", _labelStyle);
            GUILayout.Label("Keys   F focus  G follow  C cinematic  H HUD", _labelStyle);
            GUILayout.EndArea();
        }

        private void RefreshSnapshot()
        {
            var snapshot = new HudSnapshot
            {
                WorldSeconds = Time.timeSinceLevelLoad
            };

            if (!TryEnsureQueries(out var entityManager))
            {
                _snapshot = snapshot;
                return;
            }

            if (_tickQuery.CalculateEntityCount() == 1)
            {
                var tickEntity = _tickQuery.GetSingletonEntity();
                if (entityManager.HasComponent<TickTimeState>(tickEntity))
                {
                    var tick = entityManager.GetComponentData<TickTimeState>(tickEntity);
                    snapshot.Tick = tick.Tick;
                    snapshot.WorldSeconds = tick.WorldSeconds;
                }
            }

            if (TryGetFirstEntity(entityManager, _battleMetricsQuery, out var metricsEntity) &&
                entityManager.HasComponent<Space4XBattleSliceMetrics>(metricsEntity))
            {
                var metrics = entityManager.GetComponentData<Space4XBattleSliceMetrics>(metricsEntity);
                snapshot.Side0Alive = metrics.Side0Alive;
                snapshot.Side1Alive = metrics.Side1Alive;
                snapshot.Digest = metrics.Digest;
                snapshot.HasDigest = true;
            }
            else if (_battleFighterQuery.CalculateEntityCount() > 0)
            {
                using var fighters = _battleFighterQuery.ToComponentDataArray<Space4XBattleSliceFighter>(Allocator.Temp);
                for (int i = 0; i < fighters.Length; i++)
                {
                    var fighter = fighters[i];
                    if (fighter.Alive == 0)
                    {
                        continue;
                    }

                    if (fighter.Side == 0)
                    {
                        snapshot.Side0Alive++;
                    }
                    else if (fighter.Side == 1)
                    {
                        snapshot.Side1Alive++;
                    }
                }
            }

            _snapshot = snapshot;
        }

        private bool TryEnsureQueries(out EntityManager entityManager)
        {
            if (_ecsWorld == null || !_ecsWorld.IsCreated)
            {
                _ecsWorld = World.DefaultGameObjectInjectionWorld;
            }

            if (_ecsWorld == null || !_ecsWorld.IsCreated)
            {
                entityManager = default;
                return false;
            }

            entityManager = _ecsWorld.EntityManager;

            if (!_tickQueryValid)
            {
                _tickQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TickTimeState>());
                _tickQueryValid = true;
            }

            if (!_battleMetricsQueryValid)
            {
                _battleMetricsQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XBattleSliceMetrics>());
                _battleMetricsQueryValid = true;
            }

            if (!_battleFighterQueryValid)
            {
                _battleFighterQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XBattleSliceFighter>());
                _battleFighterQueryValid = true;
            }

            return true;
        }

        private static bool TryGetFirstEntity(EntityManager entityManager, EntityQuery query, out Entity entity)
        {
            entity = Entity.Null;
            var count = query.CalculateEntityCount();
            if (count <= 0)
            {
                return false;
            }

            if (count == 1)
            {
                entity = query.GetSingletonEntity();
                return entity != Entity.Null && entityManager.Exists(entity);
            }

            using var entities = query.ToEntityArray(Allocator.Temp);
            if (entities.Length <= 0)
            {
                return false;
            }

            entity = entities[0];
            return entity != Entity.Null && entityManager.Exists(entity);
        }

        private static void DisposeQuery(ref EntityQuery query, ref bool valid)
        {
            if (!valid)
            {
                return;
            }

            try
            {
                query.Dispose();
            }
            catch
            {
                // World may already be tearing down.
            }

            valid = false;
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
                fontSize = fontSize,
                padding = new RectOffset(10, 10, 8, 8)
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize
            };
            _labelStyle.normal.textColor = new Color(0.93f, 0.96f, 1f, 1f);
        }
    }
}
