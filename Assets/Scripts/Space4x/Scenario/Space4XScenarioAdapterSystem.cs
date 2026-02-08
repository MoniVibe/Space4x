using System;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.History;
using PureDOTS.Runtime.Logistics.Components;
using PureDOTS.Runtime.Resource;
using PureDOTS.Runtime.Resources;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Systems;
using Space4X.Headless;
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
using SystemEnv = System.Environment;

namespace Space4X.Scenario
{
    /// <summary>
    /// Adapter system that spawns entities from ScenarioRunner's ScenarioEntityCountElement buffer.
    /// Reads registry IDs and spawns corresponding Space4X entities with appropriate components.
    /// Note: Not Burst-compiled because it uses string operations (init-time spawn code).
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.CoreSingletonBootstrapSystem))]
    public partial struct Space4XScenarioAdapterSystem : ISystem
    {
        private const string PerfGateModeEnv = "PERF_GATE_MODE";
        private const string PerfGateSpawnBatchEnv = "SPACE4X_PERF_GATE_SPAWN_BATCH";
        private const string PerfGateLightweightEnv = "SPACE4X_PERF_GATE_LIGHTWEIGHT";
        private const int DefaultSpawnBatch = 10000;
        private static readonly FixedString64Bytes CarrierSubstring = new FixedString64Bytes("carrier");
        private static readonly FixedString64Bytes CarrierExact = new FixedString64Bytes("space4x.carrier");
        private static readonly FixedString64Bytes MinerExact = new FixedString64Bytes("space4x.miner");
        private static readonly FixedString64Bytes MiningVesselExact = new FixedString64Bytes("space4x.mining_vessel");
        private static readonly FixedString64Bytes MinerSubstring = new FixedString64Bytes("miner");
        private static readonly FixedString64Bytes MiningVesselSubstring = new FixedString64Bytes("mining_vessel");
        private static readonly FixedString64Bytes AsteroidExact = new FixedString64Bytes("space4x.asteroid");
        private static readonly FixedString64Bytes AsteroidSubstring = new FixedString64Bytes("asteroid");
        private static readonly FixedString64Bytes OreSupplyExact = new FixedString64Bytes("registry.storehouse.iron_ore_supply");
        private static readonly FixedString64Bytes OreSupplySubstring = new FixedString64Bytes("iron_ore_supply");
        private static readonly FixedString64Bytes StorehouseExact = new FixedString64Bytes("space4x.storehouse");
        private static readonly FixedString64Bytes StorehouseSubstring = new FixedString64Bytes("storehouse");
        private static readonly FixedString64Bytes StationExact = new FixedString64Bytes("space4x.station");
        private static readonly FixedString64Bytes StationSubstring = new FixedString64Bytes("station");
        private static readonly FixedString64Bytes RefineryExact = new FixedString64Bytes("space4x.refinery");
        private static readonly FixedString64Bytes RefinerySubstring = new FixedString64Bytes("refinery");
        private static readonly FixedString64Bytes FactoryExact = new FixedString64Bytes("space4x.factory");
        private static readonly FixedString64Bytes FactorySubstring = new FixedString64Bytes("factory");
        private static readonly FixedString64Bytes HaulerSubstring = new FixedString64Bytes("hauler");
        private static readonly FixedString64Bytes HaulerExact = new FixedString64Bytes("registry.hauler");

        private bool _hasSpawned;
        private bool _initialized;
        private bool _useBatching;
        private bool _lightweightSpawn;
        private int _batchSize;
        private int _entryIndex;
        private int _entrySpawned;
        private int _lastProgressEntry;
        private int _lastProgressSpawned;
        private Unity.Mathematics.Random _random;
        private bool _randomReady;

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

            var counts = SystemAPI.GetSingletonBuffer<ScenarioEntityCountElement>(true);
            if (counts.Length == 0)
            {
                _hasSpawned = true;
                state.Enabled = false;
                return;
            }

            EnsureRandom(scenarioInfo.Seed);

            var spawnCenter = float3.zero;
            const float spawnRadius = 50f;

            if (!_initialized)
            {
                _useBatching = IsPerfGateModeActive(scenarioInfo.ScenarioId);
                _batchSize = ResolveSpawnBatchSize(_useBatching);
                _lightweightSpawn = _useBatching && IsEnvTruthy(PerfGateLightweightEnv);
                _initialized = true;

                ReportSpawnProgress(ref state, "spawn", "start");
                if (_useBatching)
                {
                    UnityEngine.Debug.Log($"[Space4XScenarioAdapter] Perf gate spawn batching enabled (batch={_batchSize}, lightweight={(_lightweightSpawn ? "1" : "0")}).");
                }
            }

            if (!_useBatching || _batchSize <= 0)
            {
                var ecb = new EntityCommandBuffer(Allocator.Temp);
                for (int i = 0; i < counts.Length; i++)
                {
                    var entry = counts[i];
                    SpawnEntry(ecb, spawnCenter, spawnRadius, entry, entry.Count, 0, false);
                }

                ecb.Playback(state.EntityManager);
                ecb.Dispose();

                _hasSpawned = true;
                state.Enabled = false;
                ReportSpawnProgress(ref state, "spawn", "complete");
                return;
            }

            var spawnBudget = _batchSize;
            var batchBuffer = new EntityCommandBuffer(Allocator.Temp);

            while (spawnBudget > 0 && _entryIndex < counts.Length)
            {
                var entry = counts[_entryIndex];
                var remaining = entry.Count - _entrySpawned;
                if (remaining <= 0)
                {
                    _entryIndex++;
                    _entrySpawned = 0;
                    continue;
                }

                var spawnNow = math.min(remaining, spawnBudget);
                SpawnEntry(batchBuffer, spawnCenter, spawnRadius, entry, spawnNow, _entrySpawned, _lightweightSpawn);
                _entrySpawned += spawnNow;
                spawnBudget -= spawnNow;

                if (_entrySpawned >= entry.Count)
                {
                    _entryIndex++;
                    _entrySpawned = 0;
                }
            }

            batchBuffer.Playback(state.EntityManager);
            batchBuffer.Dispose();

            if (_entryIndex != _lastProgressEntry || _entrySpawned != _lastProgressSpawned)
            {
                ReportSpawnProgress(ref state, "spawn", $"batch_{_entryIndex}_{_entrySpawned}");
                _lastProgressEntry = _entryIndex;
                _lastProgressSpawned = _entrySpawned;
            }

            if (_entryIndex >= counts.Length)
            {
                _hasSpawned = true;
                state.Enabled = false;
                ReportSpawnProgress(ref state, "spawn", "complete");
            }
        }

        private void EnsureRandom(uint seed)
        {
            if (_randomReady)
            {
                return;
            }

            var resolvedSeed = seed == 0 ? 1u : seed;
            _random = new Unity.Mathematics.Random(resolvedSeed);
            _randomReady = true;
        }

        private static bool IsPerfGateModeActive(FixedString64Bytes scenarioId)
        {
            if (IsEnvTruthy(PerfGateModeEnv))
            {
                return true;
            }

            if (scenarioId.IsEmpty)
            {
                return false;
            }

            var scenario = scenarioId.ToString();
            return scenario.IndexOf("perf_gate", StringComparison.OrdinalIgnoreCase) >= 0
                || scenario.IndexOf("perfgate", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsEnvTruthy(string key)
        {
            var value = SystemEnv.GetEnvironmentVariable(key);
            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static int ResolveSpawnBatchSize(bool perfGate)
        {
            if (!perfGate)
            {
                return 0;
            }

            var value = SystemEnv.GetEnvironmentVariable(PerfGateSpawnBatchEnv);
            if (int.TryParse(value, out var parsed))
            {
                return parsed > 0 ? parsed : 0;
            }

            return DefaultSpawnBatch;
        }

        private static uint ResolveProgressTick(ref SystemState state)
        {
            var scenarioQuery = state.GetEntityQuery(ComponentType.ReadOnly<ScenarioRunnerTick>());
            if (scenarioQuery.TryGetSingleton(out ScenarioRunnerTick scenarioTick))
            {
                return scenarioTick.Tick;
            }

            var timeQuery = state.GetEntityQuery(ComponentType.ReadOnly<TimeState>());
            if (timeQuery.TryGetSingleton(out TimeState timeState))
            {
                return timeState.Tick;
            }

            return 0u;
        }

        private static void ReportSpawnProgress(ref SystemState state, string phase, string checkpoint)
        {
            if (!Space4XHeadlessDiagnostics.Enabled)
            {
                return;
            }

            var tick = ResolveProgressTick(ref state);
            Space4XHeadlessDiagnostics.UpdateProgress(phase, checkpoint, tick);
        }

        private void SpawnEntry(
            EntityCommandBuffer buffer,
            float3 spawnCenter,
            float spawnRadius,
            ScenarioEntityCountElement entry,
            int spawnCount,
            int startIndex,
            bool lightweight)
        {
            var registryId = entry.RegistryId;

            // Map registry IDs to entity archetypes using FixedString matching.
            if (registryId.Equals(CarrierExact) || registryId.IndexOf(CarrierSubstring) >= 0)
            {
                SpawnCarriers(buffer, spawnCenter, spawnRadius, spawnCount, entry.Count, startIndex, lightweight, ref _random);
            }
            else if (registryId.Equals(MinerExact) || registryId.Equals(MiningVesselExact) ||
                     registryId.IndexOf(MinerSubstring) >= 0 || registryId.IndexOf(MiningVesselSubstring) >= 0)
            {
                SpawnMiners(buffer, spawnCenter, spawnRadius, spawnCount, entry.Count, startIndex, lightweight, ref _random);
            }
            else if (registryId.Equals(AsteroidExact) || registryId.IndexOf(AsteroidSubstring) >= 0)
            {
                SpawnAsteroids(buffer, spawnCenter, spawnRadius, spawnCount, entry.Count, startIndex, ref _random);
            }
            else if (registryId.Equals(OreSupplyExact) || registryId.IndexOf(OreSupplySubstring) >= 0)
            {
                SpawnIronOreSupplyStations(buffer, spawnCenter, spawnRadius, spawnCount, entry.Count, startIndex, ref _random);
            }
            else if (registryId.Equals(StorehouseExact) || registryId.Equals(StationExact) ||
                     registryId.IndexOf(StorehouseSubstring) >= 0 || registryId.IndexOf(StationSubstring) >= 0)
            {
                SpawnStations(buffer, spawnCenter, spawnRadius, spawnCount, entry.Count, startIndex, ref _random);
            }
            else if (registryId.Equals(RefineryExact) || registryId.Equals(FactoryExact) ||
                     registryId.IndexOf(RefinerySubstring) >= 0 || registryId.IndexOf(FactorySubstring) >= 0)
            {
                SpawnProcessingFacilities(buffer, spawnCenter, spawnRadius, spawnCount, entry.Count, startIndex, ref _random);
            }
            else if (registryId.Equals(HaulerExact) || registryId.IndexOf(HaulerSubstring) >= 0)
            {
                SpawnHaulers(buffer, spawnCenter, spawnRadius, spawnCount, entry.Count, startIndex, ref _random);
            }
        }

        private static void SpawnCarriers(
            EntityCommandBuffer ecb,
            float3 center,
            float radius,
            int count,
            int totalCount,
            int startIndex,
            bool lightweight,
            ref Unity.Mathematics.Random random)
        {
            var endIndex = startIndex + count;
            for (int i = startIndex; i < endIndex; i++)
            {
                var angle = totalCount > 0 ? (float)i / totalCount * math.PI * 2f : 0f;
                var distance = random.NextFloat(radius * 0.3f, radius * 0.7f);
                var position = center + new float3(
                    math.cos(angle) * distance,
                    random.NextFloat(-5f, 5f),
                    math.sin(angle) * distance
                );

                var carrier = ecb.CreateEntity();
                ecb.AddComponent(carrier, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 10f));
                if (!lightweight)
                {
                    ecb.AddComponent(carrier, new PostTransformMatrix
                    {
                        Value = float4x4.Scale(new float3(0.6f, 0.4f, 6f))
                    });
                }

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

                if (!lightweight)
                {
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

                    var storage = ecb.AddBuffer<ResourceStorage>(carrier);
                    storage.Add(ResourceStorage.Create(ResourceType.Food, 10000f));
                    storage.Add(ResourceStorage.Create(ResourceType.Water, 10000f));
                    storage.Add(ResourceStorage.Create(ResourceType.Supplies, 10000f));
                    storage.Add(ResourceStorage.Create(ResourceType.Fuel, 10000f));
                    storage.Add(ResourceStorage.Create(ResourceType.Minerals, 10000f));
                    storage.Add(ResourceStorage.Create(ResourceType.RareMetals, 10000f));
                }
            }
        }

        private static void SpawnMiners(
            EntityCommandBuffer ecb,
            float3 center,
            float radius,
            int count,
            int totalCount,
            int startIndex,
            bool lightweight,
            ref Unity.Mathematics.Random random)
        {
            var endIndex = startIndex + count;
            for (int i = startIndex; i < endIndex; i++)
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

                if (!lightweight)
                {
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
        }

        private static void SpawnAsteroids(
            EntityCommandBuffer ecb,
            float3 center,
            float radius,
            int count,
            int totalCount,
            int startIndex,
            ref Unity.Mathematics.Random random)
        {
            var endIndex = startIndex + count;
            for (int i = startIndex; i < endIndex; i++)
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

        private static void SpawnStations(
            EntityCommandBuffer ecb,
            float3 center,
            float radius,
            int count,
            int totalCount,
            int startIndex,
            ref Unity.Mathematics.Random random)
        {
            var resourceIdMinerals = new FixedString64Bytes("minerals");
            var storehouseLabel = new FixedString64Bytes("Station");
            
            var endIndex = startIndex + count;
            for (int i = startIndex; i < endIndex; i++)
            {
                var angle = totalCount > 0 ? (float)i / totalCount * math.PI * 2f : 0f;
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

        private static void SpawnIronOreSupplyStations(
            EntityCommandBuffer ecb,
            float3 center,
            float radius,
            int count,
            int totalCount,
            int startIndex,
            ref Unity.Mathematics.Random random)
        {
            var resourceIdOre = new FixedString64Bytes("iron_ore");
            var storehouseLabel = new FixedString64Bytes("Ore Supply");
            const float initialOre = 80f;
            const float maxCapacity = 200f;

            var endIndex = startIndex + count;
            for (int i = startIndex; i < endIndex; i++)
            {
                var angle = totalCount > 0 ? (float)i / totalCount * math.PI * 2f : 0f;
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

        private static void SpawnProcessingFacilities(
            EntityCommandBuffer ecb,
            float3 center,
            float radius,
            int count,
            int totalCount,
            int startIndex,
            ref Unity.Mathematics.Random random)
        {
            var inputId = new FixedString64Bytes("iron_ore");
            var outputId = new FixedString64Bytes("iron_ingot");
            var energyId = new FixedString64Bytes("refined_fuels");
            var recipeId = new FixedString32Bytes("refine_iron_ingot");
            var storehouseLabel = new FixedString64Bytes("Refinery");

            var endIndex = startIndex + count;
            for (int i = startIndex; i < endIndex; i++)
            {
                var angle = totalCount > 0 ? (float)i / totalCount * math.PI * 2f : 0f;
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
                capacityBuffer.Add(new StorehouseCapacityElement
                {
                    ResourceTypeId = energyId,
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
                    TotalCapacity = 200f * capacityBuffer.Length,
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

        private static void SpawnHaulers(
            EntityCommandBuffer ecb,
            float3 center,
            float radius,
            int count,
            int totalCount,
            int startIndex,
            ref Unity.Mathematics.Random random)
        {
            var endIndex = startIndex + count;
            for (int i = startIndex; i < endIndex; i++)
            {
                var angle = totalCount > 0 ? (float)i / totalCount * math.PI * 2f : 0f;
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
