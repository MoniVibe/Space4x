using PureDOTS.Environment;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using Space4X.Physics;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.Headless
{
    [UpdateInGroup(typeof(Unity.Entities.LateSimulationSystemGroup))]
    [UpdateAfter(typeof(Space4X.Registry.Space4XMinerMiningSystem))]
    [UpdateAfter(typeof(Space4X.Systems.AI.VesselMovementSystem))]
    public partial struct Space4XHeadlessMiningStallSystem : ISystem
    {
        private const float YieldEpsilon = 0.01f;
        private const float RangePadding = 0.5f;
        private const float StallSeconds = 12f;
        private const byte StallUnknown = 0;
        private const byte StallDocked = 1;
        private const byte StallUndockWait = 2;
        private const byte StallApproach = 3;
        private const byte StallLatchWait = 4;
        private const byte StallDigZero = 5;
        private const byte StallTerrainNoEffect = 6;
        private const byte StallReturnMissing = 7;
        private const byte StallDockingFail = 8;

        private EntityQuery _missingProbeQuery;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<SpaceColliderData> _colliderLookup;
        private ComponentLookup<DockedTag> _dockedLookup;
        private ComponentLookup<DecisionTrace> _decisionTraceLookup;
        private ComponentLookup<MiningDecisionTrace> _miningDecisionLookup;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _missingProbeQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<MiningState>(), ComponentType.ReadOnly<MiningYield>() },
                None = new[] { ComponentType.ReadOnly<Space4XMiningStallState>() }
            });

            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _colliderLookup = state.GetComponentLookup<SpaceColliderData>(true);
            _dockedLookup = state.GetComponentLookup<DockedTag>(true);
            _decisionTraceLookup = state.GetComponentLookup<DecisionTrace>(false);
            _miningDecisionLookup = state.GetComponentLookup<MiningDecisionTrace>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!_missingProbeQuery.IsEmptyIgnoreFilter)
            {
                state.EntityManager.AddComponent<Space4XMiningStallState>(_missingProbeQuery);
            }

            _transformLookup.Update(ref state);
            _colliderLookup.Update(ref state);
            _dockedLookup.Update(ref state);
            _decisionTraceLookup.Update(ref state);
            _miningDecisionLookup.Update(ref state);

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

            var tick = timeState.Tick;
            var stallTicks = SecondsToTicks(StallSeconds, timeState.FixedDeltaTime);
            var hasQueue = SystemAPI.TryGetSingletonEntity<TerrainModificationQueue>(out var queueEntity);
            DynamicBuffer<TerrainModificationRequest> modificationBuffer = default;
            if (hasQueue)
            {
                modificationBuffer = SystemAPI.GetBuffer<TerrainModificationRequest>(queueEntity);
            }

            foreach (var (miningState, miningYield, transform, probe, entity) in SystemAPI
                         .Query<RefRO<MiningState>, RefRO<MiningYield>, RefRO<LocalTransform>, RefRW<Space4XMiningStallState>>()
                         .WithEntityAccess())
            {
                var probeState = probe.ValueRW;
                if (probeState.Initialized == 0)
                {
                    probeState.LastYieldAmount = miningYield.ValueRO.PendingAmount;
                    probeState.LastYieldTick = tick;
                    probeState.LastPhase = miningState.ValueRO.Phase;
                    probeState.LastPhaseTick = tick;
                    probeState.Initialized = 1;
                }

                if (miningState.ValueRO.Phase != probeState.LastPhase)
                {
                    probeState.LastPhase = miningState.ValueRO.Phase;
                    probeState.LastPhaseTick = tick;
                }

                if (miningYield.ValueRO.PendingAmount > probeState.LastYieldAmount + YieldEpsilon)
                {
                    probeState.LastYieldAmount = miningYield.ValueRO.PendingAmount;
                    probeState.LastYieldTick = tick;
                    probeState.Reported = 0;
                }

                var target = miningState.ValueRO.ActiveTarget;
                if (target == Entity.Null)
                {
                    probe.ValueRW = probeState;
                    continue;
                }

                if (!_transformLookup.HasComponent(target) || !_colliderLookup.HasComponent(target))
                {
                    probe.ValueRW = probeState;
                    continue;
                }

                var targetTransform = _transformLookup[target];
                var targetCollider = _colliderLookup[target];
                var distance = math.distance(transform.ValueRO.Position, targetTransform.Position);
                var range = RangePadding + targetCollider.Radius;

                var phase = miningState.ValueRO.Phase;
                var isActivePhase = phase == MiningPhase.ApproachTarget || phase == MiningPhase.Latching || phase == MiningPhase.Mining;
                var inRange = distance <= range;

                if (probeState.Reported == 0 &&
                    isActivePhase &&
                    inRange &&
                    tick > probeState.LastYieldTick + stallTicks)
                {
                    var isDocked = _dockedLookup.HasComponent(entity);
                    var digRequests = CountDigRequests(modificationBuffer, entity, probeState.LastYieldTick);
                    var classification = ClassifyStall(phase, isDocked, digRequests);
                    if (_miningDecisionLookup.HasComponent(entity))
                    {
                        var decisionTrace = _miningDecisionLookup[entity];
                        probeState.DecisionReason = decisionTrace.Reason;
                        probeState.DecisionTarget = decisionTrace.Target;
                        probeState.DecisionDistance = decisionTrace.DistanceToTarget;
                        probeState.DecisionRangeThreshold = decisionTrace.RangeThreshold;
                        probeState.DecisionStandoff = decisionTrace.Standoff;
                        probeState.DecisionAligned = decisionTrace.Aligned;
                        probeState.DecisionTick = decisionTrace.Tick;
                    }
                    UpdateDecisionTrace(entity, target, classification, tick);
                    AppendBlackCat(ref state, entity, target, probeState.LastYieldTick, tick, distance, phase, classification, digRequests);
                    probeState.Reported = 1;
                }

                probe.ValueRW = probeState;
            }
        }

        private void AppendBlackCat(
            ref SystemState state,
            Entity vessel,
            Entity target,
            uint startTick,
            uint endTick,
            float distance,
            MiningPhase phase,
            byte classification,
            uint digRequests)
        {
            if (!Space4XOperatorReportUtility.TryGetBlackCatBuffer(ref state, out var buffer))
            {
                return;
            }

            buffer.Add(new Space4XOperatorBlackCat
            {
                Id = new FixedString64Bytes("MINING_STALL"),
                Primary = vessel,
                Secondary = target,
                StartTick = startTick,
                EndTick = endTick,
                MetricA = endTick - startTick,
                MetricB = distance,
                MetricC = (float)phase,
                MetricD = digRequests,
                Classification = classification
            });
        }

        private static byte ClassifyStall(MiningPhase phase, bool isDocked, uint digRequests)
        {
            if (isDocked)
            {
                return StallDocked;
            }

            return phase switch
            {
                MiningPhase.Undocking => StallUndockWait,
                MiningPhase.ApproachTarget => StallApproach,
                MiningPhase.Latching => StallLatchWait,
                MiningPhase.Mining => digRequests == 0 ? StallDigZero : StallTerrainNoEffect,
                MiningPhase.ReturnApproach => StallReturnMissing,
                MiningPhase.Docking => StallDockingFail,
                _ => StallUnknown
            };
        }

        private void UpdateDecisionTrace(Entity miner, Entity target, byte classification, uint tick)
        {
            if (!_decisionTraceLookup.HasComponent(miner))
            {
                return;
            }

            var reason = classification switch
            {
                StallDocked => DecisionReasonCode.MiningUndockWait,
                StallUndockWait => DecisionReasonCode.MiningUndockWait,
                StallLatchWait => DecisionReasonCode.MiningLatchWait,
                StallDigZero => DecisionReasonCode.MiningDigging,
                StallTerrainNoEffect => DecisionReasonCode.MiningDigging,
                StallReturnMissing => DecisionReasonCode.MiningReturnFull,
                StallDockingFail => DecisionReasonCode.MiningReturnFull,
                _ => DecisionReasonCode.MiningHold
            };

            var trace = _decisionTraceLookup[miner];
            trace.ReasonCode = reason;
            trace.ChosenTarget = target;
            trace.Score = 1f;
            trace.BlockerEntity = Entity.Null;
            trace.SinceTick = tick;
            _decisionTraceLookup[miner] = trace;
        }

        private static uint CountDigRequests(DynamicBuffer<TerrainModificationRequest> buffer, Entity miner, uint sinceTick)
        {
            if (!buffer.IsCreated)
            {
                return 0;
            }

            var count = 0u;
            for (var i = 0; i < buffer.Length; i++)
            {
                var request = buffer[i];
                if (request.Actor == miner && request.RequestedTick >= sinceTick)
                {
                    count++;
                }
            }

            return count;
        }

        private static uint SecondsToTicks(float seconds, float fixedDt)
        {
            var safeDt = math.max(1e-6f, fixedDt);
            return (uint)math.ceil(math.max(0f, seconds) / safeDt);
        }
    }

    public struct Space4XMiningStallState : IComponentData
    {
        public float LastYieldAmount;
        public uint LastYieldTick;
        public uint LastPhaseTick;
        public MiningPhase LastPhase;
        public byte Reported;
        public byte Initialized;
        public MiningDecisionReason DecisionReason;
        public Entity DecisionTarget;
        public float DecisionDistance;
        public float DecisionRangeThreshold;
        public float DecisionStandoff;
        public byte DecisionAligned;
        public uint DecisionTick;
    }
}
