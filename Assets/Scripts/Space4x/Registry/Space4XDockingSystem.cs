using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Profile;
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
    /// Validates and processes docking requests.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    public partial struct Space4XDockingSystem : ISystem
    {
        private ComponentLookup<DockingCapacity> _dockingLookup;
        private ComponentLookup<CommandLoad> _commandLookup;
        private BufferLookup<DockedEntity> _dockedBufferLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _dockingLookup = state.GetComponentLookup<DockingCapacity>(false);
            _commandLookup = state.GetComponentLookup<CommandLoad>(false);
            _dockedBufferLookup = state.GetBufferLookup<DockedEntity>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _dockingLookup.Update(ref state);
            _commandLookup.Update(ref state);
            _dockedBufferLookup.Update(ref state);

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

            // Process docking requests
            foreach (var (request, entity) in SystemAPI.Query<RefRO<DockingRequest>>()
                .WithNone<DockedTag>()
                .WithEntityAccess())
            {
                ProcessDockingRequest(
                    in entity,
                    request.ValueRO,
                    ref _dockingLookup,
                    ref _commandLookup,
                    ref _dockedBufferLookup,
                    currentTick,
                    ref ecb);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        private static void ProcessDockingRequest(
            in Entity requestingEntity,
            in DockingRequest request,
            ref ComponentLookup<DockingCapacity> dockingLookup,
            ref ComponentLookup<CommandLoad> commandLookup,
            ref BufferLookup<DockedEntity> dockedBufferLookup,
            uint currentTick,
            ref EntityCommandBuffer ecb)
        {
            if (request.TargetCarrier == Entity.Null)
            {
                return;
            }

            // Check if carrier has docking capacity component
            if (!dockingLookup.HasComponent(request.TargetCarrier))
            {
                return;
            }

            var docking = dockingLookup[request.TargetCarrier];

            // Check if slot is available
            if (!docking.HasSlotAvailable(request.RequiredSlot))
            {
                return; // No slot available, keep request pending
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

            // Remove docking request
            ecb.RemoveComponent<DockingRequest>(requestingEntity);
        }
    }

    /// <summary>
    /// Handles undocking and updates capacity.
    /// </summary>
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XDockingSystem))]
    public partial struct Space4XUndockingSystem : ISystem
    {
        private const float RiskHardStop = 0.9f;
        private const float RiskHysteresis = 0.05f;
        private const uint DecisionHoldTicks = 20;
        private const uint RiskBoostTicks = 120;
        private const float HazardRangeFloor = 12f;
        private const float HazardRangePadding = 6f;
        private const float RiskSpeedWeight = 0.7f;
        private const float RiskHazardWeight = 0.3f;
        private const byte DecisionWait = 1;
        private const byte DecisionUndock = 2;

        private BufferLookup<DockedEntity> _dockedBufferLookup;
        private ComponentLookup<DockingCapacity> _dockingLookup;
        private ComponentLookup<CommandLoad> _commandLookup;
        private ComponentLookup<VesselMovement> _movementLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<VesselPilotLink> _pilotLookup;
        private ComponentLookup<ResolvedBehaviorProfile> _profileLookup;
        private ComponentLookup<MiningState> _miningStateLookup;
        private ComponentLookup<MiningOrder> _miningOrderLookup;
        private ComponentLookup<UndockDecisionState> _undockDecisionLookup;
        private BufferLookup<MoveTraceEvent> _traceLookup;
        private EntityQuery _missingDecisionQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();

            _missingDecisionQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<DockedTag>() },
                None = new[] { ComponentType.ReadOnly<UndockDecisionState>() }
            });

            _dockedBufferLookup = state.GetBufferLookup<DockedEntity>(false);
            _dockingLookup = state.GetComponentLookup<DockingCapacity>(false);
            _commandLookup = state.GetComponentLookup<CommandLoad>(false);
            _movementLookup = state.GetComponentLookup<VesselMovement>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _pilotLookup = state.GetComponentLookup<VesselPilotLink>(true);
            _profileLookup = state.GetComponentLookup<ResolvedBehaviorProfile>(true);
            _miningStateLookup = state.GetComponentLookup<MiningState>(true);
            _miningOrderLookup = state.GetComponentLookup<MiningOrder>(true);
            _undockDecisionLookup = state.GetComponentLookup<UndockDecisionState>(false);
            _traceLookup = state.GetBufferLookup<MoveTraceEvent>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!_missingDecisionQuery.IsEmptyIgnoreFilter)
            {
                state.EntityManager.AddComponent<UndockDecisionState>(_missingDecisionQuery);
            }

            _dockedBufferLookup.Update(ref state);
            _dockingLookup.Update(ref state);
            _commandLookup.Update(ref state);
            _movementLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _carrierLookup.Update(ref state);
            _pilotLookup.Update(ref state);
            _profileLookup.Update(ref state);
            _miningStateLookup.Update(ref state);
            _miningOrderLookup.Update(ref state);
            _undockDecisionLookup.Update(ref state);
            _traceLookup.Update(ref state);

            var tick = timeState.Tick;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Process entities that have both DockedTag and are in certain AI states that indicate undocking
            // This is a simplified check - in practice, undocking would be triggered by orders
            foreach (var (docked, aiState, entity) in SystemAPI.Query<RefRO<DockedTag>, RefRO<VesselAIState>>()
                .WithEntityAccess())
            {
                var target = aiState.ValueRO.TargetEntity;
                var hasNonCarrierTarget = target != Entity.Null && target != docked.ValueRO.CarrierEntity;
                var hasMiningUndock = false;
                if (_miningStateLookup.HasComponent(entity))
                {
                    var mining = _miningStateLookup[entity];
                    hasMiningUndock = mining.Phase == MiningPhase.Undocking;
                }

                // Undock when the vessel is acting on a non-carrier target (including cases where it goes
                // straight into Mining while still physically at the carrier position).
                if ((hasNonCarrierTarget || hasMiningUndock) && aiState.ValueRO.CurrentState != VesselAIState.State.Returning)
                {
                    if (_undockDecisionLookup.HasComponent(entity))
                    {
                        var decisionState = _undockDecisionLookup.GetRefRW(entity);
                        if (!ShouldUndock(entity, docked.ValueRO, aiState.ValueRO, tick, ref decisionState.ValueRW))
                        {
                            continue;
                        }
                    }

                    // Queue undocking
                    ProcessUndocking(
                        in entity,
                        docked.ValueRO,
                        ref ecb,
                        ref _dockedBufferLookup,
                        ref _dockingLookup,
                        ref _commandLookup);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private bool ShouldUndock(
            Entity vessel,
            in DockedTag docked,
            in VesselAIState aiState,
            uint tick,
            ref UndockDecisionState decisionState)
        {
            if (decisionState.HoldUntilTick > tick)
            {
                return false;
            }

            var profile = ResolveProfile(vessel);
            var risk = ComputeUndockRisk(vessel, docked, aiState, tick, out var inheritedSpeed);
            var threshold = ComputeRiskThreshold(profile, decisionState.LastDecision);

            var allow = risk <= threshold && risk < RiskHardStop;
            decisionState.Decisions++;
            decisionState.LastDecisionTick = tick;
            decisionState.LastRiskScore = risk;
            decisionState.LastInheritedSpeed = inheritedSpeed;
            decisionState.LastDecision = allow ? DecisionUndock : DecisionWait;
            decisionState.HoldUntilTick = tick + DecisionHoldTicks;
            if (allow)
            {
                decisionState.UndockCount++;
            }
            else
            {
                decisionState.WaitCount++;
            }

            PushDecisionTrace(vessel, tick, docked.CarrierEntity);
            return allow;
        }

        private ResolvedBehaviorProfile ResolveProfile(Entity vessel)
        {
            var profileEntity = vessel;
            if (_pilotLookup.HasComponent(vessel))
            {
                var pilot = _pilotLookup[vessel].Pilot;
                if (pilot != Entity.Null)
                {
                    profileEntity = pilot;
                }
            }

            return _profileLookup.HasComponent(profileEntity)
                ? _profileLookup[profileEntity]
                : ResolvedBehaviorProfile.Neutral;
        }

        private float ComputeUndockRisk(
            Entity vessel,
            in DockedTag docked,
            in VesselAIState aiState,
            uint tick,
            out float inheritedSpeed)
        {
            inheritedSpeed = 0f;
            if (docked.CarrierEntity != Entity.Null && _movementLookup.HasComponent(docked.CarrierEntity))
            {
                var carrierMovement = _movementLookup[docked.CarrierEntity];
                inheritedSpeed = math.length(carrierMovement.Velocity);
                if (carrierMovement.IsMoving != 0)
                {
                    var elapsed = carrierMovement.MoveStartTick > 0 ? tick - carrierMovement.MoveStartTick : 0u;
                    if (elapsed <= RiskBoostTicks)
                    {
                        var plannedSpeed = carrierMovement.BaseSpeed;
                        if (inheritedSpeed < plannedSpeed)
                        {
                            inheritedSpeed = plannedSpeed;
                        }
                    }
                }
            }

            var decel = 0.1f;
            if (_movementLookup.HasComponent(vessel))
            {
                decel = math.max(0.1f, _movementLookup[vessel].Deceleration);
            }

            var hazardRange = ResolveHazardRange(docked.CarrierEntity);
            var brakeDistance = inheritedSpeed * inheritedSpeed / (2f * decel);
            var speedRisk = math.saturate(brakeDistance / math.max(1f, hazardRange));
            var hazardRisk = ResolveHazardRisk(docked.CarrierEntity, vessel, aiState, hazardRange);
            return math.saturate(speedRisk * RiskSpeedWeight + hazardRisk * RiskHazardWeight);
        }

        private float ResolveHazardRange(Entity carrier)
        {
            var range = HazardRangeFloor;
            if (carrier != Entity.Null && _carrierLookup.HasComponent(carrier))
            {
                range = math.max(range, _carrierLookup[carrier].ArrivalDistance + HazardRangePadding);
            }

            return range;
        }

        private float ResolveHazardRisk(Entity carrier, Entity vessel, in VesselAIState aiState, float hazardRange)
        {
            var hazardEntity = ResolveHazardEntity(vessel, aiState);
            if (hazardEntity == Entity.Null || hazardRange <= 0f)
            {
                return 0f;
            }

            if (!_transformLookup.HasComponent(carrier) || !_transformLookup.HasComponent(hazardEntity))
            {
                return 0f;
            }

            var carrierPos = _transformLookup[carrier].Position;
            var hazardPos = _transformLookup[hazardEntity].Position;
            var distance = math.distance(carrierPos, hazardPos);
            return math.saturate(1f - (distance / hazardRange));
        }

        private Entity ResolveHazardEntity(Entity vessel, in VesselAIState aiState)
        {
            if (_miningStateLookup.HasComponent(vessel))
            {
                var mining = _miningStateLookup[vessel];
                if (mining.ActiveTarget != Entity.Null)
                {
                    return mining.ActiveTarget;
                }

                if (mining.LatchTarget != Entity.Null)
                {
                    return mining.LatchTarget;
                }
            }

            if (_miningOrderLookup.HasComponent(vessel))
            {
                var order = _miningOrderLookup[vessel];
                if (order.TargetEntity != Entity.Null)
                {
                    return order.TargetEntity;
                }

                if (order.PreferredTarget != Entity.Null)
                {
                    return order.PreferredTarget;
                }
            }

            return aiState.TargetEntity;
        }

        private float ComputeRiskThreshold(in ResolvedBehaviorProfile profile, byte lastDecision)
        {
            var baseThreshold = math.lerp(0.25f, 0.75f, profile.Chaos01);
            var riskBias = math.lerp(-0.1f, 0.1f, profile.Risk01);
            var obedienceBias = math.lerp(0.1f, -0.1f, profile.Obedience01);
            var threshold = math.saturate(baseThreshold + riskBias + obedienceBias);
            if (lastDecision == DecisionWait)
            {
                threshold = math.max(0f, threshold - RiskHysteresis);
            }

            return threshold;
        }

        private void PushDecisionTrace(Entity vessel, uint tick, Entity carrier)
        {
            if (!_traceLookup.HasBuffer(vessel))
            {
                return;
            }

            var buffer = _traceLookup[vessel];
            if (buffer.Length >= MovementDebugState.TraceCapacity)
            {
                buffer.RemoveAt(0);
            }

            buffer.Add(new MoveTraceEvent
            {
                Kind = MoveTraceEventKind.UndockDecision,
                Tick = tick,
                Target = carrier
            });
        }

        [BurstCompile]
        private static void ProcessUndocking(
            in Entity undockingEntity,
            in DockedTag docked,
            ref EntityCommandBuffer ecb,
            ref BufferLookup<DockedEntity> dockedBufferLookup,
            ref ComponentLookup<DockingCapacity> dockingLookup,
            ref ComponentLookup<CommandLoad> commandLookup)
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

            ecb.RemoveComponent<DockedTag>(undockingEntity);
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

            float avgUtilization = totalCapacity > 0 ? (float)totalDocked / totalCapacity : 0f;

            buffer.AddMetric("space4x.docking.carriers", carrierCount);
            buffer.AddMetric("space4x.docking.totalDocked", totalDocked);
            buffer.AddMetric("space4x.docking.totalCapacity", totalCapacity);
            buffer.AddMetric("space4x.docking.avgUtilization", avgUtilization, TelemetryMetricUnit.Ratio);
            buffer.AddMetric("space4x.docking.overcrowded", overcrowdedCount);
            buffer.AddMetric("space4x.docking.commandOverloaded", commandOverloadCount);
        }
    }
}
