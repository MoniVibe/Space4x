using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PureDOTS.Environment;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Modularity;
using PureDOTS.Runtime.Perception;
using PureDOTS.Runtime.Profile;
using PureDOTS.Runtime.Interrupts;
using PureDOTS.Runtime.Physics;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Runtime.Platform;
using PureDOTS.Systems;
using Space4X.Registry;
using Space4X.Physics;
using Space4X.Runtime;
using ResourceSourceState = Space4X.Registry.ResourceSourceState;
using ResourceSourceConfig = Space4X.Registry.ResourceSourceConfig;
using ResourceTypeId = Space4X.Registry.ResourceTypeId;
using ResourceType = Space4X.Registry.ResourceType;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using SystemEnv = System.Environment;

namespace Space4x.Scenario
{
    /// <summary>
    /// Loads and executes the mining scenario from JSON, spawning carriers, mining vessels, and resource deposits.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class Space4XMiningScenarioSystem : SystemBase
    {
        private const string ScenarioPathEnv = "SPACE4X_SCENARIO_PATH";
        private const string JsonExtension = ".json";
        private const float DefaultSpawnVerticalRange = 60f;
        private const string RefitScenarioFile = "space4x_refit.json";
        private const string ResearchScenarioFile = "space4x_research_mvp.json";
        private bool _hasLoaded;
        private MiningScenarioJson _scenarioData;
        private Dictionary<string, Entity> _spawnedEntities;
        private bool _useSmokeMotionTuning;
        private bool _isCollisionScenario;
        private Entity _friendlyAffiliationEntity;
        private Entity _hostileAffiliationEntity;

        protected override void OnCreate()
        {
            RequireForUpdate<TimeState>();
        }

        protected override void OnUpdate()
        {
            if (_hasLoaded)
            {
                Enabled = false;
                return;
            }

            var scenarioPath = ResolveScenarioPath(out var scenarioInfo, out var hasScenarioInfo);
            if (!string.IsNullOrWhiteSpace(scenarioPath))
            {
                scenarioPath = Path.GetFullPath(scenarioPath);
                if (!File.Exists(scenarioPath))
                {
                    Debug.LogWarning($"[Space4XMiningScenario] Override missing, falling back to ScenarioInfo: {scenarioPath}");
                    scenarioPath = null;
                }
            }

            if (string.IsNullOrWhiteSpace(scenarioPath))
            {
                if (!hasScenarioInfo)
                {
                    // Wait for ScenarioInfo injection (smoke selector/bootstrap).
                    return;
                }

                var scenarioIdForWarning = scenarioInfo.ScenarioId.ToString();
                if (string.IsNullOrWhiteSpace(scenarioIdForWarning))
                {
                    scenarioIdForWarning = "unknown";
                }

                Debug.LogWarning($"[Space4XMiningScenario] No scenario path resolved for ScenarioId='{scenarioIdForWarning}'.");
                return;
            }

            if (!hasScenarioInfo)
            {
                scenarioInfo = EnsureScenarioInfoFromPath(scenarioPath);
                hasScenarioInfo = true;
            }

            var jsonText = File.ReadAllText(scenarioPath);
            _scenarioData = JsonUtility.FromJson<MiningScenarioJson>(jsonText);
            if (_scenarioData == null || _scenarioData.spawn == null)
            {
                Debug.LogError("[Space4XMiningScenario] Failed to parse scenario JSON");
                Enabled = false;
                return;
            }

            _useSmokeMotionTuning = IsSmokeScenario(scenarioPath, scenarioInfo);
            if (_useSmokeMotionTuning)
            {
                ApplySmokeMotionProfile();
                ApplySmokeLatchConfig();
                ApplyFloatingOriginConfig();
            }
            ApplyDogfightConfig(_scenarioData.dogfightConfig);
            ApplyStanceConfig(_scenarioData.stanceConfig);

            var timeState = SystemAPI.GetSingleton<TimeState>();

            var scenarioFileName = Path.GetFileName(scenarioPath);
            var isRefitScenario = scenarioFileName.Equals(RefitScenarioFile, StringComparison.OrdinalIgnoreCase);
            var isResearchScenario = scenarioFileName.Equals(ResearchScenarioFile, StringComparison.OrdinalIgnoreCase);
            var isMiningScenario = !(isRefitScenario || isResearchScenario);
            _isCollisionScenario = scenarioFileName.Equals("space4x_collision_micro.json", StringComparison.OrdinalIgnoreCase);
            if (_isCollisionScenario)
            {
                EnsureCollisionScenarioTag();
            }

            _spawnedEntities = new Dictionary<string, Entity>();
            if (isMiningScenario)
            {
                SpawnEntities(timeState.Tick, timeState.FixedDeltaTime);
            }
            var fixedDt = math.max(1e-6f, timeState.FixedDeltaTime);
            var durationSeconds = math.max(0f, _scenarioData.duration_s);
            var durationTicks = (uint)math.ceil(durationSeconds / fixedDt);
            var startTick = timeState.Tick;
            var safeDurationTicks = durationTicks == 0 ? 1u : durationTicks;
            var endTick = startTick + safeDurationTicks;
            var runtimeEntity = EnsureScenarioRuntime(startTick, endTick, durationSeconds);
            if (isMiningScenario)
            {
                ScheduleScenarioActions(runtimeEntity, startTick, fixedDt);
            }
            UpdateScenarioInfoSingleton(scenarioInfo, safeDurationTicks);

            if (isMiningScenario)
            {
                Debug.Log($"[Space4XMiningScenario] Loaded '{scenarioPath}'. Spawned carriers/miners/asteroids. Duration={durationSeconds:F1}s ticks={safeDurationTicks} (startTick={startTick}, endTick={endTick}).");
            }
            else
            {
                Debug.Log($"[Space4XMiningScenario] Loaded '{scenarioPath}'. Deferring spawns to scenario-specific systems. Duration={durationSeconds:F1}s ticks={safeDurationTicks} (startTick={startTick}, endTick={endTick}).");
            }

            _hasLoaded = true;
            Enabled = false;
        }

        private static bool IsSmokeScenario(string scenarioPath, ScenarioInfo scenarioInfo)
        {
            var scenarioName = Path.GetFileNameWithoutExtension(scenarioPath);
            if (!string.IsNullOrWhiteSpace(scenarioName) &&
                (scenarioName.Equals("space4x_smoke", StringComparison.OrdinalIgnoreCase)
                 || scenarioName.StartsWith("space4x_movement", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            var scenarioId = scenarioInfo.ScenarioId.ToString();
            if (scenarioId.Equals("space4x_smoke", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return scenarioId.StartsWith("space4x_movement", StringComparison.OrdinalIgnoreCase);
        }

        private void ApplySmokeMotionProfile()
        {
            var config = VesselMotionProfileConfig.Default;
            config.CapitalShipSpeedMultiplier = 0.65f;
            config.CapitalShipTurnMultiplier = 0.7f;
            config.CapitalShipAccelerationMultiplier = 0.6f;
            config.CapitalShipDecelerationMultiplier = 0.7f;
            config.MiningUndockSpeedMultiplier = 0.3f;
            config.MiningApproachSpeedMultiplier = 0.6f;
            config.MiningLatchSpeedMultiplier = 0.25f;
            config.MiningDetachSpeedMultiplier = 0.35f;
            config.MiningReturnSpeedMultiplier = 0.7f;
            config.MiningDockSpeedMultiplier = 0.25f;

            if (!SystemAPI.TryGetSingletonEntity<VesselMotionProfileConfig>(out var configEntity))
            {
                configEntity = EntityManager.CreateEntity(typeof(VesselMotionProfileConfig));
            }

            EntityManager.SetComponentData(configEntity, config);

            if (!SystemAPI.TryGetSingletonEntity<Space4XLegacyMiningDisabledTag>(out _))
            {
                EntityManager.CreateEntity(typeof(Space4XLegacyMiningDisabledTag));
            }

            if (!SystemAPI.TryGetSingletonEntity<Space4XLegacyPatrolDisabledTag>(out _))
            {
                EntityManager.CreateEntity(typeof(Space4XLegacyPatrolDisabledTag));
            }

            EnsurePhysicsConfigEnabled();
        }

        private void ApplyFloatingOriginConfig()
        {
            if (!SystemAPI.TryGetSingletonEntity<Space4XFloatingOriginConfig>(out var configEntity))
            {
                configEntity = EntityManager.CreateEntity(typeof(Space4XFloatingOriginConfig));
            }

            EntityManager.SetComponentData(configEntity, new Space4XFloatingOriginConfig
            {
                Threshold = 500f,
                CooldownTicks = 300,
                Enabled = 1
            });
        }

        private void EnsurePhysicsConfigEnabled()
        {
            if (!SystemAPI.TryGetSingletonEntity<PhysicsConfig>(out var configEntity))
            {
                configEntity = EntityManager.CreateEntity(typeof(PhysicsConfig), typeof(PhysicsConfigTag));
            }

            var config = EntityManager.GetComponentData<PhysicsConfig>(configEntity);
            if (config.ProviderId == PhysicsProviderIds.None)
            {
                config.ProviderId = PhysicsProviderIds.Entities;
            }

            if (config.EnableSpace4XPhysics == 0)
            {
                config.EnableSpace4XPhysics = 1;
            }

            EntityManager.SetComponentData(configEntity, config);
        }

        private void ApplySmokeLatchConfig()
        {
            var config = Space4XMiningLatchConfig.Default;
            if (SystemAPI.TryGetSingleton<Space4XMiningLatchConfig>(out var existing))
            {
                config = existing;
            }

            // Smoke runs can stall if latch alignment never resolves; relax alignment for headless stability.
            config.SurfaceEpsilon = math.max(config.SurfaceEpsilon, 3.2f);
            config.AlignDotThreshold = -1f;

            if (!SystemAPI.TryGetSingletonEntity<Space4XMiningLatchConfig>(out var configEntity))
            {
                configEntity = EntityManager.CreateEntity(typeof(Space4XMiningLatchConfig));
            }

            EntityManager.SetComponentData(configEntity, config);
        }

        private void ApplyDogfightConfig(StrikeCraftDogfightConfigData data)
        {
            if (data == null)
            {
                return;
            }

            var config = StrikeCraftDogfightConfig.Default;
            config.TargetAcquireRadius = math.max(0f, data.acquireRadius);
            config.FireConeDegrees = math.clamp(data.coneDegrees, 1f, 180f);
            config.NavConstantN = math.max(0.1f, data.navConstantN);
            config.BreakOffDistance = math.max(0f, data.breakOffDistance);
            config.BreakOffTicks = (uint)math.max(0, data.breakOffTicks);
            config.RejoinRadius = math.max(0f, data.rejoinRadius);
            if (data.rejoinOffset != null && data.rejoinOffset.Length >= 3)
            {
                config.RejoinOffset = new float3(data.rejoinOffset[0], data.rejoinOffset[1], data.rejoinOffset[2]);
            }
            config.JinkStrength = math.max(0f, data.jinkStrength);

            if (!SystemAPI.TryGetSingletonEntity<StrikeCraftDogfightConfig>(out var configEntity))
            {
                configEntity = EntityManager.CreateEntity(typeof(StrikeCraftDogfightConfig));
            }

            EntityManager.SetComponentData(configEntity, config);
        }

        private void ApplyStanceConfig(StanceTuningConfigData data)
        {
            if (data == null)
            {
                return;
            }

            var config = Space4XStanceTuningConfig.Default;
            if (data.aggressive != null)
            {
                config.Aggressive = ApplyStanceEntry(config.Aggressive, data.aggressive);
            }
            if (data.balanced != null)
            {
                config.Balanced = ApplyStanceEntry(config.Balanced, data.balanced);
            }
            if (data.defensive != null)
            {
                config.Defensive = ApplyStanceEntry(config.Defensive, data.defensive);
            }
            if (data.evasive != null)
            {
                config.Evasive = ApplyStanceEntry(config.Evasive, data.evasive);
            }

            if (!SystemAPI.TryGetSingletonEntity<Space4XStanceTuningConfig>(out var configEntity))
            {
                configEntity = EntityManager.CreateEntity(typeof(Space4XStanceTuningConfig));
            }

            EntityManager.SetComponentData(configEntity, config);
        }

        private static StanceTuningEntry ApplyStanceEntry(in StanceTuningEntry baseEntry, StanceTuningEntryData data)
        {
            var entry = baseEntry;
            if (data == null)
            {
                return entry;
            }

            entry.AvoidanceRadius = math.max(0f, data.avoidanceRadius);
            entry.AvoidanceStrength = math.max(0f, data.avoidanceStrength);
            entry.SpeedMultiplier = math.max(0f, data.speedMultiplier);
            entry.RotationMultiplier = math.max(0f, data.rotationMultiplier);
            entry.MaintainFormationWhenAttacking = math.clamp(data.maintainFormationWhenAttacking, 0f, 1f);
            entry.EvasionJinkStrength = math.max(0f, data.evasionJinkStrength);
            entry.AutoEngageRadius = math.max(0f, data.autoEngageRadius);
            entry.AbortAttackOnDamageThreshold = math.clamp(data.abortAttackOnDamageThreshold, 0f, 1f);
            entry.ReturnToPatrolAfterCombat = (byte)(data.returnToPatrolAfterCombat ? 1 : 0);
            entry.CommandOverrideDropsToNeutral = (byte)(data.commandOverrideDropsToNeutral ? 1 : 0);
            entry.AttackMoveBearingWeight = math.max(0f, data.attackMoveBearingWeight);
            entry.AttackMoveDestinationWeight = math.max(0f, data.attackMoveDestinationWeight);
            return entry;
        }

        private Entity EnsureScenarioAffiliation(byte scenarioSide)
        {
            if (scenarioSide == 1)
            {
                if (_hostileAffiliationEntity != Entity.Null && EntityManager.Exists(_hostileAffiliationEntity))
                {
                    return _hostileAffiliationEntity;
                }
            }
            else
            {
                if (_friendlyAffiliationEntity != Entity.Null && EntityManager.Exists(_friendlyAffiliationEntity))
                {
                    return _friendlyAffiliationEntity;
                }
            }

            var entity = EntityManager.CreateEntity(typeof(AffiliationRelation));
            var affiliationName = scenarioSide == 1 ? "Scenario-Hostile" : "Scenario-Friendly";
            EntityManager.SetComponentData(entity, new AffiliationRelation
            {
                AffiliationName = new FixedString64Bytes(affiliationName)
            });

            if (scenarioSide == 1)
            {
                _hostileAffiliationEntity = entity;
            }
            else
            {
                _friendlyAffiliationEntity = entity;
            }

            return entity;
        }

        private void AddScenarioAffiliation(Entity entity, byte scenarioSide)
        {
            var affiliationEntity = EnsureScenarioAffiliation(scenarioSide);
            if (affiliationEntity == Entity.Null)
            {
                return;
            }

            if (!EntityManager.HasBuffer<AffiliationTag>(entity))
            {
                EntityManager.AddBuffer<AffiliationTag>(entity);
            }

            var buffer = EntityManager.GetBuffer<AffiliationTag>(entity);
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Target == affiliationEntity)
                {
                    return;
                }
            }

            buffer.Add(new AffiliationTag
            {
                Type = AffiliationType.Faction,
                Target = affiliationEntity,
                Loyalty = (half)1f
            });
        }

        private void EnsureDefaultSubsystems(Entity entity, float hullMax)
        {
            var subsystems = EntityManager.HasBuffer<SubsystemHealth>(entity)
                ? EntityManager.GetBuffer<SubsystemHealth>(entity)
                : EntityManager.AddBuffer<SubsystemHealth>(entity);

            if (subsystems.Length == 0)
            {
                var engineMax = math.max(5f, hullMax * 0.3f);
                var weaponMax = math.max(5f, hullMax * 0.2f);
                subsystems.Add(new SubsystemHealth
                {
                    Type = SubsystemType.Engines,
                    Current = engineMax,
                    Max = engineMax,
                    RegenPerTick = math.max(0.01f, engineMax * 0.005f),
                    Flags = SubsystemFlags.None
                });
                subsystems.Add(new SubsystemHealth
                {
                    Type = SubsystemType.Weapons,
                    Current = weaponMax,
                    Max = weaponMax,
                    RegenPerTick = math.max(0.01f, weaponMax * 0.005f),
                    Flags = SubsystemFlags.None
                });
            }

            if (!EntityManager.HasBuffer<SubsystemDisabled>(entity))
            {
                EntityManager.AddBuffer<SubsystemDisabled>(entity);
            }

            if (!EntityManager.HasBuffer<DamageScarEvent>(entity))
            {
                EntityManager.AddBuffer<DamageScarEvent>(entity);
            }
        }

        private void AddScenarioPhysics(Entity entity, float radius, Space4XPhysicsLayer layer, float restitution, float linearDamping)
        {
            var clampedRadius = math.max(0.1f, radius);
            var priority = Space4XPhysicsLayers.GetDefaultPriority(layer);
            var flags = SpacePhysicsFlags.IsActive | SpacePhysicsFlags.RaisesCollisionEvents;
            var interactionFlags = PhysicsInteractionFlags.Collidable;

            EntityManager.AddComponentData(entity, new SpacePhysicsBody
            {
                Layer = layer,
                Priority = priority,
                Flags = flags
            });

            EntityManager.AddComponentData(entity, SpaceColliderData.CreateSphere(clampedRadius));
            EntityManager.AddComponentData(entity, new SpaceVelocity
            {
                Linear = float3.zero,
                Angular = float3.zero
            });

            EntityManager.AddComponentData(entity, new RequiresPhysics
            {
                Priority = priority,
                Flags = interactionFlags
            });

            EntityManager.AddComponentData(entity, new PhysicsInteractionConfig
            {
                Mass = 1f,
                CollisionRadius = clampedRadius,
                Restitution = math.max(0f, restitution),
                Friction = 0f,
                LinearDamping = math.max(0f, linearDamping),
                AngularDamping = 0f
            });

            EntityManager.AddComponentData(entity, new PhysicsColliderSpec
            {
                Shape = PhysicsColliderShape.Sphere,
                Dimensions = new float3(clampedRadius, 0f, 0f),
                Flags = interactionFlags,
                IsTrigger = 0,
                UseCustomFilter = 1,
                CustomFilter = Space4XPhysicsLayers.CreateFilter(layer)
            });

            EntityManager.AddBuffer<SpaceCollisionEvent>(entity);
            EntityManager.AddBuffer<PhysicsCollisionEventElement>(entity);
            EntityManager.AddComponent<NeedsPhysicsSetup>(entity);
        }

        private Entity EnsureScenarioRuntime(uint startTick, uint endTick, float durationSeconds)
        {
            if (!SystemAPI.TryGetSingletonEntity<Space4XScenarioRuntime>(out var runtimeEntity))
            {
                runtimeEntity = EntityManager.CreateEntity(typeof(Space4XScenarioRuntime));
            }

            EntityManager.SetComponentData(runtimeEntity, new Space4XScenarioRuntime
            {
                StartTick = startTick,
                EndTick = endTick,
                DurationSeconds = durationSeconds
            });

            return runtimeEntity;
        }

        private ScenarioInfo EnsureScenarioInfoFromPath(string scenarioPath)
        {
            var scenarioId = Path.GetFileNameWithoutExtension(scenarioPath);
            if (string.IsNullOrWhiteSpace(scenarioId))
            {
                scenarioId = "space4x_smoke";
            }

            if (!SystemAPI.TryGetSingletonEntity<ScenarioInfo>(out var scenarioEntity))
            {
                scenarioEntity = EntityManager.CreateEntity(typeof(ScenarioInfo));
            }

            var scenarioInfo = new ScenarioInfo
            {
                ScenarioId = new FixedString64Bytes(scenarioId),
                Seed = 0,
                RunTicks = 0
            };

            EntityManager.SetComponentData(scenarioEntity, scenarioInfo);
            return scenarioInfo;
        }

        private void UpdateScenarioInfoSingleton(ScenarioInfo scenarioInfo, uint runTicks)
        {
            if (!SystemAPI.TryGetSingletonEntity<ScenarioInfo>(out var scenarioEntity))
            {
                scenarioEntity = EntityManager.CreateEntity(typeof(ScenarioInfo));
            }

            var scenarioId = scenarioInfo.ScenarioId;
            if (scenarioId.Length == 0)
            {
                scenarioId = new FixedString64Bytes("space4x_smoke");
            }

            EntityManager.SetComponentData(scenarioEntity, new ScenarioInfo
            {
                ScenarioId = scenarioId,
                Seed = (uint)math.max(0, _scenarioData.seed),
                RunTicks = (int)runTicks
            });
        }

        private string ResolveScenarioPath(out ScenarioInfo scenarioInfo, out bool hasScenarioInfo)
        {
            scenarioInfo = default;
            hasScenarioInfo = false;

            var envValue = SystemEnv.GetEnvironmentVariable(ScenarioPathEnv);
            if (!string.IsNullOrWhiteSpace(envValue))
            {
                var normalizedEnvPath = Path.GetFullPath(envValue);
                if (File.Exists(normalizedEnvPath))
                {
                    return normalizedEnvPath;
                }

                Debug.LogWarning($"[Space4XMiningScenario] {ScenarioPathEnv} was set to '{envValue}', but the file was not found. Falling back to ScenarioInfo.");
            }

            if (!SystemAPI.TryGetSingleton<ScenarioInfo>(out var info))
            {
                return null;
            }

            hasScenarioInfo = true;
            scenarioInfo = info;
            return FindScenarioPath(info.ScenarioId.ToString());
        }

        private string FindScenarioPath(string scenarioId)
        {
            if (string.IsNullOrWhiteSpace(scenarioId))
            {
                Debug.LogWarning("[Space4XMiningScenario] ScenarioId was empty; cannot resolve scenario file.");
                return null;
            }

            var normalizedScenarioId = NormalizeScenarioId(scenarioId);
            if (string.IsNullOrEmpty(normalizedScenarioId))
            {
                Debug.LogWarning("[Space4XMiningScenario] ScenarioId became empty after normalization.");
                return null;
            }

            if (File.Exists(scenarioId))
            {
                return scenarioId;
            }

            var possiblePaths = new[]
            {
                Path.Combine(Application.dataPath, "Scenarios", $"{normalizedScenarioId}{JsonExtension}"),
                Path.Combine(Application.dataPath, "..", "Assets", "Scenarios", $"{normalizedScenarioId}{JsonExtension}"),
                Path.Combine(Application.streamingAssetsPath, "Scenarios", $"{normalizedScenarioId}{JsonExtension}")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            Debug.LogWarning($"[Space4XMiningScenario] Unable to locate scenario '{scenarioId}' (normalized '{normalizedScenarioId}'). Checked: {string.Join(", ", possiblePaths)}");
            return null;
        }

        private static string NormalizeScenarioId(string scenarioId)
        {
            var trimmedId = scenarioId?.Trim();
            if (string.IsNullOrEmpty(trimmedId))
            {
                return null;
            }

            if (trimmedId.EndsWith(JsonExtension, StringComparison.OrdinalIgnoreCase))
            {
                return trimmedId.Substring(0, trimmedId.Length - JsonExtension.Length);
            }

            return trimmedId;
        }

        private void SpawnEntities(uint currentTick, float fixedDt)
        {
            foreach (var spawn in _scenarioData.spawn)
            {
                switch (spawn.kind)
                {
                    case "Carrier":
                        SpawnCarrier(spawn, currentTick, fixedDt);
                        break;
                    case "MiningVessel":
                        SpawnMiningVessel(spawn, currentTick);
                        break;
                    case "ResourceDeposit":
                        SpawnResourceDeposit(spawn);
                        break;
                }
            }
        }

        private void SpawnCarrier(MiningSpawnDefinition spawn, uint currentTick, float fixedDt)
        {
            var position = GetPosition(spawn.position);
            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            EntityManager.AddComponentData(entity, new PostTransformMatrix
            {
                Value = float4x4.Scale(new float3(0.6f, 0.4f, 6f))
            });
            EntityManager.AddComponent<SpatialIndexedTag>(entity);
            EntityManager.AddComponent<CommunicationModuleTag>(entity);
            EntityManager.AddComponentData(entity, MediumContext.Vacuum);

            var carrierId = new FixedString64Bytes(spawn.entityId ?? $"carrier-{_spawnedEntities.Count}");
            var carrierSpeed = 3f;
            var carrierAcceleration = 0.4f;
            var carrierDeceleration = 0.6f;
            var carrierTurnSpeed = 0.25f;
            var carrierSlowdown = 20f;
            var carrierArrival = 3f;
            var carrierRadius = 2.6f;
            var carrierRestitution = 0.08f;
            var carrierDamping = 0.25f;
            if (_useSmokeMotionTuning)
            {
                carrierSpeed = 7.2f;
                carrierAcceleration = 0.3f;
                carrierDeceleration = 0.45f;
                carrierTurnSpeed = 0.6f;
                carrierSlowdown = 30f;
                carrierArrival = 4f;
            }
            if (_isCollisionScenario)
            {
                carrierArrival = math.max(carrierArrival, 16f);
            }

            EntityManager.AddComponentData(entity, new Carrier
            {
                CarrierId = carrierId,
                AffiliationEntity = Entity.Null,
                Speed = carrierSpeed,
                Acceleration = carrierAcceleration,
                Deceleration = carrierDeceleration,
                TurnSpeed = carrierTurnSpeed,
                SlowdownDistance = carrierSlowdown,
                ArrivalDistance = carrierArrival,
                PatrolCenter = position,
                PatrolRadius = 50f
            });

            var isHostile = spawn.components?.Combat?.isHostile ?? false;
            var scenarioSide = (byte)(isHostile ? 1 : 0);
            EntityManager.AddComponentData(entity, new ScenarioSide
            {
                Side = scenarioSide
            });
            var law = isHostile ? -0.7f : 0.7f;
            EntityManager.AddComponentData(entity, AlignmentTriplet.FromFloats(law, 0f, 0f));
            AddScenarioAffiliation(entity, scenarioSide);
            var carrierDisposition = ResolveDisposition(spawn.disposition, BuildCarrierDisposition(spawn.components?.Combat, isHostile));
            if (carrierDisposition != EntityDispositionFlags.None)
            {
                EntityManager.AddComponentData(entity, new EntityDisposition
                {
                    Flags = carrierDisposition
                });
            }

            EnsureCarrierAuthorityAndCrew(entity, law, currentTick);

            EntityManager.AddComponentData(entity, new PatrolBehavior
            {
                CurrentWaypoint = float3.zero,
                WaitTime = 3f,
                WaitTimer = 0f
            });

            EntityManager.AddComponentData(entity, new MovementCommand
            {
                TargetPosition = float3.zero,
                ArrivalThreshold = 2f
            });

            EntityManager.AddComponentData(entity, new VesselMovement
            {
                Velocity = float3.zero,
                BaseSpeed = carrierSpeed,
                CurrentSpeed = 0f,
                Acceleration = carrierAcceleration,
                Deceleration = carrierDeceleration,
                TurnSpeed = carrierTurnSpeed,
                SlowdownDistance = carrierSlowdown,
                ArrivalDistance = carrierArrival,
                DesiredRotation = quaternion.identity,
                IsMoving = 0,
                LastMoveTick = 0
            });

            EntityManager.AddComponentData(entity, new VesselAIState
            {
                CurrentState = VesselAIState.State.Idle,
                CurrentGoal = VesselAIState.Goal.None,
                TargetEntity = Entity.Null,
                TargetPosition = float3.zero,
                StateTimer = 0f,
                StateStartTick = 0
            });

            EntityManager.AddComponentData(entity, new EntityIntent
            {
                Mode = IntentMode.Idle,
                TargetEntity = Entity.Null,
                TargetPosition = float3.zero,
                TriggeringInterrupt = InterruptType.None,
                IntentSetTick = 0,
                Priority = InterruptPriority.Low,
                IsValid = 0
            });

            EntityManager.AddBuffer<Interrupt>(entity);

            EntityManager.AddComponentData(entity, new VesselPhysicalProperties
            {
                Radius = carrierRadius,
                BaseMass = 120f,
                HullDensity = 1.2f,
                CargoMassPerUnit = 0.02f,
                Restitution = carrierRestitution,
                TangentialDamping = carrierDamping
            });

            AddScenarioPhysics(entity, carrierRadius, Space4XPhysicsLayer.Ship, carrierRestitution, carrierDamping);

            EntityManager.AddComponentData(entity, DockingCapacity.MiningCarrier);
            EntityManager.AddBuffer<DockedEntity>(entity);

            // Add ResourceStorage buffer
            var storageBuffer = EntityManager.AddBuffer<ResourceStorage>(entity);
            if (spawn.components?.ResourceStorage != null)
            {
                foreach (var storage in spawn.components.ResourceStorage)
                {
                    var resourceType = ParseResourceType(storage.type);
                    storageBuffer.Add(ResourceStorage.Create(resourceType, storage.capacity));
                }
            }
            else
            {
                // Default storage
                storageBuffer.Add(ResourceStorage.Create(ResourceType.Minerals, 10000f));
                storageBuffer.Add(ResourceStorage.Create(ResourceType.RareMetals, 10000f));
            }

            var fleetData = spawn.components?.Fleet;
            var combatData = spawn.components?.Combat;
            if (fleetData != null || combatData != null)
            {
                var fleetId = string.IsNullOrWhiteSpace(fleetData?.fleetId)
                    ? carrierId
                    : new FixedString64Bytes(fleetData.fleetId);

                var shipCount = fleetData?.shipCount > 0 ? fleetData.shipCount : 1;
                var posture = ParseFleetPosture(fleetData?.posture);

                EntityManager.AddComponentData(entity, new Space4XFleet
                {
                    FleetId = fleetId,
                    ShipCount = shipCount,
                    Posture = posture,
                    TaskForce = 0
                });

                EntityManager.AddComponentData(entity, new FleetMovementBroadcast
                {
                    Position = position,
                    Velocity = float3.zero,
                    LastUpdateTick = currentTick,
                    AllowsInterception = 1,
                    TechTier = 1
                });

                if (combatData != null && combatData.canIntercept)
                {
                    var interceptSpeed = combatData.interceptSpeed > 0f ? combatData.interceptSpeed : 10f;
                    EntityManager.AddComponentData(entity, new InterceptCapability
                    {
                        MaxSpeed = interceptSpeed,
                        TechTier = 1,
                        AllowIntercept = 1
                    });
                }
            }

            SpawnStrikeCraft(entity, position, combatData, scenarioSide);
            SpawnEscorts(entity, position, combatData, scenarioSide, currentTick, fixedDt);

            if (!string.IsNullOrEmpty(spawn.entityId))
            {
                _spawnedEntities[spawn.entityId] = entity;
            }
        }

        private void SpawnMiningVessel(MiningSpawnDefinition spawn, uint currentTick)
        {
            var position = GetPosition(spawn.position);
            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            EntityManager.AddComponent<SpatialIndexedTag>(entity);
            EntityManager.AddComponent<CommunicationModuleTag>(entity);
            EntityManager.AddComponentData(entity, MediumContext.Vacuum);

            // Find carrier entity
            Entity carrierEntity = Entity.Null;
            if (!string.IsNullOrEmpty(spawn.carrierId) && _spawnedEntities.TryGetValue(spawn.carrierId, out var carrier))
            {
                carrierEntity = carrier;
            }

            byte scenarioSide = 0;
            if (carrierEntity != Entity.Null && EntityManager.HasComponent<ScenarioSide>(carrierEntity))
            {
                scenarioSide = EntityManager.GetComponentData<ScenarioSide>(carrierEntity).Side;
            }

            EntityManager.AddComponentData(entity, new ScenarioSide
            {
                Side = scenarioSide
            });

            var law = scenarioSide == 1 ? -0.7f : 0.7f;
            EntityManager.AddComponentData(entity, AlignmentTriplet.FromFloats(law, 0f, 0f));
            AddScenarioAffiliation(entity, scenarioSide);

            var resourceId = new FixedString64Bytes(spawn.resourceId ?? "Minerals");
            var miningEfficiency = spawn.miningEfficiency > 0f ? spawn.miningEfficiency : 0.8f;
            var speed = spawn.speed > 0f ? spawn.speed : 5f;
            if (_useSmokeMotionTuning)
            {
                speed = math.max(1.8f, speed * 0.45f);
            }
            var cargoCapacity = spawn.cargoCapacity > 0f ? spawn.cargoCapacity : 100f;

            EntityManager.AddComponentData(entity, new MiningVessel
            {
                VesselId = new FixedString64Bytes(spawn.entityId ?? $"miner-{_spawnedEntities.Count}"),
                CarrierEntity = carrierEntity,
                MiningEfficiency = math.clamp(miningEfficiency, 0f, 1f),
                Speed = speed,
                CargoCapacity = cargoCapacity,
                CurrentCargo = 0f,
                CargoResourceType = ParseResourceType(spawn.resourceId ?? "Minerals")
            });

            var toolKind = ParseMiningToolKind(spawn.toolKind);
            EntityManager.AddComponentData(entity, new Space4XMiningToolProfile
            {
                ToolKind = toolKind,
                HasShapeOverride = spawn.toolShapeOverride || !string.IsNullOrWhiteSpace(spawn.toolShape) ? (byte)1 : (byte)0,
                Shape = ParseMiningToolShape(spawn.toolShape),
                RadiusOverride = spawn.toolRadiusOverride,
                RadiusMultiplier = spawn.toolRadiusMultiplier,
                StepLengthOverride = spawn.toolStepLengthOverride,
                StepLengthMultiplier = spawn.toolStepLengthMultiplier,
                DigUnitsPerMeterOverride = spawn.toolDigUnitsPerMeterOverride,
                MinStepLengthOverride = spawn.toolMinStepLengthOverride,
                MaxStepLengthOverride = spawn.toolMaxStepLengthOverride,
                YieldMultiplier = spawn.toolYieldMultiplier,
                HeatDeltaMultiplier = spawn.toolHeatDeltaMultiplier,
                InstabilityDeltaMultiplier = spawn.toolInstabilityDeltaMultiplier,
                DamageDeltaOverride = (byte)math.clamp(spawn.toolDamageDeltaOverride, 0, 255),
                DamageThresholdOverride = (byte)math.clamp(spawn.toolDamageThresholdOverride, 0, 255)
            });

            var vesselDisposition = ResolveDisposition(spawn.disposition, BuildMiningDisposition(scenarioSide));
            if (vesselDisposition != EntityDispositionFlags.None)
            {
                EntityManager.AddComponentData(entity, new EntityDisposition
                {
                    Flags = vesselDisposition
                });
            }

            var pilotProfile = FindIndividualProfile(spawn.pilotProfileId);
            var pilot = CreatePilotEntity(law, pilotProfile);
            EntityManager.AddComponentData(entity, new VesselPilotLink
            {
                Pilot = pilot
            });

            EntityManager.AddComponentData(entity, new MiningOrder
            {
                ResourceId = resourceId,
                Source = MiningOrderSource.Scripted,
                Status = MiningOrderStatus.Pending,
                PreferredTarget = Entity.Null,
                TargetEntity = Entity.Null,
                IssuedTick = 0
            });

            EntityManager.AddComponentData(entity, new MiningState
            {
                Phase = MiningPhase.Idle,
                ActiveTarget = Entity.Null,
                MiningTimer = 0f,
                TickInterval = 0.5f,
                PhaseTimer = 0f
            });

            EntityManager.AddComponentData(entity, new MiningYield
            {
                ResourceId = resourceId,
                PendingAmount = 0f,
                SpawnThreshold = math.max(1f, cargoCapacity * 0.25f),
                SpawnReady = 0
            });

            EntityManager.AddComponentData(entity, new VesselAIState
            {
                CurrentState = VesselAIState.State.Idle,
                CurrentGoal = VesselAIState.Goal.Mining,
                TargetEntity = Entity.Null,
                TargetPosition = float3.zero,
                StateTimer = 0f,
                StateStartTick = 0
            });

            var minerAcceleration = math.max(1f, speed * 0.8f);
            var minerDeceleration = math.max(1f, speed * 1.1f);
            var minerTurnSpeed = 2.4f;
            var minerSlowdown = 6f;
            var minerArrival = 1.5f;
            if (_useSmokeMotionTuning)
            {
                minerAcceleration = math.max(0.6f, speed * 0.6f);
                minerDeceleration = math.max(0.7f, speed * 0.8f);
                minerTurnSpeed = 1.2f;
                minerSlowdown = 10f;
                minerArrival = 1.8f;
            }

            EntityManager.AddComponentData(entity, new VesselMovement
            {
                Velocity = float3.zero,
                BaseSpeed = speed,
                CurrentSpeed = 0f,
                Acceleration = minerAcceleration,
                Deceleration = minerDeceleration,
                TurnSpeed = minerTurnSpeed,
                SlowdownDistance = minerSlowdown,
                ArrivalDistance = minerArrival,
                DesiredRotation = quaternion.identity,
                IsMoving = 0,
                LastMoveTick = 0
            });

            EntityManager.AddComponentData(entity, new VesselPhysicalProperties
            {
                Radius = 0.6f,
                BaseMass = 6f,
                HullDensity = 1.05f,
                CargoMassPerUnit = 0.04f,
                Restitution = 0.15f,
                TangentialDamping = 0.3f
            });

            AddScenarioPhysics(entity, 0.6f, Space4XPhysicsLayer.Miner, 0.15f, 0.3f);

            EntityManager.AddBuffer<SpawnResourceRequest>(entity);

            if (spawn.startDocked && carrierEntity != Entity.Null)
            {
                EntityManager.AddComponentData(entity, new DockingRequest
                {
                    TargetCarrier = carrierEntity,
                    RequiredSlot = DockingSlotType.Utility,
                    RequestTick = currentTick,
                    Priority = 0
                });
            }

            if (!string.IsNullOrEmpty(spawn.entityId))
            {
                _spawnedEntities[spawn.entityId] = entity;
            }
        }

        private void SpawnResourceDeposit(MiningSpawnDefinition spawn)
        {
            var position = GetPosition(spawn.position);
            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            EntityManager.AddComponent<SpatialIndexedTag>(entity);

            var resourceId = new FixedString64Bytes(spawn.resourceId ?? "Minerals");
            var resourceType = ParseResourceType(spawn.resourceId ?? "Minerals");
            var unitsRemaining = spawn.unitsRemaining > 0f ? spawn.unitsRemaining : 1000f;
            var gatherRate = spawn.gatherRatePerWorker > 0f ? spawn.gatherRatePerWorker : 10f;
            var maxWorkers = spawn.maxSimultaneousWorkers > 0 ? spawn.maxSimultaneousWorkers : 3;

            // Add Asteroid component for registry registration
            var asteroidId = spawn.entityId ?? $"asteroid-{_spawnedEntities.Count}";
            EntityManager.AddComponentData(entity, new Asteroid
            {
                AsteroidId = new FixedString64Bytes(asteroidId),
                ResourceAmount = unitsRemaining,
                MaxResourceAmount = unitsRemaining,
                ResourceType = resourceType,
                MiningRate = gatherRate
            });

            EntityManager.AddComponentData(entity, new ResourceSourceState
            {
                UnitsRemaining = unitsRemaining,
                LastHarvestTick = 0
            });

            EntityManager.AddComponentData(entity, new ResourceSourceConfig
            {
                GatherRatePerWorker = gatherRate,
                MaxSimultaneousWorkers = (ushort)maxWorkers,
                RespawnSeconds = 0f,
                Flags = 0
            });

            EntityManager.AddComponentData(entity, new ResourceTypeId
            {
                Value = resourceId
            });

            // Add rewind support
            EntityManager.AddComponent<RewindableTag>(entity);
            EntityManager.AddComponentData(entity, new LastRecordedTick { Tick = 0 });
            EntityManager.AddComponentData(entity, new HistoryTier
            {
                Tier = HistoryTier.TierType.LowVisibility,
                OverrideStrideSeconds = 0f
            });
            EntityManager.AddBuffer<ResourceHistorySample>(entity);
            EntityManager.AddBuffer<Space4XMiningLatchReservation>(entity);

            var volumeConfig = Space4XAsteroidVolumeConfig.Default;
            volumeConfig.Radius = math.max(0.1f, volumeConfig.Radius);
            EntityManager.AddComponentData(entity, volumeConfig);

            EntityManager.AddComponentData(entity, new Space4XAsteroidCenter
            {
                Position = position
            });

            AddScenarioPhysics(entity, volumeConfig.Radius, Space4XPhysicsLayer.Asteroid, 0.05f, 0.1f);

            if (!string.IsNullOrEmpty(spawn.entityId))
            {
                _spawnedEntities[spawn.entityId] = entity;
            }
        }

        private void ScheduleScenarioActions(Entity runtimeEntity, uint startTick, float fixedDt)
        {
            if (_scenarioData.actions == null || _scenarioData.actions.Count == 0)
            {
                return;
            }

            if (EntityManager.HasBuffer<Space4XScenarioAction>(runtimeEntity))
            {
                return;
            }

            var actionsBuffer = EntityManager.AddBuffer<Space4XScenarioAction>(runtimeEntity);
            var safeDt = math.max(1e-6f, fixedDt);

            foreach (var action in _scenarioData.actions)
            {
                if (action == null || string.IsNullOrWhiteSpace(action.kind))
                {
                    continue;
                }

                var kind = ParseScenarioActionKind(action.kind);
                var executeTick = startTick + (uint)math.ceil(math.max(0f, action.time_s) / safeDt);
                var targetPosition = GetPosition(action.targetPosition);

                actionsBuffer.Add(new Space4XScenarioAction
                {
                    ExecuteTick = executeTick,
                    Kind = kind,
                    FleetId = new FixedString64Bytes(action.fleetId ?? action.requesterFleetId ?? string.Empty),
                    TargetFleetId = new FixedString64Bytes(action.targetFleetId ?? string.Empty),
                    TargetPosition = targetPosition,
                    Executed = 0
                });
            }
        }

        private void EnsureCollisionScenarioTag()
        {
            if (SystemAPI.HasSingleton<Space4XCollisionScenarioTag>())
            {
                return;
            }

            var entity = EntityManager.CreateEntity(typeof(Space4XCollisionScenarioTag));
            EntityManager.SetName(entity, "Space4XCollisionScenarioTag");
        }

        private void SpawnStrikeCraft(Entity carrierEntity, float3 carrierPosition, CombatComponentData combatData, byte scenarioSide)
        {
            if (combatData == null || combatData.strikeCraftCount <= 0)
            {
                return;
            }

            var role = ParseStrikeCraftRole(combatData.strikeCraftRole);
            var craftSpeed = 12f;
            var weaponDamage = 10f;
            var weaponRange = 20f;
            var hull = HullIntegrity.LightCraft;

            switch (role)
            {
                case StrikeCraftRole.Bomber:
                    craftSpeed = 9f;
                    weaponDamage = 50f;
                    weaponRange = 18f;
                    hull = HullIntegrity.Create(80f, 0.15f);
                    break;
                case StrikeCraftRole.Interceptor:
                    craftSpeed = 14f;
                    weaponDamage = 12f;
                    weaponRange = 22f;
                    hull = HullIntegrity.Create(40f, 0.1f);
                    break;
                case StrikeCraftRole.Suppression:
                    craftSpeed = 10f;
                    weaponDamage = 20f;
                    weaponRange = 25f;
                    hull = HullIntegrity.Create(120f, 0.2f);
                    break;
                case StrikeCraftRole.Recon:
                    craftSpeed = 15f;
                    weaponDamage = 6f;
                    weaponRange = 24f;
                    hull = HullIntegrity.Create(35f, 0.05f);
                    break;
            }

            for (int i = 0; i < combatData.strikeCraftCount; i++)
            {
                var entity = EntityManager.CreateEntity();
                var offset = new float3(2f * (i + 1), 0f, 2f * (i + 1));
                EntityManager.AddComponentData(entity, LocalTransform.FromPositionRotationScale(carrierPosition + offset, quaternion.identity, 1f));
                EntityManager.AddComponent<CommunicationModuleTag>(entity);
                EntityManager.AddComponentData(entity, MediumContext.Vacuum);
                EntityManager.AddComponentData(entity, hull);
                EnsureDefaultSubsystems(entity, hull.Max);
                var weaponBuffer = EntityManager.AddBuffer<WeaponMount>(entity);
                weaponBuffer.Add(new WeaponMount
                {
                    Weapon = new Space4XWeapon
                    {
                        Type = WeaponType.Laser,
                        Size = WeaponSize.Small,
                        BaseDamage = weaponDamage,
                        OptimalRange = weaponRange * 0.8f,
                        MaxRange = weaponRange,
                        BaseAccuracy = (half)0.85f,
                        CooldownTicks = 1,
                        CurrentCooldown = 0,
                        AmmoPerShot = 0,
                        ShieldModifier = (half)1f,
                        ArmorPenetration = (half)0.3f
                    },
                    CurrentTarget = Entity.Null,
                    IsEnabled = 1
                });
                EntityManager.AddComponentData(entity, StrikeCraftProfile.Create(role, carrierEntity));
                EntityManager.AddComponentData(entity, new StrikeCraftState
                {
                    CurrentState = StrikeCraftState.State.Approaching,
                    TargetEntity = Entity.Null,
                    TargetPosition = carrierPosition + offset,
                    Experience = 0f,
                    StateStartTick = 0,
                    KamikazeActive = 0,
                    KamikazeStartTick = 0,
                    DogfightPhase = StrikeCraftDogfightPhase.Approach,
                    DogfightPhaseStartTick = 0,
                    DogfightLastFireTick = 0,
                    DogfightWingLeader = Entity.Null
                });
                EntityManager.AddComponent<StrikeCraftDogfightTag>(entity);
                EntityManager.AddComponentData(entity, AttackRunConfig.ForRole(role));
                EntityManager.AddComponentData(entity, StrikeCraftExperience.Rookie);
                EntityManager.AddComponentData(entity, new ScenarioSide
                {
                    Side = scenarioSide
                });
                EntityManager.AddComponentData(entity, new VesselMovement
                {
                    BaseSpeed = craftSpeed,
                    CurrentSpeed = 0f,
                    Velocity = float3.zero,
                    TurnSpeed = 4.5f,
                    DesiredRotation = quaternion.identity,
                    IsMoving = 0,
                    LastMoveTick = 0
                });
                EntityManager.AddComponentData(entity, SupplyStatus.DefaultStrikeCraft);
                var law = scenarioSide == 1 ? -0.7f : 0.7f;
                EntityManager.AddComponentData(entity, AlignmentTriplet.FromFloats(law, 0f, 0f));
                AddScenarioAffiliation(entity, scenarioSide);
                var craftDisposition = EntityDispositionFlags.Combatant | EntityDispositionFlags.Military;
                if (scenarioSide == 1)
                {
                    craftDisposition |= EntityDispositionFlags.Hostile;
                }
                EntityManager.AddComponentData(entity, new EntityDisposition
                {
                    Flags = craftDisposition
                });
                var pilotProfile = ResolvePilotProfile(combatData, i);
                var pilot = CreatePilotEntity(law, pilotProfile);
                EntityManager.AddComponentData(entity, new StrikeCraftPilotLink
                {
                    Pilot = pilot
                });
            }
        }

        private IndividualProfileData ResolvePilotProfile(CombatComponentData combatData, int index)
        {
            if (combatData == null || _scenarioData == null || _scenarioData.individuals == null)
            {
                return null;
            }

            string pilotId = null;
            if (combatData.pilotProfileIds != null && combatData.pilotProfileIds.Count > 0)
            {
                pilotId = combatData.pilotProfileIds[index % combatData.pilotProfileIds.Count];
            }
            else if (!string.IsNullOrWhiteSpace(combatData.pilotProfileId))
            {
                pilotId = combatData.pilotProfileId;
            }

            if (string.IsNullOrWhiteSpace(pilotId))
            {
                return null;
            }
            return FindIndividualProfile(pilotId);
        }

        private Entity CreatePilotEntity(float lawfulness, IndividualProfileData profileData)
        {
            var config = StrikeCraftPilotProfileConfig.Default;
            if (SystemAPI.TryGetSingleton<StrikeCraftPilotProfileConfig>(out var configSingleton))
            {
                config = configSingleton;
            }

            var pilot = EntityManager.CreateEntity();
            if (profileData != null)
            {
                EntityManager.AddComponentData(pilot, AlignmentTriplet.FromFloats(
                    math.clamp(profileData.law, -1f, 1f),
                    math.clamp(profileData.good, -1f, 1f),
                    math.clamp(profileData.integrity, -1f, 1f)));
                if (profileData.raceId > 0)
                {
                    EntityManager.AddComponentData(pilot, new RaceId { Value = profileData.raceId });
                }
                if (profileData.cultureId > 0)
                {
                    EntityManager.AddComponentData(pilot, new CultureId { Value = profileData.cultureId });
                }
            }
            else
            {
                EntityManager.AddComponentData(pilot, AlignmentTriplet.FromFloats(lawfulness, 0f, 0f));
            }

            EntityManager.AddBuffer<OutlookEntry>(pilot);
            EntityManager.AddBuffer<TopOutlook>(pilot);
            var outlookEntries = EntityManager.GetBuffer<OutlookEntry>(pilot);
            var outlooks = EntityManager.GetBuffer<TopOutlook>(pilot);
            if (profileData != null && profileData.outlooks != null && profileData.outlooks.Count > 0)
            {
                for (int i = 0; i < profileData.outlooks.Count; i++)
                {
                    var entry = profileData.outlooks[i];
                    outlookEntries.Add(new OutlookEntry
                    {
                        OutlookId = ParseOutlookId(entry.outlookId),
                        Weight = (half)math.clamp(entry.weight, -1f, 1f)
                    });
                }

                var ordered = profileData.outlooks
                    .OrderByDescending(o => o.weight)
                    .Take(3);
                foreach (var entry in ordered)
                {
                    outlooks.Add(new TopOutlook
                    {
                        OutlookId = ParseOutlookId(entry.outlookId),
                        Weight = (half)math.clamp(entry.weight, 0f, 1f)
                    });
                }
            }
            else
            {
                var outlookId = config.NeutralOutlook;
                if (lawfulness >= config.LoyalistLawThreshold)
                {
                    outlookId = config.FriendlyOutlook;
                }
                else if (lawfulness <= config.MutinousLawThreshold)
                {
                    outlookId = config.HostileOutlook;
                }

                outlookEntries.Add(new OutlookEntry
                {
                    OutlookId = outlookId,
                    Weight = (half)1f
                });
                outlooks.Add(new TopOutlook
                {
                    OutlookId = outlookId,
                    Weight = (half)1f
                });
            }

            var dispositionData = profileData?.behaviorDisposition;
            if (dispositionData != null && dispositionData.enabled)
            {
                EntityManager.AddComponentData(pilot, BehaviorDisposition.FromValues(
                    dispositionData.compliance,
                    dispositionData.caution,
                    dispositionData.formationAdherence,
                    dispositionData.riskTolerance,
                    dispositionData.aggression,
                    dispositionData.patience));
            }
            else
            {
                EntityManager.AddComponentData(pilot, new BehaviorDispositionSeedRequest
                {
                    Seed = 0u,
                    SeedSalt = 0u
                });
            }

            return pilot;
        }

        private void SpawnEscorts(Entity carrierEntity, float3 carrierPosition, CombatComponentData combatData, byte scenarioSide, uint currentTick, float fixedDt)
        {
            if (combatData == null || combatData.escortCount <= 0)
            {
                return;
            }

            var releaseSeconds = combatData.escortRelease_s > 0f ? combatData.escortRelease_s : 30f;
            var releaseTicks = (uint)math.ceil(releaseSeconds / math.max(1e-6f, fixedDt));
            var releaseTick = currentTick + math.max(1u, releaseTicks);

            for (int i = 0; i < combatData.escortCount; i++)
            {
                var entity = EntityManager.CreateEntity();
                var offset = new float3(3f * (i + 1), 0f, -3f * (i + 1));
                EntityManager.AddComponentData(entity, LocalTransform.FromPositionRotationScale(carrierPosition + offset, quaternion.identity, 1f));
                EntityManager.AddComponent<SpatialIndexedTag>(entity);
                EntityManager.AddComponent<CommunicationModuleTag>(entity);
                EntityManager.AddComponentData(entity, MediumContext.Vacuum);

                EntityManager.AddComponentData(entity, new VesselMovement
                {
                    Velocity = float3.zero,
                    BaseSpeed = 6f,
                    CurrentSpeed = 0f,
                    DesiredRotation = quaternion.identity,
                    IsMoving = 0,
                    LastMoveTick = currentTick
                });

                EntityManager.AddComponentData(entity, new VesselAIState
                {
                    CurrentState = VesselAIState.State.MovingToTarget,
                    CurrentGoal = VesselAIState.Goal.Escort,
                    TargetEntity = carrierEntity,
                    TargetPosition = float3.zero,
                    StateTimer = 0f,
                    StateStartTick = currentTick
                });

                EntityManager.AddComponentData(entity, new ChildVesselTether
                {
                    ParentCarrier = carrierEntity,
                    MaxTetherRange = 45f,
                    CanPatrol = 0
                });

                EntityManager.AddComponentData(entity, new EscortAssignment
                {
                    Target = carrierEntity,
                    AssignedTick = currentTick,
                    ReleaseTick = releaseTick,
                    Released = 0
                });

                EntityManager.AddComponentData(entity, new ScenarioSide
                {
                    Side = scenarioSide
                });
                var law = scenarioSide == 1 ? -0.7f : 0.7f;
                EntityManager.AddComponentData(entity, AlignmentTriplet.FromFloats(law, 0f, 0f));
                AddScenarioAffiliation(entity, scenarioSide);
                var escortDisposition = EntityDispositionFlags.Combatant | EntityDispositionFlags.Military;
                if (scenarioSide == 1)
                {
                    escortDisposition |= EntityDispositionFlags.Hostile;
                }
                EntityManager.AddComponentData(entity, new EntityDisposition
                {
                    Flags = escortDisposition
                });
            }
        }

        private void EnsureCarrierAuthorityAndCrew(Entity carrierEntity, float lawfulness, uint currentTick)
        {
            EntityManager.AddComponentData(carrierEntity, new CaptainOrder
            {
                Type = CaptainOrderType.None,
                Status = CaptainOrderStatus.None,
                Priority = 0,
                TargetEntity = Entity.Null,
                TargetPosition = float3.zero,
                IssuedTick = currentTick,
                TimeoutTick = 0,
                IssuingAuthority = Entity.Null
            });
            EntityManager.AddComponentData(carrierEntity, CaptainState.Default);
            EntityManager.AddComponentData(carrierEntity, CaptainReadiness.Standard);

            var config = StrikeCraftPilotProfileConfig.Default;
            if (SystemAPI.TryGetSingleton<StrikeCraftPilotProfileConfig>(out var configSingleton))
            {
                config = configSingleton;
            }

            var crewEntities = new Entity[6];
            crewEntities[0] = CreateCrewEntity(lawfulness, config,
                new IndividualStats
                {
                    Command = (half)90,
                    Tactics = (half)70,
                    Logistics = (half)60,
                    Diplomacy = (half)60,
                    Engineering = (half)40,
                    Resolve = (half)85
                },
                BehaviorDisposition.FromValues(0.8f, 0.6f, 0.8f, 0.4f, 0.45f, 0.7f));

            crewEntities[1] = CreateCrewEntity(lawfulness, config,
                new IndividualStats
                {
                    Command = (half)75,
                    Tactics = (half)55,
                    Logistics = (half)80,
                    Diplomacy = (half)50,
                    Engineering = (half)45,
                    Resolve = (half)70
                },
                BehaviorDisposition.FromValues(0.75f, 0.6f, 0.7f, 0.45f, 0.4f, 0.7f));

            crewEntities[2] = CreateCrewEntity(lawfulness, config,
                new IndividualStats
                {
                    Command = (half)65,
                    Tactics = (half)80,
                    Logistics = (half)50,
                    Diplomacy = (half)45,
                    Engineering = (half)40,
                    Resolve = (half)60
                },
                BehaviorDisposition.FromValues(0.65f, 0.55f, 0.7f, 0.5f, 0.45f, 0.6f));

            crewEntities[3] = CreateCrewEntity(lawfulness, config,
                new IndividualStats
                {
                    Command = (half)55,
                    Tactics = (half)75,
                    Logistics = (half)45,
                    Diplomacy = (half)60,
                    Engineering = (half)45,
                    Resolve = (half)55
                },
                BehaviorDisposition.FromValues(0.6f, 0.7f, 0.55f, 0.35f, 0.35f, 0.65f));

            crewEntities[4] = CreateCrewEntity(lawfulness, config,
                new IndividualStats
                {
                    Command = (half)70,
                    Tactics = (half)70,
                    Logistics = (half)55,
                    Diplomacy = (half)50,
                    Engineering = (half)45,
                    Resolve = (half)60
                },
                BehaviorDisposition.FromValues(0.7f, 0.55f, 0.65f, 0.5f, 0.6f, 0.55f));

            crewEntities[5] = CreateCrewEntity(lawfulness, config,
                new IndividualStats
                {
                    Command = (half)55,
                    Tactics = (half)55,
                    Logistics = (half)60,
                    Diplomacy = (half)45,
                    Engineering = (half)85,
                    Resolve = (half)55
                },
                BehaviorDisposition.FromValues(0.65f, 0.7f, 0.5f, 0.4f, 0.3f, 0.75f));

            var crew = EntityManager.AddBuffer<PlatformCrewMember>(carrierEntity);
            for (int i = 0; i < crewEntities.Length; i++)
            {
                crew.Add(new PlatformCrewMember
                {
                    CrewEntity = crewEntities[i],
                    RoleId = 0
                });
            }
        }

        private Entity CreateCrewEntity(
            float lawfulness,
            in StrikeCraftPilotProfileConfig config,
            in IndividualStats stats,
            in BehaviorDisposition disposition)
        {
            var crew = EntityManager.CreateEntity();
            EntityManager.AddComponentData(crew, AlignmentTriplet.FromFloats(lawfulness, 0f, 0f));
            EntityManager.AddComponentData(crew, stats);
            EntityManager.AddComponentData(crew, disposition);

            var outlookId = ResolveOutlookId(config, lawfulness);
            EntityManager.AddBuffer<OutlookEntry>(crew);
            EntityManager.AddBuffer<TopOutlook>(crew);
            var outlookEntries = EntityManager.GetBuffer<OutlookEntry>(crew);
            var outlooks = EntityManager.GetBuffer<TopOutlook>(crew);
            outlookEntries.Add(new OutlookEntry
            {
                OutlookId = outlookId,
                Weight = (half)1f
            });
            outlooks.Add(new TopOutlook
            {
                OutlookId = outlookId,
                Weight = (half)1f
            });

            return crew;
        }

        private static OutlookId ResolveOutlookId(in StrikeCraftPilotProfileConfig config, float lawfulness)
        {
            if (lawfulness >= config.LoyalistLawThreshold)
            {
                return config.FriendlyOutlook;
            }

            if (lawfulness <= config.MutinousLawThreshold)
            {
                return config.HostileOutlook;
            }

            return config.NeutralOutlook;
        }

        private float3 GetPosition(float[] position)
        {
            if (position != null && position.Length >= 2)
            {
                float x = position[0];
                float z = position[1];
                float y = position.Length > 2 ? position[2] : ResolveSpawnHeight(x, z);
                return new float3(x, y, z);
            }
            // Deterministic fallback to keep spawns separated when position data is missing.
            var index = _spawnedEntities?.Count ?? 0;
            var angle = math.radians(137.5f * index);
            var radius = 20f + (index % 5) * 5f;
            var xFallback = math.cos(angle) * radius;
            var zFallback = math.sin(angle) * radius;
            var yFallback = ResolveSpawnHeight(xFallback, zFallback);
            return new float3(xFallback, yFallback, zFallback);
        }

        private static float ResolveSpawnHeight(float x, float z)
        {
            uint hash = (uint)math.hash(new float2(x, z));
            float normalized = hash / (float)uint.MaxValue;
            return (normalized * 2f - 1f) * DefaultSpawnVerticalRange;
        }

        private static Space4XFleetPosture ParseFleetPosture(string posture)
        {
            return posture switch
            {
                "Engaging" => Space4XFleetPosture.Engaging,
                "Retreating" => Space4XFleetPosture.Retreating,
                _ => Space4XFleetPosture.Patrol
            };
        }

        private static StrikeCraftRole ParseStrikeCraftRole(string role)
        {
            return role switch
            {
                "Interceptor" => StrikeCraftRole.Interceptor,
                "Bomber" => StrikeCraftRole.Bomber,
                "Recon" => StrikeCraftRole.Recon,
                "Suppression" => StrikeCraftRole.Suppression,
                "EWar" => StrikeCraftRole.EWar,
                _ => StrikeCraftRole.Fighter
            };
        }

        private static Space4XScenarioActionKind ParseScenarioActionKind(string kind)
        {
            return kind switch
            {
                "TriggerIntercept" => Space4XScenarioActionKind.TriggerIntercept,
                _ => Space4XScenarioActionKind.MoveFleet
            };
        }

        private ResourceType ParseResourceType(string type)
        {
            return type switch
            {
                "Minerals" => ResourceType.Minerals,
                "RareMetals" => ResourceType.RareMetals,
                "EnergyCrystals" => ResourceType.EnergyCrystals,
                "OrganicMatter" => ResourceType.OrganicMatter,
                _ => ResourceType.Minerals
            };
        }

        private static OutlookId ParseOutlookId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return OutlookId.Neutral;
            }

            return value switch
            {
                "Loyalist" => OutlookId.Loyalist,
                "Opportunist" => OutlookId.Opportunist,
                "Fanatic" => OutlookId.Fanatic,
                "Mutinous" => OutlookId.Mutinous,
                _ => OutlookId.Neutral
            };
        }

        private IndividualProfileData FindIndividualProfile(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId) || _scenarioData == null || _scenarioData.individuals == null)
            {
                return null;
            }

            for (int i = 0; i < _scenarioData.individuals.Count; i++)
            {
                var profile = _scenarioData.individuals[i];
                if (profile != null && string.Equals(profile.id, profileId, StringComparison.OrdinalIgnoreCase))
                {
                    return profile;
                }
            }

            return null;
        }

        private static EntityDispositionFlags ResolveDisposition(string rawValue, EntityDispositionFlags fallback)
        {
            return TryParseDisposition(rawValue, out var parsed) ? parsed : fallback;
        }

        private static bool TryParseDisposition(string rawValue, out EntityDispositionFlags flags)
        {
            flags = EntityDispositionFlags.None;
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return false;
            }

            var trimmed = rawValue.Trim();
            if (string.Equals(trimmed, "None", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (ushort.TryParse(trimmed, out var numeric))
            {
                flags = (EntityDispositionFlags)numeric;
                return true;
            }

            var tokens = trimmed.Split(new[] { ',', '|', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var matched = false;
            for (int i = 0; i < tokens.Length; i++)
            {
                var token = tokens[i];
                if (string.Equals(token, "Civilian", StringComparison.OrdinalIgnoreCase))
                {
                    flags |= EntityDispositionFlags.Civilian;
                    matched = true;
                }
                else if (string.Equals(token, "Trader", StringComparison.OrdinalIgnoreCase))
                {
                    flags |= EntityDispositionFlags.Trader;
                    matched = true;
                }
                else if (string.Equals(token, "Combatant", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(token, "Combat", StringComparison.OrdinalIgnoreCase))
                {
                    flags |= EntityDispositionFlags.Combatant;
                    matched = true;
                }
                else if (string.Equals(token, "Hostile", StringComparison.OrdinalIgnoreCase))
                {
                    flags |= EntityDispositionFlags.Hostile;
                    matched = true;
                }
                else if (string.Equals(token, "Military", StringComparison.OrdinalIgnoreCase))
                {
                    flags |= EntityDispositionFlags.Military;
                    matched = true;
                }
                else if (string.Equals(token, "Mining", StringComparison.OrdinalIgnoreCase))
                {
                    flags |= EntityDispositionFlags.Mining;
                    matched = true;
                }
                else if (string.Equals(token, "Hauler", StringComparison.OrdinalIgnoreCase))
                {
                    flags |= EntityDispositionFlags.Hauler;
                    matched = true;
                }
                else if (string.Equals(token, "Support", StringComparison.OrdinalIgnoreCase))
                {
                    flags |= EntityDispositionFlags.Support;
                    matched = true;
                }
            }

            return matched;
        }

        private static EntityDispositionFlags BuildCarrierDisposition(CombatComponentData combatData, bool isHostile)
        {
            var flags = EntityDispositionFlags.Support;
            var hasCombat = isHostile ||
                            (combatData != null &&
                             (combatData.canIntercept || combatData.strikeCraftCount > 0 || combatData.escortCount > 0));
            if (hasCombat)
            {
                flags |= EntityDispositionFlags.Combatant | EntityDispositionFlags.Military;
            }
            else
            {
                flags |= EntityDispositionFlags.Civilian;
            }

            if (isHostile)
            {
                flags |= EntityDispositionFlags.Hostile;
            }

            return flags;
        }

        private static EntityDispositionFlags BuildMiningDisposition(byte scenarioSide)
        {
            var flags = EntityDispositionFlags.Mining | EntityDispositionFlags.Civilian;
            if (scenarioSide == 1)
            {
                flags |= EntityDispositionFlags.Hostile;
            }

            return flags;
        }

        private static TerrainModificationToolKind ParseMiningToolKind(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return TerrainModificationToolKind.Drill;
            }

            if (value.Equals("laser", StringComparison.OrdinalIgnoreCase))
            {
                return TerrainModificationToolKind.Laser;
            }

            if (value.Equals("microwave", StringComparison.OrdinalIgnoreCase))
            {
                return TerrainModificationToolKind.Microwave;
            }

            if (value.Equals("drill", StringComparison.OrdinalIgnoreCase))
            {
                return TerrainModificationToolKind.Drill;
            }

            return TerrainModificationToolKind.Drill;
        }

        private static TerrainModificationShape ParseMiningToolShape(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return TerrainModificationShape.Brush;
            }

            if (value.Equals("tunnel", StringComparison.OrdinalIgnoreCase))
            {
                return TerrainModificationShape.Tunnel;
            }

            if (value.Equals("ramp", StringComparison.OrdinalIgnoreCase))
            {
                return TerrainModificationShape.Ramp;
            }

            if (value.Equals("brush", StringComparison.OrdinalIgnoreCase))
            {
                return TerrainModificationShape.Brush;
            }

            return TerrainModificationShape.Brush;
        }
    }

    [System.Serializable]
    public class MiningScenarioJson
    {
        public int seed;
        public float duration_s;
        public StrikeCraftDogfightConfigData dogfightConfig;
        public StanceTuningConfigData stanceConfig;
        public List<MiningSpawnDefinition> spawn;
        public List<MiningScenarioAction> actions;
        public MiningTelemetryExpectations telemetryExpectations;
        public List<IndividualProfileData> individuals;
    }

    [System.Serializable]
    public class StrikeCraftDogfightConfigData
    {
        public float acquireRadius;
        public float coneDegrees;
        public float navConstantN;
        public float breakOffDistance;
        public int breakOffTicks;
        public float rejoinRadius;
        public float[] rejoinOffset;
        public float jinkStrength;
    }

    [System.Serializable]
    public class StanceTuningConfigData
    {
        public StanceTuningEntryData aggressive;
        public StanceTuningEntryData balanced;
        public StanceTuningEntryData defensive;
        public StanceTuningEntryData evasive;
    }

    [System.Serializable]
    public class StanceTuningEntryData
    {
        public float avoidanceRadius;
        public float avoidanceStrength;
        public float speedMultiplier;
        public float rotationMultiplier;
        public float maintainFormationWhenAttacking;
        public float evasionJinkStrength;
        public float autoEngageRadius;
        public float abortAttackOnDamageThreshold;
        public bool returnToPatrolAfterCombat;
        public bool commandOverrideDropsToNeutral;
        public float attackMoveBearingWeight;
        public float attackMoveDestinationWeight;
    }

    [System.Serializable]
    public class MiningScenarioAction
    {
        public float time_s;
        public string kind;
        public string fleetId;
        public float[] targetPosition;
        public string requesterFleetId;
        public string targetFleetId;
        public string description;
    }

    [System.Serializable]
    public class MiningSpawnDefinition
    {
        public string kind;
        public string entityId;
        public float[] position;
        public string carrierId;
        public string toolKind;
        public string toolShape;
        public bool toolShapeOverride;
        public float toolRadiusOverride;
        public float toolRadiusMultiplier;
        public float toolStepLengthOverride;
        public float toolStepLengthMultiplier;
        public float toolDigUnitsPerMeterOverride;
        public float toolMinStepLengthOverride;
        public float toolMaxStepLengthOverride;
        public float toolYieldMultiplier;
        public float toolHeatDeltaMultiplier;
        public float toolInstabilityDeltaMultiplier;
        public int toolDamageDeltaOverride;
        public int toolDamageThresholdOverride;
        public string resourceId;
        public float miningEfficiency;
        public float speed;
        public float cargoCapacity;
        public float unitsRemaining;
        public float gatherRatePerWorker;
        public int maxSimultaneousWorkers;
        public bool startDocked;
        public string pilotProfileId;
        public string disposition;
        public MiningComponentData components;
    }

    [System.Serializable]
    public class MiningComponentData
    {
        public List<ResourceStorageData> ResourceStorage;
        public FleetComponentData Fleet;
        public CombatComponentData Combat;
    }

    [System.Serializable]
    public class FleetComponentData
    {
        public string fleetId;
        public string posture;
        public int shipCount;
    }

    [System.Serializable]
    public class CombatComponentData
    {
        public bool canIntercept;
        public float interceptSpeed;
        public bool isHostile;
        public int strikeCraftCount;
        public string strikeCraftRole;
        public int escortCount;
        public float escortRelease_s;
        public string pilotProfileId;
        public List<string> pilotProfileIds;
    }

    [System.Serializable]
    public class IndividualProfileData
    {
        public string id;
        public float law;
        public float good;
        public float integrity;
        public ushort raceId;
        public ushort cultureId;
        public List<OutlookWeightData> outlooks;
        public BehaviorDispositionData behaviorDisposition;
    }

    [System.Serializable]
    public class BehaviorDispositionData
    {
        public bool enabled;
        public float compliance;
        public float caution;
        public float formationAdherence;
        public float riskTolerance;
        public float aggression;
        public float patience;
    }

    [System.Serializable]
    public class OutlookWeightData
    {
        public string outlookId;
        public float weight;
    }

    [System.Serializable]
    public class ResourceStorageData
    {
        public string type;
        public float capacity;
    }

    [System.Serializable]
    public class MiningTelemetryExpectations
    {
        public bool expectMiningYield;
        public bool expectCarrierPickup;
        public bool expectInterceptAttempts;
        public bool expectFleetRegistry;
        public bool expectResourceRegistry;
        public MiningTelemetryExport export;
    }

    [System.Serializable]
    public class MiningTelemetryExport
    {
        public string csv;
        public string json;
    }
}
