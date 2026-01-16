using PureDOTS.Environment;
using PureDOTS.Runtime.Agency;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Profile;
using PureDOTS.Systems;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
namespace Space4X.Registry
{
    /// <summary>
    /// Miner state machine that claims registry-driven resources, applies mining ticks,
    /// and publishes presentation effect requests without hybrid lookups.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PureDOTS.Systems.ResourceSystemGroup))]
    // Removed invalid UpdateAfter: GameplayFixedStepSyncSystem runs in TimeSystemGroup.
    public partial struct Space4XMinerMiningSystem : ISystem
    {
        private ComponentLookup<ResourceSourceState> _resourceStateLookup;
        private ComponentLookup<ResourceSourceConfig> _resourceConfigLookup;
        private ComponentLookup<ResourceTypeId> _resourceTypeLookup;
        private ComponentLookup<Asteroid> _asteroidLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<Space4XAsteroidVolumeConfig> _asteroidVolumeLookup;
        private ComponentLookup<MiningState> _miningStateLookup;
        private BufferLookup<Space4XMiningLatchReservation> _latchReservationLookup;
        private BufferLookup<PlayEffectRequest> _effectRequestLookup;
        private ComponentLookup<CrewSkills> _crewSkillsLookup;
        private ComponentLookup<VesselPilotLink> _pilotLinkLookup;
        private ComponentLookup<BehaviorDisposition> _behaviorDispositionLookup;
        private BufferLookup<ResolvedControl> _resolvedControlLookup;
        private ComponentLookup<Space4XMiningToolProfile> _toolProfileLookup;
        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<DockedTag> _dockedLookup;
        private ComponentLookup<DecisionTrace> _decisionTraceLookup;
        private ComponentLookup<MiningDecisionTrace> _miningDecisionLookup;
        private ComponentLookup<VesselMovement> _movementLookup;
        private Entity _effectStreamEntity;
        private static readonly FixedString64Bytes MiningSparksEffectId = CreateMiningEffectId();
        private static readonly FixedString64Bytes MiningLaserEffectId = CreateMiningLaserEffectId();
        private static readonly FixedString64Bytes MiningMicrowaveEffectId = CreateMiningMicrowaveEffectId();
        private const float UndockDuration = 1.5f;
        private const float LatchDuration = 0.9f;
        private const float DetachDuration = 0.8f;
        private const float DockDuration = 1.2f;
        private const float AlignRelaxSpeed = 0.5f;

        private static FixedString64Bytes CreateMiningEffectId()
        {
            // Build without managed strings so Burst can compile the system.
            FixedString64Bytes effectId = default;
            effectId.Append('F');
            effectId.Append('X');
            effectId.Append('.');
            effectId.Append('M');
            effectId.Append('i');
            effectId.Append('n');
            effectId.Append('i');
            effectId.Append('n');
            effectId.Append('g');
            effectId.Append('.');
            effectId.Append('S');
            effectId.Append('p');
            effectId.Append('a');
            effectId.Append('r');
            effectId.Append('k');
            effectId.Append('s');
            return effectId;
        }

        private static FixedString64Bytes CreateMiningLaserEffectId()
        {
            FixedString64Bytes effectId = default;
            effectId.Append('F');
            effectId.Append('X');
            effectId.Append('.');
            effectId.Append('M');
            effectId.Append('i');
            effectId.Append('n');
            effectId.Append('i');
            effectId.Append('n');
            effectId.Append('g');
            effectId.Append('.');
            effectId.Append('L');
            effectId.Append('a');
            effectId.Append('s');
            effectId.Append('e');
            effectId.Append('r');
            return effectId;
        }

        private static FixedString64Bytes CreateMiningMicrowaveEffectId()
        {
            FixedString64Bytes effectId = default;
            effectId.Append('F');
            effectId.Append('X');
            effectId.Append('.');
            effectId.Append('M');
            effectId.Append('i');
            effectId.Append('n');
            effectId.Append('i');
            effectId.Append('n');
            effectId.Append('g');
            effectId.Append('.');
            effectId.Append('M');
            effectId.Append('i');
            effectId.Append('c');
            effectId.Append('r');
            effectId.Append('o');
            effectId.Append('w');
            effectId.Append('a');
            effectId.Append('v');
            effectId.Append('e');
            return effectId;
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<MiningOrder>();

            _resourceStateLookup = state.GetComponentLookup<ResourceSourceState>(false);
            _resourceConfigLookup = state.GetComponentLookup<ResourceSourceConfig>(true);
            _resourceTypeLookup = state.GetComponentLookup<ResourceTypeId>(true);
            _asteroidLookup = state.GetComponentLookup<Asteroid>(false);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _asteroidVolumeLookup = state.GetComponentLookup<Space4XAsteroidVolumeConfig>(true);
            _miningStateLookup = state.GetComponentLookup<MiningState>(true);
            _latchReservationLookup = state.GetBufferLookup<Space4XMiningLatchReservation>();
            _effectRequestLookup = state.GetBufferLookup<PlayEffectRequest>();
            _crewSkillsLookup = state.GetComponentLookup<CrewSkills>(true);
            _pilotLinkLookup = state.GetComponentLookup<VesselPilotLink>(true);
            _behaviorDispositionLookup = state.GetComponentLookup<BehaviorDisposition>(true);
            _resolvedControlLookup = state.GetBufferLookup<ResolvedControl>(true);
            _toolProfileLookup = state.GetComponentLookup<Space4XMiningToolProfile>(true);
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _dockedLookup = state.GetComponentLookup<DockedTag>(true);
            _decisionTraceLookup = state.GetComponentLookup<DecisionTrace>(false);
            _miningDecisionLookup = state.GetComponentLookup<MiningDecisionTrace>(false);
            _movementLookup = state.GetComponentLookup<VesselMovement>(true);

            EnsureEffectStream(ref state);
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

            var deltaTime = timeState.FixedDeltaTime;

            _resourceStateLookup.Update(ref state);
            _resourceConfigLookup.Update(ref state);
            _resourceTypeLookup.Update(ref state);
            _asteroidLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _effectRequestLookup.Update(ref state);
            _crewSkillsLookup.Update(ref state);
            _pilotLinkLookup.Update(ref state);
            _behaviorDispositionLookup.Update(ref state);
            _resolvedControlLookup.Update(ref state);
            _asteroidVolumeLookup.Update(ref state);
            _miningStateLookup.Update(ref state);
            _latchReservationLookup.Update(ref state);
            _toolProfileLookup.Update(ref state);
            _carrierLookup.Update(ref state);
            _dockedLookup.Update(ref state);
            _decisionTraceLookup.Update(ref state);
            _miningDecisionLookup.Update(ref state);
            _movementLookup.Update(ref state);
            EnsureEffectStream(ref state);

            var actionStreamConfig = default(ProfileActionEventStreamConfig);
            var canEmitActions = SystemAPI.TryGetSingletonEntity<ProfileActionEventStream>(out var actionStreamEntity) &&
                                 SystemAPI.TryGetSingleton(out actionStreamConfig);
            DynamicBuffer<ProfileActionEvent> actionBuffer = default;
            RefRW<ProfileActionEventStream> actionStream = default;
            if (canEmitActions)
            {
                actionBuffer = SystemAPI.GetBuffer<ProfileActionEvent>(actionStreamEntity);
                actionStream = SystemAPI.GetComponentRW<ProfileActionEventStream>(actionStreamEntity);
            }

            var effectBuffer = GetEffectBuffer(ref state);
            var digConfig = Space4XMiningDigConfig.Default;
            if (SystemAPI.TryGetSingleton<Space4XMiningDigConfig>(out var digConfigSingleton))
            {
                digConfig = digConfigSingleton;
            }

            var latchConfig = Space4XMiningLatchConfig.Default;
            if (SystemAPI.TryGetSingleton<Space4XMiningLatchConfig>(out var latchConfigSingleton))
            {
                latchConfig = latchConfigSingleton;
            }

            var latchRegionCount = latchConfig.RegionCount > 0 ? latchConfig.RegionCount : Space4XMiningLatchUtility.DefaultLatchRegionCount;
            var surfaceEpsilon = math.max(0.05f, latchConfig.SurfaceEpsilon);
            var miningApproachStandoff = Space4XMiningLatchUtility.ResolveMiningApproachStandoff(surfaceEpsilon);
            var miningStandoff = math.max(Space4XMiningLatchUtility.MinerAsteroidStandoff, miningApproachStandoff);
            var rangeThreshold = Space4XMiningLatchUtility.ResolveMiningRangeThreshold(surfaceEpsilon);
            var enterRange = rangeThreshold + Space4XMiningLatchUtility.MiningEnterSlack;
            var exitRange = rangeThreshold + Space4XMiningLatchUtility.MiningExitSlack;
            var alignDotThreshold = math.saturate(latchConfig.AlignDotThreshold);
            var reserveLatchRegions = latchConfig.ReserveRegionWhileApproaching != 0;
            var telemetryStrideTicks = latchConfig.TelemetrySampleEveryTicks > 0 ? latchConfig.TelemetrySampleEveryTicks : 30u;
            var telemetryMaxSamples = latchConfig.TelemetryMaxSamples > 0 ? latchConfig.TelemetryMaxSamples : 2048;
            var settleTicks = latchConfig.SettleTicks;

            var hasModificationQueue = SystemAPI.TryGetSingletonEntity<TerrainModificationQueue>(out var modificationQueueEntity);
            DynamicBuffer<TerrainModificationRequest> modificationBuffer = default;
            if (hasModificationQueue)
            {
                modificationBuffer = SystemAPI.GetBuffer<TerrainModificationRequest>(modificationQueueEntity);
            }
            var hasCommandLog = SystemAPI.TryGetSingletonBuffer<MiningCommandLogEntry>(out var commandLog);
            var currentTick = timeState.Tick;
            var hasApproachTelemetry = SystemAPI.TryGetSingletonEntity<Space4XMiningTimeSpine>(out var miningSpineEntity) &&
                                       state.EntityManager.HasBuffer<MiningApproachTelemetrySample>(miningSpineEntity);
            DynamicBuffer<MiningApproachTelemetrySample> approachSamples = default;
            if (hasApproachTelemetry)
            {
                approachSamples = state.EntityManager.GetBuffer<MiningApproachTelemetrySample>(miningSpineEntity);
            }

            foreach (var (order, miningState, vessel, yield, transform, entity) in SystemAPI.Query<RefRW<MiningOrder>, RefRW<MiningState>, RefRW<MiningVessel>, RefRW<MiningYield>, RefRW<LocalTransform>>()
                         .WithEntityAccess())
            {
                if (!EnsureOrderResource(ref order.ValueRW, yield.ValueRO.ResourceId))
                {
                    continue;
                }

                var cargoType = Space4XMiningResourceUtility.MapToResourceType(order.ValueRO.ResourceId, vessel.ValueRO.CargoResourceType);
                if (vessel.ValueRO.CargoResourceType != cargoType)
                {
                    vessel.ValueRW.CargoResourceType = cargoType;
                }

                var phase = miningState.ValueRO.Phase;
                var isReturnPhase = phase == MiningPhase.Detaching ||
                                    phase == MiningPhase.ReturnApproach ||
                                    phase == MiningPhase.Docking;

                if (!isReturnPhase)
                {
                    if (!TryResolveTarget(ref state, ref order.ValueRW, ref miningState.ValueRW, transform.ValueRO.Position))
                    {
                        continue;
                    }
                }
                else
                {
                    EnsureReturnTarget(ref miningState.ValueRW, vessel.ValueRO.CarrierEntity);
                }

                var target = miningState.ValueRO.ActiveTarget;
                var previousLatchTarget = miningState.ValueRO.LatchTarget;
                if (previousLatchTarget != Entity.Null && previousLatchTarget != target)
                {
                    ReleaseLatchReservation(previousLatchTarget, entity, reserveLatchRegions);
                }

                if (target != Entity.Null && _transformLookup.HasComponent(target) && _asteroidVolumeLookup.HasComponent(target))
                {
                    EnsureLatchState(entity, target, ref miningState.ValueRW, _transformLookup[target], _asteroidVolumeLookup[target],
                        latchRegionCount, reserveLatchRegions, currentTick);
                }
                else if (miningState.ValueRO.HasLatchPoint != 0)
                {
                    if (miningState.ValueRO.LatchTarget != Entity.Null)
                    {
                        ReleaseLatchReservation(miningState.ValueRO.LatchTarget, entity, reserveLatchRegions);
                    }
                    ResetLatchState(ref miningState.ValueRW);
                }

                var distanceToSurface = 0f;
                var latchAligned = true;
                var hasAsteroidLatch = TryResolveAsteroidLatchMetrics(target, transform.ValueRO.Position, transform.ValueRO.Rotation,
                    miningState.ValueRO, alignDotThreshold, out distanceToSurface, out latchAligned);
                var approachTargetDistance = float.MaxValue;
                if (hasAsteroidLatch && _transformLookup.HasComponent(target) && _asteroidVolumeLookup.HasComponent(target))
                {
                    var targetTransform = _transformLookup[target];
                    var volume = _asteroidVolumeLookup[target];
                    var radius = math.max(0.5f, volume.Radius);
                    var surfacePoint = miningState.ValueRO.HasLatchPoint != 0 && miningState.ValueRO.LatchTarget == target
                        ? miningState.ValueRO.LatchSurfacePoint
                        : targetTransform.Position +
                          math.normalizesafe(transform.ValueRO.Position - targetTransform.Position, new float3(0f, 0f, 1f)) * radius;
                    var approachDirection = math.normalizesafe(surfacePoint - targetTransform.Position, new float3(0f, 0f, 1f));
                    var approachPoint = targetTransform.Position + approachDirection * (radius + miningStandoff);
                    approachTargetDistance = math.distance(transform.ValueRO.Position, approachPoint);
                }
                var inDigRange = false;
                if (target != Entity.Null && hasAsteroidLatch)
                {
                    inDigRange = miningState.ValueRO.InDigRange != 0;
                    if (!inDigRange && (distanceToSurface <= enterRange ||
                                        approachTargetDistance <= Space4XMiningLatchUtility.MiningExitSlack))
                    {
                        inDigRange = true;
                    }
                    else if (inDigRange && distanceToSurface > exitRange)
                    {
                        inDigRange = false;
                    }

                    miningState.ValueRW.InDigRange = (byte)(inDigRange ? 1 : 0);
                }
                else if (target != Entity.Null)
                {
                    inDigRange = IsTargetInRange(target, transform.ValueRO.Position, rangeThreshold);
                    miningState.ValueRW.InDigRange = (byte)(inDigRange ? 1 : 0);
                }
                else
                {
                    miningState.ValueRW.InDigRange = 0;
                }
                var alignedForLatch = latchAligned;
                if (!alignedForLatch && hasAsteroidLatch && distanceToSurface <= rangeThreshold)
                {
                    var currentSpeed = _movementLookup.HasComponent(entity) ? _movementLookup[entity].CurrentSpeed : 0f;
                    if (currentSpeed <= AlignRelaxSpeed)
                    {
                        alignedForLatch = true;
                    }
                }
                var latchReady = target != Entity.Null &&
                                 (!hasAsteroidLatch
                                     ? inDigRange
                                     : inDigRange && alignedForLatch);

                if (hasApproachTelemetry && target != Entity.Null &&
                    currentTick >= miningState.ValueRO.LastLatchTelemetryTick + telemetryStrideTicks)
                {
                    var regionId = miningState.ValueRO.HasLatchPoint != 0 ? miningState.ValueRO.LatchRegionId : -1;
                    approachSamples.Add(new MiningApproachTelemetrySample
                    {
                        Tick = currentTick,
                        Miner = entity,
                        Target = target,
                        Phase = miningState.ValueRO.Phase,
                        LatchRegionId = regionId,
                        DistanceToSurface = distanceToSurface
                    });

                    if (telemetryMaxSamples > 0 && approachSamples.Length > telemetryMaxSamples)
                    {
                        approachSamples.RemoveAt(0);
                    }

                    miningState.ValueRW.LastLatchTelemetryTick = currentTick;
                }

                var vesselData = vessel.ValueRO;
                var toolProfile = ResolveToolProfile(entity);
                var toolKind = toolProfile.ToolKind;
                var toolShape = ResolveToolShape(toolProfile);
                var disposition = ResolveBehaviorDisposition(entity);
                var returnRatio = ResolveCargoReturnRatio(disposition);
                var isCargoFull = vesselData.CurrentCargo >= vesselData.CargoCapacity * returnRatio;
                var isAsteroidEmpty = _resourceStateLookup.HasComponent(target) &&
                                      _resourceStateLookup[target].UnitsRemaining <= 0f;
                var undockDuration = ResolvePhaseDuration(UndockDuration, disposition);
                var latchDuration = ResolvePhaseDuration(LatchDuration, disposition);
                var detachDuration = ResolvePhaseDuration(DetachDuration, disposition);
                var dockDuration = ResolvePhaseDuration(DockDuration, disposition);

                switch (phase)
                {
                    case MiningPhase.Idle:
                        if (miningState.ValueRO.ActiveTarget != Entity.Null)
                        {
                            SetPhase(ref miningState.ValueRW, MiningPhase.Undocking, undockDuration);
                            order.ValueRW.Status = MiningOrderStatus.Active;
                        }
                        else
                        {
                            UpdateMiningDecisionTrace(entity, MiningDecisionReason.NoTarget, Entity.Null, 0f, currentTick);
                            ResetDigState(ref miningState.ValueRW);
                        }
                        break;

                    case MiningPhase.Undocking:
                        if (_dockedLookup.HasComponent(entity))
                        {
                            UpdateDecisionTrace(entity, DecisionReasonCode.MiningUndockWait, vessel.ValueRO.CarrierEntity, currentTick);
                            UpdateMiningDecisionTrace(entity, MiningDecisionReason.UndockWait, vessel.ValueRO.CarrierEntity, 0f, currentTick);
                            break;
                        }

                        if (AdvancePhaseTimer(ref miningState.ValueRW, deltaTime))
                        {
                            miningState.ValueRW.Phase = MiningPhase.ApproachTarget;
                        }
                        break;

                    case MiningPhase.ApproachTarget:
                        if (target == Entity.Null)
                        {
                            UpdateMiningDecisionTrace(entity, MiningDecisionReason.NoTarget, Entity.Null, 0f, currentTick);
                            miningState.ValueRW.Phase = MiningPhase.Idle;
                            ResetDigState(ref miningState.ValueRW);
                            break;
                        }

                        if (latchReady)
                        {
                            SetPhase(ref miningState.ValueRW, MiningPhase.Latching, latchDuration);
                            miningState.ValueRW.LatchSettleUntilTick = settleTicks > 0u ? currentTick + settleTicks : 0u;
                        }
                        else
                        {
                            var blockReason = ResolveLatchBlockReason(hasAsteroidLatch, distanceToSurface, rangeThreshold, latchAligned);
                            UpdateMiningDecisionTrace(entity, blockReason, target, distanceToSurface, currentTick,
                                rangeThreshold, miningStandoff, latchAligned);
                        }
                        break;

                    case MiningPhase.Latching:
                        if (target != Entity.Null)
                        {
                            StickToAsteroidSurface(target, deltaTime, ref transform.ValueRW, miningState.ValueRO);
                        }
                        if (!latchReady)
                        {
                            UpdateDecisionTrace(entity, DecisionReasonCode.MiningLatchWait, target, currentTick);
                            UpdateMiningDecisionTrace(entity, MiningDecisionReason.LatchNotReady, target, distanceToSurface, currentTick,
                                rangeThreshold, miningStandoff, latchAligned);
                            miningState.ValueRW.Phase = MiningPhase.ApproachTarget;
                            ResetDigState(ref miningState.ValueRW);
                            miningState.ValueRW.LatchSettleUntilTick = 0u;
                            break;
                        }

                        if (AdvancePhaseTimer(ref miningState.ValueRW, deltaTime))
                        {
                            miningState.ValueRW.Phase = MiningPhase.Mining;
                            miningState.ValueRW.MiningTimer = 0f;
                            if (canEmitActions)
                            {
                                var actionEvent = new ProfileActionEvent
                                {
                                    Token = ProfileActionToken.MineResource,
                                    IntentFlags = ProfileActionIntentFlags.None,
                                    JustificationFlags = ProfileActionJustificationFlags.None,
                                    OutcomeFlags = ProfileActionOutcomeFlags.None,
                                    Magnitude = 60,
                                    Actor = ResolveProfileEntity(entity),
                                    Target = target,
                                    IssuingSeat = Entity.Null,
                                    IssuingOccupant = Entity.Null,
                                    ActingSeat = Entity.Null,
                                    ActingOccupant = Entity.Null,
                                    Tick = currentTick
                                };
                                ProfileActionEventUtility.TryAppend(ref actionStream.ValueRW, actionBuffer, actionEvent, actionStreamConfig.MaxEvents);
                            }
                        }
                        break;

                    case MiningPhase.Mining:
                        if (target != Entity.Null)
                        {
                            StickToAsteroidSurface(target, deltaTime, ref transform.ValueRW, miningState.ValueRO);
                        }
                        if (target == Entity.Null || !latchReady)
                        {
                            var blockReason = ResolveLatchBlockReason(hasAsteroidLatch, distanceToSurface, rangeThreshold, latchAligned);
                            UpdateMiningDecisionTrace(entity, blockReason, target, distanceToSurface, currentTick,
                                rangeThreshold, miningStandoff, latchAligned);
                            miningState.ValueRW.Phase = MiningPhase.ApproachTarget;
                            ResetDigState(ref miningState.ValueRW);
                            miningState.ValueRW.LatchSettleUntilTick = 0u;
                            break;
                        }

                        if (isCargoFull || isAsteroidEmpty)
                        {
                            UpdateMiningDecisionTrace(entity, isCargoFull ? MiningDecisionReason.ReturnFull : MiningDecisionReason.DigZero, target, distanceToSurface, currentTick);
                            order.ValueRW.Status = MiningOrderStatus.Completed;
                            SetPhase(ref miningState.ValueRW, MiningPhase.Detaching, detachDuration);
                            EnsureReturnTarget(ref miningState.ValueRW, vessel.ValueRO.CarrierEntity);
                            ResetDigState(ref miningState.ValueRW);
                            break;
                        }

                        var tickInterval = GetTickInterval(ref miningState.ValueRW, deltaTime);
                        miningState.ValueRW.MiningTimer += deltaTime;
                        var volumeConfig = ResolveVolumeConfig(target);
                        if (miningState.ValueRO.DigVolumeEntity != target)
                        {
                            ResetDigState(ref miningState.ValueRW);
                        }

                        if (miningState.ValueRO.HasDigHead == 0 && _transformLookup.HasComponent(target))
                        {
                            EnsureDigHead(ref miningState.ValueRW, target, transform.ValueRO, _transformLookup[target], volumeConfig);
                        }

                        var safetyCounter = 0;
                        while (miningState.ValueRW.MiningTimer >= tickInterval && safetyCounter < 4)
                        {
                            miningState.ValueRW.MiningTimer -= tickInterval;
                            var digHead = miningState.ValueRW.DigHeadLocal;
                            var digDirection = miningState.ValueRW.DigDirectionLocal;
                            var yieldMultiplier = 1f;
                            if (miningState.ValueRW.HasDigHead != 0)
                            {
                                var distance = math.length(digHead);
                                var fallbackDirection = math.normalizesafe(digDirection, new float3(0f, 0f, 1f));
                                digDirection = math.normalizesafe(-digHead, fallbackDirection);
                                miningState.ValueRW.DigDirectionLocal = digDirection;
                                yieldMultiplier = ResolveYieldMultiplier(distance, math.max(0.01f, volumeConfig.Radius), volumeConfig, digConfig);
                            }

                            yieldMultiplier *= ResolveToolYieldMultiplier(toolKind, toolProfile, digConfig);
                            var mined = ApplyMiningTick(entity, target, tickInterval, yieldMultiplier, ref vessel.ValueRW, order.ValueRO.ResourceId);
                            if (mined <= 0f)
                            {
                                UpdateMiningDecisionTrace(entity, MiningDecisionReason.DigZero, target, distanceToSurface, currentTick);
                                break;
                            }

                            UpdateDecisionTrace(entity, DecisionReasonCode.MiningDigging, target, currentTick);
                            UpdateMiningDecisionTrace(entity, MiningDecisionReason.Digging, target, distanceToSurface, currentTick);
                            if (miningState.ValueRW.HasDigHead != 0)
                            {
                                var distance = math.length(digHead);
                                var stepLength = ResolveToolStepLength(toolKind, toolProfile, mined, digConfig, distance);
                                if (stepLength > 0f)
                                {
                                    var digStart = digHead;
                                    var digEnd = digStart + digDirection * stepLength;

                                    if (hasModificationQueue && modificationBuffer.IsCreated)
                                    {
                                        var end = toolShape == TerrainModificationShape.Brush ? digStart : digEnd;
                                        modificationBuffer.Add(new TerrainModificationRequest
                                        {
                                            Kind = TerrainModificationKind.Dig,
                                            Shape = toolShape,
                                            ToolKind = toolKind,
                                            Start = digStart,
                                            End = end,
                                            Radius = ResolveToolRadius(toolKind, toolProfile, digConfig),
                                            Depth = 0f,
                                            MaterialId = 0,
                                            DamageDelta = ResolveToolDamageDelta(toolKind, toolProfile, digConfig),
                                            DamageThreshold = ResolveToolDamageThreshold(toolKind, toolProfile, digConfig),
                                            YieldMultiplier = ResolveToolYieldMultiplier(toolKind, toolProfile, digConfig),
                                            HeatDelta = ResolveToolHeatDelta(toolKind, toolProfile, digConfig),
                                            InstabilityDelta = ResolveToolInstabilityDelta(toolKind, toolProfile, digConfig),
                                            Flags = TerrainModificationFlags.AffectsVolume,
                                            RequestedTick = currentTick,
                                            Actor = entity,
                                            VolumeEntity = target,
                                            Space = TerrainModificationSpace.VolumeLocal
                                        });
                                    }

                                    miningState.ValueRW.DigHeadLocal = digEnd;
                                }
                            }

                            UpdateYield(ref yield.ValueRW, order.ValueRO.ResourceId, mined);
                            LogMiningCommand(hasCommandLog, commandLog, currentTick, target, entity, vessel.ValueRO.CargoResourceType, mined, transform.ValueRO.Position);
                            var effectDirection = ResolveEffectDirection(miningState.ValueRO, target, transform.ValueRO.Position);
                            EmitEffect(effectBuffer, entity, tickInterval, toolKind, toolProfile, digConfig, transform.ValueRO.Position, effectDirection);
                            order.ValueRW.Status = MiningOrderStatus.Active;
                            safetyCounter++;

                            if (vessel.ValueRO.CurrentCargo >= vessel.ValueRO.CargoCapacity * 0.95f)
                            {
                                UpdateDecisionTrace(entity, DecisionReasonCode.MiningReturnFull, vessel.ValueRO.CarrierEntity, currentTick);
                                UpdateMiningDecisionTrace(entity, MiningDecisionReason.ReturnFull, vessel.ValueRO.CarrierEntity, 0f, currentTick);
                                order.ValueRW.Status = MiningOrderStatus.Completed;
                                SetPhase(ref miningState.ValueRW, MiningPhase.Detaching, detachDuration);
                                EnsureReturnTarget(ref miningState.ValueRW, vessel.ValueRO.CarrierEntity);
                                break;
                            }
                        }
                        break;

                    case MiningPhase.Detaching:
                        if (AdvancePhaseTimer(ref miningState.ValueRW, deltaTime))
                        {
                            miningState.ValueRW.Phase = MiningPhase.ReturnApproach;
                        }
                        break;

                    case MiningPhase.ReturnApproach:
                        if (target == Entity.Null)
                        {
                            UpdateMiningDecisionTrace(entity, MiningDecisionReason.NoTarget, Entity.Null, 0f, currentTick);
                            miningState.ValueRW.Phase = MiningPhase.Idle;
                            ResetDigState(ref miningState.ValueRW);
                            break;
                        }

                        if (IsTargetInRange(target, transform.ValueRO.Position, surfaceEpsilon))
                        {
                            SetPhase(ref miningState.ValueRW, MiningPhase.Docking, dockDuration);
                        }
                        else
                        {
                            var distance = ResolveTargetDistance(target, transform.ValueRO.Position);
                            UpdateMiningDecisionTrace(entity, MiningDecisionReason.DockingWait, target, distance, currentTick);
                        }
                        break;

                    case MiningPhase.Docking:
                        if (AdvancePhaseTimer(ref miningState.ValueRW, deltaTime))
                        {
                            miningState.ValueRW.Phase = MiningPhase.Idle;
                            miningState.ValueRW.ActiveTarget = Entity.Null;
                            ResetDigState(ref miningState.ValueRW);
                        }
                        break;
                }
            }

        }

        private static bool EnsureOrderResource(ref MiningOrder order, FixedString64Bytes fallbackResourceId)
        {
            if (order.ResourceId.IsEmpty)
            {
                order.ResourceId = fallbackResourceId;
            }

            if (order.ResourceId.IsEmpty)
            {
                order.Status = MiningOrderStatus.None;
                return false;
            }

            if (order.Status == MiningOrderStatus.None)
            {
                order.Status = MiningOrderStatus.Pending;
            }

            return true;
        }

        private bool TryResolveTarget(ref SystemState state, ref MiningOrder order, ref MiningState miningState, in float3 minerPosition)
        {
            if (order.TargetEntity != Entity.Null && _resourceStateLookup.HasComponent(order.TargetEntity))
            {
                var resourceState = _resourceStateLookup[order.TargetEntity];
                if (resourceState.UnitsRemaining > 0f && ResourceMatches(order.ResourceId, order.TargetEntity))
                {
                    miningState.ActiveTarget = order.TargetEntity;
                    return true;
                }
            }

            if (order.PreferredTarget != Entity.Null && _resourceStateLookup.HasComponent(order.PreferredTarget) && ResourceMatches(order.ResourceId, order.PreferredTarget))
            {
                var preferredState = _resourceStateLookup[order.PreferredTarget];
                if (preferredState.UnitsRemaining > 0f)
                {
                    order.TargetEntity = order.PreferredTarget;
                    miningState.ActiveTarget = order.PreferredTarget;
                    return true;
                }
            }

            var foundTarget = Entity.Null;
            var bestDistanceSq = float.MaxValue;

            foreach (var (resourceState, resourceId, resourceTransform, sourceEntity) in SystemAPI.Query<RefRO<ResourceSourceState>, RefRO<ResourceTypeId>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                if (resourceState.ValueRO.UnitsRemaining <= 0f)
                {
                    continue;
                }

                if (!ResourceMatches(order.ResourceId, resourceId.ValueRO.Value))
                {
                    continue;
                }

                var distanceSq = math.distancesq(minerPosition, resourceTransform.ValueRO.Position);
                if (distanceSq < bestDistanceSq)
                {
                    bestDistanceSq = distanceSq;
                    foundTarget = sourceEntity;
                }
            }

            if (foundTarget == Entity.Null)
            {
                miningState.Phase = MiningPhase.Idle;
                miningState.ActiveTarget = Entity.Null;
                return false;
            }

            order.TargetEntity = foundTarget;
            miningState.ActiveTarget = foundTarget;
            miningState.Phase = MiningPhase.ApproachTarget;
            return true;
        }

        private bool ResourceMatches(in FixedString64Bytes desiredId, Entity resourceEntity)
        {
            if (!_resourceTypeLookup.HasComponent(resourceEntity))
            {
                return false;
            }

            return ResourceMatches(desiredId, _resourceTypeLookup[resourceEntity].Value);
        }

        private static bool ResourceMatches(in FixedString64Bytes desiredId, in FixedString64Bytes candidateId)
        {
            return desiredId == candidateId;
        }

        private float GetTickInterval(ref MiningState miningState, float deltaTime)
        {
            if (miningState.TickInterval <= 0f)
            {
                miningState.TickInterval = math.max(deltaTime, 0.1f);
            }

            return miningState.TickInterval;
        }

        private static bool AdvancePhaseTimer(ref MiningState miningState, float deltaTime)
        {
            if (miningState.PhaseTimer <= 0f)
            {
                return true;
            }

            miningState.PhaseTimer = math.max(0f, miningState.PhaseTimer - deltaTime);
            return miningState.PhaseTimer <= 0f;
        }

        private static void SetPhase(ref MiningState miningState, MiningPhase phase, float duration)
        {
            miningState.Phase = phase;
            miningState.PhaseTimer = math.max(0f, duration);
        }

        private static void EnsureReturnTarget(ref MiningState miningState, Entity carrierEntity)
        {
            if (carrierEntity == Entity.Null)
            {
                miningState.ActiveTarget = Entity.Null;
                return;
            }

            if (miningState.ActiveTarget != carrierEntity)
            {
                miningState.ActiveTarget = carrierEntity;
            }
        }

        private bool IsTargetInRange(Entity target, float3 minerPosition, float surfaceEpsilon)
        {
            if (!_transformLookup.HasComponent(target))
            {
                return true;
            }

            var targetPosition = _transformLookup[target].Position;
            if (_asteroidVolumeLookup.HasComponent(target))
            {
                var volume = _asteroidVolumeLookup[target];
                var radius = math.max(0.5f, volume.Radius);
                var surfaceDelta = Space4XMiningLatchUtility.ResolveDistanceToSurface(targetPosition, radius, minerPosition);
                return surfaceDelta <= surfaceEpsilon;
            }

            if (_carrierLookup.HasComponent(target))
            {
                const float dockingDistance = 4.5f;
                var distanceSq = math.distancesq(minerPosition, targetPosition);
                return distanceSq <= dockingDistance * dockingDistance;
            }

            var genericDistanceSq = math.distancesq(minerPosition, targetPosition);
            const float requiredDistance = 3f;
            return genericDistanceSq <= requiredDistance * requiredDistance;
        }

        private float ApplyMiningTick(Entity miner, Entity target, float tickInterval, float yieldMultiplier, ref MiningVessel vessel, in FixedString64Bytes orderResourceId)
        {
            if (!_resourceStateLookup.HasComponent(target))
            {
                return 0f;
            }

            var resourceRef = _resourceStateLookup.GetRefRW(target);
            var available = resourceRef.ValueRO.UnitsRemaining;
            if (available <= 0f)
            {
                return 0f;
            }

            var config = _resourceConfigLookup.HasComponent(target)
                ? _resourceConfigLookup[target]
                : new ResourceSourceConfig { GatherRatePerWorker = 5f, MaxSimultaneousWorkers = 1, RespawnSeconds = 0f, Flags = 0 };

            vessel.CargoResourceType = ResolveResourceType(target, orderResourceId, vessel.CargoResourceType);
            var miningRate = math.max(0f, config.GatherRatePerWorker) * math.max(0f, vessel.MiningEfficiency);
            miningRate *= GetMiningSkillMultiplier(miner);
            miningRate *= math.max(0f, yieldMultiplier);
            var mined = math.min(miningRate * tickInterval, available);

            var remainingCapacity = math.max(0f, vessel.CargoCapacity - vessel.CurrentCargo);
            mined = math.min(mined, remainingCapacity);

            if (mined <= 0f)
            {
                return 0f;
            }

            resourceRef.ValueRW.UnitsRemaining = available - mined;
            if (_asteroidLookup.HasComponent(target))
            {
                var asteroid = _asteroidLookup[target];
                asteroid.ResourceAmount = math.max(0f, asteroid.ResourceAmount - mined);
                _asteroidLookup[target] = asteroid;
            }
            vessel.CurrentCargo += mined;
            return mined;
        }

        private static void ResetDigState(ref MiningState miningState)
        {
            miningState.DigHeadLocal = float3.zero;
            miningState.DigDirectionLocal = new float3(0f, 0f, 1f);
            miningState.DigVolumeEntity = Entity.Null;
            miningState.HasDigHead = 0;
            miningState.InDigRange = 0;
        }

        private static void ResetLatchState(ref MiningState miningState)
        {
            miningState.LatchTarget = Entity.Null;
            miningState.LatchRegionId = 0;
            miningState.LatchSurfacePoint = float3.zero;
            miningState.HasLatchPoint = 0;
            miningState.LatchSettleUntilTick = 0;
            miningState.LastLatchTelemetryTick = 0;
            miningState.InDigRange = 0;
        }

        private void ReleaseLatchReservation(Entity target, Entity miner, bool reserveRegions)
        {
            if (!reserveRegions || target == Entity.Null || !_latchReservationLookup.HasBuffer(target))
            {
                return;
            }

            var reservations = _latchReservationLookup[target];
            Space4XMiningLatchUtility.ReleaseReservation(miner, ref reservations);
        }

        private void EnsureLatchState(
            Entity miner,
            Entity target,
            ref MiningState miningState,
            in LocalTransform targetTransform,
            in Space4XAsteroidVolumeConfig volume,
            int regionCount,
            bool reserveRegions,
            uint currentTick)
        {
            if (miningState.HasLatchPoint != 0 && miningState.LatchTarget == target)
            {
                if (reserveRegions && _latchReservationLookup.HasBuffer(target))
                {
                    var reservations = _latchReservationLookup[target];
                    Space4XMiningLatchUtility.UpsertReservation(miner, miningState.LatchRegionId, currentTick, ref reservations);
                }
                return;
            }

            var radius = math.max(0.5f, volume.Radius);
            var clampedRegionCount = regionCount > 0 ? regionCount : Space4XMiningLatchUtility.DefaultLatchRegionCount;
            var regionId = 0;
            if (reserveRegions && _latchReservationLookup.HasBuffer(target))
            {
                var reservations = _latchReservationLookup[target];
                regionId = Space4XMiningLatchUtility.ResolveReservedLatchRegion(
                    miner,
                    target,
                    volume.Seed,
                    clampedRegionCount,
                    currentTick,
                    ref reservations,
                    _miningStateLookup);
            }
            else
            {
                regionId = Space4XMiningLatchUtility.ComputeLatchRegion(miner, target, volume.Seed, clampedRegionCount);
            }
            var surfacePoint = Space4XMiningLatchUtility.ComputeSurfaceLatchPoint(targetTransform.Position, radius, regionId, volume.Seed);

            miningState.LatchTarget = target;
            miningState.LatchRegionId = regionId;
            miningState.LatchSurfacePoint = surfacePoint;
            miningState.HasLatchPoint = 1;
            miningState.LastLatchTelemetryTick = 0;
        }

        private bool TryResolveAsteroidLatchMetrics(
            Entity target,
            float3 minerPosition,
            quaternion minerRotation,
            in MiningState miningState,
            float alignDotThreshold,
            out float distanceToSurface,
            out bool aligned)
        {
            distanceToSurface = 0f;
            aligned = true;

            if (target == Entity.Null || !_transformLookup.HasComponent(target) || !_asteroidVolumeLookup.HasComponent(target))
            {
                return false;
            }

            var targetTransform = _transformLookup[target];
            var volume = _asteroidVolumeLookup[target];
            var radius = math.max(0.5f, volume.Radius);
            distanceToSurface = Space4XMiningLatchUtility.ResolveDistanceToSurface(targetTransform.Position, radius, minerPosition);

            var surfacePoint = miningState.HasLatchPoint != 0 && miningState.LatchTarget == target
                ? miningState.LatchSurfacePoint
                : targetTransform.Position + math.normalizesafe(minerPosition - targetTransform.Position, new float3(0f, 0f, 1f)) * radius;
            var toSurface = surfacePoint - minerPosition;
            var forward = math.mul(minerRotation, new float3(0f, 0f, 1f));
            var toSurfaceDir = math.normalizesafe(toSurface, forward);
            aligned = math.dot(forward, toSurfaceDir) >= alignDotThreshold;
            return true;
        }

        private void StickToAsteroidSurface(Entity target, float deltaTime, ref LocalTransform transform, in MiningState miningState)
        {
            if (!_transformLookup.HasComponent(target) || !_asteroidVolumeLookup.HasComponent(target))
            {
                return;
            }

            var targetTransform = _transformLookup[target];
            var volume = _asteroidVolumeLookup[target];
            var radius = math.max(0.5f, volume.Radius);
            float3 direction;
            if (miningState.HasLatchPoint != 0 && miningState.LatchTarget == target)
            {
                direction = math.normalizesafe(miningState.LatchSurfacePoint - targetTransform.Position, new float3(0f, 0f, 1f));
            }
            else
            {
                var toSelf = transform.Position - targetTransform.Position;
                direction = math.normalizesafe(toSelf, new float3(0f, 0f, 1f));
            }
            var standoff = Space4XMiningLatchUtility.MinerAsteroidStandoff;
            var desired = targetTransform.Position + direction * (radius + standoff);
            var followSpeed = math.max(0.01f, deltaTime * 6f);
            transform.Position = math.lerp(transform.Position, desired, math.saturate(followSpeed));
        }

        private void EnsureDigHead(
            ref MiningState miningState,
            Entity target,
            in LocalTransform minerTransform,
            in LocalTransform targetTransform,
            in Space4XAsteroidVolumeConfig volumeConfig)
        {
            var radius = math.max(0.01f, volumeConfig.Radius);
            var localMiner = WorldToLocal(targetTransform, minerTransform.Position);
            var outward = math.normalizesafe(localMiner, new float3(0f, 0f, 1f));

            miningState.DigHeadLocal = outward * radius;
            miningState.DigDirectionLocal = -outward;
            miningState.DigVolumeEntity = target;
            miningState.HasDigHead = 1;
        }

        private Space4XAsteroidVolumeConfig ResolveVolumeConfig(Entity target)
        {
            if (_asteroidVolumeLookup.HasComponent(target))
            {
                var config = _asteroidVolumeLookup[target];
                if (config.Radius <= 0f)
                {
                    config.Radius = Space4XAsteroidVolumeConfig.Default.Radius;
                }
                return config;
            }

            return Space4XAsteroidVolumeConfig.Default;
        }

        private static float ResolveYieldMultiplier(
            float distance,
            float radius,
            in Space4XAsteroidVolumeConfig volumeConfig,
            in Space4XMiningDigConfig digConfig)
        {
            if (radius <= 0f)
            {
                return 1f;
            }

            var coreRadius = radius * math.saturate(volumeConfig.CoreRadiusRatio);
            var mantleRadius = radius * math.saturate(volumeConfig.MantleRadiusRatio);
            if (mantleRadius < coreRadius)
            {
                mantleRadius = coreRadius;
            }

            float layerMultiplier;
            if (distance <= coreRadius)
            {
                layerMultiplier = digConfig.CoreYieldMultiplier;
            }
            else if (distance <= mantleRadius)
            {
                layerMultiplier = digConfig.MantleYieldMultiplier;
            }
            else
            {
                layerMultiplier = digConfig.CrustYieldMultiplier;
            }

            var depthRatio = math.saturate(1f - (distance / radius));
            var exponent = math.max(0.01f, volumeConfig.OreGradeExponent);
            var oreNorm = math.pow(depthRatio, exponent) * (volumeConfig.CoreOreGrade / 255f);
            var oreMultiplier = 1f + oreNorm * math.max(0f, digConfig.OreGradeWeight);

            return math.max(0.01f, layerMultiplier * oreMultiplier);
        }

        private static float ResolveStepLength(float mined, float minStep, float maxStepLength, float unitsPerMeter, float maxStep)
        {
            if (mined <= 0f || maxStep <= 0f)
            {
                return 0f;
            }

            unitsPerMeter = math.max(0.001f, unitsPerMeter);
            minStep = math.max(0f, minStep);
            maxStepLength = math.max(minStep, maxStepLength);
            var step = mined / unitsPerMeter;
            step = math.clamp(step, minStep, maxStepLength);
            return math.min(step, maxStep);
        }

        private Space4XMiningToolProfile ResolveToolProfile(Entity entity)
        {
            if (_toolProfileLookup.HasComponent(entity))
            {
                return _toolProfileLookup[entity];
            }

            return Space4XMiningToolProfile.Default;
        }

        private static TerrainModificationShape ResolveToolShape(in Space4XMiningToolProfile profile)
        {
            if (profile.HasShapeOverride != 0)
            {
                return profile.Shape;
            }

            return profile.ToolKind == TerrainModificationToolKind.Microwave
                ? TerrainModificationShape.Brush
                : TerrainModificationShape.Tunnel;
        }

        private static float ResolveToolRadius(
            TerrainModificationToolKind toolKind,
            in Space4XMiningToolProfile profile,
            in Space4XMiningDigConfig digConfig)
        {
            if (profile.RadiusOverride > 0f)
            {
                return profile.RadiusOverride;
            }

            var radius = toolKind switch
            {
                TerrainModificationToolKind.Laser => math.max(0.1f, digConfig.LaserRadius),
                TerrainModificationToolKind.Microwave => math.max(0.1f, digConfig.MicrowaveRadius),
                _ => math.max(0.1f, digConfig.DrillRadius)
            };

            var multiplier = profile.RadiusMultiplier <= 0f ? 1f : profile.RadiusMultiplier;
            return radius * multiplier;
        }

        private static float ResolveToolStepLength(
            TerrainModificationToolKind toolKind,
            in Space4XMiningToolProfile profile,
            float mined,
            in Space4XMiningDigConfig digConfig,
            float maxStep)
        {
            if (profile.StepLengthOverride > 0f)
            {
                return profile.StepLengthOverride;
            }

            var multiplier = profile.StepLengthMultiplier <= 0f ? 1f : profile.StepLengthMultiplier;

            if (toolKind == TerrainModificationToolKind.Laser)
            {
                var baseLength = math.min(math.max(0f, digConfig.LaserStepLength), maxStep);
                return baseLength * multiplier;
            }

            var minStep = profile.MinStepLengthOverride > 0f ? profile.MinStepLengthOverride : digConfig.MinStepLength;
            var maxStepLength = profile.MaxStepLengthOverride > 0f ? profile.MaxStepLengthOverride : digConfig.MaxStepLength;
            var unitsPerMeter = profile.DigUnitsPerMeterOverride > 0f ? profile.DigUnitsPerMeterOverride : digConfig.DigUnitsPerMeter;

            var baseStepLength = ResolveStepLength(mined, minStep, maxStepLength, unitsPerMeter, maxStep);
            return baseStepLength * multiplier;
        }

        private static float ResolveToolYieldMultiplier(
            TerrainModificationToolKind toolKind,
            in Space4XMiningToolProfile profile,
            in Space4XMiningDigConfig digConfig)
        {
            var baseValue = toolKind switch
            {
                TerrainModificationToolKind.Laser => math.max(0f, digConfig.LaserYieldMultiplier),
                TerrainModificationToolKind.Microwave => math.max(0f, digConfig.MicrowaveYieldMultiplier),
                _ => 1f
            };

            var multiplier = profile.YieldMultiplier <= 0f ? 1f : profile.YieldMultiplier;
            return baseValue * multiplier;
        }

        private static float ResolveToolHeatDelta(
            TerrainModificationToolKind toolKind,
            in Space4XMiningToolProfile profile,
            in Space4XMiningDigConfig digConfig)
        {
            var baseValue = toolKind switch
            {
                TerrainModificationToolKind.Laser => math.max(0f, digConfig.LaserHeatDelta),
                TerrainModificationToolKind.Microwave => math.max(0f, digConfig.MicrowaveHeatDelta),
                _ => 0f
            };

            var multiplier = profile.HeatDeltaMultiplier <= 0f ? 1f : profile.HeatDeltaMultiplier;
            return baseValue * multiplier;
        }

        private static float ResolveToolInstabilityDelta(
            TerrainModificationToolKind toolKind,
            in Space4XMiningToolProfile profile,
            in Space4XMiningDigConfig digConfig)
        {
            var baseValue = toolKind switch
            {
                TerrainModificationToolKind.Laser => math.max(0f, digConfig.LaserInstabilityDelta),
                TerrainModificationToolKind.Microwave => math.max(0f, digConfig.MicrowaveInstabilityDelta),
                _ => 0f
            };

            var multiplier = profile.InstabilityDeltaMultiplier <= 0f ? 1f : profile.InstabilityDeltaMultiplier;
            return baseValue * multiplier;
        }

        private static byte ResolveToolDamageDelta(
            TerrainModificationToolKind toolKind,
            in Space4XMiningToolProfile profile,
            in Space4XMiningDigConfig digConfig)
        {
            if (profile.DamageDeltaOverride > 0)
            {
                return profile.DamageDeltaOverride;
            }

            return toolKind == TerrainModificationToolKind.Microwave
                ? (digConfig.MicrowaveDamageDelta == 0 ? (byte)8 : digConfig.MicrowaveDamageDelta)
                : (byte)0;
        }

        private static byte ResolveToolDamageThreshold(
            TerrainModificationToolKind toolKind,
            in Space4XMiningToolProfile profile,
            in Space4XMiningDigConfig digConfig)
        {
            if (profile.DamageThresholdOverride > 0)
            {
                return profile.DamageThresholdOverride;
            }

            return toolKind == TerrainModificationToolKind.Microwave
                ? (digConfig.MicrowaveDamageThreshold == 0 ? (byte)255 : digConfig.MicrowaveDamageThreshold)
                : (byte)0;
        }

        private static float3 WorldToLocal(in LocalTransform transform, float3 worldPosition)
        {
            var local = worldPosition - transform.Position;
            local = math.rotate(math.inverse(transform.Rotation), local);
            var scale = math.max(0.0001f, transform.Scale);
            return local / scale;
        }

        private static void UpdateYield(ref MiningYield yield, in FixedString64Bytes resourceId, float mined)
        {
            if (yield.ResourceId.IsEmpty)
            {
                yield.ResourceId = resourceId;
            }

            yield.PendingAmount += mined;
            if (yield.SpawnThreshold > 0f && yield.PendingAmount >= yield.SpawnThreshold)
            {
                yield.SpawnReady = 1;
            }
        }

        private DynamicBuffer<PlayEffectRequest> GetEffectBuffer(ref SystemState state)
        {
            if (_effectStreamEntity == Entity.Null)
            {
                return default;
            }

            if (!_effectRequestLookup.HasBuffer(_effectStreamEntity))
            {
                return default;
            }

            return _effectRequestLookup[_effectStreamEntity];
        }

        private void EmitEffect(
            DynamicBuffer<PlayEffectRequest> effectBuffer,
            Entity attachTo,
            float tickInterval,
            TerrainModificationToolKind toolKind,
            in Space4XMiningToolProfile profile,
            in Space4XMiningDigConfig digConfig,
            float3 position,
            float3 direction)
        {
            if (!effectBuffer.IsCreated)
            {
                return;
            }

            var effectId = toolKind switch
            {
                TerrainModificationToolKind.Laser => MiningLaserEffectId,
                TerrainModificationToolKind.Microwave => MiningMicrowaveEffectId,
                _ => MiningSparksEffectId
            };

            effectBuffer.Add(new PlayEffectRequest
            {
                EffectId = effectId,
                AttachTo = attachTo,
                Position = position,
                Direction = direction,
                Lifetime = math.max(0.1f, tickInterval),
                Intensity = ResolveToolEffectIntensity(toolKind, profile, digConfig, tickInterval)
            });
        }

        private float3 ResolveEffectDirection(in MiningState miningState, Entity target, float3 minerPosition)
        {
            if (miningState.HasDigHead != 0)
            {
                var localDirection = math.normalizesafe(miningState.DigDirectionLocal, new float3(0f, 0f, 1f));
                if (_transformLookup.HasComponent(target))
                {
                    var targetTransform = _transformLookup[target];
                    return math.normalizesafe(math.rotate(targetTransform.Rotation, localDirection), localDirection);
                }

                return localDirection;
            }

            if (_transformLookup.HasComponent(target))
            {
                var targetPosition = _transformLookup[target].Position;
                return math.normalizesafe(targetPosition - minerPosition, new float3(0f, 0f, 1f));
            }

            return new float3(0f, 0f, 1f);
        }

        private static float ResolveToolEffectIntensity(
            TerrainModificationToolKind toolKind,
            in Space4XMiningToolProfile profile,
            in Space4XMiningDigConfig digConfig,
            float tickInterval)
        {
            var rate = 1f / math.max(0.05f, tickInterval);
            return toolKind switch
            {
                TerrainModificationToolKind.Laser => math.saturate(rate * 0.02f *
                                                                  (1f + ResolveToolHeatDelta(toolKind, profile, digConfig)) *
                                                                  (1f + math.max(0f, 1f - ResolveToolYieldMultiplier(toolKind, profile, digConfig)))),
                TerrainModificationToolKind.Microwave => math.saturate((ResolveToolDamageDelta(toolKind, profile, digConfig) /
                                                                        math.max(1f, ResolveToolDamageThreshold(toolKind, profile, digConfig))) * 1.25f +
                                                                       ResolveToolHeatDelta(toolKind, profile, digConfig) * 0.1f),
                _ => math.saturate(0.2f + ResolveToolRadius(toolKind, profile, digConfig) * 0.06f)
            };
        }

        private void LogMiningCommand(bool hasCommandLog, DynamicBuffer<MiningCommandLogEntry> commandLog, uint tick, Entity source, Entity miner, ResourceType resourceType, float amount, in float3 fallbackPosition)
        {
            if (!hasCommandLog || amount <= 0f)
            {
                return;
            }

            var position = fallbackPosition;
            if (_transformLookup.HasComponent(source))
            {
                position = _transformLookup[source].Position;
            }

            commandLog.Add(new Space4X.Registry.MiningCommandLogEntry
            {
                Tick = tick,
                CommandType = MiningCommandType.Gather,
                SourceEntity = source,
                TargetEntity = miner,
                ResourceType = resourceType,
                Amount = amount,
                Position = position
            });
        }

        private ResourceType ResolveResourceType(Entity resourceEntity, in FixedString64Bytes orderResourceId, ResourceType fallback)
        {
            if (_resourceTypeLookup.HasComponent(resourceEntity))
            {
                return Space4XMiningResourceUtility.MapToResourceType(_resourceTypeLookup[resourceEntity], fallback);
            }

            if (!orderResourceId.IsEmpty)
            {
                return Space4XMiningResourceUtility.MapToResourceType(orderResourceId, fallback);
            }

            return fallback;
        }

        private void EnsureEffectStream(ref SystemState state)
        {
            if (_effectStreamEntity != Entity.Null && state.EntityManager.Exists(_effectStreamEntity))
            {
                return;
            }

            if (SystemAPI.TryGetSingletonEntity<Space4XEffectRequestStream>(out _effectStreamEntity))
            {
                if (!state.EntityManager.HasBuffer<PlayEffectRequest>(_effectStreamEntity))
                {
                    state.EntityManager.AddBuffer<PlayEffectRequest>(_effectStreamEntity);
                }

                return;
            }

            _effectStreamEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponent<Space4XEffectRequestStream>(_effectStreamEntity);
            state.EntityManager.AddBuffer<PlayEffectRequest>(_effectStreamEntity);
        }

        private BehaviorDisposition ResolveBehaviorDisposition(Entity miner)
        {
            var profileEntity = ResolveProfileEntity(miner);
            if (_behaviorDispositionLookup.HasComponent(profileEntity))
            {
                return _behaviorDispositionLookup[profileEntity];
            }

            if (_behaviorDispositionLookup.HasComponent(miner))
            {
                return _behaviorDispositionLookup[miner];
            }

            return BehaviorDisposition.Default;
        }

        private static float ResolvePhaseDuration(float baseDuration, in BehaviorDisposition disposition)
        {
            var caution = disposition.Caution;
            var patience = disposition.Patience;
            var cautionMultiplier = math.lerp(0.85f, 1.35f, caution);
            var patienceMultiplier = math.lerp(0.9f, 1.2f, patience);
            return math.max(0.05f, baseDuration * cautionMultiplier * patienceMultiplier);
        }

        private static float ResolveCargoReturnRatio(in BehaviorDisposition disposition)
        {
            var caution = disposition.Caution;
            var risk = disposition.RiskTolerance;
            var patience = disposition.Patience;
            var ratio = 0.9f +
                        (risk - 0.5f) * 0.2f -
                        (caution - 0.5f) * 0.2f +
                        (patience - 0.5f) * 0.1f;
            return math.clamp(ratio, 0.6f, 0.98f);
        }

        private float GetMiningSkillMultiplier(Entity miner)
        {
            if (!_crewSkillsLookup.HasComponent(miner))
            {
                return 1f;
            }

            var skills = _crewSkillsLookup[miner];
            var skill = math.saturate(skills.MiningSkill);
            // Up to +50% output at max skill.
            return 1f + 0.5f * skill;
        }

        private Entity ResolveProfileEntity(Entity miner)
        {
            if (TryResolveController(miner, AgencyDomain.Work, out var controller))
            {
                return controller != Entity.Null ? controller : miner;
            }

            if (_pilotLinkLookup.HasComponent(miner))
            {
                var link = _pilotLinkLookup[miner];
                if (link.Pilot != Entity.Null)
                {
                    return link.Pilot;
                }
            }

            return miner;
        }

        private void UpdateDecisionTrace(Entity entity, DecisionReasonCode reason, Entity target, uint tick)
        {
            if (!_decisionTraceLookup.HasComponent(entity))
            {
                return;
            }

            var trace = _decisionTraceLookup[entity];
            if (trace.ReasonCode == reason && trace.ChosenTarget == target)
            {
                return;
            }

            trace.ReasonCode = reason;
            trace.ChosenTarget = target;
            trace.Score = 1f;
            trace.BlockerEntity = Entity.Null;
            trace.SinceTick = tick;
            _decisionTraceLookup[entity] = trace;
        }

        private void UpdateMiningDecisionTrace(
            Entity entity,
            MiningDecisionReason reason,
            Entity target,
            float distance,
            uint tick,
            float rangeThreshold = 0f,
            float standoff = 0f,
            bool aligned = true)
        {
            if (!_miningDecisionLookup.HasComponent(entity))
            {
                return;
            }

            var trace = _miningDecisionLookup[entity];
            if (trace.Reason == reason && trace.Target == target &&
                math.abs(trace.DistanceToTarget - distance) < 0.01f &&
                math.abs(trace.RangeThreshold - rangeThreshold) < 0.01f &&
                math.abs(trace.Standoff - standoff) < 0.01f &&
                trace.Aligned == (byte)(aligned ? 1 : 0))
            {
                return;
            }

            trace.Reason = reason;
            trace.Target = target;
            trace.DistanceToTarget = distance;
            trace.RangeThreshold = rangeThreshold;
            trace.Standoff = standoff;
            trace.Aligned = (byte)(aligned ? 1 : 0);
            trace.Tick = tick;
            _miningDecisionLookup[entity] = trace;
        }

        private float ResolveTargetDistance(Entity target, float3 position)
        {
            if (target == Entity.Null || !_transformLookup.HasComponent(target))
            {
                return 0f;
            }

            return math.distance(position, _transformLookup[target].Position);
        }

        private static MiningDecisionReason ResolveLatchBlockReason(
            bool hasAsteroidLatch,
            float distanceToSurface,
            float rangeThreshold,
            bool latchAligned)
        {
            if (hasAsteroidLatch && distanceToSurface <= rangeThreshold && !latchAligned)
            {
                return MiningDecisionReason.NotAligned;
            }

            return MiningDecisionReason.NotInRange;
        }

        private bool TryResolveController(Entity miner, AgencyDomain domain, out Entity controller)
        {
            controller = Entity.Null;
            if (!_resolvedControlLookup.HasBuffer(miner))
            {
                return false;
            }

            var resolved = _resolvedControlLookup[miner];
            for (int i = 0; i < resolved.Length; i++)
            {
                if (resolved[i].Domain == domain)
                {
                    controller = resolved[i].Controller;
                    return true;
                }
            }

            return false;
        }
    }
}
