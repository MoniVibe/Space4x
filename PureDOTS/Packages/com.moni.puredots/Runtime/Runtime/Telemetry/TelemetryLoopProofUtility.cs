using System.Globalization;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Telemetry
{
    /// <summary>
    /// Shared helper for emitting compact headless loop proof events.
    /// </summary>
    public static class TelemetryLoopProofUtility
    {
        private static readonly FixedString64Bytes s_EventType = new FixedString64Bytes("loop_proof");
        private static readonly FixedString64Bytes s_DefaultSource = new FixedString64Bytes("headless");

        public static void Emit(
            EntityManager entityManager,
            uint tick,
            in FixedString64Bytes loopId,
            bool success,
            float observed,
            in FixedString32Bytes expected,
            uint timeoutTicks,
            in FixedString64Bytes source = default,
            in FixedString32Bytes step = default)
        {
            if (!TryGetExportConfig(entityManager, out var config))
            {
                return;
            }

            if (config.Enabled == 0 || (config.Flags & TelemetryExportFlags.IncludeTelemetryEvents) == 0)
            {
                return;
            }

            if (!IsLoopEnabled(config, loopId))
            {
                return;
            }

            var streamEntity = TelemetryStreamUtility.EnsureEventStream(entityManager);
            if (!entityManager.HasBuffer<TelemetryEvent>(streamEntity))
            {
                return;
            }

            var payload = new FixedString128Bytes();
            payload.Append('{');
            payload.Append("\"l\":\"");
            Append(ref payload, loopId);
            payload.Append('\"');
            if (step.Length > 0)
            {
                payload.Append(",\"k\":\"");
                Append(ref payload, step);
                payload.Append('\"');
            }

            payload.Append(",\"s\":");
            payload.Append(success ? 1 : 0);
            payload.Append(",\"o\":");
            payload.Append(observed.ToString("0.###", CultureInfo.InvariantCulture));
            if (expected.Length > 0)
            {
                payload.Append(",\"e\":\"");
                Append(ref payload, expected);
                payload.Append('\"');
            }

            if (timeoutTicks > 0)
            {
                payload.Append(",\"w\":");
                payload.Append(timeoutTicks);
            }

            payload.Append('}');

            var buffer = entityManager.GetBuffer<TelemetryEvent>(streamEntity);
            var resolvedSource = source.Length > 0 ? source : s_DefaultSource;
            buffer.AddEvent(s_EventType, tick, resolvedSource, payload);
        }

        private static bool TryGetExportConfig(EntityManager entityManager, out TelemetryExportConfig config)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TelemetryExportConfig>());
            if (query.IsEmptyIgnoreFilter)
            {
                config = default;
                return false;
            }

            config = query.GetSingleton<TelemetryExportConfig>();
            return true;
        }

        private static bool IsLoopEnabled(in TelemetryExportConfig config, in FixedString64Bytes loopId)
        {
            if (loopId.Equals(TelemetryLoopIds.Extract))
            {
                return (config.Loops & TelemetryLoopFlags.Extract) != 0;
            }

            if (loopId.Equals(TelemetryLoopIds.Logistics))
            {
                return (config.Loops & TelemetryLoopFlags.Logistics) != 0;
            }

            if (loopId.Equals(TelemetryLoopIds.Construction))
            {
                return (config.Loops & TelemetryLoopFlags.Construction) != 0;
            }

            if (loopId.Equals(TelemetryLoopIds.Exploration))
            {
                return (config.Loops & TelemetryLoopFlags.Exploration) != 0;
            }

            if (loopId.Equals(TelemetryLoopIds.Combat))
            {
                return (config.Loops & TelemetryLoopFlags.Combat) != 0;
            }

            if (loopId.Equals(TelemetryLoopIds.Rewind))
            {
                return (config.Loops & TelemetryLoopFlags.Rewind) != 0;
            }

            if (loopId.Equals(TelemetryLoopIds.Time))
            {
                return (config.Loops & TelemetryLoopFlags.Time) != 0;
            }

            return true;
        }

        private static void Append(ref FixedString128Bytes target, in FixedString64Bytes value)
        {
            for (int i = 0; i < value.Length && target.Length < target.Capacity; i++)
            {
                target.Append((char)value[i]);
            }
        }

        private static void Append(ref FixedString128Bytes target, in FixedString32Bytes value)
        {
            for (int i = 0; i < value.Length && target.Length < target.Capacity; i++)
            {
                target.Append((char)value[i]);
            }
        }
    }
}
