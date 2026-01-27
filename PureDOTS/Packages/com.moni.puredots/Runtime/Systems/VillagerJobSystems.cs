using PureDOTS.Runtime;
using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Knowledge;
using PureDOTS.Runtime.Registry;
using PureDOTS.Runtime.Resource;
using PureDOTS.Runtime.Skills;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Runtime.Villager;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace PureDOTS.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(TimeSystemGroup))]
    [UpdateAfter(typeof(CoreSingletonBootstrapSystem))]
    public partial struct VillagerJobBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var entityManager = state.EntityManager;

            EnsureEventStream(entityManager);
            EnsureRequestQueue(entityManager);
            EnsureDeliveryQueue(entityManager);
            EnsureDiagnostics(entityManager);

            state.Enabled = false;
        }

        public void OnUpdate(ref SystemState state)
        {
        }

        private static void EnsureEventStream(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<VillagerJobEventStream>());
            Entity eventEntity;
            if (query.IsEmptyIgnoreFilter)
            {
                eventEntity = entityManager.CreateEntity(typeof(VillagerJobEventStream), typeof(VillagerJobTicketSequence));
                entityManager.AddBuffer<VillagerJobEvent>(eventEntity);
                entityManager.SetComponentData(eventEntity, new VillagerJobTicketSequence { Value = 0 });
            }
            else
            {
                eventEntity = query.GetSingletonEntity();
                if (!entityManager.HasComponent<VillagerJobTicketSequence>(eventEntity))
                {
                    entityManager.AddComponentData(eventEntity, new VillagerJobTicketSequence { Value = 0 });
                }
                if (!entityManager.HasBuffer<VillagerJobEvent>(eventEntity))
                {
                    entityManager.AddBuffer<VillagerJobEvent>(eventEntity);
                }
            }
        }

        private static void EnsureRequestQueue(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<VillagerJobRequestQueue>());
            if (query.IsEmptyIgnoreFilter)
            {
                var entity = entityManager.CreateEntity(typeof(VillagerJobRequestQueue));
                entityManager.AddBuffer<VillagerJobRequest>(entity);
            }
            else
            {
                var entity = query.GetSingletonEntity();
                if (!entityManager.HasBuffer<VillagerJobRequest>(entity))
                {
                    entityManager.AddBuffer<VillagerJobRequest>(entity);
                }
            }
        }

        private static void EnsureDeliveryQueue(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<VillagerJobDeliveryQueue>());
            if (query.IsEmptyIgnoreFilter)
            {
                var entity = entityManager.CreateEntity(typeof(VillagerJobDeliveryQueue));
                entityManager.AddBuffer<VillagerJobDeliveryCommand>(entity);
            }
            else
            {
                var entity = query.GetSingletonEntity();
                if (!entityManager.HasBuffer<VillagerJobDeliveryCommand>(entity))
                {
                    entityManager.AddBuffer<VillagerJobDeliveryCommand>(entity);
                }
            }
        }

        private static void EnsureDiagnostics(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<VillagerJobDiagnostics>());
            if (query.IsEmptyIgnoreFilter)
            {
                var entity = entityManager.CreateEntity(typeof(VillagerJobDiagnostics));
                entityManager.SetComponentData(entity, default(VillagerJobDiagnostics));
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(VillagerJobFixedStepGroup), OrderFirst = true)]
    public partial struct VillagerJobInitializationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<VillagerJob>();
            state.RequireForUpdate<VillagerJobRequestQueue>();
            state.RequireForUpdate<VillagerJobDeliveryQueue>();
            state.RequireForUpdate<VillagerJobEventStream>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var requestEntity = SystemAPI.GetSingletonEntity<VillagerJobRequestQueue>();
            var requestBuffer = state.EntityManager.GetBuffer<VillagerJobRequest>(requestEntity);
            requestBuffer.Clear();

            var deliveryEntity = SystemAPI.GetSingletonEntity<VillagerJobDeliveryQueue>();
            var deliveryBuffer = state.EntityManager.GetBuffer<VillagerJobDeliveryCommand>(deliveryEntity);
            deliveryBuffer.Clear();

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var ecbSingleton = SystemAPI.GetSingletonRW<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            // Add missing VillagerJobTicket components (parallelized)
            var addTicketJob = new AddTicketJob
            {
                Ecb = ecb
            };
            state.Dependency = addTicketJob.ScheduleParallel(state.Dependency);

            // Add missing VillagerJobProgress components (parallelized)
            var addProgressJob = new AddProgressJob
            {
                Ecb = ecb,
                CurrentTick = timeState.Tick
            };
            state.Dependency = addProgressJob.ScheduleParallel(state.Dependency);

            // Add missing VillagerJobCarryItem buffers (parallelized)
            var addCarryBufferJob = new AddCarryBufferJob
            {
                Ecb = ecb
            };
            state.Dependency = addCarryBufferJob.ScheduleParallel(state.Dependency);

            // Add missing VillagerJobHistorySample buffers (parallelized)
            var addHistoryBufferJob = new AddHistoryBufferJob
            {
                Ecb = ecb
            };
            state.Dependency = addHistoryBufferJob.ScheduleParallel(state.Dependency);

            // Normalize job phases (parallelized)
            var normalizeJob = new NormalizeJobPhasesJob
            {
                CurrentTick = timeState.Tick
            };
            state.Dependency = normalizeJob.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct AddTicketJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter Ecb;

            public void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity, in VillagerJob job)
            {
                Ecb.AddComponent(chunkIndex, entity, new VillagerJobTicket
                {
                    TicketId = 0,
                    JobType = job.Type,
                    ResourceTypeIndex = ushort.MaxValue,
                    ResourceEntity = Entity.Null,
                    StorehouseEntity = Entity.Null,
                    Priority = 0,
                    Phase = (byte)VillagerJob.JobPhase.Idle,
                    ReservedUnits = 0f,
                    AssignedTick = 0,
                    LastProgressTick = 0
                });
            }
        }

        [BurstCompile]
        public partial struct AddProgressJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter Ecb;
            public uint CurrentTick;

            public void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity)
            {
                Ecb.AddComponent(chunkIndex, entity, new VillagerJobProgress
                {
                    Gathered = 0f,
                    Delivered = 0f,
                    TimeInPhase = 0f,
                    LastUpdateTick = CurrentTick
                });
            }
        }

        [BurstCompile]
        public partial struct AddCarryBufferJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter Ecb;

            public void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity)
            {
                Ecb.AddBuffer<VillagerJobCarryItem>(chunkIndex, entity);
            }
        }

        [BurstCompile]
        public partial struct AddHistoryBufferJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter Ecb;

            public void Execute([ChunkIndexInQuery] int chunkIndex, Entity entity)
            {
                Ecb.AddBuffer<VillagerJobHistorySample>(chunkIndex, entity);
            }
        }

        [BurstCompile]
        public partial struct NormalizeJobPhasesJob : IJobEntity
        {
            public uint CurrentTick;

            public void Execute(ref VillagerJob job)
            {
                if (job.Type == VillagerJob.JobType.None)
                {
                    job.Phase = VillagerJob.JobPhase.Idle;
                    job.ActiveTicketId = 0;
                    return;
                }

                if (job.Phase == 0)
                {
                    job.Phase = VillagerJob.JobPhase.Idle;
                    job.LastStateChangeTick = CurrentTick;
                }
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(VillagerJobFixedStepGroup))]
    [UpdateAfter(typeof(VillagerJobInitializationSystem))]
    public partial struct VillagerJobRequestSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<VillagerJob>();
            state.RequireForUpdate<VillagerJobTicket>();
            state.RequireForUpdate<VillagerAvailability>();
            state.RequireForUpdate<VillagerJobRequestQueue>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Gate by ScenarioState.EnableGodgame and initialization
            if (!SystemAPI.TryGetSingleton<ScenarioState>(out var scenario) || !scenario.EnableGodgame || !scenario.IsInitialized)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var requestEntity = SystemAPI.GetSingletonEntity<VillagerJobRequestQueue>();
            var requests = state.EntityManager.GetBuffer<VillagerJobRequest>(requestEntity);

            // Parallelize request collection using NativeList
            var requestList = new NativeList<VillagerJobRequest>(Allocator.TempJob);
            var cooldownLookup = SystemAPI.GetComponentLookup<VillagerWorkCooldown>(true);
            cooldownLookup.Update(ref state);
            var collectRequestsJob = new CollectRequestsJob
            {
                Requests = requestList.AsParallelWriter(),
                CurrentTick = timeState.Tick,
                CooldownLookup = cooldownLookup
            };
            state.Dependency = collectRequestsJob.ScheduleParallel(state.Dependency);
            state.Dependency.Complete();

            // Copy collected requests to buffer
            requests.Clear();
            requests.AddRange(requestList.AsArray());
            requestList.Dispose();
        }

        [BurstCompile]
        public partial struct CollectRequestsJob : IJobEntity
        {
            public NativeList<VillagerJobRequest>.ParallelWriter Requests;
            public uint CurrentTick;
            [ReadOnly] public ComponentLookup<VillagerWorkCooldown> CooldownLookup;

            public void Execute(
                Entity entity,
                ref VillagerJob job,
                ref VillagerJobTicket ticket,
                in VillagerAvailability availability)
            {
                if (job.Type == VillagerJob.JobType.None)
                {
                    return;
                }

                switch (job.Phase)
                {
                    case VillagerJob.JobPhase.Idle:
                    case VillagerJob.JobPhase.Completed:
                    case VillagerJob.JobPhase.Interrupted:
                        break;
                    default:
                        return;
                }

                if (availability.IsAvailable == 0)
                {
                    return;
                }

                if (CooldownLookup.HasComponent(entity))
                {
                    var cooldown = CooldownLookup[entity];
                    if (cooldown.EndTick > CurrentTick)
                    {
                        return;
                    }
                }

                ticket.JobType = job.Type;
                ticket.Priority = (byte)math.select((int)ticket.Priority, 1, availability.IsReserved != 0);
                ticket.Phase = (byte)VillagerJob.JobPhase.Idle;
                ticket.ResourceEntity = Entity.Null;
                ticket.StorehouseEntity = Entity.Null;
                ticket.ResourceTypeIndex = ushort.MaxValue;
                ticket.ReservedUnits = 0f;
                ticket.LastProgressTick = CurrentTick;

                job.Phase = VillagerJob.JobPhase.Idle;
                job.ActiveTicketId = 0;
                job.LastStateChangeTick = CurrentTick;

                Requests.AddNoResize(new VillagerJobRequest
                {
                    Villager = entity,
                    JobType = job.Type,
                    Priority = ticket.Priority
                });
            }
        }
    }

    /// <summary>
    /// WARM path: Job reassignment (replan).
    /// Only when work done/workplace changed/skills changed.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(WarmPathSystemGroup))]
    // Removed invalid UpdateAfter: VillagerJobRequestSystem runs in VillagerJobFixedStepGroup.
    public partial struct VillagerJobAssignmentSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<ResourceJobReservation> _resourceReservationLookup;
        private BufferLookup<ResourceActiveTicket> _resourceActiveTicketLookup;
        private ComponentLookup<StorehouseJobReservation> _storehouseReservationLookup;
        private BufferLookup<StorehouseReservationItem> _storehouseReservationItems;
        private ComponentLookup<ResourceSourceConfig> _resourceConfigLookup;
        
        
        public void OnCreate(ref SystemState state)
        {
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _resourceReservationLookup = state.GetComponentLookup<ResourceJobReservation>(false);
            _resourceActiveTicketLookup = state.GetBufferLookup<ResourceActiveTicket>(false);
            _storehouseReservationLookup = state.GetComponentLookup<StorehouseJobReservation>(false);
            _storehouseReservationItems = state.GetBufferLookup<StorehouseReservationItem>(false);
            _resourceConfigLookup = state.GetComponentLookup<ResourceSourceConfig>(true);
            
            state.RequireForUpdate<VillagerJobRequestQueue>();
            state.RequireForUpdate<ResourceRegistry>();
            state.RequireForUpdate<ResourceTypeIndex>();
            state.RequireForUpdate<VillagerJobEventStream>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<SpatialGridConfig>();
            state.RequireForUpdate<SpatialGridState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Gate by ScenarioState.EnableGodgame and initialization
            if (!SystemAPI.TryGetSingleton<ScenarioState>(out var scenario) || !scenario.EnableGodgame || !scenario.IsInitialized)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _transformLookup.Update(ref state);
            _resourceReservationLookup.Update(ref state);
            _resourceActiveTicketLookup.Update(ref state);
            _storehouseReservationLookup.Update(ref state);
            _storehouseReservationItems.Update(ref state);
            _resourceConfigLookup.Update(ref state);

            var requestEntity = SystemAPI.GetSingletonEntity<VillagerJobRequestQueue>();
            var requests = state.EntityManager.GetBuffer<VillagerJobRequest>(requestEntity);
            if (requests.Length == 0)
            {
                return;
            }

            if (!RegistryDirectoryLookup.TryGetRegistryBuffer<ResourceRegistryEntry>(ref state, RegistryKind.Resource, out var resourceEntries))
            {
                requests.Clear();
                return;
            }

            if (resourceEntries.Length == 0)
            {
                requests.Clear();
                return;
            }

            var eventEntity = SystemAPI.GetSingletonEntity<VillagerJobEventStream>();
            var events = state.EntityManager.GetBuffer<VillagerJobEvent>(eventEntity);
            var ticketSequence = SystemAPI.GetComponentRW<VillagerJobTicketSequence>(eventEntity);

            var spatialConfig = SystemAPI.GetSingleton<SpatialGridConfig>();
            var spatialState = SystemAPI.GetSingleton<SpatialGridState>();
            var hasSpatialData = resourceEntries.Length > 0 && spatialConfig.CellCount > 0 && spatialConfig.CellSize > 0f;

            var candidateEntryIndices = new NativeList<int>(Allocator.Temp);

            for (int i = 0; i < requests.Length; i++)
            {
                var request = requests[i];
                if (!_transformLookup.HasComponent(request.Villager))
                {
                    continue;
                }

                var villagerPos = _transformLookup[request.Villager].Position;
                var bestIndex = -1;
                var bestScore = float.MaxValue;
                var bestReservation = default(ResourceJobReservation);
                var targetConfig = default(ResourceSourceConfig);

                if (hasSpatialData)
                {
                    candidateEntryIndices.Clear();
                    SpatialHash.Quantize(villagerPos, spatialConfig, out var villagerCell);
                    var maxCellExtent = math.max(1, math.max(spatialConfig.CellCounts.x, math.max(spatialConfig.CellCounts.y, spatialConfig.CellCounts.z)));
                    var searchCellRadius = 1;

                    for (int attempt = 0; attempt < 3 && candidateEntryIndices.Length == 0; attempt++)
                    {
                        for (int r = 0; r < resourceEntries.Length; r++)
                        {
                            var entry = resourceEntries[r];
                            if (entry.CellId < 0 || entry.SpatialVersion != spatialState.Version)
                            {
                                continue;
                            }

                            if ((uint)entry.CellId >= (uint)spatialConfig.CellCount)
                            {
                                continue;
                            }

                            SpatialHash.Unflatten(entry.CellId, spatialConfig, out var entryCell);
                            if (entryCell.x < 0)
                            {
                                continue;
                            }

                            var cellDelta = math.abs(entryCell - villagerCell);
                            if (math.cmax(cellDelta) <= searchCellRadius)
                            {
                                AddUniqueIndex(ref candidateEntryIndices, r);
                            }
                        }

                        searchCellRadius = math.min(searchCellRadius * 2, maxCellExtent);
                    }

                    for (int c = 0; c < candidateEntryIndices.Length; c++)
                    {
                        TryScoreResourceCandidate(
                            candidateEntryIndices[c],
                            resourceEntries,
                            villagerPos,
                            ref _transformLookup,
                            ref _resourceReservationLookup,
                            ref _resourceConfigLookup,
                            ref bestIndex,
                            ref bestScore,
                            ref bestReservation,
                            ref targetConfig);
                    }
                }

                if (bestIndex < 0)
                {
                    for (int r = 0; r < resourceEntries.Length; r++)
                    {
                        TryScoreResourceCandidate(
                            r,
                            resourceEntries,
                            villagerPos,
                            ref _transformLookup,
                            ref _resourceReservationLookup,
                            ref _resourceConfigLookup,
                            ref bestIndex,
                            ref bestScore,
                            ref bestReservation,
                            ref targetConfig);
                    }
                }

                if (bestIndex < 0)
                {
                    continue;
                }

                var resourceEntry = resourceEntries[bestIndex];
                var villagerJob = SystemAPI.GetComponentRW<VillagerJob>(request.Villager);
                var villagerTicket = SystemAPI.GetComponentRW<VillagerJobTicket>(request.Villager);
                var villagerProgress = SystemAPI.GetComponentRW<VillagerJobProgress>(request.Villager);

                var newTicketId = ticketSequence.ValueRW.Value + 1u;
                if (newTicketId == 0u)
                {
                    newTicketId = 1u;
                }
                ticketSequence.ValueRW.Value = newTicketId;

                var reservedUnits = math.min(resourceEntry.UnitsRemaining, targetConfig.GatherRatePerWorker * timeState.FixedDeltaTime * 5f);
                bestReservation.ActiveTickets = (byte)math.min(255, bestReservation.ActiveTickets + 1);
                bestReservation.ReservedUnits += reservedUnits;
                bestReservation.LastMutationTick = timeState.Tick;
                bestReservation.ClaimFlags |= ResourceRegistryClaimFlags.VillagerReserved;

                _resourceReservationLookup[resourceEntry.SourceEntity] = bestReservation;

                if (_resourceActiveTicketLookup.HasBuffer(resourceEntry.SourceEntity))
                {
                    var activeTickets = _resourceActiveTicketLookup[resourceEntry.SourceEntity];
                    activeTickets.Add(new ResourceActiveTicket
                    {
                        Villager = request.Villager,
                        TicketId = newTicketId,
                        ReservedUnits = reservedUnits
                    });
                }

                villagerJob.ValueRW.Phase = VillagerJob.JobPhase.Assigned;
                villagerJob.ValueRW.ActiveTicketId = newTicketId;
                villagerJob.ValueRW.LastStateChangeTick = timeState.Tick;

                villagerTicket.ValueRW.TicketId = newTicketId;
                villagerTicket.ValueRW.JobType = request.JobType;
                villagerTicket.ValueRW.ResourceTypeIndex = resourceEntry.ResourceTypeIndex;
                villagerTicket.ValueRW.ResourceEntity = resourceEntry.SourceEntity;
                villagerTicket.ValueRW.StorehouseEntity = Entity.Null;
                villagerTicket.ValueRW.Priority = request.Priority;
                villagerTicket.ValueRW.Phase = (byte)VillagerJob.JobPhase.Assigned;
                villagerTicket.ValueRW.ReservedUnits = reservedUnits;
                villagerTicket.ValueRW.AssignedTick = timeState.Tick;
                villagerTicket.ValueRW.LastProgressTick = timeState.Tick;

                villagerProgress.ValueRW.TimeInPhase = 0f;

                events.Add(new VillagerJobEvent
                {
                    Tick = timeState.Tick,
                    Villager = request.Villager,
                    EventType = VillagerJobEventType.JobAssigned,
                    ResourceTypeIndex = resourceEntry.ResourceTypeIndex,
                    Amount = 0f,
                    TicketId = newTicketId
                });
            }

            candidateEntryIndices.Dispose();

            requests.Clear();
        }

        private static void AddUniqueIndex(ref NativeList<int> indices, int value)
        {
            for (int i = 0; i < indices.Length; i++)
            {
                if (indices[i] == value)
                {
                    return;
                }
            }

            indices.Add(value);
        }

        private static void TryScoreResourceCandidate(
            int entryIndex,
            DynamicBuffer<ResourceRegistryEntry> entries,
            float3 villagerPos,
            ref ComponentLookup<LocalTransform> transformLookup,
            ref ComponentLookup<ResourceJobReservation> reservationLookup,
            ref ComponentLookup<ResourceSourceConfig> configLookup,
            ref int bestIndex,
            ref float bestScore,
            ref ResourceJobReservation bestReservation,
            ref ResourceSourceConfig targetConfig)
        {
            if ((uint)entryIndex >= (uint)entries.Length)
            {
                return;
            }

            var entry = entries[entryIndex];
            if (entry.UnitsRemaining <= 0f)
            {
                return;
            }

            if (entry.Tier != ResourceTier.Raw)
            {
                return;
            }

            if (!transformLookup.HasComponent(entry.SourceEntity))
            {
                return;
            }

            var reservation = reservationLookup.HasComponent(entry.SourceEntity)
                ? reservationLookup[entry.SourceEntity]
                : new ResourceJobReservation();

            var config = configLookup.HasComponent(entry.SourceEntity)
                ? configLookup[entry.SourceEntity]
                : new ResourceSourceConfig { GatherRatePerWorker = 10f, MaxSimultaneousWorkers = 1 };

            if (config.MaxSimultaneousWorkers <= 0)
            {
                return;
            }

            if (reservation.ActiveTickets >= config.MaxSimultaneousWorkers)
            {
                return;
            }

            if ((reservation.ClaimFlags & ResourceRegistryClaimFlags.PlayerClaim) != 0)
            {
                return;
            }

            var availableUnits = entry.UnitsRemaining - reservation.ReservedUnits;
            if (availableUnits <= 0f)
            {
                return;
            }

            var distSq = math.distancesq(villagerPos, entry.Position);
            var score = distSq + (reservation.ActiveTickets * 5f);
            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = entryIndex;
                bestReservation = reservation;
                targetConfig = config;
            }
        }
    }

    /// <summary>
    /// HOT path: Executes current job step (follow plan).
    /// Performs job anim/loop with simple timers.
    /// No job re-selection every tick.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(HotPathSystemGroup))]
    // Removed invalid UpdateAfter: VillagerJobAssignmentSystem runs in WarmPathSystemGroup.
    public partial struct VillagerJobExecutionSystem : ISystem
    {
        private ComponentLookup<ResourceSourceState> _resourceStateLookup;
        private ComponentLookup<ResourceSourceConfig> _resourceConfigLookup;
        private ComponentLookup<ResourceJobReservation> _resourceReservationLookup;
        private BufferLookup<ResourceActiveTicket> _resourceActiveTicketLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<SkillSet> _skillSetLookup;
        private ComponentLookup<VillagerKnowledge> _knowledgeLookup;
        private ComponentLookup<VillagerStats> _statsLookup;
        private ComponentLookup<VillagerAttributes> _attributesLookup;
        private BufferLookup<VillagerLessonShare> _lessonShareLookup;
        private BufferLookup<VillagerJobEvent> _eventBufferLookup;
        // Construction lookups
        private ComponentLookup<ConstructionSiteProgress> _constructionProgressLookup;
        private BufferLookup<ConstructionDeliveredElement> _constructionDeliveredLookup;
        private ComponentLookup<ConstructionSiteFlags> _constructionFlagsLookup;
        // Combat lookups
        private ComponentLookup<VillagerCombatStats> _combatStatsLookup;
        private ComponentLookup<VillagerFlags> _villagerFlagsLookup;
        private const float SecondsPerSimYear = 600f;
        
        // Instance fields for Burst-compatible FixedString patterns (initialized in OnCreate)
        private FixedString64Bytes _resourceIdIronOre;
        private FixedString64Bytes _resourceIdRareMetals;
        private FixedString64Bytes _resourceIdIronOak;
        private FixedString64Bytes _lessonIdIronOre;
        private FixedString64Bytes _lessonIdRareMetals;
        private FixedString64Bytes _lessonIdIronOak;
        private FixedString64Bytes _lessonIdGeneral;

        public void OnCreate(ref SystemState state)
        {
            _resourceStateLookup = state.GetComponentLookup<ResourceSourceState>(false);
            _resourceConfigLookup = state.GetComponentLookup<ResourceSourceConfig>(true);
            _resourceReservationLookup = state.GetComponentLookup<ResourceJobReservation>(false);
            _resourceActiveTicketLookup = state.GetBufferLookup<ResourceActiveTicket>(false);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _skillSetLookup = state.GetComponentLookup<SkillSet>(false);
            _knowledgeLookup = state.GetComponentLookup<VillagerKnowledge>(false);
            _statsLookup = state.GetComponentLookup<VillagerStats>(true);
            _attributesLookup = state.GetComponentLookup<VillagerAttributes>(true);
            _lessonShareLookup = state.GetBufferLookup<VillagerLessonShare>(false);
            _eventBufferLookup = state.GetBufferLookup<VillagerJobEvent>(false);
            // Construction lookups
            _constructionProgressLookup = state.GetComponentLookup<ConstructionSiteProgress>(false);
            _constructionDeliveredLookup = state.GetBufferLookup<ConstructionDeliveredElement>(false);
            _constructionFlagsLookup = state.GetComponentLookup<ConstructionSiteFlags>(false);
            // Combat lookups
            _combatStatsLookup = state.GetComponentLookup<VillagerCombatStats>(false);
            _villagerFlagsLookup = state.GetComponentLookup<VillagerFlags>(false);
            
            // Initialize FixedString patterns (OnCreate is not Burst-compiled)
            _resourceIdIronOre = new FixedString64Bytes("space4x.minerals");
            _resourceIdRareMetals = new FixedString64Bytes("space4x.rare_metals");
            _resourceIdIronOak = new FixedString64Bytes("resource.tree.ironoak");
            _lessonIdIronOre = new FixedString64Bytes("lesson.harvest.iron_ore");
            _lessonIdRareMetals = new FixedString64Bytes("lesson.harvest.legendary_alloy");
            _lessonIdIronOak = new FixedString64Bytes("lesson.harvest.ironoak");
            _lessonIdGeneral = new FixedString64Bytes("lesson.harvest.general");

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<VillagerJobTicket>();
            state.RequireForUpdate<VillagerJobEventStream>();
            state.RequireForUpdate<ResourceTypeIndex>();
            state.RequireForUpdate<KnowledgeLessonEffectCatalog>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Gate by ScenarioState.EnableGodgame and initialization
            if (!SystemAPI.TryGetSingleton<ScenarioState>(out var scenario) || !scenario.EnableGodgame || !scenario.IsInitialized)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _resourceStateLookup.Update(ref state);
            _resourceConfigLookup.Update(ref state);
            _resourceReservationLookup.Update(ref state);
            _resourceActiveTicketLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _skillSetLookup.Update(ref state);
            _knowledgeLookup.Update(ref state);
            _statsLookup.Update(ref state);
            _attributesLookup.Update(ref state);
            _lessonShareLookup.Update(ref state);
            _eventBufferLookup.Update(ref state);
            // Construction lookups
            _constructionProgressLookup.Update(ref state);
            _constructionDeliveredLookup.Update(ref state);
            _constructionFlagsLookup.Update(ref state);
            // Combat lookups
            _combatStatsLookup.Update(ref state);
            _villagerFlagsLookup.Update(ref state);

            var gatherDistanceSq = 9f;
            var deltaTime = timeState.FixedDeltaTime;
            var eventEntity = SystemAPI.GetSingletonEntity<VillagerJobEventStream>();
            var lessonCatalog = SystemAPI.GetSingleton<KnowledgeLessonEffectCatalog>();
            var lessonBlob = lessonCatalog.Blob;
            var xpCurve = SystemAPI.TryGetSingleton(out SkillXpCurveConfig xpConfig)
                ? xpConfig
                : SkillXpCurveConfig.CreateDefaults();
            var resourceCatalog = SystemAPI.GetSingleton<ResourceTypeIndex>();
            if (!resourceCatalog.Catalog.IsCreated)
            {
                return;
            }

            // Parallelize job execution
            var executeJob = new ExecuteJobJob
            {
                ResourceStateLookup = _resourceStateLookup,
                ResourceConfigLookup = _resourceConfigLookup,
                ResourceReservationLookup = _resourceReservationLookup,
                ResourceActiveTicketLookup = _resourceActiveTicketLookup,
                TransformLookup = _transformLookup,
                SkillSetLookup = _skillSetLookup,
                KnowledgeLookup = _knowledgeLookup,
                StatsLookup = _statsLookup,
                AttributesLookup = _attributesLookup,
                LessonShareLookup = _lessonShareLookup,
                EventBuffers = _eventBufferLookup,
                EventEntity = eventEntity,
                LessonBlob = lessonBlob,
                XpCurve = xpCurve,
                ResourceCatalog = resourceCatalog.Catalog,
                GatherDistanceSq = gatherDistanceSq,
                DeltaTime = deltaTime,
                CurrentTick = timeState.Tick,
                // Construction lookups
                ConstructionProgressLookup = _constructionProgressLookup,
                ConstructionDeliveredLookup = _constructionDeliveredLookup,
                ConstructionFlagsLookup = _constructionFlagsLookup,
                // Combat lookups
                CombatStatsLookup = _combatStatsLookup,
                VillagerFlagsLookup = _villagerFlagsLookup
            };
            state.Dependency = executeJob.ScheduleParallel(state.Dependency);
            state.Dependency.Complete();
        }

        [BurstCompile]
        public partial struct ExecuteJobJob : IJobEntity
        {
            [NativeDisableParallelForRestriction] public ComponentLookup<ResourceSourceState> ResourceStateLookup;
            [ReadOnly] public ComponentLookup<ResourceSourceConfig> ResourceConfigLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<ResourceJobReservation> ResourceReservationLookup;
            [NativeDisableParallelForRestriction] public BufferLookup<ResourceActiveTicket> ResourceActiveTicketLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<SkillSet> SkillSetLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<VillagerKnowledge> KnowledgeLookup;
            [ReadOnly] public ComponentLookup<VillagerStats> StatsLookup;
            [ReadOnly] public ComponentLookup<VillagerAttributes> AttributesLookup;
            [NativeDisableParallelForRestriction] public BufferLookup<VillagerLessonShare> LessonShareLookup;
            [NativeDisableParallelForRestriction] public BufferLookup<VillagerJobEvent> EventBuffers;
            public Entity EventEntity;
            [ReadOnly] public BlobAssetReference<KnowledgeLessonEffectBlob> LessonBlob;
            [ReadOnly] public SkillXpCurveConfig XpCurve;
            [ReadOnly] public BlobAssetReference<ResourceTypeIndexBlob> ResourceCatalog;
            public float GatherDistanceSq;
            public float DeltaTime;
            public uint CurrentTick;
            // Construction lookups
            [NativeDisableParallelForRestriction] public ComponentLookup<ConstructionSiteProgress> ConstructionProgressLookup;
            [NativeDisableParallelForRestriction] public BufferLookup<ConstructionDeliveredElement> ConstructionDeliveredLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<ConstructionSiteFlags> ConstructionFlagsLookup;
            // Combat lookups
            [NativeDisableParallelForRestriction] public ComponentLookup<VillagerCombatStats> CombatStatsLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<VillagerFlags> VillagerFlagsLookup;

            public void Execute(
                Entity entity,
                ref VillagerJob job,
                ref VillagerJobTicket ticket,
                ref VillagerJobProgress progress,
                in VillagerNeeds needs,
                in LocalTransform transform,
                DynamicBuffer<VillagerJobCarryItem> carry)
            {
                if (job.Type == VillagerJob.JobType.None)
                {
                    return;
                }

                // Delegate to behavior methods based on job type
                switch (job.Type)
                {
                    case VillagerJob.JobType.Gatherer:
                    case VillagerJob.JobType.Farmer:
                    case VillagerJob.JobType.Hunter:
                        VillagerJobBehaviors.ExecuteGather(
                            entity, ref job, ref ticket, ref progress, needs, transform, carry,
                            ResourceStateLookup, ResourceConfigLookup, ResourceReservationLookup,
                            TransformLookup, SkillSetLookup, KnowledgeLookup, StatsLookup,
                            AttributesLookup, LessonShareLookup, EventBuffers, EventEntity,
                            LessonBlob, XpCurve, ResourceCatalog, GatherDistanceSq, DeltaTime, CurrentTick);
                        break;

                    case VillagerJob.JobType.Builder:
                        VillagerJobBehaviors.ExecuteBuild(
                            entity, ref job, ref ticket, ref progress, needs, transform, carry,
                            TransformLookup, SkillSetLookup, EventBuffers, EventEntity,
                            ConstructionProgressLookup, ConstructionDeliveredLookup, ConstructionFlagsLookup,
                            ResourceCatalog, XpCurve, GatherDistanceSq, DeltaTime, CurrentTick);
                        break;

                    case VillagerJob.JobType.Crafter:
                        VillagerJobBehaviors.ExecuteCraft(
                            entity, ref job, ref ticket, ref progress, needs, transform, carry,
                            TransformLookup, SkillSetLookup, EventBuffers, EventEntity,
                            ResourceCatalog, XpCurve, GatherDistanceSq, DeltaTime, CurrentTick);
                        break;

                    case VillagerJob.JobType.Guard:
                        VillagerJobBehaviors.ExecuteCombat(
                            entity, ref job, ref ticket, ref progress, needs, transform, carry,
                            TransformLookup, CombatStatsLookup, VillagerFlagsLookup, EventBuffers, EventEntity,
                            XpCurve, GatherDistanceSq, DeltaTime, CurrentTick);
                        break;

                    default:
                        // Unknown job type, do nothing
                        break;
                }
            }
        }

        private static float GetCarryAmount(DynamicBuffer<VillagerJobCarryItem> carry, ushort resourceTypeIndex)
        {
            var total = 0f;
            for (int i = 0; i < carry.Length; i++)
            {
                if (carry[i].ResourceTypeIndex == resourceTypeIndex)
                {
                    total += carry[i].Amount;
                }
            }
            return total;
        }

        private static SkillId ResolveSkillId(VillagerJob.JobType jobType)
        {
            return jobType switch
            {
                VillagerJob.JobType.Hunter => SkillId.AnimalHandling,
                VillagerJob.JobType.Crafter => SkillId.Processing,
                _ => SkillId.HarvestBotany
            };
        }

        private static XpPool ResolveXpPool(SkillId skillId)
        {
            return skillId switch
            {
                SkillId.AnimalHandling => XpPool.Will,
                SkillId.Processing => XpPool.Finesse,
                SkillId.Mining => XpPool.Physique,
                SkillId.HarvestBotany => XpPool.Physique,
                _ => XpPool.General
            };
        }

        private void GrantHarvestXp(Entity villager, SkillId skillId, float gatherAmount, in SkillXpCurveConfig xpCurve)
        {
            if (!_skillSetLookup.HasComponent(villager) || gatherAmount <= 0f)
            {
                return;
            }

            var skillSet = _skillSetLookup[villager];
            var pool = ResolveXpPool(skillId);
            var scalar = xpCurve.GetScalar(pool);
            var adjusted = gatherAmount * scalar;
            skillSet.AddSkillXp(skillId, adjusted);
            switch (pool)
            {
                case XpPool.Physique:
                    skillSet.PhysiqueXp += adjusted;
                    break;
                case XpPool.Finesse:
                    skillSet.FinesseXp += adjusted;
                    break;
                case XpPool.Will:
                    skillSet.WillXp += adjusted;
                    break;
                default:
                    skillSet.GeneralXp += adjusted;
                    break;
            }
            _skillSetLookup[villager] = skillSet;
        }

        private FixedString64Bytes MapPlaceholderLesson(in FixedString64Bytes resourceId)
        {
            if (resourceId.Equals(_resourceIdIronOre))
            {
                return _lessonIdIronOre;
            }

            if (resourceId.Equals(_resourceIdRareMetals))
            {
                return _lessonIdRareMetals;
            }

            if (resourceId.Equals(_resourceIdIronOak))
            {
                return _lessonIdIronOak;
            }

            return _lessonIdGeneral;
        }

        private static FixedString64Bytes ResolveResourceId(BlobAssetReference<ResourceTypeIndexBlob> catalog, ushort resourceTypeIndex)
        {
            if (!catalog.IsCreated)
            {
                return default;
            }

            ref var blob = ref catalog.Value;
            if (resourceTypeIndex >= blob.Ids.Length)
            {
                return default;
            }

            return blob.Ids[resourceTypeIndex];
        }

        private float ResolveAgeYears(Entity entity, uint currentTick, float fixedDeltaTime)
        {
            if (!_statsLookup.HasComponent(entity))
            {
                return 25f;
            }

            var stats = _statsLookup[entity];
            var livedTicks = currentTick >= stats.BirthTick ? currentTick - stats.BirthTick : 0u;
            var livedSeconds = livedTicks * fixedDeltaTime;
            return math.max(1f, livedSeconds / SecondsPerSimYear);
        }

        private void ResolveMindStats(Entity entity, out float intelligence, out float wisdom)
        {
            if (_attributesLookup.HasComponent(entity))
            {
                var attributes = _attributesLookup[entity];
                intelligence = attributes.Intelligence;
                wisdom = attributes.Wisdom;
            }
            else
            {
                intelligence = 50f;
                wisdom = 50f;
            }
        }

        private static float ComputeAgeLearningScalar(float ageYears)
        {
            if (ageYears <= 12f)
            {
                return math.lerp(1.75f, 1.3f, math.saturate(ageYears / 12f));
            }

            if (ageYears <= 25f)
            {
                return math.lerp(1.3f, 1f, (ageYears - 12f) / 13f);
            }

            if (ageYears <= 45f)
            {
                return 1f;
            }

            if (ageYears <= 70f)
            {
                return math.lerp(1f, 0.85f, (ageYears - 45f) / 25f);
            }

            return 0.7f;
        }

        private static float ComputeMindScalar(float intelligence, float wisdom)
        {
            var combined = math.max(5f, (intelligence + wisdom) * 0.5f);
            return math.clamp(0.6f + combined / 200f, 0.6f, 1.8f);
        }

        private static float ConsumeLessonShares(ref DynamicBuffer<VillagerLessonShare> shares, in FixedString64Bytes lessonId)
        {
            if (!shares.IsCreated || shares.Length == 0)
            {
                return 0f;
            }

            float granted = 0f;
            for (int i = 0; i < shares.Length; i++)
            {
                if (!shares[i].LessonId.Equals(lessonId) || shares[i].Progress <= 0f)
                {
                    continue;
                }

                granted += shares[i].Progress;
                var entry = shares[i];
                entry.Progress = 0f;
                shares[i] = entry;
            }

            return granted;
        }

        private static float ApplyOppositionRules(ref VillagerKnowledge knowledge, in KnowledgeLessonMetadata metadata, float delta)
        {
            if (delta <= 0f || metadata.OppositeLessonId.Length == 0)
            {
                return delta;
            }

            var oppositeProgress = knowledge.GetProgress(metadata.OppositeLessonId);
            if (oppositeProgress > 0f)
            {
                var penalty = delta * 0.33f;
                knowledge.AddProgress(metadata.OppositeLessonId, -penalty, out _);
            }

            if ((metadata.Flags & KnowledgeLessonFlags.AllowParallelOpposites) == 0 && oppositeProgress < 1f)
            {
                delta *= 0.4f;
            }

            return delta;
        }

        private bool TryLearnResourceLesson(
            ref VillagerKnowledge knowledge,
            in FixedString64Bytes resourceId,
            float skillLevel,
            float gatherAmount,
            float ageYears,
            float intelligence,
            float wisdom,
            DynamicBuffer<VillagerLessonShare> lessonShares,
            ref KnowledgeLessonEffectBlob lessonBlob)
        {
            if (resourceId.Length == 0 || gatherAmount <= 0f)
            {
                return false;
            }

            var lessonId = MapPlaceholderLesson(resourceId);
            if (lessonId.Length == 0)
            {
                return false;
            }

            if (knowledge.FindLessonIndex(lessonId) < 0 && knowledge.Lessons.Length >= knowledge.Lessons.Capacity)
            {
                return false;
            }

            var previousProgress = knowledge.GetProgress(lessonId);
            var hasMetadata = KnowledgeLessonEffectUtility.TryGetLessonMetadata(ref lessonBlob, lessonId, out var metadata);
            var difficulty = hasMetadata && metadata.Difficulty > 0 ? metadata.Difficulty : (byte)25;

            var baseDelta = gatherAmount * 0.001f;
            baseDelta *= math.max(0.35f, skillLevel / 120f);
            baseDelta /= math.max(10f, difficulty);

            var delta = baseDelta * ComputeAgeLearningScalar(ageYears) * ComputeMindScalar(intelligence, wisdom);
            delta += ConsumeLessonShares(ref lessonShares, lessonId);

            if (hasMetadata)
            {
                delta = ApplyOppositionRules(ref knowledge, metadata, delta);
            }

            if (delta <= 0f)
            {
                return false;
            }

            knowledge.AddProgress(lessonId, delta, out var newProgress);
            return newProgress > previousProgress;
        }

    }

    [BurstCompile]
    [UpdateInGroup(typeof(VillagerJobFixedStepGroup))]
    // Removed invalid UpdateAfter: VillagerJobExecutionSystem runs in HotPathSystemGroup; cross-group ordering must be handled by group scheduling.
    public partial struct VillagerJobDeliverySystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<ResourceJobReservation> _resourceReservationLookup;
        private BufferLookup<ResourceActiveTicket> _resourceActiveTicketLookup;
        private ComponentLookup<StorehouseJobReservation> _storehouseReservationLookup;
        private BufferLookup<StorehouseReservationItem> _storehouseReservationItems;

        public void OnCreate(ref SystemState state)
        {
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _resourceReservationLookup = state.GetComponentLookup<ResourceJobReservation>(false);
            _resourceActiveTicketLookup = state.GetBufferLookup<ResourceActiveTicket>(false);
            _storehouseReservationLookup = state.GetComponentLookup<StorehouseJobReservation>(false);
            _storehouseReservationItems = state.GetBufferLookup<StorehouseReservationItem>(false);

            state.RequireForUpdate<StorehouseRegistry>();
            state.RequireForUpdate<VillagerJobTicket>();
            state.RequireForUpdate<VillagerJobEventStream>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<SpatialGridConfig>();
            state.RequireForUpdate<SpatialGridState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Gate by ScenarioState.EnableGodgame and initialization
            if (!SystemAPI.TryGetSingleton<ScenarioState>(out var scenario) || !scenario.EnableGodgame || !scenario.IsInitialized)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _transformLookup.Update(ref state);
            _resourceReservationLookup.Update(ref state);
            _resourceActiveTicketLookup.Update(ref state);
            _storehouseReservationLookup.Update(ref state);
            _storehouseReservationItems.Update(ref state);

            var storehouseEntity = SystemAPI.GetSingletonEntity<StorehouseRegistry>();
            var storehouseEntries = state.EntityManager.GetBuffer<StorehouseRegistryEntry>(storehouseEntity);
            if (storehouseEntries.Length == 0)
            {
                return;
            }

            var eventEntity = SystemAPI.GetSingletonEntity<VillagerJobEventStream>();
            var events = state.EntityManager.GetBuffer<VillagerJobEvent>(eventEntity);

            var spatialConfig = SystemAPI.GetSingleton<SpatialGridConfig>();
            var spatialState = SystemAPI.GetSingleton<SpatialGridState>();
            var hasSpatialData = storehouseEntries.Length > 0 && spatialConfig.CellCount > 0 && spatialConfig.CellSize > 0f;

            var storehouseCandidateIndices = new NativeList<int>(Allocator.Temp);

            foreach (var (job, ticket, progress, carry, aiState, transform, entity) in SystemAPI.Query<RefRW<VillagerJob>, RefRW<VillagerJobTicket>, RefRW<VillagerJobProgress>, DynamicBuffer<VillagerJobCarryItem>, RefRW<VillagerAIState>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                if (job.ValueRO.Phase != VillagerJob.JobPhase.Delivering)
                {
                    continue;
                }

                var carriedAmount = GetCarryAmount(carry, ticket.ValueRO.ResourceTypeIndex);
                if (carriedAmount <= 0f)
                {
                    CompleteJob(ref job.ValueRW, ref ticket.ValueRW, ref progress.ValueRW, carry, timeState.Tick);
                    events.Add(new VillagerJobEvent
                    {
                        Tick = timeState.Tick,
                        Villager = entity,
                        EventType = VillagerJobEventType.JobCompleted,
                        ResourceTypeIndex = ticket.ValueRO.ResourceTypeIndex,
                        Amount = 0f,
                        TicketId = ticket.ValueRO.TicketId
                    });
                    continue;
                }

                if (ticket.ValueRO.StorehouseEntity == Entity.Null)
                {
                    var bestStorehouse = Entity.Null;
                    var bestScore = float.MaxValue;
                    var villagerPos = transform.ValueRO.Position;
                    var resourceTypeIndex = ticket.ValueRO.ResourceTypeIndex;

                    if (hasSpatialData)
                    {
                        storehouseCandidateIndices.Clear();
                        SpatialHash.Quantize(villagerPos, spatialConfig, out var villagerCell);
                        var maxCellExtent = math.max(1, math.max(spatialConfig.CellCounts.x, math.max(spatialConfig.CellCounts.y, spatialConfig.CellCounts.z)));
                        var searchCellRadius = 1;

                        for (int attempt = 0; attempt < 3 && storehouseCandidateIndices.Length == 0; attempt++)
                        {
                            for (int s = 0; s < storehouseEntries.Length; s++)
                            {
                                var entry = storehouseEntries[s];
                                if (entry.CellId < 0 || entry.SpatialVersion != spatialState.Version)
                                {
                                    continue;
                                }

                                if ((uint)entry.CellId >= (uint)spatialConfig.CellCount)
                                {
                                    continue;
                                }

                                SpatialHash.Unflatten(entry.CellId, spatialConfig, out var entryCell);
                                if (entryCell.x < 0)
                                {
                                    continue;
                                }

                                var cellDelta = math.abs(entryCell - villagerCell);
                                if (math.cmax(cellDelta) <= searchCellRadius)
                                {
                                    AddUniqueIndex(ref storehouseCandidateIndices, s);
                                }
                            }

                            searchCellRadius = math.min(searchCellRadius * 2, maxCellExtent);
                        }

                        for (int c = 0; c < storehouseCandidateIndices.Length; c++)
                        {
                            TryScoreStorehouseCandidate(
                                storehouseCandidateIndices[c],
                                storehouseEntries,
                                villagerPos,
                                resourceTypeIndex,
                                ref _transformLookup,
                                ref bestStorehouse,
                                ref bestScore);
                        }
                    }

                    if (bestStorehouse == Entity.Null)
                    {
                        for (int s = 0; s < storehouseEntries.Length; s++)
                        {
                            TryScoreStorehouseCandidate(
                                s,
                                storehouseEntries,
                                villagerPos,
                                resourceTypeIndex,
                                ref _transformLookup,
                                ref bestStorehouse,
                                ref bestScore);
                        }
                    }

                    ticket.ValueRW.StorehouseEntity = bestStorehouse;
                    aiState.ValueRW.TargetEntity = bestStorehouse;
                    aiState.ValueRW.CurrentState = VillagerAIState.State.Working;
                    aiState.ValueRW.CurrentGoal = VillagerAIState.Goal.Work;

                    if (bestStorehouse != Entity.Null)
                    {
                        ReserveStorehouse(bestStorehouse, ticket.ValueRO.ResourceTypeIndex, carriedAmount, timeState.Tick);
                    }
                }
            }

            storehouseCandidateIndices.Dispose();
        }

        private static void AddUniqueIndex(ref NativeList<int> indices, int value)
        {
            for (int i = 0; i < indices.Length; i++)
            {
                if (indices[i] == value)
                {
                    return;
                }
            }

            indices.Add(value);
        }

        private static void TryScoreStorehouseCandidate(
            int entryIndex,
            DynamicBuffer<StorehouseRegistryEntry> entries,
            float3 villagerPos,
            ushort resourceTypeIndex,
            ref ComponentLookup<LocalTransform> transformLookup,
            ref Entity bestStorehouse,
            ref float bestScore)
        {
            if ((uint)entryIndex >= (uint)entries.Length)
            {
                return;
            }

            var entry = entries[entryIndex];
            if (!transformLookup.HasComponent(entry.StorehouseEntity))
            {
                return;
            }

            float available = 0f;
            for (int t = 0; t < entry.TypeSummaries.Length; t++)
            {
                var summary = entry.TypeSummaries[t];
                if (summary.ResourceTypeIndex == resourceTypeIndex)
                {
                    available = summary.Capacity - (summary.Stored + summary.Reserved);
                    break;
                }
            }

            if (available <= 0f)
            {
                return;
            }

            var storehousePos = transformLookup[entry.StorehouseEntity].Position;
            var score = math.distancesq(villagerPos, storehousePos);

            if (score < bestScore)
            {
                bestScore = score;
                bestStorehouse = entry.StorehouseEntity;
            }
        }

        private void CompleteJob(ref VillagerJob job, ref VillagerJobTicket ticket, ref VillagerJobProgress progress, DynamicBuffer<VillagerJobCarryItem> carry, uint currentTick)
        {
            var resourceEntity = ticket.ResourceEntity;
            var storehouseEntity = ticket.StorehouseEntity;
            var ticketId = ticket.TicketId;
            var deliveredAmount = progress.Gathered;

            job.Phase = VillagerJob.JobPhase.Completed;
            job.ActiveTicketId = 0;
            job.LastStateChangeTick = currentTick;

            ticket.ResourceEntity = Entity.Null;
            ticket.StorehouseEntity = Entity.Null;
            ticket.ReservedUnits = 0f;
            ticket.TicketId = 0;
            ticket.Phase = (byte)VillagerJob.JobPhase.Completed;
            ticket.LastProgressTick = currentTick;

            carry.Clear();
            progress.Delivered += progress.Gathered;
            progress.Gathered = 0f;
            progress.TimeInPhase = 0f;
            progress.LastUpdateTick = currentTick;

            if (resourceEntity != Entity.Null && _resourceReservationLookup.HasComponent(resourceEntity))
            {
                var reservation = _resourceReservationLookup[resourceEntity];
                reservation.ActiveTickets = (byte)math.max(0, reservation.ActiveTickets - 1);
                reservation.ReservedUnits = math.max(0f, reservation.ReservedUnits - deliveredAmount);
                reservation.LastMutationTick = currentTick;
                _resourceReservationLookup[resourceEntity] = reservation;

                if (_resourceActiveTicketLookup.HasBuffer(resourceEntity))
                {
                    var activeTickets = _resourceActiveTicketLookup[resourceEntity];
                    for (int i = activeTickets.Length - 1; i >= 0; i--)
                    {
                        if (activeTickets[i].TicketId == ticketId)
                        {
                            activeTickets.RemoveAt(i);
                            break;
                        }
                    }
                }
            }

            if (storehouseEntity != Entity.Null && _storehouseReservationLookup.HasComponent(storehouseEntity))
            {
                var reservation = _storehouseReservationLookup[storehouseEntity];
                reservation.ReservedCapacity = math.max(0f, reservation.ReservedCapacity - deliveredAmount);
                reservation.LastMutationTick = currentTick;
                _storehouseReservationLookup[storehouseEntity] = reservation;

                if (_storehouseReservationItems.HasBuffer(storehouseEntity))
                {
                    var items = _storehouseReservationItems[storehouseEntity];
                    for (int i = 0; i < items.Length; i++)
                    {
                        if (items[i].ResourceTypeIndex == ticket.ResourceTypeIndex)
                        {
                            var item = items[i];
                            item.Reserved = math.max(0f, item.Reserved - deliveredAmount);
                            items[i] = item;
                            break;
                        }
                    }
                }
            }
        }

        private static float GetCarryAmount(DynamicBuffer<VillagerJobCarryItem> carry, ushort resourceTypeIndex)
        {
            for (int i = 0; i < carry.Length; i++)
            {
                if (carry[i].ResourceTypeIndex == resourceTypeIndex)
                {
                    return carry[i].Amount;
                }
            }

            return 0f;
        }

        private void ReserveStorehouse(Entity storehouse, ushort resourceTypeIndex, float amount, uint currentTick)
        {
            if (storehouse == Entity.Null || amount <= 0f)
            {
                return;
            }

            if (_storehouseReservationLookup.HasComponent(storehouse))
            {
                var reservation = _storehouseReservationLookup[storehouse];
                reservation.ReservedCapacity += amount;
                reservation.LastMutationTick = currentTick;
                _storehouseReservationLookup[storehouse] = reservation;
            }

            if (_storehouseReservationItems.HasBuffer(storehouse))
            {
                var items = _storehouseReservationItems[storehouse];
                var updated = false;
                for (int i = 0; i < items.Length; i++)
                {
                    if (items[i].ResourceTypeIndex == resourceTypeIndex)
                    {
                        var item = items[i];
                        item.Reserved += amount;
                        items[i] = item;
                        updated = true;
                        break;
                    }
                }

                if (!updated)
                {
                    items.Add(new StorehouseReservationItem
                    {
                        ResourceTypeIndex = resourceTypeIndex,
                        Reserved = amount
                    });
                }
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(VillagerJobFixedStepGroup))]
    [UpdateAfter(typeof(VillagerJobDeliverySystem))]
    public partial struct VillagerJobInterruptSystem : ISystem
    {
        private ComponentLookup<ResourceJobReservation> _resourceReservationLookup;
        private BufferLookup<ResourceActiveTicket> _resourceActiveTicketLookup;
        private ComponentLookup<StorehouseJobReservation> _storehouseReservationLookup;
        private BufferLookup<StorehouseReservationItem> _storehouseReservationItems;

        public void OnCreate(ref SystemState state)
        {
            _resourceReservationLookup = state.GetComponentLookup<ResourceJobReservation>(false);
            _resourceActiveTicketLookup = state.GetBufferLookup<ResourceActiveTicket>(false);
            _storehouseReservationLookup = state.GetComponentLookup<StorehouseJobReservation>(false);
            _storehouseReservationItems = state.GetBufferLookup<StorehouseReservationItem>(false);

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<VillagerJob>();
            state.RequireForUpdate<VillagerJobTicket>();
            state.RequireForUpdate<VillagerJobEventStream>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Gate by ScenarioState.EnableGodgame and initialization
            if (!SystemAPI.TryGetSingleton<ScenarioState>(out var scenario) || !scenario.EnableGodgame || !scenario.IsInitialized)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _resourceReservationLookup.Update(ref state);
            _resourceActiveTicketLookup.Update(ref state);
            _storehouseReservationLookup.Update(ref state);
            _storehouseReservationItems.Update(ref state);

            var eventEntity = SystemAPI.GetSingletonEntity<VillagerJobEventStream>();
            var events = state.EntityManager.GetBuffer<VillagerJobEvent>(eventEntity);

            foreach (var (job, ticket, progress, carry, entity) in SystemAPI.Query<RefRW<VillagerJob>, RefRW<VillagerJobTicket>, RefRW<VillagerJobProgress>, DynamicBuffer<VillagerJobCarryItem>>()
                         .WithEntityAccess())
            {
                if (job.ValueRO.Type == VillagerJob.JobType.None)
                {
                    continue;
                }

                var resourceEntity = ticket.ValueRO.ResourceEntity;
                var storehouseEntity = ticket.ValueRO.StorehouseEntity;
                var resourceTypeIndex = ticket.ValueRO.ResourceTypeIndex;
                var reservedUnits = ticket.ValueRO.ReservedUnits;

                if (resourceEntity == Entity.Null)
                {
                    continue;
                }

                if (!_resourceReservationLookup.HasComponent(resourceEntity))
                {
                    continue;
                }

                var reservation = _resourceReservationLookup[resourceEntity];
                if ((reservation.ClaimFlags & ResourceRegistryClaimFlags.PlayerClaim) == 0)
                {
                    continue;
                }

                reservation.ActiveTickets = (byte)math.max(0, reservation.ActiveTickets - 1);
                reservation.ReservedUnits = math.max(0f, reservation.ReservedUnits - reservedUnits);
                reservation.LastMutationTick = timeState.Tick;
                reservation.ClaimFlags &= unchecked((byte)~ResourceRegistryClaimFlags.VillagerReserved);
                _resourceReservationLookup[resourceEntity] = reservation;

                if (_resourceActiveTicketLookup.HasBuffer(resourceEntity))
                {
                    var buffer = _resourceActiveTicketLookup[resourceEntity];
                    for (int i = buffer.Length - 1; i >= 0; i--)
                    {
                        if (buffer[i].Villager == entity)
                        {
                            buffer.RemoveAt(i);
                            break;
                        }
                    }
                }

                carry.Clear();
                progress.ValueRW.TimeInPhase = 0f;
                progress.ValueRW.LastUpdateTick = timeState.Tick;

                job.ValueRW.Phase = VillagerJob.JobPhase.Interrupted;
                job.ValueRW.ActiveTicketId = 0;
                job.ValueRW.LastStateChangeTick = timeState.Tick;

                var interruptedTicketId = ticket.ValueRO.TicketId;
                ticket.ValueRW.ResourceEntity = Entity.Null;
                ticket.ValueRW.StorehouseEntity = Entity.Null;
                ticket.ValueRW.ReservedUnits = 0f;
                ticket.ValueRW.TicketId = 0;
                ticket.ValueRW.Phase = (byte)VillagerJob.JobPhase.Interrupted;
                ticket.ValueRW.LastProgressTick = timeState.Tick;

                var hasStorehouseReservation = _storehouseReservationLookup.HasComponent(storehouseEntity);
                if (storehouseEntity != Entity.Null && hasStorehouseReservation)
                {
                    var storeReservation = _storehouseReservationLookup[storehouseEntity];
                    storeReservation.ReservedCapacity = math.max(0f, storeReservation.ReservedCapacity - reservedUnits);
                    storeReservation.LastMutationTick = timeState.Tick;
                    _storehouseReservationLookup[storehouseEntity] = storeReservation;

                var hasReservationItems = _storehouseReservationItems.HasBuffer(storehouseEntity);
                if (hasReservationItems)
                    {
                        var items = _storehouseReservationItems[storehouseEntity];
                        for (int i = 0; i < items.Length; i++)
                        {
                            if (items[i].ResourceTypeIndex == resourceTypeIndex)
                            {
                                var item = items[i];
                                item.Reserved = math.max(0f, item.Reserved - reservedUnits);
                                items[i] = item;
                                break;
                            }
                        }
                    }
                }

                events.Add(new VillagerJobEvent
                {
                    Tick = timeState.Tick,
                    Villager = entity,
                    EventType = VillagerJobEventType.JobInterrupted,
                    ResourceTypeIndex = resourceTypeIndex,
                    Amount = 0f,
                    TicketId = interruptedTicketId
                });
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(RecordSimulationSystemGroup))]
    public partial struct VillagerJobEventFlushSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<VillagerJobEventStream>();
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var eventEntity = SystemAPI.GetSingletonEntity<VillagerJobEventStream>();
            var events = state.EntityManager.GetBuffer<VillagerJobEvent>(eventEntity);

            // Retain only recent events to avoid unbounded growth.
            var horizonTick = timeState.Tick > 120 ? timeState.Tick - 120u : 0u;
            for (int i = events.Length - 1; i >= 0; i--)
            {
                if (events[i].Tick < horizonTick)
                {
                    events.RemoveAt(i);
                }
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(HistorySystemGroup))]
    public partial struct VillagerJobHistorySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<VillagerJobHistorySample>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<HistorySettings>();
            state.RequireForUpdate<RewindState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Gate by ScenarioState.EnableGodgame and initialization
            if (!SystemAPI.TryGetSingleton<ScenarioState>(out var scenario) || !scenario.EnableGodgame || !scenario.IsInitialized)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var historySettings = SystemAPI.GetSingleton<HistorySettings>();
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var strideTicks = (uint)math.max(1f, historySettings.DefaultStrideSeconds / math.max(0.0001f, timeState.FixedDeltaTime));
            if (strideTicks == 0 || timeState.Tick % strideTicks != 0)
            {
                return;
            }

            foreach (var (job, ticket, progress, transform, entity) in SystemAPI.Query<RefRO<VillagerJob>, RefRO<VillagerJobTicket>, RefRO<VillagerJobProgress>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                var buffer = state.EntityManager.GetBuffer<VillagerJobHistorySample>(entity);
                buffer.Add(new VillagerJobHistorySample
                {
                    Tick = timeState.Tick,
                    TicketId = ticket.ValueRO.TicketId,
                    Phase = job.ValueRO.Phase,
                    Gathered = progress.ValueRO.Gathered,
                    Delivered = progress.ValueRO.Delivered,
                    TargetPosition = transform.ValueRO.Position
                });
                PruneHistory(ref buffer, timeState.Tick, historySettings.DefaultHorizonSeconds, timeState.FixedDeltaTime);
            }
        }

        private static void PruneHistory(ref DynamicBuffer<VillagerJobHistorySample> buffer, uint currentTick, float horizonSeconds, float fixedDt)
        {
            if (buffer.Length == 0)
            {
                return;
            }

            var horizonTicks = (uint)math.max(1f, horizonSeconds / math.max(0.0001f, fixedDt));
            for (int i = 0; i < buffer.Length; i++)
            {
                if (currentTick - buffer[i].Tick <= horizonTicks)
                {
                    if (i > 0)
                    {
                        buffer.RemoveRange(0, i);
                    }
                    return;
                }
            }

            buffer.Clear();
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(PlaybackSimulationSystemGroup))]
    public partial struct VillagerJobPlaybackSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<VillagerJobHistorySample>();
            state.RequireForUpdate<VillagerJob>();
            state.RequireForUpdate<VillagerJobTicket>();
            state.RequireForUpdate<VillagerJobProgress>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Gate by ScenarioState.EnableGodgame and initialization
            if (!SystemAPI.TryGetSingleton<ScenarioState>(out var scenario) || !scenario.EnableGodgame || !scenario.IsInitialized)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState))
            {
                return;
            }
            if (rewindState.Mode != RewindMode.Playback)
            {
                return;
            }

            var targetTick = SystemAPI.GetSingleton<TimeState>().Tick;
            foreach (var (job, ticket, progress, historyBuffer) in SystemAPI.Query<RefRW<VillagerJob>, RefRW<VillagerJobTicket>, RefRW<VillagerJobProgress>, DynamicBuffer<VillagerJobHistorySample>>())
            {
                if (historyBuffer.Length == 0)
                {
                    continue;
                }

                var sampleIndex = FindSampleIndex(historyBuffer, targetTick);
                if (sampleIndex < 0)
                {
                    continue;
                }

                var sample = historyBuffer[sampleIndex];
                job.ValueRW.Phase = sample.Phase;
                job.ValueRW.ActiveTicketId = sample.TicketId;

                ticket.ValueRW.TicketId = sample.TicketId;
                ticket.ValueRW.Phase = (byte)sample.Phase;

                progress.ValueRW.Gathered = sample.Gathered;
                progress.ValueRW.Delivered = sample.Delivered;
            }
        }

        private static int FindSampleIndex(DynamicBuffer<VillagerJobHistorySample> buffer, uint tick)
        {
            for (int i = buffer.Length - 1; i >= 0; i--)
            {
                if (buffer[i].Tick <= tick)
                {
                    return i;
                }
            }
            return -1;
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(VillagerJobFixedStepGroup))]
    // Removed invalid UpdateAfter: VillagerJobAssignmentSystem executes in WarmPathSystemGroup; cross-group ordering is managed by group scheduling.
    public partial struct VillagerJobDiagnosticsSystem : ISystem
    {
        private EntityQuery _jobQuery;
        private EntityQuery _ticketQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _jobQuery = SystemAPI.QueryBuilder().WithAll<VillagerJob>().WithNone<PlaybackGuardTag>().Build();
            _ticketQuery = SystemAPI.QueryBuilder().WithAll<VillagerJobTicket>().WithNone<PlaybackGuardTag>().Build();

            state.RequireForUpdate<VillagerJobDiagnostics>();
            state.RequireForUpdate<VillagerJobRequestQueue>();
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Gate by ScenarioState.EnableGodgame and initialization
            if (!SystemAPI.TryGetSingleton<ScenarioState>(out var scenario) || !scenario.EnableGodgame || !scenario.IsInitialized)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var diagnosticsEntity = SystemAPI.GetSingletonEntity<VillagerJobDiagnostics>();
            var diagnostics = SystemAPI.GetComponentRW<VillagerJobDiagnostics>(diagnosticsEntity);

            var totalVillagers = _jobQuery.CalculateEntityCount();
            var idleVillagers = 0;
            var assignedVillagers = 0;

            foreach (var job in SystemAPI.Query<RefRO<VillagerJob>>().WithNone<PlaybackGuardTag>())
            {
                if (job.ValueRO.Type == VillagerJob.JobType.None || job.ValueRO.Phase == VillagerJob.JobPhase.Idle)
                {
                    idleVillagers++;
                }
                else
                {
                    assignedVillagers++;
                }
            }

            var requestEntity = SystemAPI.GetSingletonEntity<VillagerJobRequestQueue>();
            var requests = state.EntityManager.GetBuffer<VillagerJobRequest>(requestEntity);
            var pendingRequests = requests.Length;

            var activeTickets = 0;
            foreach (var ticket in SystemAPI.Query<RefRO<VillagerJobTicket>>().WithNone<PlaybackGuardTag>())
            {
                if (ticket.ValueRO.JobType != VillagerJob.JobType.None && ticket.ValueRO.ResourceEntity != Entity.Null)
                {
                    activeTickets++;
                }
            }

            diagnostics.ValueRW = new VillagerJobDiagnostics
            {
                Frame = timeState.Tick,
                TotalVillagers = totalVillagers,
                IdleVillagers = idleVillagers,
                AssignedVillagers = assignedVillagers,
                PendingRequests = pendingRequests,
                ActiveTickets = activeTickets
            };
        }
    }
}
