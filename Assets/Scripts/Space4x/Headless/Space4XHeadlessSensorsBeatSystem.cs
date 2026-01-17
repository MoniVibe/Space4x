using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Perception;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Headless
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.Perception.PerceptionUpdateSystem))]
    public partial struct Space4XHeadlessSensorsBeatSystem : ISystem
    {
        private const float MinAcquireDetectedRatio = 0.8f;
        private const float MaxDropDetectedRatio = 0.1f;
        private const uint MaxToggleCount = 2u;

        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<PerceptionState> _perceptionStateLookup;
        private ComponentLookup<SenseCapability> _senseLookup;
        private BufferLookup<PerceivedEntity> _perceivedLookup;

        private Entity _observer;
        private Entity _target;
        private uint _measureStartTick;
        private uint _measureEndTick;
        private uint _acquireStartTick;
        private uint _acquireEndTick;
        private uint _dropStartTick;
        private uint _dropEndTick;
        private uint _sampleCount;
        private uint _acquireSamples;
        private uint _acquireDetected;
        private uint _dropSamples;
        private uint _dropDetected;
        private uint _toggleCount;
        private uint _staleSamples;
        private uint _maxTicksSinceUpdate;
        private uint _expectedUpdateTicks;
        private byte _lastDetected;
        private byte _hasLastDetected;
        private byte _initialized;
        private byte _done;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XScenarioRuntime>();
            state.RequireForUpdate<Space4XSensorsBeatConfig>();

            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _perceptionStateLookup = state.GetComponentLookup<PerceptionState>(true);
            _senseLookup = state.GetComponentLookup<SenseCapability>(true);
            _perceivedLookup = state.GetBufferLookup<PerceivedEntity>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_done != 0)
            {
                return;
            }

            _carrierLookup.Update(ref state);
            _perceptionStateLookup.Update(ref state);
            _senseLookup.Update(ref state);
            _perceivedLookup.Update(ref state);

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var runtime = SystemAPI.GetSingleton<Space4XScenarioRuntime>();
            var config = SystemAPI.GetSingleton<Space4XSensorsBeatConfig>();
            if (config.Initialized == 0)
            {
                InitializeConfig(ref config, runtime.StartTick, timeState.FixedDeltaTime);
                SystemAPI.SetSingleton(config);
            }

            if (_initialized == 0)
            {
                _acquireStartTick = config.AcquireStartTick;
                _acquireEndTick = config.AcquireEndTick;
                _dropStartTick = config.DropStartTick;
                _dropEndTick = config.DropEndTick;
                _measureStartTick = ResolveMeasureStart(_acquireStartTick, _dropStartTick);
                _measureEndTick = ResolveMeasureEnd(_acquireEndTick, _dropEndTick);
                _initialized = 1;
            }

            var tick = timeState.Tick;
            if (_observer == Entity.Null || !state.EntityManager.Exists(_observer))
            {
                _observer = ResolveCarrier(config.ObserverCarrierId, ref state);
                _target = Entity.Null;
                ResetTracking();
            }

            if (_target == Entity.Null || !state.EntityManager.Exists(_target))
            {
                _target = ResolveCarrier(config.TargetCarrierId, ref state);
            }

            if (tick < _measureStartTick)
            {
                return;
            }

            if (tick > _measureEndTick)
            {
                FinalizeReport(ref state, config);
                config.Completed = 1;
                SystemAPI.SetSingleton(config);
                _done = 1;
                return;
            }

            MeasureTick(_observer, _target, timeState.FixedDeltaTime, tick);
        }

        private static void InitializeConfig(ref Space4XSensorsBeatConfig config, uint startTick, float fixedDt)
        {
            var acquireStart = startTick + SecondsToTicks(config.AcquireStartSeconds, fixedDt);
            var acquireDuration = SecondsToTicks(config.AcquireDurationSeconds, fixedDt);
            var dropStart = startTick + SecondsToTicks(config.DropStartSeconds, fixedDt);
            var dropDuration = SecondsToTicks(config.DropDurationSeconds, fixedDt);

            config.AcquireStartTick = acquireStart;
            config.AcquireEndTick = acquireStart + acquireDuration;
            config.DropStartTick = dropStart;
            config.DropEndTick = dropStart + dropDuration;
            config.Initialized = 1;
        }

        private void MeasureTick(Entity observer, Entity target, float fixedDt, uint tick)
        {
            if (observer == Entity.Null || target == Entity.Null)
            {
                return;
            }

            var inAcquire = tick >= _acquireStartTick && tick < _acquireEndTick;
            var inDrop = tick >= _dropStartTick && tick < _dropEndTick;
            if (!inAcquire && !inDrop)
            {
                return;
            }

            _sampleCount++;

            if (_perceptionStateLookup.HasComponent(observer))
            {
                var perceptionState = _perceptionStateLookup[observer];
                var ticksSinceUpdate = tick - perceptionState.LastUpdateTick;
                if (ticksSinceUpdate > _maxTicksSinceUpdate)
                {
                    _maxTicksSinceUpdate = ticksSinceUpdate;
                }

                var expectedTicks = ResolveExpectedUpdateTicks(observer, fixedDt);
                if (expectedTicks > 0 && ticksSinceUpdate > expectedTicks * 2)
                {
                    _staleSamples++;
                }
            }

            var detected = IsTargetDetected(observer, target);
            if (_hasLastDetected != 0 && detected != (_lastDetected != 0))
            {
                _toggleCount++;
            }

            _lastDetected = (byte)(detected ? 1 : 0);
            _hasLastDetected = 1;

            if (inAcquire)
            {
                _acquireSamples++;
                if (detected)
                {
                    _acquireDetected++;
                }
            }

            if (inDrop)
            {
                _dropSamples++;
                if (detected)
                {
                    _dropDetected++;
                }
            }
        }

        private void FinalizeReport(ref SystemState state, in Space4XSensorsBeatConfig config)
        {
            var hasSense = _observer != Entity.Null &&
                           _senseLookup.HasComponent(_observer) &&
                           _perceivedLookup.HasBuffer(_observer);

            if (_observer == Entity.Null || _target == Entity.Null || _sampleCount == 0 || !hasSense)
            {
                AppendSkippedBlackCat(ref state);
                EmitOperatorSummary(ref state);
                return;
            }

            var acquireRatio = _acquireSamples > 0 ? (float)_acquireDetected / _acquireSamples : 0f;
            var dropRatio = _dropSamples > 0 ? (float)_dropDetected / _dropSamples : 0f;

            if (_staleSamples > 0 || acquireRatio < MinAcquireDetectedRatio)
            {
                AppendStaleBlackCat(ref state, acquireRatio);
            }

            if (dropRatio > MaxDropDetectedRatio)
            {
                AppendGhostBlackCat(ref state, dropRatio);
            }

            if (_toggleCount > MaxToggleCount)
            {
                AppendThrashBlackCat(ref state);
            }

            EmitOperatorSummary(ref state);
        }

        private void EmitOperatorSummary(ref SystemState state)
        {
            if (!Space4XOperatorReportUtility.TryGetMetricBuffer(ref state, out var buffer))
            {
                return;
            }

            var acquireRatio = _acquireSamples > 0 ? (float)_acquireDetected / _acquireSamples : 0f;
            var dropRatio = _dropSamples > 0 ? (float)_dropDetected / _dropSamples : 0f;
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.sensor.sample_count"), _sampleCount);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.sensor.acquire_samples"), _acquireSamples);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.sensor.acquire_detected"), _acquireDetected);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.sensor.acquire_detected_ratio"), acquireRatio);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.sensor.drop_samples"), _dropSamples);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.sensor.drop_detected"), _dropDetected);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.sensor.drop_detected_ratio"), dropRatio);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.sensor.toggle_count"), _toggleCount);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.sensor.stale_samples"), _staleSamples);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.sensor.max_ticks_since_update"), _maxTicksSinceUpdate);
            AddOrUpdateMetric(buffer, new FixedString64Bytes("space4x.sensor.expected_update_ticks"), _expectedUpdateTicks);
        }

        private uint ResolveExpectedUpdateTicks(Entity observer, float fixedDt)
        {
            if (_expectedUpdateTicks > 0)
            {
                return _expectedUpdateTicks;
            }

            if (!_senseLookup.HasComponent(observer))
            {
                return 0u;
            }

            var capability = _senseLookup[observer];
            var interval = math.max(0.01f, capability.UpdateInterval);
            var expectedTicks = SecondsToTicks(interval, fixedDt);
            _expectedUpdateTicks = math.max(1u, expectedTicks);
            return _expectedUpdateTicks;
        }

        private bool IsTargetDetected(Entity observer, Entity target)
        {
            if (!_perceivedLookup.HasBuffer(observer))
            {
                return false;
            }

            var perceived = _perceivedLookup[observer];
            for (int i = 0; i < perceived.Length; i++)
            {
                if (perceived[i].TargetEntity == target)
                {
                    return true;
                }
            }

            return false;
        }

        private void AppendSkippedBlackCat(ref SystemState state)
        {
            if (!Space4XOperatorReportUtility.TryGetBlackCatBuffer(ref state, out var buffer))
            {
                return;
            }

            var classification = (byte)0;
            if (_observer == Entity.Null)
            {
                classification = 1;
            }
            else if (_target == Entity.Null)
            {
                classification = 2;
            }
            else if (!_senseLookup.HasComponent(_observer))
            {
                classification = 3;
            }
            else if (!_perceivedLookup.HasBuffer(_observer))
            {
                classification = 4;
            }
            else
            {
                classification = 5;
            }

            buffer.Add(new Space4XOperatorBlackCat
            {
                Id = new FixedString64Bytes("SENSORS_BEAT_SKIPPED"),
                Primary = _observer,
                Secondary = _target,
                StartTick = _measureStartTick,
                EndTick = _measureEndTick,
                MetricA = _sampleCount,
                MetricB = _acquireSamples,
                MetricC = _dropSamples,
                MetricD = classification,
                Classification = classification
            });
        }

        private void AppendStaleBlackCat(ref SystemState state, float acquireRatio)
        {
            if (!Space4XOperatorReportUtility.TryGetBlackCatBuffer(ref state, out var buffer))
            {
                return;
            }

            var classification = (byte)(_staleSamples > 0 ? 1 : 2);
            buffer.Add(new Space4XOperatorBlackCat
            {
                Id = new FixedString64Bytes("PERCEPTION_STALE"),
                Primary = _observer,
                Secondary = _target,
                StartTick = _measureStartTick,
                EndTick = _measureEndTick,
                MetricA = _staleSamples,
                MetricB = _maxTicksSinceUpdate,
                MetricC = _expectedUpdateTicks,
                MetricD = acquireRatio,
                Classification = classification
            });
        }

        private void AppendGhostBlackCat(ref SystemState state, float dropRatio)
        {
            if (!Space4XOperatorReportUtility.TryGetBlackCatBuffer(ref state, out var buffer))
            {
                return;
            }

            buffer.Add(new Space4XOperatorBlackCat
            {
                Id = new FixedString64Bytes("CONTACT_GHOST"),
                Primary = _observer,
                Secondary = _target,
                StartTick = _dropStartTick,
                EndTick = _dropEndTick,
                MetricA = _dropDetected,
                MetricB = _dropSamples,
                MetricC = dropRatio,
                MetricD = _toggleCount,
                Classification = 0
            });
        }

        private void AppendThrashBlackCat(ref SystemState state)
        {
            if (!Space4XOperatorReportUtility.TryGetBlackCatBuffer(ref state, out var buffer))
            {
                return;
            }

            buffer.Add(new Space4XOperatorBlackCat
            {
                Id = new FixedString64Bytes("CONTACT_THRASH"),
                Primary = _observer,
                Secondary = _target,
                StartTick = _measureStartTick,
                EndTick = _measureEndTick,
                MetricA = _toggleCount,
                MetricB = _acquireDetected,
                MetricC = _dropDetected,
                MetricD = _sampleCount,
                Classification = 0
            });
        }

        private void ResetTracking()
        {
            _sampleCount = 0;
            _acquireSamples = 0;
            _acquireDetected = 0;
            _dropSamples = 0;
            _dropDetected = 0;
            _toggleCount = 0;
            _staleSamples = 0;
            _maxTicksSinceUpdate = 0;
            _expectedUpdateTicks = 0;
            _lastDetected = 0;
            _hasLastDetected = 0;
        }

        private static uint ResolveMeasureStart(uint acquireStart, uint dropStart)
        {
            if (acquireStart == 0u)
            {
                return dropStart;
            }

            if (dropStart == 0u)
            {
                return acquireStart;
            }

            return math.min(acquireStart, dropStart);
        }

        private static uint ResolveMeasureEnd(uint acquireEnd, uint dropEnd)
        {
            if (acquireEnd == 0u)
            {
                return dropEnd;
            }

            if (dropEnd == 0u)
            {
                return acquireEnd;
            }

            return math.max(acquireEnd, dropEnd);
        }

        private static Entity ResolveCarrier(FixedString64Bytes carrierId, ref SystemState state)
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
