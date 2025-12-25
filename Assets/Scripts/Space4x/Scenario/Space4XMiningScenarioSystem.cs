using System;
using System.Collections.Generic;
using System.IO;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Modularity;
using PureDOTS.Runtime.Perception;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Systems;
using Space4X.Registry;
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
        private bool _hasLoaded;
        private MiningScenarioJson _scenarioData;
        private Dictionary<string, Entity> _spawnedEntities;

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

            var jsonText = File.ReadAllText(scenarioPath);
            _scenarioData = JsonUtility.FromJson<MiningScenarioJson>(jsonText);
            if (_scenarioData == null || _scenarioData.spawn == null)
            {
                Debug.LogError("[Space4XMiningScenario] Failed to parse scenario JSON");
                Enabled = false;
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();

            _spawnedEntities = new Dictionary<string, Entity>();
            SpawnEntities(timeState.Tick, timeState.FixedDeltaTime);
            var fixedDt = math.max(1e-6f, timeState.FixedDeltaTime);
            var durationSeconds = math.max(0f, _scenarioData.duration_s);
            var durationTicks = (uint)math.ceil(durationSeconds / fixedDt);
            var startTick = timeState.Tick;
            var safeDurationTicks = durationTicks == 0 ? 1u : durationTicks;
            var endTick = startTick + safeDurationTicks;
            var runtimeEntity = EnsureScenarioRuntime(startTick, endTick, durationSeconds);
            ScheduleScenarioActions(runtimeEntity, startTick, fixedDt);

            Debug.Log($"[Space4XMiningScenario] Loaded '{scenarioPath}'. Spawned carriers/miners/asteroids. Duration={durationSeconds:F1}s ticks={safeDurationTicks} (startTick={startTick}, endTick={endTick}).");

            _hasLoaded = true;
            Enabled = false;
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
            EntityManager.AddComponent<SpatialIndexedTag>(entity);
            EntityManager.AddComponent<CommunicationModuleTag>(entity);
            EntityManager.AddComponentData(entity, MediumContext.Vacuum);

            var carrierId = new FixedString64Bytes(spawn.entityId ?? $"carrier-{_spawnedEntities.Count}");
            EntityManager.AddComponentData(entity, new Carrier
            {
                CarrierId = carrierId,
                AffiliationEntity = Entity.Null,
                Speed = 3f,
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

            var resourceId = new FixedString64Bytes(spawn.resourceId ?? "Minerals");
            var miningEfficiency = spawn.miningEfficiency > 0f ? spawn.miningEfficiency : 0.8f;
            var speed = spawn.speed > 0f ? spawn.speed : 5f;
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
                TickInterval = 0.5f
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

            EntityManager.AddComponentData(entity, new VesselMovement
            {
                Velocity = float3.zero,
                BaseSpeed = speed,
                CurrentSpeed = 0f,
                DesiredRotation = quaternion.identity,
                IsMoving = 0,
                LastMoveTick = 0
            });

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

        private void SpawnStrikeCraft(Entity carrierEntity, float3 carrierPosition, CombatComponentData combatData, byte scenarioSide)
        {
            if (combatData == null || combatData.strikeCraftCount <= 0)
            {
                return;
            }

            var role = ParseStrikeCraftRole(combatData.strikeCraftRole);
            for (int i = 0; i < combatData.strikeCraftCount; i++)
            {
                var entity = EntityManager.CreateEntity();
                var offset = new float3(2f * (i + 1), 0f, 2f * (i + 1));
                EntityManager.AddComponentData(entity, LocalTransform.FromPositionRotationScale(carrierPosition + offset, quaternion.identity, 1f));
                EntityManager.AddComponent<CommunicationModuleTag>(entity);
                EntityManager.AddComponentData(entity, MediumContext.Vacuum);
                EntityManager.AddComponentData(entity, StrikeCraftProfile.Create(role, carrierEntity));
                EntityManager.AddComponentData(entity, AttackRunConfig.ForRole(role));
                EntityManager.AddComponentData(entity, StrikeCraftExperience.Rookie);
                EntityManager.AddComponentData(entity, new ScenarioSide
                {
                    Side = scenarioSide
                });
                var law = scenarioSide == 1 ? -0.7f : 0.7f;
                EntityManager.AddComponentData(entity, AlignmentTriplet.FromFloats(law, 0f, 0f));
            }
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
            }
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
            return float3.zero;
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
    }

    [System.Serializable]
    public class MiningScenarioJson
    {
        public int seed;
        public float duration_s;
        public List<MiningSpawnDefinition> spawn;
        public List<MiningScenarioAction> actions;
        public MiningTelemetryExpectations telemetryExpectations;
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
        public string resourceId;
        public float miningEfficiency;
        public float speed;
        public float cargoCapacity;
        public float unitsRemaining;
        public float gatherRatePerWorker;
        public int maxSimultaneousWorkers;
        public bool startDocked;
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
