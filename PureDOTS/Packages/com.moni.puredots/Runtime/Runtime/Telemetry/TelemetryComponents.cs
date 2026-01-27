using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Telemetry
{
    /// <summary>
    /// Units associated with telemetry metric values to assist downstream formatting.
    /// </summary>
    public enum TelemetryMetricUnit : byte
    {
        Count = 0,
        Ratio = 1,
        DurationMilliseconds = 2,
        Bytes = 3,
        None = 254,
        Custom = 255
    }

    /// <summary>
    /// Dynamic buffer element capturing a scalar telemetry reading.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct TelemetryMetric : IBufferElementData
    {
        public FixedString64Bytes Key;
        public float Value;
        public TelemetryMetricUnit Unit;
    }

    /// <summary>
    /// Singleton tagging the active telemetry stream with versioning for change detection.
    /// </summary>
    public struct TelemetryStream : IComponentData
    {
        public uint Version;
        public uint LastTick;
    }

    /// <summary>
    /// Tag identifying the entity that holds telemetry event buffers.
    /// </summary>
    public struct TelemetryStreamTag : IComponentData
    {
    }

    /// <summary>
    /// Singleton reference pointing to the telemetry event stream entity.
    /// </summary>
    public struct TelemetryStreamSingleton : IComponentData
    {
        public Entity Stream;
    }

    /// <summary>
    /// Telemetry event payload written to the NDJSON export for high-signal state changes.
    /// Payload is expected to be a compact JSON object describing event-specific fields.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct TelemetryEvent : IBufferElementData
    {
        public FixedString64Bytes EventType;
        public uint Tick;
        public FixedString64Bytes Source;
        public FixedString128Bytes Payload;
    }

    /// <summary>
    /// Helper extensions for appending metrics/events without repeated boilerplate.
    /// </summary>
    public static class TelemetryBufferExtensions
    {
        public static void AddMetric(this DynamicBuffer<TelemetryMetric> buffer, in FixedString64Bytes key, float value, TelemetryMetricUnit unit = TelemetryMetricUnit.Count)
        {
            buffer.Add(new TelemetryMetric
            {
                Key = key,
                Value = value,
                Unit = unit
            });
        }

        public static void AddEvent(this DynamicBuffer<TelemetryEvent> buffer, in FixedString64Bytes eventType, uint tick, in FixedString64Bytes source, in FixedString128Bytes payload)
        {
            buffer.Add(new TelemetryEvent
            {
                EventType = eventType,
                Tick = tick,
                Source = source,
                Payload = payload
            });
        }
    }

    /// <summary>
    /// Utility helpers for ensuring telemetry stream infrastructure exists.
    /// </summary>
    public static class TelemetryStreamUtility
    {
        private static readonly ComponentType s_TelemetryTagType = ComponentType.ReadWrite<TelemetryStreamTag>();
        private static readonly ComponentType s_TelemetryEventBufferType = ComponentType.ReadWrite<TelemetryEvent>();

        public static Entity EnsureEventStream(EntityManager entityManager)
        {
            var resolvedEntity = ResolveExistingEventStream(entityManager);
            var needsDedicatedStream = !IsValidDedicatedEventStream(entityManager, resolvedEntity);

            if (needsDedicatedStream)
            {
                var legacyEntity = resolvedEntity;
                resolvedEntity = CreateDedicatedEventStreamEntity(entityManager);
                CleanupLegacyEventComponents(entityManager, legacyEntity);
            }
            else if (!entityManager.HasBuffer<TelemetryEvent>(resolvedEntity))
            {
                entityManager.AddBuffer<TelemetryEvent>(resolvedEntity);
            }

            EnsureSingletonReference(entityManager, resolvedEntity);
            return resolvedEntity;
        }

        private static Entity ResolveExistingEventStream(EntityManager entityManager)
        {
            using var singletonQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TelemetryStreamSingleton>());
            if (!singletonQuery.IsEmptyIgnoreFilter)
            {
                var singletonEntity = singletonQuery.GetSingletonEntity();
                var singleton = entityManager.GetComponentData<TelemetryStreamSingleton>(singletonEntity);
                if (singleton.Stream != Entity.Null && entityManager.Exists(singleton.Stream))
                {
                    return singleton.Stream;
                }
            }

            using var tagQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TelemetryStreamTag>());
            if (!tagQuery.IsEmptyIgnoreFilter)
            {
                return tagQuery.GetSingletonEntity();
            }

            return Entity.Null;
        }

        private static bool IsValidDedicatedEventStream(EntityManager entityManager, Entity entity)
        {
            if (entity == Entity.Null || !entityManager.Exists(entity))
            {
                return false;
            }

            using var componentTypes = entityManager.GetComponentTypes(entity, Allocator.Temp);
            for (int i = 0; i < componentTypes.Length; i++)
            {
                var typeIndex = componentTypes[i].TypeIndex;
                if (typeIndex == s_TelemetryTagType.TypeIndex || typeIndex == s_TelemetryEventBufferType.TypeIndex)
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private static Entity CreateDedicatedEventStreamEntity(EntityManager entityManager)
        {
            var eventEntity = entityManager.CreateEntity();
            entityManager.AddComponent<TelemetryStreamTag>(eventEntity);
            entityManager.AddBuffer<TelemetryEvent>(eventEntity);
            return eventEntity;
        }

        private static void CleanupLegacyEventComponents(EntityManager entityManager, Entity legacyEntity)
        {
            if (legacyEntity == Entity.Null || !entityManager.Exists(legacyEntity))
            {
                return;
            }

            if (entityManager.HasComponent<TelemetryEvent>(legacyEntity))
            {
                entityManager.RemoveComponent<TelemetryEvent>(legacyEntity);
            }

            if (entityManager.HasComponent<TelemetryStreamTag>(legacyEntity))
            {
                entityManager.RemoveComponent<TelemetryStreamTag>(legacyEntity);
            }
        }

        private static void EnsureSingletonReference(EntityManager entityManager, Entity eventEntity)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TelemetryStreamSingleton>());

            if (query.IsEmptyIgnoreFilter)
            {
                var singleton = entityManager.CreateEntity(typeof(TelemetryStreamSingleton));
                entityManager.SetComponentData(singleton, new TelemetryStreamSingleton
                {
                    Stream = eventEntity
                });
            }
            else
            {
                var singleton = query.GetSingletonEntity();
                var data = entityManager.GetComponentData<TelemetryStreamSingleton>(singleton);
                if (data.Stream != eventEntity)
                {
                    data.Stream = eventEntity;
                    entityManager.SetComponentData(singleton, data);
                }
            }
        }
    }
}
