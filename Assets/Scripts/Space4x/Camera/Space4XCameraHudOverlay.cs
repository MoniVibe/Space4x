using PureDOTS.Runtime.Components;
using Space4X.BattleSlice;
using Space4x.Scenario;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
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
        private EntityQuery _fleetcrawlDirectorQuery;
        private bool _fleetcrawlDirectorQueryValid;
        private EntityQuery _enemyTelegraphQuery;
        private bool _enemyTelegraphQueryValid;
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
            public bool HasFleetcrawl;
            public int FleetcrawlRoomIndex;
            public int FleetcrawlRoomCount;
            public Space4XFleetcrawlRoomKind FleetcrawlRoomKind;
            public float FleetcrawlRemainingSeconds;
            public Space4XFleetcrawlDifficultyStatus FleetcrawlStatus;
            public int FleetcrawlKills;
            public int FleetcrawlSpawned;
            public int FleetcrawlMiniBossKills;
            public int FleetcrawlBossKills;
            public Space4XFleetcrawlRoomEndLogic FleetcrawlObjectiveLogic;
            public Space4XFleetcrawlRoomEndConditionFlags FleetcrawlObjectiveConditions;
            public int FleetcrawlKillQuota;
            public int FleetcrawlMiniBossQuota;
            public int FleetcrawlBossQuota;
            public int FleetcrawlLevel;
            public int FleetcrawlExperience;
            public int FleetcrawlExperienceToNext;
            public int FleetcrawlUnspentUpgrades;
            public int FleetcrawlMetaShards;
            public int FleetcrawlMetaUnlocks;
            public float FleetcrawlMetaDamageDealt;
            public float FleetcrawlMetaDamageMitigated;
            public float FleetcrawlMetaCloakSeconds;
            public float FleetcrawlMetaTimeStopSeconds;
            public float FleetcrawlMetaMissileDamage;
            public int FleetcrawlMetaCraftShotDown;
            public int FleetcrawlMetaCapitalKills;
            public int FleetcrawlMetaCachesFound;
            public Space4XFleetcrawlChallengeKind FleetcrawlChallengeKind;
            public int FleetcrawlChallengeRisk;
            public float FleetcrawlChallengeSpawnMultiplier;
            public float FleetcrawlChallengeCurrencyMultiplier;
            public float FleetcrawlChallengeExperienceMultiplier;
            public int TelegraphNormalWindup;
            public int TelegraphMiniWindup;
            public int TelegraphBossWindup;
            public int TelegraphNormalBurst;
            public int TelegraphMiniBurst;
            public int TelegraphBossBurst;
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
            DisposeQuery(ref _fleetcrawlDirectorQuery, ref _fleetcrawlDirectorQueryValid);
            DisposeQuery(ref _enemyTelegraphQuery, ref _enemyTelegraphQueryValid);
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

            if (UnityEngine.Time.unscaledTime < _nextRefreshTime)
            {
                return;
            }

            _nextRefreshTime = UnityEngine.Time.unscaledTime + 1f / Mathf.Max(1f, refreshRateHz);
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

            if (_snapshot.HasFleetcrawl)
            {
                GUILayout.Space(4f);
                GUILayout.Label(
                    $"Fleetcrawl room {_snapshot.FleetcrawlRoomIndex + 1}/{_snapshot.FleetcrawlRoomCount} kind={_snapshot.FleetcrawlRoomKind} remaining={_snapshot.FleetcrawlRemainingSeconds:0.0}s",
                    _labelStyle);
                GUILayout.Label(
                    $"Status {_snapshot.FleetcrawlStatus}  kills={_snapshot.FleetcrawlKills}/{math.max(0, _snapshot.FleetcrawlSpawned)}  mini={_snapshot.FleetcrawlMiniBossKills}  boss={_snapshot.FleetcrawlBossKills}",
                    _labelStyle);
                GUILayout.Label(
                    $"Progress lvl={_snapshot.FleetcrawlLevel} xp={_snapshot.FleetcrawlExperience}/{_snapshot.FleetcrawlExperienceToNext} unspent={_snapshot.FleetcrawlUnspentUpgrades} shards={_snapshot.FleetcrawlMetaShards}",
                    _labelStyle);
                GUILayout.Label(
                    $"Meta unlocks={_snapshot.FleetcrawlMetaUnlocks} dealt={_snapshot.FleetcrawlMetaDamageDealt:0} mitigated={_snapshot.FleetcrawlMetaDamageMitigated:0} cloak={_snapshot.FleetcrawlMetaCloakSeconds:0.0}s stop={_snapshot.FleetcrawlMetaTimeStopSeconds:0.0}s",
                    _labelStyle);
                GUILayout.Label(
                    $"Meta ordnance={_snapshot.FleetcrawlMetaMissileDamage:0} craft={_snapshot.FleetcrawlMetaCraftShotDown} capital={_snapshot.FleetcrawlMetaCapitalKills} caches={_snapshot.FleetcrawlMetaCachesFound}",
                    _labelStyle);
                GUILayout.Label(
                    $"Challenge {_snapshot.FleetcrawlChallengeKind} risk={_snapshot.FleetcrawlChallengeRisk} spawn={_snapshot.FleetcrawlChallengeSpawnMultiplier:0.00}x xp={_snapshot.FleetcrawlChallengeExperienceMultiplier:0.00}x currency={_snapshot.FleetcrawlChallengeCurrencyMultiplier:0.00}x",
                    _labelStyle);
                GUILayout.Label(BuildFleetcrawlObjectiveText(in _snapshot), _labelStyle);
                GUILayout.Label(
                    $"Telegraphs: windup N:{_snapshot.TelegraphNormalWindup} M:{_snapshot.TelegraphMiniWindup} B:{_snapshot.TelegraphBossWindup} | burst N:{_snapshot.TelegraphNormalBurst} M:{_snapshot.TelegraphMiniBurst} B:{_snapshot.TelegraphBossBurst}",
                    _labelStyle);
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
                WorldSeconds = UnityEngine.Time.timeSinceLevelLoad,
                FleetcrawlChallengeSpawnMultiplier = 1f,
                FleetcrawlChallengeCurrencyMultiplier = 1f,
                FleetcrawlChallengeExperienceMultiplier = 1f
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

            if (TryGetFirstEntity(entityManager, _fleetcrawlDirectorQuery, out var directorEntity) &&
                entityManager.HasComponent<Space4XFleetcrawlDirectorState>(directorEntity) &&
                entityManager.HasBuffer<Space4XFleetcrawlRoom>(directorEntity))
            {
                var director = entityManager.GetComponentData<Space4XFleetcrawlDirectorState>(directorEntity);
                var rooms = entityManager.GetBuffer<Space4XFleetcrawlRoom>(directorEntity);
                if (rooms.Length > 0)
                {
                    var roomIndex = math.clamp(director.CurrentRoomIndex, 0, rooms.Length - 1);
                    var room = rooms[roomIndex];
                    var dt = 1f / 60f;
                    if (_tickQuery.CalculateEntityCount() == 1)
                    {
                        var tickEntity = _tickQuery.GetSingletonEntity();
                        if (entityManager.HasComponent<TickTimeState>(tickEntity))
                        {
                            var tick = entityManager.GetComponentData<TickTimeState>(tickEntity);
                            dt = tick.FixedDeltaTime > 0f ? tick.FixedDeltaTime : dt;
                        }
                    }

                    snapshot.HasFleetcrawl = true;
                    snapshot.FleetcrawlRoomIndex = roomIndex;
                    snapshot.FleetcrawlRoomCount = rooms.Length;
                    snapshot.FleetcrawlRoomKind = room.Kind;
                    snapshot.FleetcrawlRemainingSeconds = math.max(0f, (director.RoomEndTick - snapshot.Tick) * dt);
                    snapshot.FleetcrawlStatus = director.DifficultyStatus;
                    snapshot.FleetcrawlKills = director.EnemiesDestroyedInRoom;
                    snapshot.FleetcrawlSpawned = director.EnemiesSpawnedInRoom;
                    snapshot.FleetcrawlMiniBossKills = director.MiniBossesDestroyedInRoom;
                    snapshot.FleetcrawlBossKills = director.BossesDestroyedInRoom;
                    snapshot.FleetcrawlObjectiveLogic = room.EndLogic;
                    snapshot.FleetcrawlObjectiveConditions = room.EndConditions;
                    snapshot.FleetcrawlKillQuota = room.KillQuota;
                    snapshot.FleetcrawlMiniBossQuota = room.MiniBossQuota;
                    snapshot.FleetcrawlBossQuota = room.BossQuota;
                    if (entityManager.HasComponent<Space4XRunProgressionState>(directorEntity))
                    {
                        var progression = entityManager.GetComponentData<Space4XRunProgressionState>(directorEntity);
                        snapshot.FleetcrawlLevel = progression.Level;
                        snapshot.FleetcrawlExperience = progression.Experience;
                        snapshot.FleetcrawlExperienceToNext = progression.ExperienceToNext;
                        snapshot.FleetcrawlUnspentUpgrades = progression.UnspentUpgrades;
                    }
                    if (entityManager.HasComponent<Space4XRunMetaResourceState>(directorEntity))
                    {
                        var meta = entityManager.GetComponentData<Space4XRunMetaResourceState>(directorEntity);
                        snapshot.FleetcrawlMetaShards = meta.Shards;
                    }
                    if (entityManager.HasComponent<Space4XRunMetaUnlockState>(directorEntity))
                    {
                        var metaUnlocks = entityManager.GetComponentData<Space4XRunMetaUnlockState>(directorEntity);
                        snapshot.FleetcrawlMetaUnlocks = metaUnlocks.UnlockCount;
                    }
                    if (entityManager.HasComponent<Space4XRunMetaProficiencyState>(directorEntity))
                    {
                        var metaProficiency = entityManager.GetComponentData<Space4XRunMetaProficiencyState>(directorEntity);
                        snapshot.FleetcrawlMetaDamageDealt = metaProficiency.DamageDealtEnergy +
                                                            metaProficiency.DamageDealtThermal +
                                                            metaProficiency.DamageDealtEM +
                                                            metaProficiency.DamageDealtRadiation +
                                                            metaProficiency.DamageDealtCaustic +
                                                            metaProficiency.DamageDealtKinetic +
                                                            metaProficiency.DamageDealtExplosive;
                        snapshot.FleetcrawlMetaDamageMitigated = metaProficiency.DamageMitigatedEnergy +
                                                                metaProficiency.DamageMitigatedThermal +
                                                                metaProficiency.DamageMitigatedEM +
                                                                metaProficiency.DamageMitigatedRadiation +
                                                                metaProficiency.DamageMitigatedCaustic +
                                                                metaProficiency.DamageMitigatedKinetic +
                                                                metaProficiency.DamageMitigatedExplosive;
                        snapshot.FleetcrawlMetaCloakSeconds = metaProficiency.CloakSeconds;
                        snapshot.FleetcrawlMetaTimeStopSeconds = metaProficiency.TimeStopRequestedSeconds;
                        snapshot.FleetcrawlMetaMissileDamage = metaProficiency.MissileDamageDealt;
                        snapshot.FleetcrawlMetaCraftShotDown = metaProficiency.CraftShotDown;
                        snapshot.FleetcrawlMetaCapitalKills = metaProficiency.CapitalShipsDestroyed;
                        snapshot.FleetcrawlMetaCachesFound = metaProficiency.HiddenCachesFound;
                    }
                    if (entityManager.HasComponent<Space4XRunChallengeState>(directorEntity))
                    {
                        var challenge = entityManager.GetComponentData<Space4XRunChallengeState>(directorEntity);
                        snapshot.FleetcrawlChallengeKind = challenge.Kind;
                        snapshot.FleetcrawlChallengeRisk = challenge.RiskTier;
                        snapshot.FleetcrawlChallengeSpawnMultiplier = challenge.SpawnMultiplier;
                        snapshot.FleetcrawlChallengeCurrencyMultiplier = challenge.CurrencyMultiplier;
                        snapshot.FleetcrawlChallengeExperienceMultiplier = challenge.ExperienceMultiplier;
                    }
                }
            }

            if (!_enemyTelegraphQuery.IsEmptyIgnoreFilter)
            {
                using var states = _enemyTelegraphQuery.ToComponentDataArray<Space4XEnemyTelegraphState>(Allocator.Temp);
                using var tags = _enemyTelegraphQuery.ToComponentDataArray<Space4XRunEnemyTag>(Allocator.Temp);
                var count = math.min(states.Length, tags.Length);
                for (var i = 0; i < count; i++)
                {
                    var telegraphing = states[i].IsTelegraphing != 0;
                    var bursting = states[i].IsBursting != 0;
                    switch (tags[i].EnemyClass)
                    {
                        case Space4XFleetcrawlEnemyClass.MiniBoss:
                            if (telegraphing) snapshot.TelegraphMiniWindup++;
                            if (bursting) snapshot.TelegraphMiniBurst++;
                            break;
                        case Space4XFleetcrawlEnemyClass.Boss:
                            if (telegraphing) snapshot.TelegraphBossWindup++;
                            if (bursting) snapshot.TelegraphBossBurst++;
                            break;
                        default:
                            if (telegraphing) snapshot.TelegraphNormalWindup++;
                            if (bursting) snapshot.TelegraphNormalBurst++;
                            break;
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

            if (!_fleetcrawlDirectorQueryValid)
            {
                _fleetcrawlDirectorQuery = entityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<Space4XFleetcrawlDirectorState>(),
                    ComponentType.ReadOnly<Space4XFleetcrawlRoom>());
                _fleetcrawlDirectorQueryValid = true;
            }

            if (!_enemyTelegraphQueryValid)
            {
                _enemyTelegraphQuery = entityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<Space4XEnemyTelegraphState>(),
                    ComponentType.ReadOnly<Space4XRunEnemyTag>());
                _enemyTelegraphQueryValid = true;
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

        private static string BuildFleetcrawlObjectiveText(in HudSnapshot snapshot)
        {
            var conditions = snapshot.FleetcrawlObjectiveConditions;
            if (conditions == 0)
            {
                conditions = Space4XFleetcrawlRoomEndConditionFlags.Timer;
            }

            var text = $"Objective ({snapshot.FleetcrawlObjectiveLogic}): ";
            var hasPart = false;
            if ((conditions & Space4XFleetcrawlRoomEndConditionFlags.Timer) != 0)
            {
                text += $"timer {snapshot.FleetcrawlRemainingSeconds:0.0}s";
                hasPart = true;
            }
            if ((conditions & Space4XFleetcrawlRoomEndConditionFlags.KillQuota) != 0)
            {
                if (hasPart) text += " | ";
                var target = math.max(1, snapshot.FleetcrawlKillQuota);
                var progress = math.min(target, snapshot.FleetcrawlKills);
                text += $"kills {progress}/{target}";
                hasPart = true;
            }
            if ((conditions & Space4XFleetcrawlRoomEndConditionFlags.MiniBossQuota) != 0)
            {
                if (hasPart) text += " | ";
                var target = math.max(1, snapshot.FleetcrawlMiniBossQuota);
                var progress = math.min(target, snapshot.FleetcrawlMiniBossKills);
                text += $"mini {progress}/{target}";
                hasPart = true;
            }
            if ((conditions & Space4XFleetcrawlRoomEndConditionFlags.BossQuota) != 0)
            {
                if (hasPart) text += " | ";
                var target = math.max(1, snapshot.FleetcrawlBossQuota);
                var progress = math.min(target, snapshot.FleetcrawlBossKills);
                text += $"boss {progress}/{target}";
                hasPart = true;
            }

            return hasPart ? text : "Objective: <none>";
        }
    }
}
