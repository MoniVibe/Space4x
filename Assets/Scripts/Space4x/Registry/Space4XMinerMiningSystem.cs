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
        private BufferLookup<PlayEffectRequest> _effectRequestLookup;
        private ComponentLookup<CrewSkills> _crewSkillsLookup;
        private ComponentLookup<VesselPilotLink> _pilotLinkLookup;
        private ComponentLookup<BehaviorDisposition> _behaviorDispositionLookup;
        private BufferLookup<ResolvedControl> _resolvedControlLookup;
        private ComponentLookup<Space4XMiningToolProfile> _toolProfileLookup;
        private Entity _effectStreamEntity;
        private static readonly FixedString64Bytes MiningSparksEffectId = CreateMiningEffectId();
        private static readonly FixedString64Bytes MiningLaserEffectId = CreateMiningLaserEffectId();
        private static readonly FixedString64Bytes MiningMicrowaveEffectId = CreateMiningMicrowaveEffectId();
        private const float UndockDuration = 1.5f;
        private const float LatchDuration = 0.9f;
        private const float DetachDuration = 0.8f;
        private const float DockDuration = 1.2f;

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
            _effectRequestLookup = state.GetBufferLookup<PlayEffectRequest>();
            _crewSkillsLookup = state.GetComponentLookup<CrewSkills>(true);
            _pilotLinkLookup = state.GetComponentLookup<VesselPilotLink>(true);
            _behaviorDispositionLookup = state.GetComponentLookup<BehaviorDisposition>(true);
            _resolvedControlLookup = state.GetBufferLookup<ResolvedControl>(true);
            _toolProfileLookup = state.GetComponentLookup<Space4XMiningToolProfile>(true);

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
            _toolProfileLookup.Update(ref state);
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

            var hasModificationQueue = SystemAPI.TryGetSingletonEntity<TerrainModificationQueue>(out var modificationQueueEntity);
            DynamicBuffer<TerrainModificationRequest> modificationBuffer = default;
            if (hasModificationQueue)
            {
                modificationBuffer = SystemAPI.GetBuffer<TerrainModificationRequest>(modificationQueueEntity);
            }
            var hasCommandLog = SystemAPI.TryGetSingletonBuffer<MiningCommandLogEntry>(out var commandLog);
            var currentTick = timeState.Tick;

            foreach (var (order, miningState, vessel, yield, transform, entity) in SystemAPI.Query<RefRW<MiningOrder>, RefRW<MiningState>, RefRW<MiningVessel>, RefRW<MiningYield>, RefRO<LocalTransform>>()
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
                var vesselData = vessel.ValueRO;
                var toolKind = ResolveToolKind(entity);
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
                            ResetDigState(ref miningState.ValueRW);
                        }
                        break;

                    case MiningPhase.Undocking:
                        if (AdvancePhaseTimer(ref miningState.ValueRW, deltaTime))
                        {
                            miningState.ValueRW.Phase = MiningPhase.ApproachTarget;
                        }
                        break;

                    case MiningPhase.ApproachTarget:
                        if (target == Entity.Null)
                        {
                            miningState.ValueRW.Phase = MiningPhase.Idle;
                            ResetDigState(ref miningState.ValueRW);
                            break;
                        }

                        if (IsTargetInRange(target, transform.ValueRO.Position))
                        {
                            SetPhase(ref miningState.ValueRW, MiningPhase.Latching, latchDuration);
                        }
                        break;

                    case MiningPhase.Latching:
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
                        if (target == Entity.Null || !IsTargetInRange(target, transform.ValueRO.Position))
                        {
                            miningState.ValueRW.Phase = MiningPhase.ApproachTarget;
                            ResetDigState(ref miningState.ValueRW);
                            break;
                        }

                        if (isCargoFull || isAsteroidEmpty)
                        {
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

                            yieldMultiplier *= ResolveToolYieldMultiplier(toolKind, digConfig);
                            var mined = ApplyMiningTick(entity, target, tickInterval, yieldMultiplier, ref vessel.ValueRW, order.ValueRO.ResourceId);
                            if (mined <= 0f)
                            {
                                break;
                            }

                            if (miningState.ValueRW.HasDigHead != 0)
                            {
                                var distance = math.length(digHead);
                                var stepLength = ResolveToolStepLength(toolKind, mined, digConfig, distance);
                                if (stepLength > 0f)
                                {
                                    var digStart = digHead;
                                    var digEnd = digStart + digDirection * stepLength;

                                    if (hasModificationQueue && modificationBuffer.IsCreated)
                                    {
                                        modificationBuffer.Add(new TerrainModificationRequest
                                        {
                                            Kind = TerrainModificationKind.Dig,
                                            Shape = toolKind == TerrainModificationToolKind.Microwave ? TerrainModificationShape.Brush : TerrainModificationShape.Tunnel,
                                            ToolKind = toolKind,
                                            Start = digStart,
                                            End = toolKind == TerrainModificationToolKind.Microwave ? digStart : digEnd,
                                            Radius = ResolveToolRadius(toolKind, digConfig),
                                            Depth = 0f,
                                            MaterialId = 0,
                                            DamageDelta = ResolveToolDamageDelta(toolKind, digConfig),
                                            DamageThreshold = ResolveToolDamageThreshold(toolKind, digConfig),
                                            YieldMultiplier = ResolveToolYieldMultiplier(toolKind, digConfig),
                                            HeatDelta = ResolveToolHeatDelta(toolKind, digConfig),
                                            InstabilityDelta = ResolveToolInstabilityDelta(toolKind, digConfig),
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
                            EmitEffect(effectBuffer, entity, tickInterval, toolKind, digConfig, transform.ValueRO.Position, effectDirection);
                            order.ValueRW.Status = MiningOrderStatus.Active;
                            safetyCounter++;

                            if (vessel.ValueRO.CurrentCargo >= vessel.ValueRO.CargoCapacity * 0.95f)
                            {
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
                            miningState.ValueRW.Phase = MiningPhase.Idle;
                            ResetDigState(ref miningState.ValueRW);
                            break;
                        }

                        if (IsTargetInRange(target, transform.ValueRO.Position))
                        {
                            SetPhase(ref miningState.ValueRW, MiningPhase.Docking, dockDuration);
                        }
                        break;

                    case MiningPhase.Docking:
                        AdvancePhaseTimer(ref miningState.ValueRW, deltaTime);
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

        private bool IsTargetInRange(Entity target, float3 minerPosition)
        {
            if (!_transformLookup.HasComponent(target))
            {
                return true;
            }

            var targetPosition = _transformLookup[target].Position;
            var distanceSq = math.distancesq(minerPosition, targetPosition);
            const float requiredDistance = 3f;
            return distanceSq <= requiredDistance * requiredDistance;
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

        private static float ResolveStepLength(float mined, in Space4XMiningDigConfig digConfig, float maxStep)
        {
            if (mined <= 0f || maxStep <= 0f)
            {
                return 0f;
            }

            var unitsPerMeter = math.max(0.001f, digConfig.DigUnitsPerMeter);
            var minStep = math.max(0f, digConfig.MinStepLength);
            var maxStepLength = math.max(minStep, digConfig.MaxStepLength);
            var step = mined / unitsPerMeter;
            step = math.clamp(step, minStep, maxStepLength);
            return math.min(step, maxStep);
        }

        private TerrainModificationToolKind ResolveToolKind(Entity entity)
        {
            if (_toolProfileLookup.HasComponent(entity))
            {
                return _toolProfileLookup[entity].ToolKind;
            }

            return TerrainModificationToolKind.Drill;
        }

        private static float ResolveToolRadius(TerrainModificationToolKind toolKind, in Space4XMiningDigConfig digConfig)
        {
            return toolKind switch
            {
                TerrainModificationToolKind.Laser => math.max(0.1f, digConfig.LaserRadius),
                TerrainModificationToolKind.Microwave => math.max(0.1f, digConfig.MicrowaveRadius),
                _ => math.max(0.1f, digConfig.DrillRadius)
            };
        }

        private static float ResolveToolStepLength(TerrainModificationToolKind toolKind, float mined, in Space4XMiningDigConfig digConfig, float maxStep)
        {
            if (toolKind == TerrainModificationToolKind.Laser)
            {
                return math.min(math.max(0f, digConfig.LaserStepLength), maxStep);
            }

            return ResolveStepLength(mined, digConfig, maxStep);
        }

        private static float ResolveToolYieldMultiplier(TerrainModificationToolKind toolKind, in Space4XMiningDigConfig digConfig)
        {
            return toolKind switch
            {
                TerrainModificationToolKind.Laser => math.max(0f, digConfig.LaserYieldMultiplier),
                TerrainModificationToolKind.Microwave => math.max(0f, digConfig.MicrowaveYieldMultiplier),
                _ => 1f
            };
        }

        private static float ResolveToolHeatDelta(TerrainModificationToolKind toolKind, in Space4XMiningDigConfig digConfig)
        {
            return toolKind switch
            {
                TerrainModificationToolKind.Laser => math.max(0f, digConfig.LaserHeatDelta),
                TerrainModificationToolKind.Microwave => math.max(0f, digConfig.MicrowaveHeatDelta),
                _ => 0f
            };
        }

        private static float ResolveToolInstabilityDelta(TerrainModificationToolKind toolKind, in Space4XMiningDigConfig digConfig)
        {
            return toolKind switch
            {
                TerrainModificationToolKind.Laser => math.max(0f, digConfig.LaserInstabilityDelta),
                TerrainModificationToolKind.Microwave => math.max(0f, digConfig.MicrowaveInstabilityDelta),
                _ => 0f
            };
        }

        private static byte ResolveToolDamageDelta(TerrainModificationToolKind toolKind, in Space4XMiningDigConfig digConfig)
        {
            return toolKind == TerrainModificationToolKind.Microwave
                ? digConfig.MicrowaveDamageDelta
                : (byte)0;
        }

        private static byte ResolveToolDamageThreshold(TerrainModificationToolKind toolKind, in Space4XMiningDigConfig digConfig)
        {
            return toolKind == TerrainModificationToolKind.Microwave
                ? digConfig.MicrowaveDamageThreshold
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
                Intensity = ResolveToolEffectIntensity(toolKind, digConfig, tickInterval)
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

        private static float ResolveToolEffectIntensity(TerrainModificationToolKind toolKind, in Space4XMiningDigConfig digConfig, float tickInterval)
        {
            var rate = 1f / math.max(0.05f, tickInterval);
            return toolKind switch
            {
                TerrainModificationToolKind.Laser => math.saturate(rate * 0.02f * (1f + digConfig.LaserHeatDelta) *
                                                                  (1f + math.max(0f, 1f - digConfig.LaserYieldMultiplier))),
                TerrainModificationToolKind.Microwave => math.saturate((digConfig.MicrowaveDamageDelta / math.max(1f, digConfig.MicrowaveDamageThreshold)) * 1.25f +
                                                                       digConfig.MicrowaveHeatDelta * 0.1f),
                _ => math.saturate(0.2f + digConfig.DrillRadius * 0.06f)
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
