using System.Collections.Generic;
using System.IO;
using PureDOTS.Runtime.Components;
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

namespace Space4x.Scenario
{
    /// <summary>
    /// Loads and executes the mining scenario from JSON, spawning carriers, mining vessels, and resource deposits.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class Space4XMiningScenarioSystem : SystemBase
    {
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

            if (!SystemAPI.TryGetSingleton<ScenarioInfo>(out var scenarioInfo))
            {
                return;
            }

            var scenarioPath = FindScenarioPath(scenarioInfo.ScenarioId.ToString());
            if (string.IsNullOrEmpty(scenarioPath) || !File.Exists(scenarioPath))
            {
                Debug.LogWarning($"[Space4XMiningScenario] Scenario file not found: {scenarioPath}");
                Enabled = false;
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

            _spawnedEntities = new Dictionary<string, Entity>();
            SpawnEntities();

            _hasLoaded = true;
            Enabled = false;
        }

        private string FindScenarioPath(string scenarioId)
        {
            var possiblePaths = new[]
            {
                Path.Combine(Application.dataPath, "Scenarios", $"{scenarioId}.json"),
                Path.Combine(Application.dataPath, "..", "Assets", "Scenarios", $"{scenarioId}.json"),
                Path.Combine(Application.streamingAssetsPath, "Scenarios", $"{scenarioId}.json")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return null;
        }

        private void SpawnEntities()
        {
            foreach (var spawn in _scenarioData.spawn)
            {
                switch (spawn.kind)
                {
                    case "Carrier":
                        SpawnCarrier(spawn);
                        break;
                    case "MiningVessel":
                        SpawnMiningVessel(spawn);
                        break;
                    case "ResourceDeposit":
                        SpawnResourceDeposit(spawn);
                        break;
                }
            }
        }

        private void SpawnCarrier(MiningSpawnDefinition spawn)
        {
            var position = GetPosition(spawn.position);
            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            EntityManager.AddComponent<SpatialIndexedTag>(entity);

            EntityManager.AddComponentData(entity, new Carrier
            {
                CarrierId = new FixedString64Bytes(spawn.entityId ?? $"carrier-{_spawnedEntities.Count}"),
                AffiliationEntity = Entity.Null,
                Speed = 3f,
                PatrolCenter = position,
                PatrolRadius = 50f
            });

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

            if (!string.IsNullOrEmpty(spawn.entityId))
            {
                _spawnedEntities[spawn.entityId] = entity;
            }
        }

        private void SpawnMiningVessel(MiningSpawnDefinition spawn)
        {
            var position = GetPosition(spawn.position);
            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            EntityManager.AddComponent<SpatialIndexedTag>(entity);

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
                CurrentGoal = VesselAIState.Goal.None,
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

        private float3 GetPosition(float[] position)
        {
            if (position != null && position.Length >= 2)
            {
                return new float3(position[0], position.Length > 2 ? position[2] : 0f, position[1]);
            }
            return float3.zero;
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
        public List<object> actions;
        public MiningTelemetryExpectations telemetryExpectations;
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
        public MiningComponentData components;
    }

    [System.Serializable]
    public class MiningComponentData
    {
        public List<ResourceStorageData> ResourceStorage;
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
        public MiningTelemetryExport export;
    }

    [System.Serializable]
    public class MiningTelemetryExport
    {
        public string csv;
        public string json;
    }
}

