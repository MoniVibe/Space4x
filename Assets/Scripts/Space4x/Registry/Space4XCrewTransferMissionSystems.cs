using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Runtime;
using Space4X.Systems.AI;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Registry
{
    /// <summary>
    /// Assigns crew transfer missions to idle transport vessels.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(Space4XTransportAISystemGroup))]
    [UpdateAfter(typeof(VesselAISystem))]
    [UpdateBefore(typeof(VesselTargetingSystem))]
    public partial struct Space4XCrewTransferMissionAssignmentSystem : ISystem
    {
        private ComponentLookup<DockedTag> _dockedLookup;
        private ComponentLookup<EscortAssignment> _escortLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<CrewTransferMission>();
            _dockedLookup = state.GetComponentLookup<DockedTag>(true);
            _escortLookup = state.GetComponentLookup<EscortAssignment>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (time.IsPaused || rewind.Mode != RewindMode.Record)
            {
                return;
            }

            _dockedLookup.Update(ref state);
            _escortLookup.Update(ref state);

            foreach (var (mission, aiState, entity) in SystemAPI.Query<RefRW<CrewTransferMission>, RefRW<VesselAIState>>()
                         .WithNone<SimulationDisabledTag>()
                         .WithEntityAccess())
            {
                if (mission.ValueRO.Target == Entity.Null)
                {
                    mission.ValueRW.Status = CrewTransferMissionStatus.Failed;
                    mission.ValueRW.LastUpdateTick = time.Tick;
                    continue;
                }

                if (_dockedLookup.HasComponent(entity) || _escortLookup.HasComponent(entity))
                {
                    continue;
                }

                if (aiState.ValueRO.CurrentGoal == VesselAIState.Goal.Returning)
                {
                    continue;
                }

                if (mission.ValueRO.Status == CrewTransferMissionStatus.Completed ||
                    mission.ValueRO.Status == CrewTransferMissionStatus.Failed)
                {
                    continue;
                }

                var target = mission.ValueRO.Target;
                if (aiState.ValueRO.TargetEntity != target || aiState.ValueRO.CurrentState == VesselAIState.State.Idle)
                {
                    aiState.ValueRW.TargetEntity = target;
                    aiState.ValueRW.TargetPosition = float3.zero;
                    aiState.ValueRW.CurrentGoal = VesselAIState.Goal.Patrol;
                    aiState.ValueRW.CurrentState = VesselAIState.State.MovingToTarget;
                    aiState.ValueRW.StateTimer = 0f;
                    aiState.ValueRW.StateStartTick = time.Tick;
                }

                if (mission.ValueRO.Status == CrewTransferMissionStatus.Pending)
                {
                    mission.ValueRW.Status = CrewTransferMissionStatus.EnRoute;
                }

                mission.ValueRW.LastUpdateTick = time.Tick;
            }
        }
    }

    /// <summary>
    /// Overrides mission target positions using computed intercept courses.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(Space4XTransportAISystemGroup))]
    [UpdateAfter(typeof(VesselTargetingSystem))]
    public partial struct Space4XCrewTransferMissionInterceptOverrideSystem : ISystem
    {
        private ComponentLookup<InterceptCourse> _courseLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<CrewTransferMission>();
            _courseLookup = state.GetComponentLookup<InterceptCourse>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (time.IsPaused || rewind.Mode != RewindMode.Record)
            {
                return;
            }

            _courseLookup.Update(ref state);

            foreach (var (mission, aiState, entity) in SystemAPI.Query<RefRO<CrewTransferMission>, RefRW<VesselAIState>>().WithEntityAccess())
            {
                if (mission.ValueRO.Target == Entity.Null)
                {
                    continue;
                }

                if (!_courseLookup.HasComponent(entity))
                {
                    continue;
                }

                var course = _courseLookup[entity];
                if (course.TargetFleet != mission.ValueRO.Target)
                {
                    continue;
                }

                aiState.ValueRW.TargetPosition = course.InterceptPoint;
            }
        }
    }

    /// <summary>
    /// Issues intercept requests for active crew transfer missions.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(FleetInterceptRequestSystem))]
    [UpdateBefore(typeof(InterceptPathfindingSystem))]
    public partial struct Space4XCrewTransferMissionInterceptRequestSystem : ISystem
    {
        private ComponentLookup<InterceptCapability> _capabilityLookup;
        private ComponentLookup<VesselMovement> _movementLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<SimulationDisabledTag> _disabledLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XFleetInterceptQueue>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<CrewTransferMission>();

            _capabilityLookup = state.GetComponentLookup<InterceptCapability>(true);
            _movementLookup = state.GetComponentLookup<VesselMovement>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _disabledLookup = state.GetComponentLookup<SimulationDisabledTag>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (time.IsPaused || rewind.Mode != RewindMode.Record)
            {
                return;
            }

            _capabilityLookup.Update(ref state);
            _movementLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _disabledLookup.Update(ref state);

            var queueEntity = SystemAPI.GetSingletonEntity<Space4XFleetInterceptQueue>();
            var requests = state.EntityManager.GetBuffer<InterceptRequest>(queueEntity);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (mission, entity) in SystemAPI.Query<RefRW<CrewTransferMission>>().WithEntityAccess())
            {
                if (mission.ValueRO.Target == Entity.Null)
                {
                    continue;
                }

                if (mission.ValueRO.Status == CrewTransferMissionStatus.Completed ||
                    mission.ValueRO.Status == CrewTransferMissionStatus.Failed)
                {
                    continue;
                }

                if (_disabledLookup.HasComponent(entity))
                {
                    continue;
                }

                if (!_transformLookup.HasComponent(entity))
                {
                    continue;
                }

                byte priority = 1;
                if (_capabilityLookup.HasComponent(entity))
                {
                    priority = _capabilityLookup[entity].TechTier;
                }
                else
                {
                    var maxSpeed = 1f;
                    if (_movementLookup.HasComponent(entity))
                    {
                        maxSpeed = math.max(0.1f, _movementLookup[entity].BaseSpeed);
                    }

                    ecb.AddComponent(entity, new InterceptCapability
                    {
                        MaxSpeed = maxSpeed,
                        TechTier = 1,
                        AllowIntercept = 1
                    });
                    mission.ValueRW.AddedInterceptCapability = 1;
                }

                requests.Add(new InterceptRequest
                {
                    Requester = entity,
                    Target = mission.ValueRO.Target,
                    Priority = priority,
                    RequestTick = time.Tick,
                    RequireRendezvous = 0
                });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Issues docking requests for crew transfer missions when rendezvous is not allowed.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ResourceSystemGroup))]
    [UpdateAfter(typeof(Space4X.Systems.AI.VesselMovementSystem))]
    [UpdateBefore(typeof(Space4XCrewTransferMissionCompletionSystem))]
    public partial struct Space4XCrewTransferMissionDockingRequestSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<DockingPolicy> _policyLookup;
        private ComponentLookup<DockingState> _stateLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<CrewTransferMission>();

            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _policyLookup = state.GetComponentLookup<DockingPolicy>(true);
            _stateLookup = state.GetComponentLookup<DockingState>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (time.IsPaused || rewind.Mode != RewindMode.Record)
            {
                return;
            }

            _transformLookup.Update(ref state);
            _policyLookup.Update(ref state);
            _stateLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (mission, transform, entity) in SystemAPI.Query<RefRO<CrewTransferMission>, RefRO<LocalTransform>>()
                         .WithNone<DockingRequest, DockedTag, SimulationDisabledTag>()
                         .WithEntityAccess())
            {
                var target = mission.ValueRO.Target;
                if (target == Entity.Null)
                {
                    continue;
                }

                if (!_policyLookup.HasComponent(target))
                {
                    continue;
                }

                var policy = _policyLookup[target];
                if (policy.AllowDocking == 0 || policy.AllowRendezvous != 0)
                {
                    continue;
                }

                if (!_transformLookup.HasComponent(target))
                {
                    continue;
                }

                var dockingRange = policy.DockingRange > 0f ? policy.DockingRange : 4.5f;
                var distanceSq = math.lengthsq(transform.ValueRO.Position - _transformLookup[target].Position);
                if (distanceSq > dockingRange * dockingRange)
                {
                    continue;
                }

                ecb.AddComponent(entity, new DockingRequest
                {
                    TargetCarrier = target,
                    RequiredSlot = DockingSlotType.Utility,
                    RequestTick = time.Tick,
                    Priority = 0
                });

                var dockingState = new DockingState
                {
                    Phase = DockingPhase.Docking,
                    Target = target,
                    SlotType = DockingSlotType.Utility,
                    PresenceMode = policy.DefaultPresence,
                    RequestTick = time.Tick,
                    PhaseTick = time.Tick
                };
                if (_stateLookup.HasComponent(entity))
                {
                    ecb.SetComponent(entity, dockingState);
                }
                else
                {
                    ecb.AddComponent(entity, dockingState);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Completes crew transfer missions once the provider reaches the target.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ResourceSystemGroup))]
    [UpdateAfter(typeof(Space4X.Systems.AI.VesselMovementSystem))]
    public partial struct Space4XCrewTransferMissionCompletionSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<FleetMovementBroadcast> _broadcastLookup;
        private ComponentLookup<CrewCapacity> _capacityLookup;
        private ComponentLookup<CrewTransferPolicy> _policyLookup;
        private ComponentLookup<DockingPolicy> _dockingPolicyLookup;
        private ComponentLookup<DockingThroughputState> _throughputLookup;
        private ComponentLookup<DockedTag> _dockedLookup;
        private ComponentLookup<CrewTrainingState> _trainingLookup;
        private ComponentLookup<CrewGrowthState> _growthLookup;
        private ComponentLookup<CrewReservePool> _reserveLookup;
        private ComponentLookup<InterceptCourse> _courseLookup;
        private ComponentLookup<InterceptCapability> _capabilityLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<CrewTransferMission>();

            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _broadcastLookup = state.GetComponentLookup<FleetMovementBroadcast>(true);
            _capacityLookup = state.GetComponentLookup<CrewCapacity>(false);
            _policyLookup = state.GetComponentLookup<CrewTransferPolicy>(true);
            _dockingPolicyLookup = state.GetComponentLookup<DockingPolicy>(true);
            _throughputLookup = state.GetComponentLookup<DockingThroughputState>(false);
            _dockedLookup = state.GetComponentLookup<DockedTag>(true);
            _trainingLookup = state.GetComponentLookup<CrewTrainingState>(false);
            _growthLookup = state.GetComponentLookup<CrewGrowthState>(false);
            _reserveLookup = state.GetComponentLookup<CrewReservePool>(false);
            _courseLookup = state.GetComponentLookup<InterceptCourse>(true);
            _capabilityLookup = state.GetComponentLookup<InterceptCapability>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (time.IsPaused || rewind.Mode != RewindMode.Record)
            {
                return;
            }

            _transformLookup.Update(ref state);
            _broadcastLookup.Update(ref state);
            _capacityLookup.Update(ref state);
            _policyLookup.Update(ref state);
            _dockingPolicyLookup.Update(ref state);
            _throughputLookup.Update(ref state);
            _dockedLookup.Update(ref state);
            _trainingLookup.Update(ref state);
            _growthLookup.Update(ref state);
            _reserveLookup.Update(ref state);
            _courseLookup.Update(ref state);
            _capabilityLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (mission, aiState, entity) in SystemAPI.Query<RefRW<CrewTransferMission>, RefRW<VesselAIState>>().WithEntityAccess())
            {
                var target = mission.ValueRO.Target;
                if (target == Entity.Null)
                {
                    ReturnCrewToProvider(ref _reserveLookup, entity, mission.ValueRO.ReservedCrew, mission.ValueRO.ReservedTraining);
                    ClearMission(ref ecb, entity, ref aiState.ValueRW, ref _courseLookup, ref _capabilityLookup, mission.ValueRO, time.Tick);
                    continue;
                }

                if (!_transformLookup.HasComponent(entity))
                {
                    ReturnCrewToProvider(ref _reserveLookup, entity, mission.ValueRO.ReservedCrew, mission.ValueRO.ReservedTraining);
                    ClearMission(ref ecb, entity, ref aiState.ValueRW, ref _courseLookup, ref _capabilityLookup, mission.ValueRO, time.Tick);
                    continue;
                }

                var providerPos = _transformLookup[entity].Position;
                var hasTargetPos = TryGetTargetPosition(target, ref _transformLookup, ref _broadcastLookup, out var targetPos);
                if (!hasTargetPos)
                {
                    ReturnCrewToProvider(ref _reserveLookup, entity, mission.ValueRO.ReservedCrew, mission.ValueRO.ReservedTraining);
                    ClearMission(ref ecb, entity, ref aiState.ValueRW, ref _courseLookup, ref _capabilityLookup, mission.ValueRO, time.Tick);
                    continue;
                }

                var radius = mission.ValueRO.TransferRadius > 0f ? mission.ValueRO.TransferRadius : 3f;
                if (math.distancesq(providerPos, targetPos) > radius * radius)
                {
                    continue;
                }

                mission.ValueRW.Status = CrewTransferMissionStatus.Arrived;
                mission.ValueRW.LastUpdateTick = time.Tick;

                var hasDockPolicy = _dockingPolicyLookup.HasComponent(target);
                var dockingPolicy = hasDockPolicy ? _dockingPolicyLookup[target] : DockingPolicy.Default;
                var isDocked = _dockedLookup.HasComponent(entity) && _dockedLookup[entity].CarrierEntity == target;
                if (hasDockPolicy && dockingPolicy.AllowRendezvous == 0 && !isDocked)
                {
                    continue;
                }

                if (!_capacityLookup.HasComponent(target))
                {
                    ReturnCrewToProvider(ref _reserveLookup, entity, mission.ValueRO.ReservedCrew, mission.ValueRO.ReservedTraining);
                    ClearMission(ref ecb, entity, ref aiState.ValueRW, ref _courseLookup, ref _capabilityLookup, mission.ValueRO, time.Tick);
                    continue;
                }

                var capacity = _capacityLookup[target];
                var maxCrew = capacity.MaxCrew;
                if (maxCrew <= 0)
                {
                    ReturnCrewToProvider(ref _reserveLookup, entity, mission.ValueRO.ReservedCrew, mission.ValueRO.ReservedTraining);
                    ClearMission(ref ecb, entity, ref aiState.ValueRW, ref _courseLookup, ref _capabilityLookup, mission.ValueRO, time.Tick);
                    continue;
                }

                var ratio = 1f;
                if (_policyLookup.HasComponent(target))
                {
                    ratio = math.max(0f, _policyLookup[target].DesiredCrewRatio);
                }

                var desired = (int)math.ceil(maxCrew * ratio);
                if (capacity.CriticalMax > 0)
                {
                    desired = math.min(desired, capacity.CriticalMax);
                }

                var needed = desired - capacity.CurrentCrew;
                if (needed <= 0 || mission.ValueRO.ReservedCrew <= 0)
                {
                    ReturnCrewToProvider(ref _reserveLookup, entity, mission.ValueRO.ReservedCrew, mission.ValueRO.ReservedTraining);
                    ClearMission(ref ecb, entity, ref aiState.ValueRW, ref _courseLookup, ref _capabilityLookup, mission.ValueRO, time.Tick);
                    continue;
                }

                var toTransfer = math.min(needed, mission.ValueRO.ReservedCrew);
                toTransfer = ClampTransferByThroughput(target, toTransfer, time.Tick, in dockingPolicy, ref _throughputLookup);
                if (toTransfer <= 0)
                {
                    continue;
                }

                var reservedCrew = mission.ValueRO.ReservedCrew;
                var reservedTraining = mission.ValueRO.ReservedTraining;
                var applyRatio = reservedCrew > 0 ? (float)toTransfer / reservedCrew : 0f;
                var appliedTraining = reservedTraining * applyRatio;

                capacity.CurrentCrew += toTransfer;
                _capacityLookup[target] = capacity;

                if (_trainingLookup.HasComponent(target))
                {
                    var training = _trainingLookup[target];
                    var previousCrew = capacity.CurrentCrew - toTransfer;
                    var weightedTraining = training.TrainingLevel * previousCrew;
                    var newTotal = capacity.CurrentCrew;
                    var newTraining = newTotal > 0 ? (weightedTraining + appliedTraining) / newTotal : training.TrainingLevel;
                    training.TrainingLevel = math.clamp(newTraining, 0f, training.MaxTraining);
                    _trainingLookup[target] = training;
                }

                if (_growthLookup.HasComponent(target))
                {
                    var growth = _growthLookup[target];
                    growth.CurrentCrew = capacity.CurrentCrew;
                    _growthLookup[target] = growth;
                }

                var remainingCrew = math.max(0, reservedCrew - toTransfer);
                var remainingTraining = math.max(0f, reservedTraining - appliedTraining);
                mission.ValueRW.ReservedCrew = remainingCrew;
                mission.ValueRW.ReservedTraining = remainingTraining;
                mission.ValueRW.LastUpdateTick = time.Tick;
                mission.ValueRW.Status = CrewTransferMissionStatus.Arrived;

                var remainingNeeded = desired - capacity.CurrentCrew;
                if (remainingNeeded <= 0)
                {
                    if (remainingCrew > 0)
                    {
                        ReturnCrewToProvider(ref _reserveLookup, entity, remainingCrew, remainingTraining);
                    }
                    ClearMission(ref ecb, entity, ref aiState.ValueRW, ref _courseLookup, ref _capabilityLookup, mission.ValueRO, time.Tick);
                }
                else if (remainingCrew <= 0)
                {
                    ClearMission(ref ecb, entity, ref aiState.ValueRW, ref _courseLookup, ref _capabilityLookup, mission.ValueRO, time.Tick);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static bool TryGetTargetPosition(
            Entity target,
            ref ComponentLookup<LocalTransform> transformLookup,
            ref ComponentLookup<FleetMovementBroadcast> broadcastLookup,
            out float3 position)
        {
            if (transformLookup.HasComponent(target))
            {
                position = transformLookup[target].Position;
                return true;
            }

            if (broadcastLookup.HasComponent(target))
            {
                position = broadcastLookup[target].Position;
                return true;
            }

            position = float3.zero;
            return false;
        }

        private static void ReturnCrewToProvider(
            ref ComponentLookup<CrewReservePool> reserveLookup,
            Entity provider,
            int crew,
            float trainingWeight)
        {
            if (crew <= 0 || !reserveLookup.HasComponent(provider))
            {
                return;
            }

            trainingWeight = math.max(0f, trainingWeight);
            var pool = reserveLookup[provider];
            var totalTraining = pool.TrainingLevel * pool.Available + trainingWeight;
            pool.Available += crew;
            if (pool.Available > 0f)
            {
                pool.TrainingLevel = math.clamp(totalTraining / pool.Available, 0f, 1f);
            }

            reserveLookup[provider] = pool;
        }

        private static int ClampTransferByThroughput(
            Entity target,
            int requested,
            uint tick,
            in DockingPolicy policy,
            ref ComponentLookup<DockingThroughputState> throughputLookup)
        {
            if (requested <= 0)
            {
                return 0;
            }

            if (!throughputLookup.HasComponent(target))
            {
                return requested;
            }

            var throughput = throughputLookup[target];
            if (throughput.LastResetTick != tick)
            {
                throughput.LastResetTick = tick;
                throughput.CrewRemaining = math.max(0f, policy.CrewTransferPerTick);
                throughput.CargoRemaining = math.max(0f, policy.CargoTransferPerTick);
                throughput.AmmoRemaining = math.max(0f, policy.AmmoTransferPerTick);
            }

            var available = (int)math.floor(math.max(0f, throughput.CrewRemaining));
            var allowed = math.min(requested, available);
            throughput.CrewRemaining = math.max(0f, throughput.CrewRemaining - allowed);
            throughputLookup[target] = throughput;
            return allowed;
        }

        private static void ClearMission(
            ref EntityCommandBuffer ecb,
            Entity provider,
            ref VesselAIState aiState,
            ref ComponentLookup<InterceptCourse> courseLookup,
            ref ComponentLookup<InterceptCapability> capabilityLookup,
            in CrewTransferMission mission,
            uint tick)
        {
            aiState.CurrentGoal = VesselAIState.Goal.Idle;
            aiState.CurrentState = VesselAIState.State.Idle;
            aiState.TargetEntity = Entity.Null;
            aiState.TargetPosition = float3.zero;
            aiState.StateTimer = 0f;
            aiState.StateStartTick = tick;

            ecb.RemoveComponent<CrewTransferMission>(provider);

            if (courseLookup.HasComponent(provider))
            {
                ecb.RemoveComponent<InterceptCourse>(provider);
            }

            if (mission.AddedInterceptCapability != 0 && capabilityLookup.HasComponent(provider))
            {
                ecb.RemoveComponent<InterceptCapability>(provider);
            }
        }
    }
}
