using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Systems;
using Space4X.Mining;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Scenario
{
    /// <summary>
    /// Adapter system that spawns entities from ScenarioRunner's ScenarioEntityCountElement buffer.
    /// Reads registry IDs and spawns corresponding Space4X entities with appropriate components.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.CoreSingletonBootstrapSystem))]
    public partial struct Space4XScenarioAdapterSystem : ISystem
    {
        private bool _hasSpawned;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioInfo>();
            state.RequireForUpdate<ScenarioEntityCountElement>();
        }

        [BurstCompile]
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

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var random = new Unity.Mathematics.Random(scenarioInfo.Seed);
            var spawnCenter = float3.zero;
            const float spawnRadius = 50f;

            for (int i = 0; i < counts.Length; i++)
            {
                var entry = counts[i];
                var registryId = entry.RegistryId.ToString().ToLowerInvariant();
                var count = entry.Count;

                // Map registry IDs to entity archetypes
                // Phase 0: Hardcoded mappings (will be replaced with registry system in Phase 0.5)
                if (registryId.Contains("carrier") || registryId == "space4x.carrier")
                {
                    SpawnCarriers(ref state, ecb, spawnCenter, spawnRadius, count, ref random);
                }
                else if (registryId.Contains("miner") || registryId.Contains("mining_vessel") || 
                         registryId == "space4x.miner" || registryId == "space4x.mining_vessel")
                {
                    SpawnMiners(ref state, ecb, spawnCenter, spawnRadius, count, ref random);
                }
                else if (registryId.Contains("asteroid") || registryId == "space4x.asteroid")
                {
                    SpawnAsteroids(ref state, ecb, spawnCenter, spawnRadius, count, ref random);
                }
                else if (registryId.Contains("storehouse") || registryId.Contains("station") || 
                         registryId == "space4x.storehouse" || registryId == "space4x.station")
                {
                    SpawnStations(ref state, ecb, spawnCenter, spawnRadius, count, ref random);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            _hasSpawned = true;
            state.Enabled = false;
        }

        [BurstCompile]
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

                ecb.AddComponent(carrier, new Carrier
                {
                    CarrierId = new FixedString64Bytes($"carrier_{i + 1}"),
                    AffiliationEntity = Entity.Null,
                    Speed = 5f,
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
                    BaseSpeed = 5f,
                    CurrentSpeed = 0f,
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

        [BurstCompile]
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

                ecb.AddComponent(miner, new MiningVessel
                {
                    VesselId = new FixedString64Bytes($"miner_{i + 1}"),
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
                    TickInterval = 0.1f
                });

                ecb.AddComponent(miner, new VesselAIState
                {
                    CurrentState = VesselAIState.State.Idle,
                    CurrentGoal = VesselAIState.Goal.Mine,
                    TargetEntity = Entity.Null,
                    TargetPosition = position
                });

                ecb.AddComponent(miner, new VesselMovement
                {
                    BaseSpeed = 10f,
                    CurrentSpeed = 0f,
                    IsMoving = 0
                });
            }
        }

        [BurstCompile]
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
                ecb.AddComponent(asteroid, new Asteroid
                {
                    AsteroidId = new FixedString64Bytes($"asteroid_{i + 1}"),
                    ResourceType = ResourceType.Minerals,
                    ResourceAmount = resourceAmount,
                    MaxResourceAmount = resourceAmount,
                    MiningRate = 10f
                });

                ecb.AddComponent(asteroid, new ResourceSourceState
                {
                    UnitsRemaining = resourceAmount
                });

                ecb.AddComponent(asteroid, new ResourceSourceConfig
                {
                    GatherRatePerWorker = 10f,
                    MaxSimultaneousWorkers = 4
                });
            }
        }

        [BurstCompile]
        private static void SpawnStations(ref SystemState state, EntityCommandBuffer ecb, float3 center, float radius, int count, ref Unity.Mathematics.Random random)
        {
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

                // Minimal station components
                ecb.AddComponent<StorehouseTag>(station);
                ecb.AddBuffer<StorehouseInventoryItem>(station);
                ecb.AddBuffer<StorehouseCapacityElement>(station);
            }
        }
    }
}

