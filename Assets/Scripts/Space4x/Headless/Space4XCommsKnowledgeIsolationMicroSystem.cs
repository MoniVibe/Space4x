using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Scenarios;
using Space4X.Registry;
using Space4X.Runtime;
using Space4x.Scenario;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Space4X.Headless
{
    [UpdateInGroup(typeof(Unity.Entities.LateSimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XHeadlessOperatorReportSystem))]
    public partial struct Space4XCommsKnowledgeIsolationMicroSystem : ISystem
    {
        private const float ObserverSenseRange = 180f;
        private const float AllySenseRange = 140f;
        private const uint KnowledgeTtlTicks = 2u;
        private const float CommsCutSeconds = 8f;
        private const float LocalRetentionWindowSeconds = 2.5f;

        private static readonly FixedString64Bytes ScenarioId = new FixedString64Bytes("space4x_comms_knowledge_isolation_micro");
        private static readonly FixedString64Bytes ObserverCarrierId = new FixedString64Bytes("comms-ki-observer");
        private static readonly FixedString64Bytes AllyCarrierId = new FixedString64Bytes("comms-ki-ally");
        private static readonly FixedString64Bytes TargetCarrierId = new FixedString64Bytes("comms-ki-target");

        private static readonly FixedString64Bytes SharedContactsCountKey = new FixedString64Bytes("space4x.comms.shared_contacts.count");
        private static readonly FixedString64Bytes LocalContactsCountKey = new FixedString64Bytes("space4x.comms.local_contacts.count");
        private static readonly FixedString64Bytes AllyHasTargetKnowledgeKey = new FixedString64Bytes("space4x.comms.ally_has_target_knowledge");
        private static readonly FixedString64Bytes DigestKey = new FixedString64Bytes("space4x.comms.digest");
        private static readonly FixedString64Bytes SharedContactsPreCutKey = new FixedString64Bytes("space4x.comms.shared_contacts.pre_cut");
        private static readonly FixedString64Bytes SharedContactsPostCutKey = new FixedString64Bytes("space4x.comms.shared_contacts.post_cut");
        private static readonly FixedString64Bytes LocalContactsAfterCutKey = new FixedString64Bytes("space4x.comms.local_contacts.after_cut_window");

        private Entity _observer;
        private Entity _ally;
        private Entity _target;

        private FixedString64Bytes _activeScenario;
        private uint _commsCutTick;
        private uint _localRetentionWindowEndTick;
        private uint _sharedContactsCount;
        private uint _localContactsCount;
        private uint _sharedContactsPreCutCount;
        private uint _sharedContactsPostCutCount;
        private uint _localContactsAfterCutCount;
        private uint _allyKnowledgeTtlTicks;
        private uint _digest;

        private byte _allyHasTargetKnowledgeAfterCut;
        private byte _initialized;
        private byte _emitted;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XScenarioRuntime>();
            state.RequireForUpdate<ScenarioInfo>();
            ResetState();
        }

        public void OnUpdate(ref SystemState state)
        {
            var scenarioInfo = SystemAPI.GetSingleton<ScenarioInfo>();
            if (!scenarioInfo.ScenarioId.Equals(ScenarioId))
            {
                return;
            }

            if (!_activeScenario.Equals(scenarioInfo.ScenarioId))
            {
                ResetState();
                _activeScenario = scenarioInfo.ScenarioId;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var runtime = SystemAPI.GetSingleton<Space4XScenarioRuntime>();
            if (_initialized == 0)
            {
                _commsCutTick = runtime.StartTick + SecondsToTicks(CommsCutSeconds, timeState.FixedDeltaTime);
                _localRetentionWindowEndTick = _commsCutTick + SecondsToTicks(LocalRetentionWindowSeconds, timeState.FixedDeltaTime);
                _initialized = 1;
            }

            ResolveEntities(ref state);
            if (_observer == Entity.Null || _ally == Entity.Null || _target == Entity.Null)
            {
                return;
            }

            var tick = timeState.Tick;
            EvaluateTick(ref state, tick);

            if (_emitted == 0 && runtime.EndTick > 0u && tick >= runtime.EndTick)
            {
                EmitMetrics(ref state);
                _emitted = 1;
            }
        }

        private void ResolveEntities(ref SystemState state)
        {
            if (_observer != Entity.Null && _ally != Entity.Null && _target != Entity.Null &&
                state.EntityManager.Exists(_observer) &&
                state.EntityManager.Exists(_ally) &&
                state.EntityManager.Exists(_target))
            {
                return;
            }

            _observer = ResolveCarrier(ObserverCarrierId, ref state);
            _ally = ResolveCarrier(AllyCarrierId, ref state);
            _target = ResolveCarrier(TargetCarrierId, ref state);
        }

        private void EvaluateTick(ref SystemState state, uint tick)
        {
            if (!state.EntityManager.HasComponent<LocalTransform>(_observer) ||
                !state.EntityManager.HasComponent<LocalTransform>(_ally) ||
                !state.EntityManager.HasComponent<LocalTransform>(_target))
            {
                return;
            }

            var observerPosition = state.EntityManager.GetComponentData<LocalTransform>(_observer).Position;
            var allyPosition = state.EntityManager.GetComponentData<LocalTransform>(_ally).Position;
            var targetPosition = state.EntityManager.GetComponentData<LocalTransform>(_target).Position;

            var observerLocalContact = math.distance(observerPosition, targetPosition) <= ObserverSenseRange;
            var allyLocalContact = math.distance(allyPosition, targetPosition) <= AllySenseRange;

            if (observerLocalContact)
            {
                _localContactsCount++;
            }

            if (_allyKnowledgeTtlTicks > 0u)
            {
                _allyKnowledgeTtlTicks--;
            }

            if (tick < _commsCutTick)
            {
                if (observerLocalContact)
                {
                    _sharedContactsCount++;
                    _sharedContactsPreCutCount++;
                    _allyKnowledgeTtlTicks = KnowledgeTtlTicks;
                }
            }
            else
            {
                // Comms cut enforces isolation: new shared contact propagation is blocked.
                _allyKnowledgeTtlTicks = 0u;
                if (observerLocalContact && tick <= _localRetentionWindowEndTick)
                {
                    _localContactsAfterCutCount++;
                }
            }

            var allyHasKnowledgeNow = allyLocalContact || _allyKnowledgeTtlTicks > 0u;
            if (tick >= _commsCutTick && observerLocalContact && allyHasKnowledgeNow)
            {
                _allyHasTargetKnowledgeAfterCut = 1;
                _sharedContactsPostCutCount++;
            }

            _digest = MixDigest(_digest, (uint)(observerLocalContact ? 1 : 0));
            _digest = MixDigest(_digest, (uint)(allyLocalContact ? 1 : 0));
            _digest = MixDigest(_digest, (uint)(_allyKnowledgeTtlTicks > 0u ? 1 : 0));
            _digest = MixDigest(_digest, _sharedContactsCount);
            _digest = MixDigest(_digest, _localContactsCount);
        }

        private void EmitMetrics(ref SystemState state)
        {
            if (!Space4XOperatorReportUtility.TryGetMetricBuffer(ref state, out var buffer))
            {
                return;
            }

            AddOrUpdateMetric(buffer, SharedContactsCountKey, _sharedContactsCount);
            AddOrUpdateMetric(buffer, LocalContactsCountKey, _localContactsCount);
            AddOrUpdateMetric(buffer, AllyHasTargetKnowledgeKey, _allyHasTargetKnowledgeAfterCut != 0 ? 1f : 0f);
            AddOrUpdateMetric(buffer, DigestKey, _digest == 0u ? 1f : _digest);
            AddOrUpdateMetric(buffer, SharedContactsPreCutKey, _sharedContactsPreCutCount);
            AddOrUpdateMetric(buffer, SharedContactsPostCutKey, _sharedContactsPostCutCount);
            AddOrUpdateMetric(buffer, LocalContactsAfterCutKey, _localContactsAfterCutCount);
        }

        private void ResetState()
        {
            _observer = Entity.Null;
            _ally = Entity.Null;
            _target = Entity.Null;
            _activeScenario = default;
            _commsCutTick = 0u;
            _localRetentionWindowEndTick = 0u;
            _sharedContactsCount = 0u;
            _localContactsCount = 0u;
            _sharedContactsPreCutCount = 0u;
            _sharedContactsPostCutCount = 0u;
            _localContactsAfterCutCount = 0u;
            _allyKnowledgeTtlTicks = 0u;
            _digest = 2166136261u;
            _allyHasTargetKnowledgeAfterCut = 0;
            _initialized = 0;
            _emitted = 0;
        }

        private Entity ResolveCarrier(FixedString64Bytes carrierId, ref SystemState state)
        {
            if (carrierId.IsEmpty)
            {
                return Entity.Null;
            }

            foreach (var (carrier, entity) in SystemAPI.Query<RefRO<Carrier>>().WithEntityAccess())
            {
                if (carrier.ValueRO.CarrierId.Equals(carrierId))
                {
                    return entity;
                }
            }

            return Entity.Null;
        }

        private static uint SecondsToTicks(float seconds, float fixedDt)
        {
            if (seconds <= 0f || fixedDt <= 0f)
            {
                return 0u;
            }

            return (uint)math.ceil(seconds / fixedDt);
        }

        private static uint MixDigest(uint digest, uint value)
        {
            return (digest ^ (value + 0x9e3779b9u)) * 16777619u;
        }

        private static void AddOrUpdateMetric(DynamicBuffer<Space4XOperatorMetric> buffer, FixedString64Bytes key, float value)
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                var metric = buffer[i];
                if (!metric.Key.Equals(key))
                {
                    continue;
                }

                metric.Value = value;
                buffer[i] = metric;
                return;
            }

            buffer.Add(new Space4XOperatorMetric
            {
                Key = key,
                Value = value
            });
        }
    }
}
