using PureDOTS.Runtime.Telemetry;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.TestUtilities
{
    /// <summary>
    /// Mock telemetry stream for testing telemetry publishing without a full system graph.
    /// </summary>
    public class MockTelemetryStream
    {
        private readonly EntityManager _entityManager;
        private readonly Entity _streamEntity;
        private readonly Entity _eventStreamEntity;
        private uint _version;

        public MockTelemetryStream(EntityManager entityManager)
        {
            _entityManager = entityManager;

            // Check if stream already exists
            var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TelemetryStream>());
            if (!query.IsEmptyIgnoreFilter)
            {
                _streamEntity = query.GetSingletonEntity();
            }
            else
            {
                _streamEntity = entityManager.CreateEntity();
                entityManager.AddComponentData(_streamEntity, new TelemetryStream
                {
                    Version = 0,
                    LastTick = 0
                });
                entityManager.AddBuffer<TelemetryMetric>(_streamEntity);
            }

            _eventStreamEntity = TelemetryStreamUtility.EnsureEventStream(_entityManager);
            _version = 0;
        }

        /// <summary>
        /// The stream singleton entity.
        /// </summary>
        public Entity StreamEntity => _streamEntity;

        /// <summary>
        /// The telemetry event stream entity.
        /// </summary>
        public Entity EventStreamEntity => _eventStreamEntity;

        /// <summary>
        /// Current version of the stream.
        /// </summary>
        public uint Version => _version;

        /// <summary>
        /// Publishes a metric to the stream.
        /// </summary>
        public void PublishMetric(string key, float value, TelemetryMetricUnit unit = TelemetryMetricUnit.Count)
        {
            var buffer = _entityManager.GetBuffer<TelemetryMetric>(_streamEntity);
            buffer.AddMetric(new FixedString64Bytes(key), value, unit);
            _version++;
            UpdateStreamState();
        }

        /// <summary>
        /// Tries to get a metric value by key.
        /// </summary>
        public bool TryGetMetric(string key, out float value)
        {
            value = 0f;
            var buffer = _entityManager.GetBuffer<TelemetryMetric>(_streamEntity);
            var keyFs = new FixedString64Bytes(key);

            for (var i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Key.Equals(keyFs))
                {
                    value = buffer[i].Value;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets all metrics with the specified key prefix.
        /// </summary>
        public NativeList<TelemetryMetric> GetMetricsWithPrefix(string prefix, Allocator allocator)
        {
            var result = new NativeList<TelemetryMetric>(allocator);
            var buffer = _entityManager.GetBuffer<TelemetryMetric>(_streamEntity);

            for (var i = 0; i < buffer.Length; i++)
            {
                var metric = buffer[i];
                if (metric.Key.ToString().StartsWith(prefix))
                {
                    result.Add(metric);
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the number of metrics in the stream.
        /// </summary>
        public int MetricCount
        {
            get
            {
                var buffer = _entityManager.GetBuffer<TelemetryMetric>(_streamEntity);
                return buffer.Length;
            }
        }

        /// <summary>
        /// Clears all metrics.
        /// </summary>
        public void Clear()
        {
            var buffer = _entityManager.GetBuffer<TelemetryMetric>(_streamEntity);
            buffer.Clear();
            _version++;
            UpdateStreamState();
        }

        /// <summary>
        /// Gets all metrics as a native array.
        /// </summary>
        public NativeArray<TelemetryMetric> GetAllMetrics(Allocator allocator)
        {
            var buffer = _entityManager.GetBuffer<TelemetryMetric>(_streamEntity);
            var result = new NativeArray<TelemetryMetric>(buffer.Length, allocator);
            buffer.AsNativeArray().CopyTo(result);
            return result;
        }

        private void UpdateStreamState()
        {
            var stream = _entityManager.GetComponentData<TelemetryStream>(_streamEntity);
            stream.Version = _version;
            _entityManager.SetComponentData(_streamEntity, stream);
        }
    }
}
