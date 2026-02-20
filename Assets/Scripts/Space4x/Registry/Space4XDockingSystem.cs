using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Systems;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

using SpatialSystemGroup = PureDOTS.Systems.SpatialSystemGroup;

namespace Space4X.Registry
{
    /// <summary>
    /// Seeds docking policies and throughput state for carriers/stations.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XShipLoopBootstrapSystem))]
    public partial struct Space4XDockingPolicyBootstrapSystem : ISystem
    {
        private ComponentLookup<StationId> _stationLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DockingCapacity>();
            _stationLookup = state.GetComponentLookup<StationId>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _stationLookup.Update(ref state);

            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in SystemAPI.Query<RefRO<DockingCapacity>>().WithEntityAccess())
            {
                if (!em.HasComponent<DockingPolicy>(entity))
                {
                    var policy = _stationLookup.HasComponent(entity)
                        ? DockingPolicy.StationDefault
                        : DockingPolicy.Default;
                    ecb.AddComponent(entity, policy);
                }

                if (!em.HasComponent<DockingThroughputState>(entity))
                {
                    ecb.AddComponent(entity, new DockingThroughputState
                    {
                        CrewRemaining = 0f,
                        CargoRemaining = 0f,
                        AmmoRemaining = 0f,
                        LastResetTick = 0u
                    });
                }

                if (!em.HasComponent<DockingQueuePolicy>(entity))
                {
                    var queuePolicy = _stationLookup.HasComponent(entity)
                        ? DockingQueuePolicy.StationDefault
                        : DockingQueuePolicy.Default;
                    ecb.AddComponent(entity, queuePolicy);
                }

                if (!em.HasComponent<DockingQueueState>(entity))
                {
                    ecb.AddComponent(entity, new DockingQueueState
                    {
                        LastTick = 0u,
                        PendingRequests = 0,
                        ProcessedRequests = 0
                    });
                }
            }

            ecb.Playback(em);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Validates and processes docking requests.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    public partial struct Space4XDockingSystem : ISystem
    {
        private ComponentLookup<DockingCapacity> _dockingLookup;
        private ComponentLookup<CommandLoad> _commandLookup;
        private BufferLookup<DockedEntity> _dockedBufferLookup;
        private ComponentLookup<DockingPolicy> _policyLookup;
        private ComponentLookup<DockingQueuePolicy> _queuePolicyLookup;
        private ComponentLookup<DockingQueueState> _queueStateLookup;
        private ComponentLookup<DockingState> _stateLookup;
        private ComponentLookup<DockedPresence> _presenceLookup;
        private ComponentLookup<Space4XStationAccessPolicy> _stationAccessLookup;
        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<Space4XFaction> _factionLookup;
        private BufferLookup<AffiliationTag> _affiliationLookup;
        private BufferLookup<Space4XContactStanding> _contactLookup;
        private BufferLookup<FactionRelationEntry> _relationLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _dockingLookup = state.GetComponentLookup<DockingCapacity>(false);
            _commandLookup = state.GetComponentLookup<CommandLoad>(false);
            _dockedBufferLookup = state.GetBufferLookup<DockedEntity>(false);
            _policyLookup = state.GetComponentLookup<DockingPolicy>(true);
            _queuePolicyLookup = state.GetComponentLookup<DockingQueuePolicy>(true);
            _queueStateLookup = state.GetComponentLookup<DockingQueueState>(false);
            _stateLookup = state.GetComponentLookup<DockingState>(true);
            _presenceLookup = state.GetComponentLookup<DockedPresence>(true);
            _stationAccessLookup = state.GetComponentLookup<Space4XStationAccessPolicy>(true);
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _factionLookup = state.GetComponentLookup<Space4XFaction>(true);
            _affiliationLookup = state.GetBufferLookup<AffiliationTag>(true);
            _contactLookup = state.GetBufferLookup<Space4XContactStanding>(true);
            _relationLookup = state.GetBufferLookup<FactionRelationEntry>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _dockingLookup.Update(ref state);
            _commandLookup.Update(ref state);
            _dockedBufferLookup.Update(ref state);
            _policyLookup.Update(ref state);
            _queuePolicyLookup.Update(ref state);
            _queueStateLookup.Update(ref state);
            _stateLookup.Update(ref state);
            _presenceLookup.Update(ref state);
            _stationAccessLookup.Update(ref state);
            _carrierLookup.Update(ref state);
            _factionLookup.Update(ref state);
            _affiliationLookup.Update(ref state);
            _contactLookup.Update(ref state);
            _relationLookup.Update(ref state);

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var currentTick = timeState.Tick;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var queueState in SystemAPI.Query<RefRW<DockingQueueState>>())
            {
                queueState.ValueRW.LastTick = currentTick;
                queueState.ValueRW.PendingRequests = 0;
                queueState.ValueRW.ProcessedRequests = 0;
            }

            // Process docking requests
            foreach (var (request, entity) in SystemAPI.Query<RefRO<DockingRequest>>()
                .WithNone<DockedTag>()
                .WithEntityAccess())
            {
                var target = request.ValueRO.TargetCarrier;
                if (target != Entity.Null && _queueStateLookup.HasComponent(target))
                {
                    var queueState = _queueStateLookup[target];
                    queueState.PendingRequests = (ushort)math.min(ushort.MaxValue, queueState.PendingRequests + 1);

                    if (_queuePolicyLookup.HasComponent(target))
                    {
                        var queuePolicy = _queuePolicyLookup[target];
                        if (queuePolicy.MaxProcessedPerTick > 0 && queueState.ProcessedRequests >= queuePolicy.MaxProcessedPerTick)
                        {
                            _queueStateLookup[target] = queueState;
                            continue;
                        }
                    }

                    _queueStateLookup[target] = queueState;
                }

                var docked = ProcessDockingRequest(
                    in entity,
                    request.ValueRO,
                    ref _dockingLookup,
                    ref _commandLookup,
                    ref _dockedBufferLookup,
                    ref _policyLookup,
                    ref _stateLookup,
                    ref _presenceLookup,
                    ref _stationAccessLookup,
                    ref _carrierLookup,
                    ref _factionLookup,
                    ref _affiliationLookup,
                    ref _contactLookup,
                    ref _relationLookup,
                    currentTick,
                    ref ecb);

                if (docked && target != Entity.Null && _queueStateLookup.HasComponent(target))
                {
                    var queueState = _queueStateLookup[target];
                    queueState.ProcessedRequests = (ushort)math.min(ushort.MaxValue, queueState.ProcessedRequests + 1);
                    _queueStateLookup[target] = queueState;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        private static bool ProcessDockingRequest(
            in Entity requestingEntity,
            in DockingRequest request,
            ref ComponentLookup<DockingCapacity> dockingLookup,
            ref ComponentLookup<CommandLoad> commandLookup,
            ref BufferLookup<DockedEntity> dockedBufferLookup,
            ref ComponentLookup<DockingPolicy> policyLookup,
            ref ComponentLookup<DockingState> dockingStateLookup,
            ref ComponentLookup<DockedPresence> dockedPresenceLookup,
            ref ComponentLookup<Space4XStationAccessPolicy> stationAccessLookup,
            ref ComponentLookup<Carrier> carrierLookup,
            ref ComponentLookup<Space4XFaction> factionLookup,
            ref BufferLookup<AffiliationTag> affiliationLookup,
            ref BufferLookup<Space4XContactStanding> contactLookup,
            ref BufferLookup<FactionRelationEntry> relationLookup,
            uint currentTick,
            ref EntityCommandBuffer ecb)
        {
            if (request.TargetCarrier == Entity.Null)
            {
                return false;
            }

            // Check if carrier has docking capacity component
            if (!dockingLookup.HasComponent(request.TargetCarrier))
            {
                return false;
            }

            if (stationAccessLookup.HasComponent(request.TargetCarrier))
            {
                var stationAccess = stationAccessLookup[request.TargetCarrier];
                if (stationAccess.DenyDockingWithoutStanding != 0)
                {
                    var passesDocking = Space4XStationAccessUtility.PassesStandingGate(
                        requestingEntity,
                        request.TargetCarrier,
                        stationAccess.MinStandingForDock,
                        in carrierLookup,
                        in affiliationLookup,
                        in factionLookup,
                        in contactLookup,
                        in relationLookup);
                    if (!passesDocking)
                    {
                        return false;
                    }
                }
            }

            var docking = dockingLookup[request.TargetCarrier];

            DockingPresenceMode presenceMode = DockingPresenceMode.Latch;
            if (policyLookup.HasComponent(request.TargetCarrier))
            {
                var policy = policyLookup[request.TargetCarrier];
                if (policy.AllowDocking == 0)
                {
                    return false;
                }

                presenceMode = policy.DefaultPresence;
                if (presenceMode == DockingPresenceMode.Despawn && policy.AllowDespawn == 0)
                {
                    presenceMode = DockingPresenceMode.Latch;
                }
            }

            // Check if slot is available
            if (!docking.HasSlotAvailable(request.RequiredSlot))
            {
                return false; // No slot available, keep request pending
            }

            // Calculate command cost
            int commandCost = DockingUtility.GetCommandPointCost(request.RequiredSlot);

            // Check command load if carrier has it
            if (commandLookup.HasComponent(request.TargetCarrier))
            {
                var commandLoad = commandLookup[request.TargetCarrier];
                // Allow docking even if overloaded, but apply penalties in other systems
                commandLoad.CurrentCommandPoints += commandCost;
                commandLookup[request.TargetCarrier] = commandLoad;
            }

            // Update docking capacity
            switch (request.RequiredSlot)
            {
                case DockingSlotType.SmallCraft:
                    docking.CurrentSmallCraft++;
                    break;
                case DockingSlotType.MediumCraft:
                    docking.CurrentMediumCraft++;
                    break;
                case DockingSlotType.LargeCraft:
                    docking.CurrentLargeCraft++;
                    break;
                case DockingSlotType.ExternalMooring:
                    docking.CurrentExternalMooring++;
                    break;
                case DockingSlotType.Utility:
                    docking.CurrentUtility++;
                    break;
            }
            dockingLookup[request.TargetCarrier] = docking;

            // Add to docked entities buffer
            if (dockedBufferLookup.HasBuffer(request.TargetCarrier))
            {
                var dockedBuffer = dockedBufferLookup[request.TargetCarrier];
                dockedBuffer.Add(new DockedEntity
                {
                    Entity = requestingEntity,
                    SlotType = request.RequiredSlot,
                    DockedTick = currentTick,
                    CommandPointCost = (byte)commandCost
                });
            }

            // Add DockedTag to requesting entity
            ecb.AddComponent(requestingEntity, new DockedTag
            {
                CarrierEntity = request.TargetCarrier,
                SlotIndex = (byte)(docking.TotalDocked - 1)
            });

            var dockingState = new DockingState
            {
                Phase = DockingPhase.Docked,
                Target = request.TargetCarrier,
                SlotType = request.RequiredSlot,
                PresenceMode = presenceMode,
                RequestTick = request.RequestTick,
                PhaseTick = currentTick
            };
            if (dockingStateLookup.HasComponent(requestingEntity))
            {
                ecb.SetComponent(requestingEntity, dockingState);
            }
            else
            {
                ecb.AddComponent(requestingEntity, dockingState);
            }

            var presence = new DockedPresence
            {
                Carrier = request.TargetCarrier,
                Mode = presenceMode,
                LatchOffset = float3.zero,
                IsLatched = presenceMode == DockingPresenceMode.Despawn ? (byte)0 : (byte)1
            };
            if (dockedPresenceLookup.HasComponent(requestingEntity))
            {
                ecb.SetComponent(requestingEntity, presence);
            }
            else
            {
                ecb.AddComponent(requestingEntity, presence);
            }

            // Remove docking request
            ecb.RemoveComponent<DockingRequest>(requestingEntity);
            return true;
        }
    }

    /// <summary>
    /// Handles undocking and updates capacity.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XDockingSystem))]
    public partial struct Space4XUndockingSystem : ISystem
    {
        private BufferLookup<DockedEntity> _dockedBufferLookup;
        private ComponentLookup<DockingCapacity> _dockingLookup;
        private ComponentLookup<CommandLoad> _commandLookup;
        private ComponentLookup<DockingState> _stateLookup;
        private ComponentLookup<DockedPresence> _presenceLookup;
        private ComponentLookup<SimulationDisabledTag> _disabledLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();

            _dockedBufferLookup = state.GetBufferLookup<DockedEntity>(false);
            _dockingLookup = state.GetComponentLookup<DockingCapacity>(false);
            _commandLookup = state.GetComponentLookup<CommandLoad>(false);
            _stateLookup = state.GetComponentLookup<DockingState>(true);
            _presenceLookup = state.GetComponentLookup<DockedPresence>(true);
            _disabledLookup = state.GetComponentLookup<SimulationDisabledTag>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _dockedBufferLookup.Update(ref state);
            _dockingLookup.Update(ref state);
            _commandLookup.Update(ref state);
            _stateLookup.Update(ref state);
            _presenceLookup.Update(ref state);
            _disabledLookup.Update(ref state);

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var currentTick = timeState.Tick;

            // Process entities that have both DockedTag and are in certain AI states that indicate undocking
            // This is a simplified check - in practice, undocking would be triggered by orders
            foreach (var (docked, aiState, entity) in SystemAPI.Query<RefRO<DockedTag>, RefRO<VesselAIState>>()
                .WithEntityAccess())
            {
                var target = aiState.ValueRO.TargetEntity;
                var hasNonCarrierTarget = target != Entity.Null && target != docked.ValueRO.CarrierEntity;

                // Undock when the vessel is acting on a non-carrier target (including cases where it goes
                // straight into Mining while still physically at the carrier position).
                if (hasNonCarrierTarget && aiState.ValueRO.CurrentState != VesselAIState.State.Returning)
                {
                    // Queue undocking
                    ProcessUndocking(
                        in entity,
                        docked.ValueRO,
                        ref ecb,
                        ref _dockedBufferLookup,
                        ref _dockingLookup,
                        ref _commandLookup,
                        ref _stateLookup,
                        ref _presenceLookup,
                        ref _disabledLookup,
                        currentTick);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        private static void ProcessUndocking(
            in Entity undockingEntity,
            in DockedTag docked,
            ref EntityCommandBuffer ecb,
            ref BufferLookup<DockedEntity> dockedBufferLookup,
            ref ComponentLookup<DockingCapacity> dockingLookup,
            ref ComponentLookup<CommandLoad> commandLookup,
            ref ComponentLookup<DockingState> dockingStateLookup,
            ref ComponentLookup<DockedPresence> dockedPresenceLookup,
            ref ComponentLookup<SimulationDisabledTag> disabledLookup,
            uint currentTick)
        {
            if (docked.CarrierEntity == Entity.Null)
            {
                ecb.RemoveComponent<DockedTag>(undockingEntity);
                return;
            }

            // Update carrier's docked entities buffer
            if (dockedBufferLookup.HasBuffer(docked.CarrierEntity))
            {
                var dockedBuffer = dockedBufferLookup[docked.CarrierEntity];
                for (int i = dockedBuffer.Length - 1; i >= 0; i--)
                {
                    if (dockedBuffer[i].Entity == undockingEntity)
                    {
                        var dockedEntry = dockedBuffer[i];

                        // Update docking capacity
                        if (dockingLookup.HasComponent(docked.CarrierEntity))
                        {
                            var docking = dockingLookup.GetRefRW(docked.CarrierEntity);
                            switch (dockedEntry.SlotType)
                            {
                                case DockingSlotType.SmallCraft:
                                    docking.ValueRW.CurrentSmallCraft = (byte)math.max(0, docking.ValueRO.CurrentSmallCraft - 1);
                                    break;
                                case DockingSlotType.MediumCraft:
                                    docking.ValueRW.CurrentMediumCraft = (byte)math.max(0, docking.ValueRO.CurrentMediumCraft - 1);
                                    break;
                                case DockingSlotType.LargeCraft:
                                    docking.ValueRW.CurrentLargeCraft = (byte)math.max(0, docking.ValueRO.CurrentLargeCraft - 1);
                                    break;
                                case DockingSlotType.ExternalMooring:
                                    docking.ValueRW.CurrentExternalMooring = (byte)math.max(0, docking.ValueRO.CurrentExternalMooring - 1);
                                    break;
                                case DockingSlotType.Utility:
                                    docking.ValueRW.CurrentUtility = (byte)math.max(0, docking.ValueRO.CurrentUtility - 1);
                                    break;
                            }
                        }

                        // Update command load
                        if (commandLookup.HasComponent(docked.CarrierEntity))
                        {
                            var commandLoad = commandLookup.GetRefRW(docked.CarrierEntity);
                            commandLoad.ValueRW.CurrentCommandPoints = math.max(0, commandLoad.ValueRO.CurrentCommandPoints - dockedEntry.CommandPointCost);
                        }

                        dockedBuffer.RemoveAt(i);
                        break;
                    }
                }
            }

            if (dockingStateLookup.HasComponent(undockingEntity))
            {
                var dockingState = dockingStateLookup[undockingEntity];
                dockingState.Phase = DockingPhase.Undocking;
                dockingState.Target = docked.CarrierEntity;
                dockingState.PhaseTick = currentTick;
                ecb.SetComponent(undockingEntity, dockingState);
            }

            if (dockedPresenceLookup.HasComponent(undockingEntity))
            {
                ecb.RemoveComponent<DockedPresence>(undockingEntity);
            }

            if (disabledLookup.HasComponent(undockingEntity))
            {
                ecb.RemoveComponent<SimulationDisabledTag>(undockingEntity);
            }

            ecb.RemoveComponent<DockedTag>(undockingEntity);
        }
    }

    /// <summary>
    /// Applies docking presence rules (attach/latch/despawn) and disables simulation when needed.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XUndockingSystem))]
    public partial struct Space4XDockingPresenceSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<VesselMovement> _movementLookup;
        private ComponentLookup<VesselAIState> _aiLookup;
        private ComponentLookup<SimulationDisabledTag> _disabledLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(false);
            _movementLookup = state.GetComponentLookup<VesselMovement>(false);
            _aiLookup = state.GetComponentLookup<VesselAIState>(false);
            _disabledLookup = state.GetComponentLookup<SimulationDisabledTag>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _transformLookup.Update(ref state);
            _movementLookup.Update(ref state);
            _aiLookup.Update(ref state);
            _disabledLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (presence, entity) in SystemAPI.Query<RefRW<DockedPresence>>().WithEntityAccess())
            {
                if (presence.ValueRO.Carrier == Entity.Null)
                {
                    continue;
                }

                if (presence.ValueRO.Mode == DockingPresenceMode.Despawn)
                {
                    if (!_disabledLookup.HasComponent(entity))
                    {
                        ecb.AddComponent<SimulationDisabledTag>(entity);
                    }

                    ZeroMovement(entity, ref _movementLookup, ref _aiLookup);
                    continue;
                }

                if (_disabledLookup.HasComponent(entity))
                {
                    ecb.RemoveComponent<SimulationDisabledTag>(entity);
                }

                if (presence.ValueRO.IsLatched == 0)
                {
                    continue;
                }

                if (!_transformLookup.HasComponent(entity) || !_transformLookup.HasComponent(presence.ValueRO.Carrier))
                {
                    continue;
                }

                var carrierTransform = _transformLookup[presence.ValueRO.Carrier];
                var selfTransform = _transformLookup[entity];
                if (presence.ValueRO.LatchOffset.Equals(float3.zero))
                {
                    presence.ValueRW.LatchOffset = selfTransform.Position - carrierTransform.Position;
                }

                selfTransform.Position = carrierTransform.Position + presence.ValueRO.LatchOffset;
                _transformLookup[entity] = selfTransform;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static void ZeroMovement(
            Entity entity,
            ref ComponentLookup<VesselMovement> movementLookup,
            ref ComponentLookup<VesselAIState> aiLookup)
        {
            if (movementLookup.HasComponent(entity))
            {
                var movement = movementLookup[entity];
                movement.Velocity = float3.zero;
                movement.CurrentSpeed = 0f;
                movement.IsMoving = 0;
                movementLookup[entity] = movement;
            }

            if (aiLookup.HasComponent(entity))
            {
                var aiState = aiLookup[entity];
                aiState.CurrentGoal = VesselAIState.Goal.Idle;
                aiState.CurrentState = VesselAIState.State.Idle;
                aiState.TargetEntity = Entity.Null;
                aiState.TargetPosition = float3.zero;
                aiLookup[entity] = aiState;
            }
        }
    }

    /// <summary>
    /// Applies overcrowding and command overload penalties.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XUndockingSystem))]
    public partial struct Space4XDockingPenaltySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var currentTick = timeState.Tick;

            // Apply penalties to carriers with overcrowding or command overload
            foreach (var (crewCapacity, morale, entity) in
                SystemAPI.Query<RefRO<CrewCapacity>, RefRW<MoraleState>>()
                    .WithEntityAccess())
            {
                float penalty = DockingUtility.GetOvercrowdingPenalty(crewCapacity.ValueRO);
                if (penalty > 0f)
                {
                    // Apply morale penalty
                    float currentMorale = (float)morale.ValueRO.Current;
                    currentMorale -= penalty * 0.01f; // Slow drain
                    morale.ValueRW.Current = (half)math.clamp(currentMorale, -1f, 1f);
                }
            }

            // Apply command overload penalties to efficiency
            foreach (var (commandLoad, carrierState, entity) in
                SystemAPI.Query<RefRO<CommandLoad>, RefRW<CarrierDepartmentState>>()
                    .WithEntityAccess())
            {
                float penalty = DockingUtility.GetCommandOverloadPenalty(commandLoad.ValueRO);
                if (penalty > 0f)
                {
                    float efficiency = (float)carrierState.ValueRO.OverallEfficiency;
                    efficiency = math.max(0.5f, efficiency - penalty);
                    carrierState.ValueRW.OverallEfficiency = (half)efficiency;
                }
            }
        }
    }

    /// <summary>
    /// Emits docking telemetry metrics.
    /// </summary>
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XDockingPenaltySystem))]
    public partial struct Space4XDockingTelemetrySystem : ISystem
    {
        private EntityQuery _dockingQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TelemetryStream>();

            _dockingQuery = SystemAPI.QueryBuilder()
                .WithAll<DockingCapacity>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            var telemetryEntity = SystemAPI.GetSingletonEntity<TelemetryStream>();
            var buffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);

            int carrierCount = _dockingQuery.CalculateEntityCount();
            int totalDocked = 0;
            int totalCapacity = 0;
            int overcrowdedCount = 0;
            int commandOverloadCount = 0;
            int queuePending = 0;
            int queueProcessed = 0;

            foreach (var docking in SystemAPI.Query<RefRO<DockingCapacity>>())
            {
                totalDocked += docking.ValueRO.TotalDocked;
                totalCapacity += docking.ValueRO.TotalCapacity;
            }

            foreach (var crew in SystemAPI.Query<RefRO<CrewCapacity>>())
            {
                if (crew.ValueRO.IsOvercrowded)
                {
                    overcrowdedCount++;
                }
            }

            foreach (var command in SystemAPI.Query<RefRO<CommandLoad>>())
            {
                if (command.ValueRO.IsOverloaded)
                {
                    commandOverloadCount++;
                }
            }

            foreach (var queue in SystemAPI.Query<RefRO<DockingQueueState>>())
            {
                queuePending += queue.ValueRO.PendingRequests;
                queueProcessed += queue.ValueRO.ProcessedRequests;
            }

            float avgUtilization = totalCapacity > 0 ? (float)totalDocked / totalCapacity : 0f;

            buffer.AddMetric("space4x.docking.carriers", carrierCount);
            buffer.AddMetric("space4x.docking.totalDocked", totalDocked);
            buffer.AddMetric("space4x.docking.totalCapacity", totalCapacity);
            buffer.AddMetric("space4x.docking.avgUtilization", avgUtilization, TelemetryMetricUnit.Ratio);
            buffer.AddMetric("space4x.docking.overcrowded", overcrowdedCount);
            buffer.AddMetric("space4x.docking.commandOverloaded", commandOverloadCount);
            buffer.AddMetric("space4x.docking.queuePending", queuePending);
            buffer.AddMetric("space4x.docking.queueProcessed", queueProcessed);
        }
    }
}
