using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Scenarios
{
    /// <summary>
    /// Buffer element storing collected scenario metrics inside the ECS world.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct ScenarioMetricSample : IBufferElementData
    {
        public FixedString64Bytes Key;
        public double Value;
    }

    public enum ScenarioMetricUpdateMode : byte
    {
        Set = 0,
        Add = 1,
        Max = 2
    }

    /// <summary>
    /// Utility helpers for writing/reading scenario metrics from any system.
    /// </summary>
    [BurstCompile]
    public static class ScenarioMetricsUtility
    {
        public static void SetMetric(EntityManager entityManager, FixedString64Bytes key, double value)
            => UpdateMetric(entityManager, key, value, ScenarioMetricUpdateMode.Set);

        public static void AddMetric(EntityManager entityManager, FixedString64Bytes key, double delta)
            => UpdateMetric(entityManager, key, delta, ScenarioMetricUpdateMode.Add);

        /// <summary>
        /// Burst-compatible overload that takes the buffer lookup and scenario entity directly.
        /// Use this in Burst-compiled systems after resolving the lookup and entity once.
        /// </summary>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetMetric(ref BufferLookup<ScenarioMetricSample> metricLookup, in Entity scenarioEntity, in FixedString64Bytes key, double value)
            => UpdateMetric(ref metricLookup, scenarioEntity, key, value, ScenarioMetricUpdateMode.Set);

        /// <summary>
        /// Burst-compatible overload that takes the buffer lookup and scenario entity directly.
        /// Use this in Burst-compiled systems after resolving the lookup and entity once.
        /// </summary>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddMetric(ref BufferLookup<ScenarioMetricSample> metricLookup, in Entity scenarioEntity, in FixedString64Bytes key, double delta)
            => UpdateMetric(ref metricLookup, scenarioEntity, key, delta, ScenarioMetricUpdateMode.Add);

        /// <summary>
        /// Burst-compatible overload that takes the metrics buffer directly.
        /// Use this in Burst-compiled systems after resolving the buffer once.
        /// </summary>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetMetric(ref DynamicBuffer<ScenarioMetricSample> buffer, in FixedString64Bytes key, double value)
            => UpdateMetric(ref buffer, key, value, ScenarioMetricUpdateMode.Set);

        /// <summary>
        /// Burst-compatible overload that takes the metrics buffer directly.
        /// Use this in Burst-compiled systems after resolving the buffer once.
        /// </summary>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddMetric(ref DynamicBuffer<ScenarioMetricSample> buffer, in FixedString64Bytes key, double delta)
            => UpdateMetric(ref buffer, key, delta, ScenarioMetricUpdateMode.Add);

        public static void SetMetricIfUnset(EntityManager entityManager, FixedString64Bytes key, double value)
        {
            if (!TryGetMetricsBuffer(entityManager, out var buffer))
            {
                return;
            }

            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Key.Equals(key))
                {
                    return;
                }
            }

            buffer.Add(new ScenarioMetricSample { Key = key, Value = value });
        }

        /// <summary>
        /// Burst-compatible overload that takes the buffer lookup and scenario entity directly.
        /// </summary>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetMetricIfUnset(ref BufferLookup<ScenarioMetricSample> metricLookup, in Entity scenarioEntity, in FixedString64Bytes key, double value)
        {
            if (scenarioEntity == Entity.Null || !metricLookup.HasBuffer(scenarioEntity))
            {
                return;
            }

            var buffer = metricLookup[scenarioEntity];
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Key.Equals(key))
                {
                    return;
                }
            }

            buffer.Add(new ScenarioMetricSample { Key = key, Value = value });
        }

        public static void UpdateMetric(
            EntityManager entityManager,
            FixedString64Bytes key,
            double value,
            ScenarioMetricUpdateMode mode)
        {
            if (key.Length == 0 || !TryGetMetricsBuffer(entityManager, out var buffer))
            {
                return;
            }

            for (int i = 0; i < buffer.Length; i++)
            {
                if (!buffer[i].Key.Equals(key))
                {
                    continue;
                }

                var sample = buffer[i];
                switch (mode)
                {
                    case ScenarioMetricUpdateMode.Add:
                        sample.Value += value;
                        break;
                    case ScenarioMetricUpdateMode.Max:
                        sample.Value = Unity.Mathematics.math.max(sample.Value, value);
                        break;
                    default:
                        sample.Value = value;
                        break;
                }

                buffer[i] = sample;
                return;
            }

            buffer.Add(new ScenarioMetricSample
            {
                Key = key,
                Value = mode == ScenarioMetricUpdateMode.Add ? value : Unity.Mathematics.math.max(0.0, value)
            });
        }


        /// <summary>
        /// Burst-compatible overload that takes the buffer lookup and scenario entity directly.
        /// Requires the metrics buffer to already exist on the scenario entity.
        /// </summary>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UpdateMetric(
            ref BufferLookup<ScenarioMetricSample> metricLookup,
            in Entity scenarioEntity,
            in FixedString64Bytes key,
            double value,
            ScenarioMetricUpdateMode mode)
        {
            if (key.Length == 0 || scenarioEntity == Entity.Null)
            {
                return;
            }

            if (!metricLookup.HasBuffer(scenarioEntity))
            {
                // Buffer must be created by bootstrap system; cannot create in Burst
                return;
            }

            var buffer = metricLookup[scenarioEntity];
            UpdateMetric(ref buffer, key, value, mode);
        }

        /// <summary>
        /// Burst-compatible overload that takes the metrics buffer directly.
        /// </summary>
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UpdateMetric(
            ref DynamicBuffer<ScenarioMetricSample> buffer,
            in FixedString64Bytes key,
            double value,
            ScenarioMetricUpdateMode mode)
        {
            if (key.Length == 0)
            {
                return;
            }

            for (int i = 0; i < buffer.Length; i++)
            {
                if (!buffer[i].Key.Equals(key))
                {
                    continue;
                }

                var sample = buffer[i];
                switch (mode)
                {
                    case ScenarioMetricUpdateMode.Add:
                        sample.Value += value;
                        break;
                    case ScenarioMetricUpdateMode.Max:
                        sample.Value = Unity.Mathematics.math.max(sample.Value, value);
                        break;
                    default:
                        sample.Value = value;
                        break;
                }

                buffer[i] = sample;
                return;
            }

            buffer.Add(new ScenarioMetricSample
            {
                Key = key,
                Value = mode == ScenarioMetricUpdateMode.Add ? value : Unity.Mathematics.math.max(0.0, value)
            });
        }

        public static bool TryGetMetricsBuffer(EntityManager entityManager, out DynamicBuffer<ScenarioMetricSample> buffer)
        {
            buffer = default;
            var scenarioEntity = ResolveScenarioEntity(entityManager);
            if (scenarioEntity == Entity.Null)
            {
                return false;
            }

            buffer = entityManager.HasBuffer<ScenarioMetricSample>(scenarioEntity)
                ? entityManager.GetBuffer<ScenarioMetricSample>(scenarioEntity)
                : entityManager.AddBuffer<ScenarioMetricSample>(scenarioEntity);
            return true;
        }

        public static void ClearMetrics(EntityManager entityManager)
        {
            if (TryGetMetricsBuffer(entityManager, out var buffer))
            {
                buffer.Clear();
            }
        }

        private static Entity ResolveScenarioEntity(EntityManager entityManager)
        {
            // Try to read from singleton component (populated by ScenarioEntityBootstrapSystem)
            if (entityManager.CreateEntityQuery(typeof(ScenarioEntitySingleton)).TryGetSingleton<ScenarioEntitySingleton>(out var singleton))
            {
                if (singleton.Value != Entity.Null && entityManager.Exists(singleton.Value))
                {
                    return singleton.Value;
                }
            }

            // Fallback: resolve directly (for cases where bootstrap hasn't run yet)
            var fallbackQuery = entityManager.CreateEntityQuery(typeof(ScenarioInfo));
            if (fallbackQuery.IsEmptyIgnoreFilter)
            {
                return Entity.Null;
            }

            return fallbackQuery.GetSingletonEntity();
        }

    }
}
