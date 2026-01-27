using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Handles vegetation reproduction and spreading based on reproduction timers.
    /// Checks for mature vegetation, updates reproduction timers, and spawns offspring deterministically.
    /// Runs after VegetationGrowthSystem to ensure lifecycle stages are up-to-date.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VegetationSystemGroup))]
    [UpdateAfter(typeof(VegetationGrowthSystem))]
    public partial struct VegetationReproductionSystem : ISystem
    {
        private EntityQuery _vegetationQuery;
        private ComponentLookup<VegetationProduction> _productionLookup;
        private static readonly ProfilerMarker s_UpdateVegetationReproductionMarker = 
            new ProfilerMarker("VegetationReproductionSystem.Update");

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _vegetationQuery = SystemAPI.QueryBuilder()
                .WithAll<VegetationId, VegetationLifecycle, VegetationReproduction, VegetationSpeciesIndex, LocalTransform>()
                .WithAny<VegetationMatureTag>()
                .WithNone<VegetationDeadTag, PlaybackGuardTag>()
                .Build();

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<VegetationSpeciesLookup>();
            state.RequireForUpdate<VegetationSpawnCommandQueue>();
            state.RequireForUpdate(_vegetationQuery);
            _productionLookup = state.GetComponentLookup<VegetationProduction>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using (s_UpdateVegetationReproductionMarker.Auto())
            {
                var timeState = SystemAPI.GetSingleton<TimeState>();
                if (timeState.IsPaused)
                {
                    return;
                }

                if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
                {
                    return;
                }

                // Safety check: ensure species catalog exists
                if (!SystemAPI.HasSingleton<VegetationSpeciesLookup>())
                {
#if UNITY_EDITOR
                    LogMissingSpeciesLookup();
#endif
                    return;
                }

                var speciesLookup = SystemAPI.GetSingleton<VegetationSpeciesLookup>();

                if (!speciesLookup.CatalogBlob.IsCreated)
                {
#if UNITY_EDITOR
                    LogCatalogNotCreated();
#endif
                    return;
                }

                var spawnQueueEntity = SystemAPI.GetSingletonEntity<VegetationSpawnCommandQueue>();
                var spawnBuffer = state.EntityManager.GetBuffer<VegetationSpawnCommand>(spawnQueueEntity);

                var spawnCommands = new NativeQueue<VegetationSpawnCommand>(Allocator.TempJob);

                _productionLookup.Update(ref state);

                var job = new UpdateVegetationReproductionJob
                {
                    DeltaTime = timeState.FixedDeltaTime,
                    CurrentTick = timeState.Tick,
                    SpeciesCatalogBlob = speciesLookup.CatalogBlob,
                    SpawnCommands = spawnCommands.AsParallelWriter(),
                    ProductionLookup = _productionLookup
                };

                state.Dependency = job.ScheduleParallel(state.Dependency);
                state.Dependency.Complete();

                while (spawnCommands.TryDequeue(out var command))
                {
                    spawnBuffer.Add(command);
                }

                spawnCommands.Dispose();

#if UNITY_EDITOR
                LogUpdateSummary(_vegetationQuery, timeState.Tick);
#endif
            }
        }

        [BurstCompile]
        public partial struct UpdateVegetationReproductionJob : IJobEntity
        {
            public float DeltaTime;
            public uint CurrentTick;
            public BlobAssetReference<VegetationSpeciesCatalogBlob> SpeciesCatalogBlob;
            public NativeQueue<VegetationSpawnCommand>.ParallelWriter SpawnCommands;
            [ReadOnly] public ComponentLookup<VegetationProduction> ProductionLookup;

            public void Execute(
                ref VegetationReproduction reproduction,
                ref VegetationRandomState randomState,
                ref VegetationLifecycle lifecycle,
                DynamicBuffer<VegetationHistoryEvent> historyEvents,
                in VegetationSpeciesIndex speciesIndex,
                in VegetationId vegetationId,
                in LocalTransform transform,
                in Entity entity)
            {
                // Get species data from blob
                if (!SpeciesCatalogBlob.IsCreated || speciesIndex.Value >= SpeciesCatalogBlob.Value.Species.Length)
                {
                    return; // Invalid species index
                }

                ref var speciesData = ref SpeciesCatalogBlob.Value.Species[speciesIndex.Value];

                // Check if vegetation is mature enough to reproduce
                if (lifecycle.CurrentStage < VegetationLifecycle.LifecycleStage.Mature)
                {
                    return; // Not mature enough
                }

                // Update reproduction timer
                reproduction.ReproductionTimer += DeltaTime;

                // Check if cooldown has elapsed
                var requiredCooldown = speciesData.ReproductionCooldown > 0f
                    ? speciesData.ReproductionCooldown
                    : math.max(0.001f, reproduction.ReproductionCooldown);

                if (reproduction.ReproductionTimer < requiredCooldown)
                {
                    return;
                }

                var seedsPerEvent = math.max(0, speciesData.SeedsPerEvent);
                if (seedsPerEvent == 0)
                {
                    reproduction.ReproductionTimer = math.max(0f, reproduction.ReproductionTimer - requiredCooldown);
                    return;
                }

                var availableOffspringSlots = math.max(0, speciesData.OffspringCapPerParent - reproduction.ActiveOffspring);
                if (availableOffspringSlots <= 0)
                {
                    reproduction.ReproductionTimer = math.max(0f, reproduction.ReproductionTimer - requiredCooldown);
                    return;
                }

                var baseSeed = speciesData.ReproductionSeed
                               ^ (uint)math.max(1, vegetationId.Value)
                               ^ randomState.ReproductionRandomIndex
                               ^ CurrentTick;
                if (baseSeed == 0)
                {
                    baseSeed = 1u;
                }

                var rng = Unity.Mathematics.Random.CreateFromIndex(baseSeed);
                var spawnProbability = math.clamp(reproduction.SpreadChance <= 0f ? 1f : reproduction.SpreadChance, 0f, 1f);

                var spawnCount = (ushort)math.min(seedsPerEvent, availableOffspringSlots);
                if (rng.NextFloat() > spawnProbability)
                {
                    randomState.ReproductionRandomIndex++;
                    reproduction.ReproductionTimer = math.max(0f, reproduction.ReproductionTimer - requiredCooldown);
                    return;
                }

                if (spawnCount > 0)
                {
                    var spreadRadius = math.max(speciesData.SpreadRadius, reproduction.SpreadRange);
                    var padding = math.max(1, speciesData.GridCellPadding);

                    for (int i = 0; i < spawnCount; i++)
                    {
                        var angle = rng.NextFloat(0f, 2f * math.PI);
                        var radius = spreadRadius > 0f ? rng.NextFloat(0f, spreadRadius) : 0f;
                        var offset = new float3(math.cos(angle) * radius, 0f, math.sin(angle) * radius);

                        if (padding > 1)
                        {
                            var pad = (float)padding;
                            offset = math.round(offset / pad) * pad;
                        }

                        var resourceType = default(FixedString64Bytes);
                        if (ProductionLookup.HasComponent(entity))
                        {
                            resourceType = ProductionLookup[entity].ResourceTypeId;
                        }

                        var command = new VegetationSpawnCommand
                        {
                            SpeciesIndex = speciesIndex.Value,
                            Position = transform.Position + offset,
                            Parent = entity,
                            ParentId = (uint)math.abs(vegetationId.Value),
                            IssuedTick = CurrentTick,
                            SequenceId = reproduction.SpawnSequence + (uint)i,
                            ResourceTypeId = resourceType
                        };

                        SpawnCommands.Enqueue(command);
                    }

                    reproduction.ActiveOffspring = (ushort)math.min(ushort.MaxValue, reproduction.ActiveOffspring + spawnCount);
                    reproduction.SpawnSequence += spawnCount;

                    historyEvents.Add(new VegetationHistoryEvent
                    {
                        Type = VegetationHistoryEvent.EventType.Reproduced,
                        EventTick = CurrentTick,
                        Value = spawnCount
                    });
                }

                randomState.ReproductionRandomIndex++;
                reproduction.ReproductionTimer = math.max(0f, reproduction.ReproductionTimer - requiredCooldown);
            }
        }

#if UNITY_EDITOR
        [BurstDiscard]
        private static void LogMissingSpeciesLookup()
        {
            UnityEngine.Debug.LogWarning("[VegetationReproductionSystem] VegetationSpeciesLookup singleton not found. Skipping update.");
        }

        [BurstDiscard]
        private static void LogCatalogNotCreated()
        {
            UnityEngine.Debug.LogWarning("[VegetationReproductionSystem] Species catalog blob not created. Skipping update.");
        }

        [BurstDiscard]
        private static void LogUpdateSummary(EntityQuery query, uint tick)
        {
            UnityEngine.Debug.Log($"[VegetationReproductionSystem] Updated {query.CalculateEntityCount()} vegetation entities at tick {tick}");
        }
#endif
    }
}

