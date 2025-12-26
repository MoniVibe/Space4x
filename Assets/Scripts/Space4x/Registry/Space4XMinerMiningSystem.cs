using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Profile;
using PureDOTS.Systems;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Registry
{
    using Debug = UnityEngine.Debug;

    
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
        private ComponentLookup<LocalTransform> _transformLookup;
        private BufferLookup<PlayEffectRequest> _effectRequestLookup;
        private ComponentLookup<CrewSkills> _crewSkillsLookup;
        private ComponentLookup<VesselPilotLink> _pilotLinkLookup;
        private Entity _effectStreamEntity;
        private static readonly FixedString64Bytes MiningSparksEffectId = CreateMiningEffectId();
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

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<MiningOrder>();

            _resourceStateLookup = state.GetComponentLookup<ResourceSourceState>(false);
            _resourceConfigLookup = state.GetComponentLookup<ResourceSourceConfig>(true);
            _resourceTypeLookup = state.GetComponentLookup<ResourceTypeId>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _effectRequestLookup = state.GetBufferLookup<PlayEffectRequest>();
            _crewSkillsLookup = state.GetComponentLookup<CrewSkills>(true);
            _pilotLinkLookup = state.GetComponentLookup<VesselPilotLink>(true);

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
            _transformLookup.Update(ref state);
            _effectRequestLookup.Update(ref state);
            _crewSkillsLookup.Update(ref state);
            _pilotLinkLookup.Update(ref state);
            EnsureEffectStream(ref state);

            var canEmitActions = SystemAPI.TryGetSingletonEntity<ProfileActionEventStream>(out var actionStreamEntity) &&
                                 SystemAPI.TryGetSingleton<ProfileActionEventStreamConfig>(out var actionStreamConfig);
            DynamicBuffer<ProfileActionEvent> actionBuffer = default;
            RefRW<ProfileActionEventStream> actionStream = default;
            if (canEmitActions)
            {
                actionBuffer = SystemAPI.GetBuffer<ProfileActionEvent>(actionStreamEntity);
                actionStream = SystemAPI.GetComponentRW<ProfileActionEventStream>(actionStreamEntity);
            }

            var effectBuffer = GetEffectBuffer(ref state);
            var hasCommandLog = SystemAPI.TryGetSingletonBuffer<MiningCommandLogEntry>(out var commandLog);
            var currentTick = timeState.Tick;

            // Debug instrumentation: count miners
            int minerCount = 0;
            foreach (var (order, miningState, vessel, yield, transform, entity) in SystemAPI.Query<RefRW<MiningOrder>, RefRW<MiningState>, RefRW<MiningVessel>, RefRW<MiningYield>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                minerCount++;

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
                var isCargoFull = vesselData.CurrentCargo >= vesselData.CargoCapacity * 0.95f;
                var isAsteroidEmpty = _resourceStateLookup.HasComponent(target) &&
                                      _resourceStateLookup[target].UnitsRemaining <= 0f;

                switch (phase)
                {
                    case MiningPhase.Idle:
                        if (miningState.ValueRO.ActiveTarget != Entity.Null)
                        {
                            SetPhase(ref miningState.ValueRW, MiningPhase.Undocking, UndockDuration);
                            order.ValueRW.Status = MiningOrderStatus.Active;
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
                            break;
                        }

                        if (IsTargetInRange(target, transform.ValueRO.Position))
                        {
                            SetPhase(ref miningState.ValueRW, MiningPhase.Latching, LatchDuration);
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
                            break;
                        }

                        if (isCargoFull || isAsteroidEmpty)
                        {
                            order.ValueRW.Status = MiningOrderStatus.Completed;
                            SetPhase(ref miningState.ValueRW, MiningPhase.Detaching, DetachDuration);
                            EnsureReturnTarget(ref miningState.ValueRW, vessel.ValueRO.CarrierEntity);
                            break;
                        }

                        var tickInterval = GetTickInterval(ref miningState.ValueRW, deltaTime);
                        miningState.ValueRW.MiningTimer += deltaTime;

                        var safetyCounter = 0;
                        while (miningState.ValueRW.MiningTimer >= tickInterval && safetyCounter < 4)
                        {
                            miningState.ValueRW.MiningTimer -= tickInterval;
                            var mined = ApplyMiningTick(entity, target, tickInterval, ref vessel.ValueRW, order.ValueRO.ResourceId);
                            if (mined <= 0f)
                            {
                                break;
                            }

                            UpdateYield(ref yield.ValueRW, order.ValueRO.ResourceId, mined);
                            LogMiningCommand(hasCommandLog, commandLog, currentTick, target, entity, vessel.ValueRO.CargoResourceType, mined, transform.ValueRO.Position);
                            EmitEffect(effectBuffer, entity, tickInterval);
                            order.ValueRW.Status = MiningOrderStatus.Active;
                            safetyCounter++;

                            if (vessel.ValueRO.CurrentCargo >= vessel.ValueRO.CargoCapacity * 0.95f)
                            {
                                order.ValueRW.Status = MiningOrderStatus.Completed;
                                SetPhase(ref miningState.ValueRW, MiningPhase.Detaching, DetachDuration);
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
                            break;
                        }

                        if (IsTargetInRange(target, transform.ValueRO.Position))
                        {
                            SetPhase(ref miningState.ValueRW, MiningPhase.Docking, DockDuration);
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

        private float ApplyMiningTick(Entity miner, Entity target, float tickInterval, ref MiningVessel vessel, in FixedString64Bytes orderResourceId)
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
            var mined = math.min(miningRate * tickInterval, available);

            var remainingCapacity = math.max(0f, vessel.CargoCapacity - vessel.CurrentCargo);
            mined = math.min(mined, remainingCapacity);

            if (mined <= 0f)
            {
                return 0f;
            }

            resourceRef.ValueRW.UnitsRemaining = available - mined;
            vessel.CurrentCargo += mined;
            return mined;
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

        private void EmitEffect(DynamicBuffer<PlayEffectRequest> effectBuffer, Entity attachTo, float tickInterval)
        {
            if (!effectBuffer.IsCreated)
            {
                return;
            }

            effectBuffer.Add(new PlayEffectRequest
            {
                EffectId = MiningSparksEffectId,
                AttachTo = attachTo,
                Lifetime = math.max(0.1f, tickInterval)
            });
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
    }
}
