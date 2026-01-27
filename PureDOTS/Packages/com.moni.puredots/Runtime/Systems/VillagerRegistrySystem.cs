using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Knowledge;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Maintains a registry of all villagers for fast lookup by other systems.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VillagerSystemGroup), OrderFirst = true)]
    public partial struct VillagerRegistrySystem : ISystem
    {
        private EntityQuery _villagerQuery;
        private ComponentLookup<VillagerJobTicket> _ticketLookup;
        private ComponentLookup<VillagerDisciplineState> _disciplineLookup;
        private ComponentLookup<SpatialGridResidency> _residencyLookup;
        private ComponentLookup<VillagerNeeds> _needsLookup;
        private ComponentLookup<VillagerMood> _moodLookup;
        private ComponentLookup<VillagerAIState> _aiStateLookup;
        private ComponentLookup<VillagerCombatStats> _combatLookup;
        private ComponentLookup<VillagerKnowledge> _knowledgeLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _villagerQuery = SystemAPI.QueryBuilder()
                .WithAll<VillagerId, VillagerJob, VillagerAvailability, LocalTransform, VillagerFlags>()
                .Build();

            _ticketLookup = state.GetComponentLookup<VillagerJobTicket>(true);
            _disciplineLookup = state.GetComponentLookup<VillagerDisciplineState>(true);
            _residencyLookup = state.GetComponentLookup<SpatialGridResidency>(true);
            _needsLookup = state.GetComponentLookup<VillagerNeeds>(true);
            _moodLookup = state.GetComponentLookup<VillagerMood>(true);
            _aiStateLookup = state.GetComponentLookup<VillagerAIState>(true);
            _combatLookup = state.GetComponentLookup<VillagerCombatStats>(true);
            _knowledgeLookup = state.GetComponentLookup<VillagerKnowledge>(true);

            state.RequireForUpdate<VillagerRegistry>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<KnowledgeLessonEffectCatalog>();
            state.RequireForUpdate(_villagerQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            state.EntityManager.CompleteDependencyBeforeRO<VillagerAIState>();
            state.EntityManager.CompleteDependencyBeforeRO<SpatialGridResidency>();
            state.CompleteDependency();

            _ticketLookup.Update(ref state);
            _disciplineLookup.Update(ref state);
            _residencyLookup.Update(ref state);
            _needsLookup.Update(ref state);
            _moodLookup.Update(ref state);
            _aiStateLookup.Update(ref state);
            _combatLookup.Update(ref state);
            _knowledgeLookup.Update(ref state);

            var registryEntity = SystemAPI.GetSingletonEntity<VillagerRegistry>();
            var registry = SystemAPI.GetComponentRW<VillagerRegistry>(registryEntity);
            var entries = state.EntityManager.GetBuffer<VillagerRegistryEntry>(registryEntity);
            var lessonBuffer = state.EntityManager.GetBuffer<VillagerLessonRegistryEntry>(registryEntity);
            ref var registryMetadata = ref SystemAPI.GetComponentRW<RegistryMetadata>(registryEntity).ValueRW;

            var hasSpatialConfig = SystemAPI.TryGetSingleton(out SpatialGridConfig spatialConfig);
            var hasSpatialState = SystemAPI.TryGetSingleton(out SpatialGridState spatialState);
            var hasSpatialGrid = hasSpatialConfig
                                 && hasSpatialState
                                 && spatialConfig.CellCount > 0
                                 && spatialConfig.CellSize > 0f;

            var hasSyncState = SystemAPI.TryGetSingleton(out RegistrySpatialSyncState spatialSyncState);
            var lessonCatalog = SystemAPI.GetSingleton<KnowledgeLessonEffectCatalog>();
            var lessonCapacity = math.max(1, _villagerQuery.CalculateEntityCount() * 4);
            var totalVillagers = 0;
            var availableCount = 0;
            var idleCount = 0;
            var reservedCount = 0;
            var combatReadyCount = 0;

            var healthAccumulator = 0f;
            var moraleAccumulator = 0f;
            var energyAccumulator = 0f;
            var healthSampleCount = 0;
            var moraleSampleCount = 0;
            var energySampleCount = 0;

            var resolvedCount = 0;
            var fallbackCount = 0;
            var unmappedCount = 0;

            var lessonBuilder = new DeterministicRegistryBuilder<VillagerLessonRegistryEntry>(lessonCapacity, Allocator.Temp);
            var requireSpatialSync = false;
            var spatialVersionSource = 0u;
            try
            {
                requireSpatialSync = registryMetadata.SupportsSpatialQueries && hasSyncState && spatialSyncState.HasSpatialData;
                spatialVersionSource = hasSpatialGrid
                    ? spatialState.Version
                    : (hasSyncState && spatialSyncState.HasSpatialData ? spatialSyncState.SpatialVersion : 0u);

                var expectedCount = math.max(32, _villagerQuery.CalculateEntityCount());
                using var builder = new DeterministicRegistryBuilder<VillagerRegistryEntry>(expectedCount, Allocator.Temp);
                foreach (var (villagerId, job, availability, transform, flags, entity) in SystemAPI.Query<RefRO<VillagerId>, RefRO<VillagerJob>, RefRO<VillagerAvailability>, RefRO<LocalTransform>, RefRO<VillagerFlags>>()
                             .WithNone<PlaybackGuardTag>()
                             .WithEntityAccess())
                {
                // Skip dead villagers
                if (flags.ValueRO.IsDead)
                {
                    continue;
                }
                
                var position = transform.ValueRO.Position;
                var cellId = -1;
                var entrySpatialVersion = spatialVersionSource;

                if (hasSpatialGrid)
                {
                    var resolved = false;
                    var fallback = false;

                    if (_residencyLookup.HasComponent(entity))
                    {
                        var residency = _residencyLookup[entity];
                        if ((uint)residency.CellId < (uint)spatialConfig.CellCount && residency.Version == spatialState.Version)
                        {
                            cellId = residency.CellId;
                            entrySpatialVersion = residency.Version;
                            resolved = true;
                        }
                    }

                    if (!resolved)
                    {
                        SpatialHash.Quantize(position, spatialConfig, out var coords);
                        var computedCell = SpatialHash.Flatten(in coords, in spatialConfig);
                        if ((uint)computedCell < (uint)spatialConfig.CellCount)
                        {
                            cellId = computedCell;
                            entrySpatialVersion = spatialState.Version;
                            fallback = true;
                        }
                        else
                        {
                            cellId = -1;
                            entrySpatialVersion = 0;
                            unmappedCount++;
                        }
                    }

                    if (resolved)
                    {
                        resolvedCount++;
                    }
                    else if (fallback)
                    {
                        fallbackCount++;
                    }
                }

                var availabilityFlags = VillagerAvailabilityFlags.FromAvailability(availability.ValueRO);
                var isAvailable = (availabilityFlags & VillagerAvailabilityFlags.Available) != 0;
                var isReserved = availability.ValueRO.IsReserved != 0;

                var healthPercent = 0f;
                var energyPercentFloat = 0f;
                if (_needsLookup.HasComponent(entity))
                {
                    var needs = _needsLookup[entity];
                    if (needs.MaxHealth > 0f)
                    {
                        healthPercent = math.saturate(needs.Health / needs.MaxHealth) * 100f;
                        healthAccumulator += healthPercent;
                        healthSampleCount++;
                    }

                    energyPercentFloat = math.clamp(needs.EnergyFloat, 0f, 100f);
                    energyAccumulator += energyPercentFloat;
                    energySampleCount++;
                }

                var moralePercent = 0f;
                if (_moodLookup.HasComponent(entity))
                {
                    var mood = _moodLookup[entity];
                    moralePercent = math.clamp(mood.Mood, 0f, 100f);
                    moraleAccumulator += moralePercent;
                    moraleSampleCount++;
                }

                var energyPercent = (byte)math.clamp(math.round(energyPercentFloat), 0f, 100f);

                var aiStateValue = (byte)VillagerAIState.State.Idle;
                var aiGoalValue = (byte)VillagerAIState.Goal.None;
                var aiTarget = Entity.Null;

                if (_aiStateLookup.HasComponent(entity))
                {
                    var aiState = _aiStateLookup[entity];
                    aiStateValue = (byte)aiState.CurrentState;
                    aiGoalValue = (byte)aiState.CurrentGoal;
                    aiTarget = aiState.TargetEntity;
                }

                if (isAvailable)
                {
                    availableCount++;
                }

                if (!isReserved && isAvailable && aiStateValue == (byte)VillagerAIState.State.Idle)
                {
                    idleCount++;
                }

                if (isReserved)
                {
                    reservedCount++;
                }

                if (_combatLookup.HasComponent(entity) && isAvailable && !isReserved)
                {
                    combatReadyCount++;
                }

                var entry = new VillagerRegistryEntry
                {
                    VillagerEntity = entity,
                    VillagerId = villagerId.ValueRO.Value,
                    FactionId = villagerId.ValueRO.FactionId,
                    Position = position,
                    CellId = cellId,
                    SpatialVersion = entrySpatialVersion,
                    JobType = job.ValueRO.Type,
                    JobPhase = job.ValueRO.Phase,
                    ActiveTicketId = job.ValueRO.ActiveTicketId,
                    AvailabilityFlags = availabilityFlags,
                    CurrentResourceTypeIndex = ushort.MaxValue,
                    Discipline = (byte)VillagerDisciplineType.Unassigned,
                    HealthPercent = (byte)math.clamp(math.round(healthPercent), 0f, 100f),
                    MoralePercent = (byte)math.clamp(math.round(moralePercent), 0f, 100f),
                    EnergyPercent = energyPercent,
                    AIState = aiStateValue,
                    AIGoal = aiGoalValue,
                    CurrentTarget = aiTarget,
                    Productivity = job.ValueRO.Productivity
                };

                if (_ticketLookup.HasComponent(entity))
                {
                    var ticket = _ticketLookup[entity];
                    entry.ActiveTicketId = ticket.TicketId;
                    entry.CurrentResourceTypeIndex = ticket.ResourceTypeIndex;
                }

                if (_disciplineLookup.HasComponent(entity))
                {
                    var discipline = _disciplineLookup[entity];
                    entry.Discipline = (byte)discipline.Value;
                }

                if (_knowledgeLookup.HasComponent(entity))
                {
                    var knowledge = _knowledgeLookup[entity];
                    AppendLessonEntries(entity, in knowledge, lessonCatalog.Blob, ref lessonBuilder);
                }

                builder.Add(entry);

                totalVillagers++;
            }

            var continuity = hasSpatialGrid
                ? RegistryContinuitySnapshot.WithSpatialData(spatialVersionSource, resolvedCount, fallbackCount, unmappedCount, requireSpatialSync)
                : RegistryContinuitySnapshot.WithoutSpatialData(requireSpatialSync);

            builder.ApplyTo(ref entries, ref registryMetadata, timeState.Tick, continuity);
            lessonBuilder.ApplyTo(ref lessonBuffer);

            }
            finally
            {
                lessonBuilder.Dispose();
            }

            var averageHealth = healthSampleCount > 0 ? healthAccumulator / healthSampleCount : 0f;
            var averageMorale = moraleSampleCount > 0 ? moraleAccumulator / moraleSampleCount : 0f;
            var averageEnergy = energySampleCount > 0 ? energyAccumulator / energySampleCount : 0f;

            registry.ValueRW = new VillagerRegistry
            {
                TotalVillagers = totalVillagers,
                AvailableVillagers = availableCount,
                IdleVillagers = idleCount,
                ReservedVillagers = reservedCount,
                CombatReadyVillagers = combatReadyCount,
                AverageHealthPercent = averageHealth,
                AverageMoralePercent = averageMorale,
                AverageEnergyPercent = averageEnergy,
                LastUpdateTick = timeState.Tick,
                LastSpatialVersion = spatialVersionSource,
                SpatialResolvedCount = resolvedCount,
                SpatialFallbackCount = fallbackCount,
                SpatialUnmappedCount = unmappedCount
            };
        }

        private static void AppendLessonEntries(
            Entity entity,
            in VillagerKnowledge knowledge,
            BlobAssetReference<KnowledgeLessonEffectBlob> lessonCatalog,
            ref DeterministicRegistryBuilder<VillagerLessonRegistryEntry> builder)
        {
            if (knowledge.Lessons.Length == 0)
            {
                return;
            }

            for (var i = 0; i < knowledge.Lessons.Length; i++)
            {
                var lesson = knowledge.Lessons[i];
                if (lesson.LessonId.Length == 0)
                {
                    continue;
                }

                var entry = new VillagerLessonRegistryEntry
                {
                    VillagerEntity = entity,
                    LessonId = lesson.LessonId,
                    AxisId = default,
                    OppositeLessonId = default,
                    Progress = lesson.Progress,
                    Difficulty = 0,
                    MetadataFlags = 0
                };

                if (lessonCatalog.IsCreated && KnowledgeLessonEffectUtility.TryGetLessonMetadata(ref lessonCatalog.Value, lesson.LessonId, out var metadata))
                {
                    entry.AxisId = metadata.AxisId;
                    entry.OppositeLessonId = metadata.OppositeLessonId;
                    entry.Difficulty = metadata.Difficulty;
                    entry.MetadataFlags = (byte)metadata.Flags;
                }

                builder.Add(entry);
            }
        }
    }
}
