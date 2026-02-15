using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Profile;
using PureDOTS.Runtime.Scenarios;
using Space4X.Runtime;
using Space4X.Registry;
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
    public partial struct Space4XProfileBiasMicroSystem : ISystem
    {
        private static readonly FixedString64Bytes NavScenarioId = new FixedString64Bytes("space4x_profilebias_nav_micro");
        private static readonly FixedString64Bytes SensorsScenarioId = new FixedString64Bytes("space4x_profilebias_sensors_micro");

        private static readonly FixedString64Bytes NavAggressiveCarrierId = new FixedString64Bytes("profile-nav-aggressive");
        private static readonly FixedString64Bytes NavCautiousCarrierId = new FixedString64Bytes("profile-nav-cautious");

        private static readonly FixedString64Bytes SensorsAggressiveCarrierId = new FixedString64Bytes("profile-sensors-aggressive");
        private static readonly FixedString64Bytes SensorsCautiousCarrierId = new FixedString64Bytes("profile-sensors-cautious");
        private static readonly FixedString64Bytes SensorsTargetCarrierId = new FixedString64Bytes("profile-sensors-target");

        private static readonly FixedString64Bytes NavAggressiveAvgDistanceKey = new FixedString64Bytes("space4x.profilebias.nav.aggressive.avg_approach_distance");
        private static readonly FixedString64Bytes NavCautiousAvgDistanceKey = new FixedString64Bytes("space4x.profilebias.nav.cautious.avg_approach_distance");
        private static readonly FixedString64Bytes NavRangeDeltaKey = new FixedString64Bytes("space4x.profilebias.nav.range_delta");

        private static readonly FixedString64Bytes SensorsAggressiveDropDistanceKey = new FixedString64Bytes("space4x.profilebias.sensors.aggressive.drop_distance");
        private static readonly FixedString64Bytes SensorsCautiousDropDistanceKey = new FixedString64Bytes("space4x.profilebias.sensors.cautious.drop_distance");
        private static readonly FixedString64Bytes SensorsDropDistanceDeltaKey = new FixedString64Bytes("space4x.profilebias.sensors.drop_distance_delta");

        private FixedString64Bytes _activeScenario;
        private Entity _navAggressive;
        private Entity _navCautious;
        private Entity _sensorsAggressive;
        private Entity _sensorsCautious;
        private Entity _sensorsTarget;

        private float3 _navAggressiveBaselineTarget;
        private float3 _navCautiousBaselineTarget;
        private float _navAggressiveDistanceSum;
        private float _navCautiousDistanceSum;
        private uint _navAggressiveSamples;
        private uint _navCautiousSamples;
        private byte _navAggressiveBaselineSet;
        private byte _navCautiousBaselineSet;

        private float _sensorsAggressiveDropDistance;
        private float _sensorsCautiousDropDistance;
        private byte _sensorsAggressiveDropSet;
        private byte _sensorsCautiousDropSet;
        private byte _sensorsAggressiveWasDetected;
        private byte _sensorsCautiousWasDetected;
        private byte _sensorsAggressiveDetectedAtLeastOnce;
        private byte _sensorsCautiousDetectedAtLeastOnce;

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
            var scenarioId = scenarioInfo.ScenarioId;
            var isNavScenario = scenarioId.Equals(NavScenarioId);
            var isSensorsScenario = scenarioId.Equals(SensorsScenarioId);
            if (!isNavScenario && !isSensorsScenario)
            {
                return;
            }

            if (!_activeScenario.Equals(scenarioId))
            {
                ResetState();
                _activeScenario = scenarioId;
            }

            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            if (isNavScenario)
            {
                UpdateNavScenario(ref state);
            }
            else
            {
                UpdateSensorsScenario(ref state);
            }

            var runtime = SystemAPI.GetSingleton<Space4XScenarioRuntime>();
            if (_emitted == 0 && runtime.EndTick > 0u && time.Tick >= runtime.EndTick)
            {
                EmitMetrics(ref state, isNavScenario);
                _emitted = 1;
            }
        }

        private void UpdateNavScenario(ref SystemState state)
        {
            ResolveNavEntities(ref state);
            if (_navAggressive == Entity.Null || _navCautious == Entity.Null)
            {
                return;
            }

            ApplyTuning(ref state, _navAggressive, aggressive: true);
            ApplyTuning(ref state, _navCautious, aggressive: false);

            UpdateNavCarrier(ref state, _navAggressive, aggressive: true, ref _navAggressiveBaselineTarget, ref _navAggressiveBaselineSet,
                ref _navAggressiveDistanceSum, ref _navAggressiveSamples);
            UpdateNavCarrier(ref state, _navCautious, aggressive: false, ref _navCautiousBaselineTarget, ref _navCautiousBaselineSet,
                ref _navCautiousDistanceSum, ref _navCautiousSamples);
        }

        private void UpdateSensorsScenario(ref SystemState state)
        {
            ResolveSensorsEntities(ref state);
            if (_sensorsAggressive == Entity.Null || _sensorsCautious == Entity.Null || _sensorsTarget == Entity.Null)
            {
                return;
            }

            ApplyTuning(ref state, _sensorsAggressive, aggressive: true);
            ApplyTuning(ref state, _sensorsCautious, aggressive: false);

            if (!state.EntityManager.HasComponent<LocalTransform>(_sensorsAggressive) ||
                !state.EntityManager.HasComponent<LocalTransform>(_sensorsCautious) ||
                !state.EntityManager.HasComponent<LocalTransform>(_sensorsTarget))
            {
                return;
            }

            var targetPos = state.EntityManager.GetComponentData<LocalTransform>(_sensorsTarget).Position;
            var aggressivePos = state.EntityManager.GetComponentData<LocalTransform>(_sensorsAggressive).Position;
            var cautiousPos = state.EntityManager.GetComponentData<LocalTransform>(_sensorsCautious).Position;

            var aggressiveDistance = math.distance(aggressivePos, targetPos);
            var cautiousDistance = math.distance(cautiousPos, targetPos);
            var aggressiveRange = ResolveVirtualSensorRange(aggressive: true);
            var cautiousRange = ResolveVirtualSensorRange(aggressive: false);

            var aggressiveDetected = aggressiveDistance <= aggressiveRange;
            var cautiousDetected = cautiousDistance <= cautiousRange;

            if (aggressiveDetected)
            {
                _sensorsAggressiveDetectedAtLeastOnce = 1;
            }

            if (cautiousDetected)
            {
                _sensorsCautiousDetectedAtLeastOnce = 1;
            }

            if (_sensorsAggressiveDetectedAtLeastOnce != 0 &&
                _sensorsAggressiveDropSet == 0 &&
                _sensorsAggressiveWasDetected != 0 &&
                !aggressiveDetected)
            {
                _sensorsAggressiveDropDistance = aggressiveDistance;
                _sensorsAggressiveDropSet = 1;
            }

            if (_sensorsCautiousDetectedAtLeastOnce != 0 &&
                _sensorsCautiousDropSet == 0 &&
                _sensorsCautiousWasDetected != 0 &&
                !cautiousDetected)
            {
                _sensorsCautiousDropDistance = cautiousDistance;
                _sensorsCautiousDropSet = 1;
            }

            _sensorsAggressiveWasDetected = (byte)(aggressiveDetected ? 1 : 0);
            _sensorsCautiousWasDetected = (byte)(cautiousDetected ? 1 : 0);
        }

        private void UpdateNavCarrier(
            ref SystemState state,
            Entity entity,
            bool aggressive,
            ref float3 baselineTarget,
            ref byte baselineSet,
            ref float distanceSum,
            ref uint sampleCount)
        {
            if (!state.EntityManager.HasComponent<MoveIntent>(entity) || !state.EntityManager.HasComponent<LocalTransform>(entity))
            {
                return;
            }

            var intent = state.EntityManager.GetComponentData<MoveIntent>(entity);
            if (intent.IntentType != MoveIntentType.MoveTo)
            {
                return;
            }

            if (baselineSet == 0)
            {
                baselineTarget = intent.TargetPosition;
                baselineSet = 1;
            }

            var position = state.EntityManager.GetComponentData<LocalTransform>(entity).Position;
            var toBaseline = baselineTarget - position;
            var distanceToBaseline = math.length(toBaseline);
            var standoff = ResolveNavStandoff(aggressive);

            if (distanceToBaseline > standoff + 0.5f)
            {
                var direction = math.normalizesafe(toBaseline, new float3(1f, 0f, 0f));
                intent.TargetPosition = baselineTarget - direction * standoff;
            }
            else
            {
                intent.TargetPosition = baselineTarget;
            }

            state.EntityManager.SetComponentData(entity, intent);

            distanceSum += distanceToBaseline;
            sampleCount++;
        }

        private void ResolveNavEntities(ref SystemState state)
        {
            if (_navAggressive != Entity.Null &&
                _navCautious != Entity.Null &&
                state.EntityManager.Exists(_navAggressive) &&
                state.EntityManager.Exists(_navCautious))
            {
                return;
            }

            _navAggressive = ResolveCarrier(NavAggressiveCarrierId, ref state);
            _navCautious = ResolveCarrier(NavCautiousCarrierId, ref state);
        }

        private void ResolveSensorsEntities(ref SystemState state)
        {
            if (_sensorsAggressive != Entity.Null &&
                _sensorsCautious != Entity.Null &&
                _sensorsTarget != Entity.Null &&
                state.EntityManager.Exists(_sensorsAggressive) &&
                state.EntityManager.Exists(_sensorsCautious) &&
                state.EntityManager.Exists(_sensorsTarget))
            {
                return;
            }

            _sensorsAggressive = ResolveCarrier(SensorsAggressiveCarrierId, ref state);
            _sensorsCautious = ResolveCarrier(SensorsCautiousCarrierId, ref state);
            _sensorsTarget = ResolveCarrier(SensorsTargetCarrierId, ref state);
        }

        private static Entity ResolveCarrier(FixedString64Bytes carrierId, ref SystemState state)
        {
            using var query = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Carrier>());
            var carriers = query.ToComponentDataArray<Carrier>(Allocator.Temp);
            var entities = query.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < carriers.Length; i++)
            {
                if (carriers[i].CarrierId.Equals(carrierId))
                {
                    return entities[i];
                }
            }

            return Entity.Null;
        }

        private static float ResolveNavStandoff(bool aggressive)
        {
            return aggressive ? 16f : 260f;
        }

        private static float ResolveVirtualSensorRange(bool aggressive)
        {
            return aggressive ? 96f : 82f;
        }

        private static void ApplyTuning(ref SystemState state, Entity entity, bool aggressive)
        {
            var compliance = aggressive ? 0.9f : 0.35f;
            var caution = aggressive ? 0.18f : 0.92f;
            var formation = aggressive ? 0.42f : 0.72f;
            var riskTolerance = aggressive ? 0.86f : 0.18f;
            var aggression = aggressive ? 0.87f : 0.2f;
            var patience = aggressive ? 0.36f : 0.82f;

            var behavior = BehaviorDisposition.FromValues(
                compliance,
                caution,
                formation,
                riskTolerance,
                aggression,
                patience);

            if (state.EntityManager.HasComponent<BehaviorDisposition>(entity))
            {
                state.EntityManager.SetComponentData(entity, behavior);
            }
            else
            {
                state.EntityManager.AddComponentData(entity, behavior);
            }
        }

        private void EmitMetrics(ref SystemState state, bool navScenario)
        {
            if (!Space4XOperatorReportUtility.TryGetMetricBuffer(ref state, out var buffer))
            {
                return;
            }

            if (navScenario)
            {
                var aggressiveAverage = _navAggressiveSamples > 0 ? _navAggressiveDistanceSum / _navAggressiveSamples : 0f;
                var cautiousAverage = _navCautiousSamples > 0 ? _navCautiousDistanceSum / _navCautiousSamples : 0f;
                var rangeDelta = aggressiveAverage - cautiousAverage;

                AddOrUpdateMetric(buffer, NavAggressiveAvgDistanceKey, aggressiveAverage);
                AddOrUpdateMetric(buffer, NavCautiousAvgDistanceKey, cautiousAverage);
                AddOrUpdateMetric(buffer, NavRangeDeltaKey, rangeDelta);
                return;
            }

            var aggressiveDropDistance = _sensorsAggressiveDropSet != 0 ? _sensorsAggressiveDropDistance : 0f;
            var cautiousDropDistance = _sensorsCautiousDropSet != 0 ? _sensorsCautiousDropDistance : 0f;
            var dropDelta = (_sensorsAggressiveDropSet != 0 && _sensorsCautiousDropSet != 0)
                ? aggressiveDropDistance - cautiousDropDistance
                : 0f;

            AddOrUpdateMetric(buffer, SensorsAggressiveDropDistanceKey, aggressiveDropDistance);
            AddOrUpdateMetric(buffer, SensorsCautiousDropDistanceKey, cautiousDropDistance);
            AddOrUpdateMetric(buffer, SensorsDropDistanceDeltaKey, dropDelta);
        }

        private void ResetState()
        {
            _activeScenario = default;
            _navAggressive = Entity.Null;
            _navCautious = Entity.Null;
            _sensorsAggressive = Entity.Null;
            _sensorsCautious = Entity.Null;
            _sensorsTarget = Entity.Null;

            _navAggressiveBaselineTarget = float3.zero;
            _navCautiousBaselineTarget = float3.zero;
            _navAggressiveDistanceSum = 0f;
            _navCautiousDistanceSum = 0f;
            _navAggressiveSamples = 0u;
            _navCautiousSamples = 0u;
            _navAggressiveBaselineSet = 0;
            _navCautiousBaselineSet = 0;

            _sensorsAggressiveDropDistance = 0f;
            _sensorsCautiousDropDistance = 0f;
            _sensorsAggressiveDropSet = 0;
            _sensorsCautiousDropSet = 0;
            _sensorsAggressiveWasDetected = 0;
            _sensorsCautiousWasDetected = 0;
            _sensorsAggressiveDetectedAtLeastOnce = 0;
            _sensorsCautiousDetectedAtLeastOnce = 0;
            _emitted = 0;
        }

        private static void AddOrUpdateMetric(
            DynamicBuffer<Space4XOperatorMetric> buffer,
            FixedString64Bytes key,
            float value)
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
