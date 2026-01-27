using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interaction;
using PureDOTS.Runtime.Mining;
using PureDOTS.Runtime.Time;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Mining
{
    /// <summary>
    /// Hardened mining system for Godgame villagers with robust state machine and physics disruption handling.
    /// Integrates with existing VillagerJobSystems to add MiningSession tracking and robustness checks.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VillagerJobFixedStepGroup))]
    // Removed invalid UpdateAfter: VillagerJobExecutionSystem runs in HotPathSystemGroup; cross-group ordering must be handled via group scheduling.
    public partial struct VillagerMiningSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<ResourceSourceState> _resourceStateLookup;
        private ComponentLookup<ResourceSourceConfig> _resourceConfigLookup;
        private ComponentLookup<MineableSource> _mineableSourceLookup;
        private ComponentLookup<StorehouseConfig> _storehouseLookup;
        private ComponentLookup<ResourceSink> _resourceSinkLookup;
        private ComponentLookup<MiningSession> _miningSessionLookup;
        private ComponentLookup<MiningStateComponent> _miningStateLookup;
        private ComponentLookup<MovementSuppressed> _movementSuppressedLookup;
        private ComponentLookup<BeingThrown> _beingThrownLookup;
        private ComponentLookup<HandHeldTag> _handHeldLookup;
        private ComponentLookup<VillagerJob> _villagerJobLookup;
        private ComponentLookup<VillagerJobTicket> _villagerJobTicketLookup;
        private BufferLookup<VillagerJobCarryItem> _carryItemLookup;
        private BufferLookup<StorehouseInventoryItem> _storehouseInventoryLookup;
        private BufferLookup<StorehouseCapacityElement> _storehouseCapacityLookup;

        private const float MiningRange = 3f;
        private const float DeliveryRange = 5f;

#if UNITY_EDITOR
        private ComponentLookup<MiningDiagnostics> _diagnosticsLookup;
#endif

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _resourceStateLookup = state.GetComponentLookup<ResourceSourceState>(false);
            _resourceConfigLookup = state.GetComponentLookup<ResourceSourceConfig>(true);
            _mineableSourceLookup = state.GetComponentLookup<MineableSource>(false);
            _storehouseLookup = state.GetComponentLookup<StorehouseConfig>(true);
            _resourceSinkLookup = state.GetComponentLookup<ResourceSink>(false);
            _miningSessionLookup = state.GetComponentLookup<MiningSession>(false);
            _miningStateLookup = state.GetComponentLookup<MiningStateComponent>(false);
            _movementSuppressedLookup = state.GetComponentLookup<MovementSuppressed>(true);
            _beingThrownLookup = state.GetComponentLookup<BeingThrown>(true);
            _handHeldLookup = state.GetComponentLookup<HandHeldTag>(true);
            _villagerJobLookup = state.GetComponentLookup<VillagerJob>(false);
            _villagerJobTicketLookup = state.GetComponentLookup<VillagerJobTicket>(false);
            _carryItemLookup = state.GetBufferLookup<VillagerJobCarryItem>(false);
            _storehouseInventoryLookup = state.GetBufferLookup<StorehouseInventoryItem>(false);
            _storehouseCapacityLookup = state.GetBufferLookup<StorehouseCapacityElement>(true);

#if UNITY_EDITOR
            _diagnosticsLookup = state.GetComponentLookup<MiningDiagnostics>(false);
#endif
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

            _transformLookup.Update(ref state);
            _resourceStateLookup.Update(ref state);
            _resourceConfigLookup.Update(ref state);
            _mineableSourceLookup.Update(ref state);
            _storehouseLookup.Update(ref state);
            _resourceSinkLookup.Update(ref state);
            _miningSessionLookup.Update(ref state);
            _miningStateLookup.Update(ref state);
            _movementSuppressedLookup.Update(ref state);
            _beingThrownLookup.Update(ref state);
            _handHeldLookup.Update(ref state);
            _villagerJobLookup.Update(ref state);
            _villagerJobTicketLookup.Update(ref state);
            _carryItemLookup.Update(ref state);
            _storehouseInventoryLookup.Update(ref state);
            _storehouseCapacityLookup.Update(ref state);

#if UNITY_EDITOR
            _diagnosticsLookup.Update(ref state);
            Entity diagnosticsEntity = Entity.Null;
            if (SystemAPI.HasSingleton<MiningDiagnostics>())
            {
                diagnosticsEntity = SystemAPI.GetSingletonEntity<MiningDiagnostics>();
            }
#endif

            var deltaTime = timeState.FixedDeltaTime;
            var currentTick = timeState.Tick;

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            var job = new ProcessVillagerMiningJob
            {
                TransformLookup = _transformLookup,
                ResourceStateLookup = _resourceStateLookup,
                ResourceConfigLookup = _resourceConfigLookup,
                MineableSourceLookup = _mineableSourceLookup,
                StorehouseLookup = _storehouseLookup,
                ResourceSinkLookup = _resourceSinkLookup,
                MiningSessionLookup = _miningSessionLookup,
                MiningStateLookup = _miningStateLookup,
                MovementSuppressedLookup = _movementSuppressedLookup,
                BeingThrownLookup = _beingThrownLookup,
                HandHeldLookup = _handHeldLookup,
                VillagerJobLookup = _villagerJobLookup,
                VillagerJobTicketLookup = _villagerJobTicketLookup,
                CarryItemLookup = _carryItemLookup,
                StorehouseInventoryLookup = _storehouseInventoryLookup,
                StorehouseCapacityLookup = _storehouseCapacityLookup,
                Ecb = ecb,
                DeltaTime = deltaTime,
                CurrentTick = currentTick,
                MiningRange = MiningRange,
                DeliveryRange = DeliveryRange
#if UNITY_EDITOR
                ,
                DiagnosticsEntity = diagnosticsEntity,
                DiagnosticsLookup = _diagnosticsLookup
#endif
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct ProcessVillagerMiningJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [NativeDisableParallelForRestriction]
            public ComponentLookup<ResourceSourceState> ResourceStateLookup;
            [ReadOnly] public ComponentLookup<ResourceSourceConfig> ResourceConfigLookup;
            [NativeDisableParallelForRestriction]
            public ComponentLookup<MineableSource> MineableSourceLookup;
            [ReadOnly] public ComponentLookup<StorehouseConfig> StorehouseLookup;
            [NativeDisableParallelForRestriction]
            public ComponentLookup<ResourceSink> ResourceSinkLookup;
            [NativeDisableParallelForRestriction]
            public ComponentLookup<MiningSession> MiningSessionLookup;
            [NativeDisableParallelForRestriction]
            public ComponentLookup<MiningStateComponent> MiningStateLookup;
            [ReadOnly] public ComponentLookup<MovementSuppressed> MovementSuppressedLookup;
            [ReadOnly] public ComponentLookup<BeingThrown> BeingThrownLookup;
            [ReadOnly] public ComponentLookup<HandHeldTag> HandHeldLookup;
            [NativeDisableParallelForRestriction]
            public ComponentLookup<VillagerJob> VillagerJobLookup;
            [NativeDisableParallelForRestriction]
            public ComponentLookup<VillagerJobTicket> VillagerJobTicketLookup;
            [NativeDisableParallelForRestriction]
            public BufferLookup<VillagerJobCarryItem> CarryItemLookup;
            [NativeDisableParallelForRestriction]
            public BufferLookup<StorehouseInventoryItem> StorehouseInventoryLookup;
            [ReadOnly] public BufferLookup<StorehouseCapacityElement> StorehouseCapacityLookup;
            public EntityCommandBuffer.ParallelWriter Ecb;

            public float DeltaTime;
            public uint CurrentTick;
            public float MiningRange;
            public float DeliveryRange;
#if UNITY_EDITOR
            public Entity DiagnosticsEntity;
            [NativeDisableParallelForRestriction]
            public ComponentLookup<MiningDiagnostics> DiagnosticsLookup;
#endif

            public void Execute(
                [EntityIndexInQuery] int entityInQueryIndex,
                in Entity entity,
                in MiningJobTag miningJobTag,
                in LocalTransform transform)
            {
                // Skip mining logic if movement is suppressed or entity is being thrown/held
                bool movementSuppressed = MovementSuppressedLookup.HasComponent(entity) &&
                                          MovementSuppressedLookup.IsComponentEnabled(entity);
                bool beingThrown = BeingThrownLookup.HasComponent(entity) &&
                                   BeingThrownLookup.IsComponentEnabled(entity);
                if (movementSuppressed || beingThrown || HandHeldLookup.HasComponent(entity))
                {
                    return;
                }

                var hasJob = VillagerJobLookup.HasComponent(entity);
                var hasTicket = VillagerJobTicketLookup.HasComponent(entity);
                var hasSession = MiningSessionLookup.HasComponent(entity);
                var hasState = MiningStateLookup.HasComponent(entity);

                if (!hasJob || !hasTicket)
                {
                    // No job or ticket - clear session if exists
                    if (hasSession)
                    {
                        Ecb.RemoveComponent<MiningSession>(entityInQueryIndex, entity);
                    }
                    if (hasState)
                    {
                        Ecb.RemoveComponent<MiningStateComponent>(entityInQueryIndex, entity);
                    }
                    return;
                }

                var job = VillagerJobLookup[entity];
                var ticket = VillagerJobTicketLookup[entity];

                // Only process mining jobs (Gatherer, Farmer, Hunter)
                if (job.Type != VillagerJob.JobType.Gatherer &&
                    job.Type != VillagerJob.JobType.Farmer &&
                    job.Type != VillagerJob.JobType.Hunter)
                {
                    return;
                }

                var session = hasSession ? MiningSessionLookup[entity] : default(MiningSession);
                var miningState = hasState
                    ? MiningStateLookup[entity]
                    : new MiningStateComponent { State = MiningState.Idle };

                var position = transform.Position;

                // Validate existing session
                if (hasSession)
                {
                    // Check if source is still valid
                    if (session.Source == Entity.Null ||
                        !ResourceStateLookup.HasComponent(session.Source) ||
                        !TransformLookup.HasComponent(session.Source))
                    {
                        // Invalid source - mark job as failed
                        job.Phase = VillagerJob.JobPhase.Interrupted;
                        VillagerJobLookup[entity] = job;
                        Ecb.RemoveComponent<MiningSession>(entityInQueryIndex, entity);
                        if (hasState)
                        {
                            MiningStateLookup[entity] = new MiningStateComponent
                            {
                                State = MiningState.Idle,
                                LastStateChangeTick = CurrentTick
                            };
                        }
                        miningState.State = MiningState.Idle;
                        session = default(MiningSession);
                        hasSession = false;
#if UNITY_EDITOR
                        if (DiagnosticsEntity != Entity.Null && DiagnosticsLookup.HasComponent(DiagnosticsEntity))
                        {
                            var diag = DiagnosticsLookup[DiagnosticsEntity];
                            diag.InvalidSourceResets++;
                            DiagnosticsLookup[DiagnosticsEntity] = diag;
                        }
#endif
                    }
                    else if (MineableSourceLookup.HasComponent(session.Source))
                    {
                        var source = MineableSourceLookup[session.Source];
                        if (source.CurrentAmount <= 0f)
                        {
                            // Source depleted - mark job as completed
                            job.Phase = VillagerJob.JobPhase.Completed;
                            VillagerJobLookup[entity] = job;
                            Ecb.RemoveComponent<MiningSession>(entityInQueryIndex, entity);
                            if (hasState)
                            {
                                MiningStateLookup[entity] = new MiningStateComponent
                                {
                                    State = MiningState.Idle,
                                    LastStateChangeTick = CurrentTick
                                };
                            }
                            miningState.State = MiningState.Idle;
                            session = default(MiningSession);
                            hasSession = false;
                        }
                    }

                    // Check if storehouse is still valid
                    if (hasSession && session.Carrier != Entity.Null)
                    {
                        if (!StorehouseLookup.HasComponent(session.Carrier) ||
                            !TransformLookup.HasComponent(session.Carrier))
                        {
                            // Invalid storehouse - try to find a new one from ticket or clear session
                            if (ticket.StorehouseEntity != Entity.Null &&
                                StorehouseLookup.HasComponent(ticket.StorehouseEntity) &&
                                TransformLookup.HasComponent(ticket.StorehouseEntity))
                            {
                                session.Carrier = ticket.StorehouseEntity;
                                MiningSessionLookup[entity] = session;
                            }
                            else
                            {
                                // No valid storehouse - mark job as interrupted
                                job.Phase = VillagerJob.JobPhase.Interrupted;
                                VillagerJobLookup[entity] = job;
                                Ecb.RemoveComponent<MiningSession>(entityInQueryIndex, entity);
                                if (hasState)
                                {
                                    MiningStateLookup[entity] = new MiningStateComponent
                                    {
                                        State = MiningState.Idle,
                                        LastStateChangeTick = CurrentTick
                                    };
                                }
                                miningState.State = MiningState.Idle;
                                session = default(MiningSession);
                                hasSession = false;
#if UNITY_EDITOR
                                if (DiagnosticsEntity != Entity.Null && DiagnosticsLookup.HasComponent(DiagnosticsEntity))
                                {
                                    var diag = DiagnosticsLookup[DiagnosticsEntity];
                                    diag.InvalidCarrierResets++;
                                    DiagnosticsLookup[DiagnosticsEntity] = diag;
                                }
#endif
                            }
                        }
                        else if (ResourceSinkLookup.HasComponent(session.Carrier))
                        {
                            var sink = ResourceSinkLookup[session.Carrier];
                            if (sink.CurrentAmount >= sink.Capacity)
                            {
                                // Storehouse full - try to find a new one
                                if (ticket.StorehouseEntity != Entity.Null &&
                                    ticket.StorehouseEntity != session.Carrier &&
                                    StorehouseLookup.HasComponent(ticket.StorehouseEntity) &&
                                    TransformLookup.HasComponent(ticket.StorehouseEntity))
                                {
                                    session.Carrier = ticket.StorehouseEntity;
                                    MiningSessionLookup[entity] = session;
                                }
                            }
                        }
                    }
                }

                // Initialize session if needed
                if (!hasSession && job.Phase == VillagerJob.JobPhase.Assigned && ticket.ResourceEntity != Entity.Null)
                {
                    // Create new session
                    var newSession = new MiningSession
                    {
                        Source = ticket.ResourceEntity,
                        Carrier = ticket.StorehouseEntity,
                        Accumulated = 0f,
                        Capacity = 100f, // Default capacity, could be derived from villager stats
                        StartTick = CurrentTick
                    };

                    MiningSessionLookup[entity] = newSession;
                    session = newSession;

                    // Update MineableSource component if missing
                    if (!MineableSourceLookup.HasComponent(ticket.ResourceEntity) &&
                        ResourceStateLookup.HasComponent(ticket.ResourceEntity) &&
                        ResourceConfigLookup.HasComponent(ticket.ResourceEntity))
                    {
                        var resourceState = ResourceStateLookup[ticket.ResourceEntity];
                        var resourceConfig = ResourceConfigLookup[ticket.ResourceEntity];
                        MineableSourceLookup[ticket.ResourceEntity] = new MineableSource
                        {
                            CurrentAmount = resourceState.UnitsRemaining,
                            MaxAmount = resourceState.UnitsRemaining, // Could be tracked separately
                            ExtractionRate = resourceConfig.GatherRatePerWorker
                        };
                    }

                    miningState.State = MiningState.GoingToSource;
                    miningState.LastStateChangeTick = CurrentTick;
                    if (!hasState)
                    {
                        MiningStateLookup[entity] = miningState;
                    }
                    else
                    {
                        MiningStateLookup[entity] = miningState;
                    }
                }

                // Update session based on job phase and state
                if (hasSession && session.Source != Entity.Null)
                {
                    var sourcePos = TransformLookup[session.Source].Position;
                    var distanceToSource = math.distance(position, sourcePos);

                    // Sync session with job phase
                    if (job.Phase == VillagerJob.JobPhase.Gathering)
                    {
                        if (distanceToSource <= MiningRange)
                        {
                            miningState.State = MiningState.Mining;
                            miningState.LastStateChangeTick = CurrentTick;
                            MiningStateLookup[entity] = miningState;

                            // Update accumulated from carry items
                            if (CarryItemLookup.HasBuffer(entity))
                            {
                                var carryItems = CarryItemLookup[entity];
                                var totalCarried = 0f;
                                for (int i = 0; i < carryItems.Length; i++)
                                {
                                    totalCarried += carryItems[i].Amount;
                                }
                                session.Accumulated = totalCarried;
                                MiningSessionLookup[entity] = session;
                            }
                        }
                        else
                        {
                            miningState.State = MiningState.GoingToSource;
                            miningState.LastStateChangeTick = CurrentTick;
                            MiningStateLookup[entity] = miningState;
                        }
                    }
                    else if (job.Phase == VillagerJob.JobPhase.Delivering)
                    {
                        if (session.Carrier != Entity.Null)
                        {
                            var carrierPos = TransformLookup[session.Carrier].Position;
                            var distanceToCarrier = math.distance(position, carrierPos);

                            if (distanceToCarrier <= DeliveryRange)
                            {
                                miningState.State = MiningState.Delivering;
                                miningState.LastStateChangeTick = CurrentTick;
                                MiningStateLookup[entity] = miningState;
                            }
                            else
                            {
                                miningState.State = MiningState.ReturningToCarrier;
                                miningState.LastStateChangeTick = CurrentTick;
                                MiningStateLookup[entity] = miningState;
                            }
                        }
                    }
                    else if (job.Phase == VillagerJob.JobPhase.Completed)
                    {
                        // Job completed - clear session
                        Ecb.RemoveComponent<MiningSession>(entityInQueryIndex, entity);
                        if (hasState)
                        {
                            MiningStateLookup[entity] = new MiningStateComponent
                            {
                                State = MiningState.Idle,
                                LastStateChangeTick = CurrentTick
                            };
                        }
                    }
                    else if (job.Phase == VillagerJob.JobPhase.Interrupted)
                    {
                        // Job interrupted - clear session
                        Ecb.RemoveComponent<MiningSession>(entityInQueryIndex, entity);
                        if (hasState)
                        {
                            MiningStateLookup[entity] = new MiningStateComponent
                            {
                                State = MiningState.Idle,
                                LastStateChangeTick = CurrentTick
                            };
                        }
#if UNITY_EDITOR
                        if (DiagnosticsEntity != Entity.Null && DiagnosticsLookup.HasComponent(DiagnosticsEntity))
                        {
                            var diag = DiagnosticsLookup[DiagnosticsEntity];
                            diag.PhysicsDisruptionResets++;
                            DiagnosticsLookup[DiagnosticsEntity] = diag;
                        }
#endif
                    }
                }
            }
        }
    }
}
