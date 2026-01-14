using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.History;
using PureDOTS.Runtime.Logistics.Components;
using PureDOTS.Runtime.Resource;
using PureDOTS.Runtime.Resources;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Systems;
using Space4X.Mining;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using ResourceSourceState = Space4X.Registry.ResourceSourceState;
using ResourceSourceConfig = Space4X.Registry.ResourceSourceConfig;
using ResourceTypeId = Space4X.Registry.ResourceTypeId;

namespace Space4X.Scenario
{
    /// <summary>
    /// Adapter system that spawns entities from ScenarioRunner's ScenarioEntityCountElement buffer.
    /// Reads registry IDs and spawns corresponding Space4X entities with appropriate components.
    /// Note: Not Burst-compiled because it uses string operations (init-time spawn code).
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.CoreSingletonBootstrapSystem))]
    [UpdateAfter(typeof(global::Space4x.Scenario.Space4XMiningScenarioSystem))]
    public partial struct Space4XScenarioAdapterSystem : ISystem
    {
        private bool _hasSpawned;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioInfo>();
            state.RequireForUpdate<ScenarioEntityCountElement>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_hasSpawned)
            {
                state.Enabled = false;
                return;
            }

            if (!SystemAPI.TryGetSingleton<ScenarioInfo>(out var scenarioInfo))
            {
                return;
            }

            if (!EnsureScenarioRunnerLane(ref state))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                UnityEngine.Debug.Log("[Space4XScenarioAdapter] Adapter disabled because spawn lane is not ScenarioRunnerCounts.");
#endif
                _hasSpawned = true;
                state.Enabled = false;
                return;
            }

            var counts = SystemAPI.GetSingletonBuffer<ScenarioEntityCountElement>(true);
            if (counts.Length == 0)
            {
                _hasSpawned = true;
                state.Enabled = false;
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var random = new Unity.Mathematics.Random(scenarioInfo.Seed);
            var spawnCenter = float3.zero;
            const float spawnRadius = 50f;

            // Burst-safe FixedString matching (no ToString/Contains in Burst)
            var carrierSubstring = new FixedString64Bytes("carrier");
            var carrierExact = new FixedString64Bytes("space4x.carrier");
            var minerSubstring = new FixedString64Bytes("miner");
            var miningVesselSubstring = new FixedString64Bytes("mining_vessel");
            var minerExact = new FixedString64Bytes("space4x.miner");
            var miningVesselExact = new FixedString64Bytes("space4x.mining_vessel");
            var asteroidSubstring = new FixedString64Bytes("asteroid");
            var asteroidExact = new FixedString64Bytes("space4x.asteroid");
            var storehouseSubstring = new FixedString64Bytes("storehouse");
            var stationSubstring = new FixedString64Bytes("station");
            var storehouseExact = new FixedString64Bytes("space4x.storehouse");
            var stationExact = new FixedString64Bytes("space4x.station");
            var oreSupplySubstring = new FixedString64Bytes("iron_ore_supply");
            var oreSupplyExact = new FixedString64Bytes("registry.storehouse.iron_ore_supply");
            var refinerySubstring = new FixedString64Bytes("refinery");
            var factorySubstring = new FixedString64Bytes("factory");
            var refineryExact = new FixedString64Bytes("space4x.refinery");
            var factoryExact = new FixedString64Bytes("space4x.factory");
            var haulerSubstring = new FixedString64Bytes("hauler");
            var haulerExact = new FixedString64Bytes("registry.hauler");

            for (int i = 0; i < counts.Length; i++)
            {
                var entry = counts[i];
                var registryId = entry.RegistryId;
                var count = entry.Count;

                // Map registry IDs to entity archetypes using FixedString matching
                // Phase 0: Hardcoded mappings (will be replaced with registry system in Phase 0.5)
                if (registryId.Equals(carrierExact) || registryId.IndexOf(carrierSubstring) >= 0)
                {
                    SpawnCarriers(ref state, ecb, spawnCenter, spawnRadius, count, ref random);
                }
                else if (registryId.Equals(minerExact) || registryId.Equals(miningVesselExact) ||
                         registryId.IndexOf(minerSubstring) >= 0 || registryId.IndexOf(miningVesselSubstring) >= 0)
                {
                    SpawnMiners(ref state, ecb, spawnCenter, spawnRadius, count, ref random);
                }
                else if (registryId.Equals(asteroidExact) || registryId.IndexOf(asteroidSubstring) >= 0)
                {
                    SpawnAsteroids(ref state, ecb, spawnCenter, spawnRadius, count, ref random);
                }
                else if (registryId.Equals(oreSupplyExact) || registryId.IndexOf(oreSupplySubstring) >= 0)
                {
                    SpawnIronOreSupplyStations(ref state, ecb, spawnCenter, spawnRadius, count, ref random);
                }
                else if (registryId.Equals(storehouseExact) || registryId.Equals(stationExact) ||
                         registryId.IndexOf(storehouseSubstring) >= 0 || registryId.IndexOf(stationSubstring) >= 0)
                {
                    SpawnStations(ref state, ecb, spawnCenter, spawnRadius, count, ref random);
                }
                else if (registryId.Equals(refineryExact) || registryId.Equals(factoryExact) ||
                         registryId.IndexOf(refinerySubstring) >= 0 || registryId.IndexOf(factorySubstring) >= 0)
                {
                    SpawnProcessingFacilities(ref state, ecb, spawnCenter, spawnRadius, count, ref random);
                }
                else if (registryId.Equals(haulerExact) || registryId.IndexOf(haulerSubstring) >= 0)
                {
                    SpawnHaulers(ref state, ecb, spawnCenter, spawnRadius, count, ref random);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            _hasSpawned = true;
            state.Enabled = false;
        }

        private bool EnsureScenarioRunnerLane(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<Space4XScenarioSpawnLane>(out var laneEntity))
            {
                laneEntity = state.EntityManager.CreateEntity(typeof(Space4XScenarioSpawnLane));
                state.EntityManager.SetComponentData(laneEntity, new Space4XScenarioSpawnLane
                {
                    Kind = Space4XScenarioSpawnLaneKind.ScenarioRunnerCounts
                });
                return true;
            }

            var lane = state.EntityManager.GetComponentData<Space4XScenarioSpawnLane>(laneEntity);
            if (lane.Kind == Space4XScenarioSpawnLaneKind.None)
            {
                lane.Kind = Space4XScenarioSpawnLaneKind.ScenarioRunnerCounts;
                state.EntityManager.SetComponentData(laneEntity, lane);
                return true;
            }

            return lane.Kind == Space4XScenarioSpawnLaneKind.ScenarioRunnerCounts;
        }

        private static void SpawnCarriers(ref SystemState state, EntityCommandBuffer ecb, float3 center, float radius, int count, ref Unity.Mathematics.Random random)
        {
            for (int i = 0; i < count; i++)
            {
                var angle = (float)i / count * math.PI * 2f;
                var distance = random.NextFloat(radius * 0.3f, radius * 0.7f);
                var position = center + new float3(
                    math.cos(angle) * distance,
                    random.NextFloat(-5f, 5f),
                    math.sin(angle) * distance
                );

                var carrier = ecb.CreateEntity();
                ecb.AddComponent(carrier, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 10f));
                ecb.AddComponent(carrier, new PostTransformMatrix
                {
                    Value = float4x4.Scale(new float3(0.6f, 0.4f, 6f))
                });

                var carrierId = new FixedString64Bytes();
                carrierId.Append("carrier_");
                // Manual number conversion (avoid ToString for consistency)
                var num = i + 1;
                if (num >= 100)
                {
                    carrierId.Append((char)('0' + (num / 100 % 10)));
                }
                if (num >= 10)
                {
                    carrierId.Append((char)('0' + (num / 10 % 10)));
                }
                carrierId.Append((char)('0' + (num % 10)));
                
                ecb.AddComponent(carrier, new Carrier
                {
                    CarrierId = carrierId,
                    AffiliationEntity = Entity.Null,
                    Speed = 5f,
                    Acceleration = 0.6f,
                    Deceleration = 0.8f,
                    TurnSpeed = 0.35f,
                    SlowdownDistance = 18f,
                    ArrivalDistance = 3f,
                    PatrolCenter = position,
                    PatrolRadius = 50f
                });

                ecb.AddComponent(carrier, new VesselAIState
                {
                    CurrentState = VesselAIState.State.Idle,
                    CurrentGoal = VesselAIState.Goal.Patrol,
                    TargetEntity = Entity.Null,
                    TargetPosition = position
                });

                ecb.AddComponent(carrier, new VesselMovement
                {
                    Velocity = float3.zero,
                    BaseSpeed = 5f,
                    CurrentSpeed = 0f,
                    Acceleration = 0.6f,
                    Deceleration = 0.8f,
                    TurnSpeed = 0.35f,
                    SlowdownDistance = 18f,
                    ArrivalDistance = 3f,
                    DesiredRotation = quaternion.identity,
                    IsMoving = 0
                });

                ecb.AddComponent(carrier, new DockingCapacity
                {
                    MaxSmallCraft = 24,
                    CurrentSmallCraft = 0
                });

                ecb.AddBuffer<ResourceStorage>(carrier);
            }
        }

        private static void SpawnMiners(ref SystemState state, EntityCommandBuffer ecb, float3 center, float radius, int count, ref Unity.Mathematics.Random random)
        {
            for (int i = 0; i < count; i++)
            {
                var angle = random.NextFloat(0f, math.PI * 2f);
                var distance = random.NextFloat(radius * 0.5f, radius);
                var position = center + new float3(
                    math.cos(angle) * distance,
                    random.NextFloat(-10f, 10f),
                    math.sin(angle) * distance
                );

                var miner = ecb.CreateEntity();
                ecb.AddComponent(miner, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));

                var minerId = new FixedString64Bytes();
                minerId.Append("miner_");
                // Manual number conversion (avoid ToString for consistency)
                var num = i + 1;
                if (num >= 100)
                {
                    minerId.Append((char)('0' + (num / 100 % 10)));
                }
                if (num >= 10)
                {
                    minerId.Append((char)('0' + (num / 10 % 10)));
                }
                minerId.Append((char)('0' + (num % 10)));
                
                ecb.AddComponent(miner, new MiningVessel
                {
                    VesselId = minerId,
                    CarrierEntity = Entity.Null,
                    MiningEfficiency = 0.8f,
                    Speed = 10f,
                    CargoCapacity = 100f,
                    CurrentCargo = 0f,
                    CargoResourceType = ResourceType.Minerals
                });

                ecb.AddComponent(miner, new MiningState
                {
                    Phase = Space4X.Registry.MiningPhase.Idle,
                    ActiveTarget = Entity.Null,
                    MiningTimer = 0f,
                    TickInterval = 0.1f,
                    PhaseTimer = 0f
                });

                ecb.AddComponent(miner, new VesselAIState
                {
                    CurrentState = VesselAIState.State.Idle,
                    CurrentGoal = VesselAIState.Goal.Mining,
                    TargetEntity = Entity.Null,
                    TargetPosition = position
                });

                ecb.AddComponent(miner, new VesselMovement
                {
                    Velocity = float3.zero,
                    BaseSpeed = 10f,
                    CurrentSpeed = 0f,
                    Acceleration = 6f,
                    Deceleration = 8f,
                    TurnSpeed = 2.5f,
                    SlowdownDistance = 6f,
                    ArrivalDistance = 1.5f,
                    DesiredRotation = quaternion.identity,
                    IsMoving = 0
                });
            }
        }

        private static void SpawnAsteroids(ref SystemState state, EntityCommandBuffer ecb, float3 center, float radius, int count, ref Unity.Mathematics.Random random)
        {
            for (int i = 0; i < count; i++)
            {
                var angle = random.NextFloat(0f, math.PI * 2f);
                var distance = random.NextFloat(radius, radius * 1.5f);
                var position = center + new float3(
                    math.cos(angle) * distance,
                    random.NextFloat(-10f, 10f),
                    math.sin(angle) * distance
                );

                var asteroid = ecb.CreateEntity();
                var randomEuler = random.NextFloat3(new float3(0f), new float3(360f));
                ecb.AddComponent(asteroid, LocalTransform.FromPositionRotationScale(
                    position,
                    quaternion.Euler(math.radians(randomEuler)),
                    1f + random.NextFloat(0.5f, 2f)
                ));

                var resourceAmount = 500f;
                var asteroidId = new FixedString64Bytes();
                asteroidId.Append("asteroid_");
                // Manual number conversion (avoid ToString for consistency)
                var num = i + 1;
                if (num >= 100)
                {
                    asteroidId.Append((char)('0' + (num / 100 % 10)));
                }
                if (num >= 10)
                {
                    asteroidId.Append((char)('0' + (num / 10 % 10)));
                }
                asteroidId.Append((char)('0' + (num % 10)));
                
                ecb.AddComponent(asteroid, new Asteroid
                {
                    AsteroidId = asteroidId,
                    ResourceType = ResourceType.Minerals,
                    ResourceAmount = resourceAmount,
                    MaxResourceAmount = resourceAmount,
                    MiningRate = 10f
                });

                // Add ResourceTypeId for registry population system
                ecb.AddComponent(asteroid, new ResourceTypeId { Value = new FixedString64Bytes("space4x.resource.minerals") });

                ecb.AddComponent(asteroid, new ResourceSourceState
                {
                    UnitsRemaining = resourceAmount
                });

                ecb.AddComponent(asteroid, new ResourceSourceConfig
                {
                    GatherRatePerWorker = 10f,
                    MaxSimultaneousWorkers = 4
                });

                var volumeConfig = Space4XAsteroidVolumeConfig.Default;
                volumeConfig.Radius = math.max(0.1f, volumeConfig.Radius);
                ecb.AddComponent(asteroid, volumeConfig);

                ecb.AddComponent(asteroid, new Space4XAsteroidCenter
                {
                    Position = position
                });

            }
        }

        private static void SpawnStations(ref SystemState state, EntityCommandBuffer ecb, float3 center, float radius, int count, ref Unity.Mathematics.Random random)
        {
            var resourceIdMinerals = new FixedString64Bytes("minerals");
            var storehouseLabel = new FixedString64Bytes("Station");
            
            for (int i = 0; i < count; i++)
            {
                var angle = (float)i / count * math.PI * 2f;
                var distance = random.NextFloat(radius * 0.2f, radius * 0.5f);
                var position = center + new float3(
                    math.cos(angle) * distance,
                    0f,
                    math.sin(angle) * distance
                );

                var station = ecb.CreateEntity();
                ecb.AddComponent(station, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 5f));
                ecb.AddComponent<SpatialIndexedTag>(station);
                ecb.AddComponent<RewindableTag>(station);

                // Add storehouse capacity buffer
                var capacityBuffer = ecb.AddBuffer<StorehouseCapacityElement>(station);
                capacityBuffer.Add(new StorehouseCapacityElement
                {
                    ResourceTypeId = resourceIdMinerals,
                    MaxCapacity = 1000f
                });

                // Add storehouse config and inventory
                ecb.AddComponent(station, new StorehouseConfig
                {
                    ShredRate = 1f,
                    MaxShredQueueSize = 8,
                    InputRate = 20f,
                    OutputRate = 18f,
                    Label = storehouseLabel
                });
                ecb.AddComponent(station, new StorehouseInventory
                {
                    TotalStored = 0f,
                    TotalCapacity = 1000f,
                    ItemTypeCount = capacityBuffer.Length,
                    IsShredding = 0,
                    LastUpdateTick = 0
                });

                ecb.AddBuffer<StorehouseInventoryItem>(station);
                
                // Optional: Add history components like sample bootstrap
                ecb.AddComponent(station, new HistoryTier
                {
                    Tier = HistoryTier.TierType.Default,
                    OverrideStrideSeconds = 0f
                });
                ecb.AddBuffer<StorehouseHistorySample>(station);
            }
        }

        private static void SpawnIronOreSupplyStations(ref SystemState state, EntityCommandBuffer ecb, float3 center, float radius, int count, ref Unity.Mathematics.Random random)
        {
            var resourceIdOre = new FixedString64Bytes("iron_ore");
            var storehouseLabel = new FixedString64Bytes("Ore Supply");
            const float initialOre = 80f;
            const float maxCapacity = 200f;

            for (int i = 0; i < count; i++)
            {
                var angle = (float)i / count * math.PI * 2f;
                var distance = random.NextFloat(radius * 0.2f, radius * 0.6f);
                var position = center + new float3(
                    math.cos(angle) * distance,
                    0f,
                    math.sin(angle) * distance
                );

                var station = ecb.CreateEntity();
                ecb.AddComponent(station, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 5f));
                ecb.AddComponent<SpatialIndexedTag>(station);
                ecb.AddComponent<RewindableTag>(station);

                var capacityBuffer = ecb.AddBuffer<StorehouseCapacityElement>(station);
                capacityBuffer.Add(new StorehouseCapacityElement
                {
                    ResourceTypeId = resourceIdOre,
                    MaxCapacity = maxCapacity
                });

                ecb.AddComponent(station, new StorehouseConfig
                {
                    ShredRate = 1f,
                    MaxShredQueueSize = 8,
                    InputRate = 20f,
                    OutputRate = 18f,
                    Label = storehouseLabel
                });
                ecb.AddComponent(station, new StorehouseInventory
                {
                    TotalStored = initialOre,
                    TotalCapacity = maxCapacity,
                    ItemTypeCount = capacityBuffer.Length,
                    IsShredding = 0,
                    LastUpdateTick = 0
                });

                var items = ecb.AddBuffer<StorehouseInventoryItem>(station);
                items.Add(new StorehouseInventoryItem
                {
                    ResourceTypeId = resourceIdOre,
                    Amount = initialOre,
                    Reserved = 0f,
                    TierId = (byte)ResourceQualityTier.Unknown,
                    AverageQuality = 0
                });

                ecb.AddBuffer<StorehouseReservationItem>(station);
                ecb.AddComponent(station, new StorehouseJobReservation
                {
                    ReservedCapacity = 0f,
                    LastMutationTick = 0
                });

                ecb.AddComponent(station, new HistoryTier
                {
                    Tier = HistoryTier.TierType.Default,
                    OverrideStrideSeconds = 0f
                });
                ecb.AddBuffer<StorehouseHistorySample>(station);
            }
        }

        private static void SpawnProcessingFacilities(ref SystemState state, EntityCommandBuffer ecb, float3 center, float radius, int count, ref Unity.Mathematics.Random random)
        {
            var inputId = new FixedString64Bytes("iron_ore");
            var outputId = new FixedString64Bytes("iron_ingot");
            var recipeId = new FixedString32Bytes("refine_iron_ingot");
            var storehouseLabel = new FixedString64Bytes("Refinery");

            for (int i = 0; i < count; i++)
            {
                var angle = (float)i / count * math.PI * 2f;
                var distance = random.NextFloat(radius * 0.15f, radius * 0.4f);
                var position = center + new float3(
                    math.cos(angle) * distance,
                    0f,
                    math.sin(angle) * distance
                );

                var facility = ecb.CreateEntity();
                ecb.AddComponent(facility, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 4f));
                ecb.AddComponent<SpatialIndexedTag>(facility);
                ecb.AddComponent<RewindableTag>(facility);
                ecb.AddComponent(facility, ProcessingFacility.Tier1);

                var queueBuffer = ecb.AddBuffer<ProcessingQueueEntry>(facility);
                queueBuffer.Add(new ProcessingQueueEntry
                {
                    RecipeId = recipeId,
                    BatchCount = 1,
                    Priority = 64,
                    QueuedTick = 0
                });

                ecb.AddBuffer<NeedRequest>(facility);

                var capacityBuffer = ecb.AddBuffer<StorehouseCapacityElement>(facility);
                capacityBuffer.Add(new StorehouseCapacityElement
                {
                    ResourceTypeId = inputId,
                    MaxCapacity = 200f
                });
                capacityBuffer.Add(new StorehouseCapacityElement
                {
                    ResourceTypeId = outputId,
                    MaxCapacity = 200f
                });

                ecb.AddComponent(facility, new StorehouseConfig
                {
                    ShredRate = 1f,
                    MaxShredQueueSize = 8,
                    InputRate = 20f,
                    OutputRate = 18f,
                    Label = storehouseLabel
                });
                ecb.AddComponent(facility, new StorehouseInventory
                {
                    TotalStored = 0f,
                    TotalCapacity = 400f,
                    ItemTypeCount = capacityBuffer.Length,
                    IsShredding = 0,
                    LastUpdateTick = 0
                });

                ecb.AddBuffer<StorehouseInventoryItem>(facility);
                ecb.AddBuffer<StorehouseReservationItem>(facility);
                ecb.AddComponent(facility, new StorehouseJobReservation
                {
                    ReservedCapacity = 0f,
                    LastMutationTick = 0
                });
            }
        }

        private static void SpawnHaulers(ref SystemState state, EntityCommandBuffer ecb, float3 center, float radius, int count, ref Unity.Mathematics.Random random)
        {
            for (int i = 0; i < count; i++)
            {
                var angle = (float)i / count * math.PI * 2f;
                var distance = random.NextFloat(radius * 0.2f, radius * 0.6f);
                var position = center + new float3(
                    math.cos(angle) * distance,
                    0f,
                    math.sin(angle) * distance
                );

                var hauler = ecb.CreateEntity();
                ecb.AddComponent(hauler, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 2f));
                ecb.AddComponent<SpatialIndexedTag>(hauler);
                ecb.AddComponent<RewindableTag>(hauler);
                ecb.AddComponent(hauler, new HaulerTag());
                ecb.AddComponent(hauler, new HaulerCapacity
                {
                    MaxMass = 1000f,
                    MaxVolume = 1000f,
                    MaxContainers = 0
                });
            }
        }
    }
}
