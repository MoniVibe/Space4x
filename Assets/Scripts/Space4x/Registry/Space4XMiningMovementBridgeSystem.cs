using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Runtime;
using Space4X.Registry;
using Space4X.Systems.AI;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Registry
{
    /// <summary>
    /// Bridges MiningState.Phase to VesselAIState.CurrentState for entities with MiningOrder,
    /// ensuring movement systems can respond to mining state changes.
    /// Runs after Space4XMinerMiningSystem but before VesselMovementSystem.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(Space4XTransportAISystemGroup))]
    // Removed invalid UpdateAfter: Space4XMinerMiningSystem runs in FixedStepSimulationSystemGroup.
    [UpdateAfter(typeof(Space4X.Systems.AI.VesselAISystem))]
    [UpdateBefore(typeof(Space4X.Systems.AI.VesselTargetingSystem))]
    public partial struct Space4XMiningMovementBridgeSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<Space4XAsteroidVolumeConfig> _asteroidVolumeLookup;
        private ComponentLookup<MiningVessel> _miningVesselLookup;
        private ComponentLookup<VesselPhysicalProperties> _physicalLookup;
        private ComponentLookup<MiningState> _miningStateLookup;
        private BufferLookup<Space4XMiningLatchReservation> _latchReservationLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _asteroidVolumeLookup = state.GetComponentLookup<Space4XAsteroidVolumeConfig>(true);
            _miningVesselLookup = state.GetComponentLookup<MiningVessel>(true);
            _physicalLookup = state.GetComponentLookup<VesselPhysicalProperties>(true);
            _miningStateLookup = state.GetComponentLookup<MiningState>(true);
            _latchReservationLookup = state.GetBufferLookup<Space4XMiningLatchReservation>();
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

            var currentTick = timeState.Tick;
            _transformLookup.Update(ref state);
            _asteroidVolumeLookup.Update(ref state);
            _miningVesselLookup.Update(ref state);
            _physicalLookup.Update(ref state);
            _miningStateLookup.Update(ref state);
            _latchReservationLookup.Update(ref state);

            var latchConfig = Space4XMiningLatchConfig.Default;
            if (SystemAPI.TryGetSingleton<Space4XMiningLatchConfig>(out var latchConfigSingleton))
            {
                latchConfig = latchConfigSingleton;
            }

            var latchRegionCount = latchConfig.RegionCount > 0 ? latchConfig.RegionCount : Space4XMiningLatchUtility.DefaultLatchRegionCount;
            var surfaceEpsilon = math.max(0.05f, latchConfig.SurfaceEpsilon);
            var miningApproachStandoff = Space4XMiningLatchUtility.ResolveMiningApproachStandoff(surfaceEpsilon);
            var reserveLatchRegions = latchConfig.ReserveRegionWhileApproaching != 0;

            foreach (var (miningState, miningOrder, transform, aiState, entity) in SystemAPI.Query<RefRW<MiningState>, RefRO<MiningOrder>, RefRO<LocalTransform>, RefRW<VesselAIState>>()
                         .WithAll<MiningOrder>()
                         .WithEntityAccess())
            {
                // Only skip when no order exists; Completed orders still need return/dock syncing.
                if (miningOrder.ValueRO.Status == MiningOrderStatus.None)
                {
                    continue;
                }

                // Sync MiningState.Phase to VesselAIState.CurrentState
                var phase = miningState.ValueRO.Phase;
                var targetEntity = miningState.ValueRO.ActiveTarget;

                // Map MiningPhase to VesselAIState.State
                VesselAIState.State newState;
                switch (phase)
                {
                    case MiningPhase.Idle:
                        newState = VesselAIState.State.Idle;
                        break;
                    case MiningPhase.Undocking:
                        newState = VesselAIState.State.Idle;
                        break;
                    case MiningPhase.ApproachTarget:
                        newState = VesselAIState.State.MovingToTarget;
                        break;
                    case MiningPhase.Mining:
                        newState = VesselAIState.State.Mining;
                        break;
                    case MiningPhase.Latching:
                    case MiningPhase.Detaching:
                        newState = VesselAIState.State.Mining;
                        break;
                    case MiningPhase.ReturnApproach:
                    case MiningPhase.Docking:
                        newState = VesselAIState.State.Returning;
                        break;
                    default:
                        newState = VesselAIState.State.Idle;
                        break;
                }

                var desiredGoal = (phase == MiningPhase.ReturnApproach || phase == MiningPhase.Docking)
                    ? VesselAIState.Goal.Returning
                    : VesselAIState.Goal.Mining;

                // Update VesselAIState if changed
                if (aiState.ValueRO.CurrentState != newState)
                {
                    aiState.ValueRW.CurrentState = newState;
                    aiState.ValueRW.CurrentGoal = desiredGoal;

                    // Sync target entity from MiningState if available
                    if (targetEntity != Entity.Null && aiState.ValueRO.TargetEntity != targetEntity)
                    {
                        aiState.ValueRW.TargetEntity = targetEntity;
                    }

                    // Update state timing
                    if (newState != VesselAIState.State.Idle)
                    {
                        aiState.ValueRW.StateTimer = 0f;
                        aiState.ValueRW.StateStartTick = currentTick;
                    }
                }
                else if (targetEntity != Entity.Null && aiState.ValueRO.TargetEntity != targetEntity)
                {
                    // Update target even if state hasn't changed
                    aiState.ValueRW.TargetEntity = targetEntity;
                }

                if (aiState.ValueRO.CurrentGoal != desiredGoal)
                {
                    aiState.ValueRW.CurrentGoal = desiredGoal;
                }

                var previousLatchTarget = miningState.ValueRO.LatchTarget;
                if (previousLatchTarget != Entity.Null && previousLatchTarget != targetEntity &&
                    reserveLatchRegions && _latchReservationLookup.HasBuffer(previousLatchTarget))
                {
                    var reservations = _latchReservationLookup[previousLatchTarget];
                    Space4XMiningLatchUtility.ReleaseReservation(entity, ref reservations);
                }

                // Populate TargetPosition directly when we have a transform so movement systems don't skip MiningOrder vessels
                if (targetEntity != Entity.Null && _transformLookup.HasComponent(targetEntity))
                {
                    var targetTransform = _transformLookup[targetEntity];
                    var targetPos = targetTransform.Position;
                    if (_asteroidVolumeLookup.HasComponent(targetEntity))
                    {
                        var volume = _asteroidVolumeLookup[targetEntity];
                        var radius = math.max(0.5f, volume.Radius);
                        float3 latchPoint;
                        if (miningState.ValueRO.LatchTarget != targetEntity || miningState.ValueRO.HasLatchPoint == 0)
                        {
                            int regionId;
                            if (reserveLatchRegions && _latchReservationLookup.HasBuffer(targetEntity))
                            {
                                var reservations = _latchReservationLookup[targetEntity];
                                regionId = Space4XMiningLatchUtility.ResolveReservedLatchRegion(
                                    entity,
                                    targetEntity,
                                    volume.Seed,
                                    latchRegionCount,
                                    currentTick,
                                    ref reservations,
                                    _miningStateLookup);
                            }
                            else
                            {
                                regionId = Space4XMiningLatchUtility.ComputeLatchRegion(entity, targetEntity, volume.Seed, latchRegionCount);
                            }
                            var surfacePoint = Space4XMiningLatchUtility.ComputeSurfaceLatchPoint(targetPos, radius, regionId, volume.Seed);
                            miningState.ValueRW.LatchTarget = targetEntity;
                            miningState.ValueRW.LatchRegionId = regionId;
                            miningState.ValueRW.LatchSurfacePoint = surfacePoint;
                            miningState.ValueRW.HasLatchPoint = 1;
                            latchPoint = surfacePoint;
                        }
                        else
                        {
                            latchPoint = miningState.ValueRO.LatchSurfacePoint;
                            if (reserveLatchRegions && _latchReservationLookup.HasBuffer(targetEntity))
                            {
                                var reservations = _latchReservationLookup[targetEntity];
                                Space4XMiningLatchUtility.UpsertReservation(entity, miningState.ValueRO.LatchRegionId, currentTick, ref reservations);
                            }
                        }
                        var direction = math.normalizesafe(latchPoint - targetPos, new float3(0f, 0f, 1f));
                        var isMiner = _miningVesselLookup.HasComponent(entity);
                        var vesselRadius = _physicalLookup.HasComponent(entity)
                            ? math.max(0.1f, _physicalLookup[entity].Radius)
                            : 0.6f;
                        var standoff = Space4XMiningLatchUtility.ResolveAsteroidStandoff(
                            isMiner,
                            false,
                            vesselRadius);
                        if (isMiner)
                        {
                            standoff = math.max(standoff, miningApproachStandoff);
                        }
                        targetPos = targetPos + direction * radius + direction * standoff;
                    }
                    else if (miningState.ValueRO.HasLatchPoint != 0)
                    {
                        if (reserveLatchRegions && miningState.ValueRO.LatchTarget != Entity.Null &&
                            _latchReservationLookup.HasBuffer(miningState.ValueRO.LatchTarget))
                        {
                            var reservations = _latchReservationLookup[miningState.ValueRO.LatchTarget];
                            Space4XMiningLatchUtility.ReleaseReservation(entity, ref reservations);
                        }
                        miningState.ValueRW.LatchTarget = Entity.Null;
                        miningState.ValueRW.HasLatchPoint = 0;
                        miningState.ValueRW.LatchRegionId = 0;
                        miningState.ValueRW.LatchSurfacePoint = float3.zero;
                        miningState.ValueRW.LatchSettleUntilTick = 0;
                    }
                    aiState.ValueRW.TargetPosition = targetPos;
                }
                else
                {
                    if (miningState.ValueRO.HasLatchPoint != 0)
                    {
                        if (reserveLatchRegions && miningState.ValueRO.LatchTarget != Entity.Null &&
                            _latchReservationLookup.HasBuffer(miningState.ValueRO.LatchTarget))
                        {
                            var reservations = _latchReservationLookup[miningState.ValueRO.LatchTarget];
                            Space4XMiningLatchUtility.ReleaseReservation(entity, ref reservations);
                        }
                        miningState.ValueRW.LatchTarget = Entity.Null;
                        miningState.ValueRW.HasLatchPoint = 0;
                        miningState.ValueRW.LatchRegionId = 0;
                        miningState.ValueRW.LatchSurfacePoint = float3.zero;
                        miningState.ValueRW.LatchSettleUntilTick = 0;
                    }
                    aiState.ValueRW.TargetPosition = float3.zero;
                }
            }
        }
    }
}
