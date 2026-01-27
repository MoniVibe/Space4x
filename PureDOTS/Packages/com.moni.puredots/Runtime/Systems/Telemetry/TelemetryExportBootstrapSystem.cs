using System;
using System.IO;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Systems;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Systems.Telemetry
{
    /// <summary>
    /// Enables telemetry export automatically in headless or env-var controlled runs.
    /// </summary>
    [UpdateInGroup(typeof(TimeSystemGroup))]
    [UpdateAfter(typeof(CoreSingletonBootstrapSystem))]
    public partial struct TelemetryExportBootstrapSystem : ISystem
    {
        private bool _initialized;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TelemetryExportConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_initialized)
            {
                return;
            }

            var configRW = SystemAPI.GetSingletonRW<TelemetryExportConfig>();
            if (configRW.ValueRO.Enabled != 0 && configRW.ValueRO.OutputPath.Length > 0)
            {
                _initialized = true;
                return;
            }

            var enableValue = GetEnv("PUREDOTS_TELEMETRY_ENABLE");
            var shouldEnable = Application.isBatchMode || IsTruthy(enableValue);
            if (!shouldEnable)
            {
                _initialized = true;
                return;
            }

            var runIdValue = GetEnv("PUREDOTS_TELEMETRY_RUN_ID");
            var pathValue = GetEnv("PUREDOTS_TELEMETRY_PATH");
            var flagsValue = GetEnv("PUREDOTS_TELEMETRY_FLAGS");
            var cadenceValue = GetEnv("PUREDOTS_TELEMETRY_CADENCE_TICKS");
            var lodValue = GetEnv("PUREDOTS_TELEMETRY_LOD");
            var loopsValue = GetEnv("PUREDOTS_TELEMETRY_LOOPS");
            var maxEventsValue = GetEnv("PUREDOTS_TELEMETRY_MAX_EVENTS_PER_TICK");
            var levelValue = GetEnv("PUREDOTS_TELEMETRY_LEVEL");
            var maxBytesValue = GetEnv("PUREDOTS_TELEMETRY_MAX_BYTES");

            var flags = configRW.ValueRO.Flags;
            var level = ResolveLevel(levelValue);
            if (Application.isBatchMode
                && shouldEnable
                && level == TelemetryExportLevel.Unspecified
                && string.IsNullOrEmpty(flagsValue))
            {
                level = TelemetryExportLevel.Summary;
            }
            if (level == TelemetryExportLevel.Summary && string.IsNullOrEmpty(flagsValue))
            {
                flags = TelemetryExportFlags.IncludeTelemetryMetrics | TelemetryExportFlags.IncludeFrameTiming;
            }
            else if (level == TelemetryExportLevel.Full && string.IsNullOrEmpty(flagsValue))
            {
                flags = TelemetryExportFlags.IncludeTelemetryMetrics |
                        TelemetryExportFlags.IncludeFrameTiming |
                        TelemetryExportFlags.IncludeBehaviorTelemetry |
                        TelemetryExportFlags.IncludeReplayEvents |
                        TelemetryExportFlags.IncludeTelemetryEvents;
            }

            if (!string.IsNullOrEmpty(flagsValue) && TryParseFlags(flagsValue, out var parsedFlags))
            {
                flags = parsedFlags;
            }

            FixedString128Bytes runId = configRW.ValueRO.RunId;
            if (!string.IsNullOrEmpty(runIdValue))
            {
                runId = new FixedString128Bytes(runIdValue);
            }
            else if (runId.Length == 0)
            {
                runId = GenerateRunId();
            }

            if (string.IsNullOrEmpty(pathValue))
            {
                pathValue = BuildDefaultPath(runId.ToString());
            }

            configRW.ValueRW.OutputPath = new FixedString512Bytes(pathValue);
            configRW.ValueRW.RunId = runId;
            configRW.ValueRW.Flags = flags;
            configRW.ValueRW.CadenceTicks = ResolveCadence(configRW.ValueRO.CadenceTicks, cadenceValue);
            configRW.ValueRW.Lod = ResolveLod(ResolveLodFromLevel(configRW.ValueRO.Lod, level), lodValue);
            configRW.ValueRW.Loops = ResolveLoops(configRW.ValueRO.Loops, loopsValue);
            configRW.ValueRW.MaxEventsPerTick = ResolveMaxEvents(configRW.ValueRO.MaxEventsPerTick, maxEventsValue);
            configRW.ValueRW.MaxOutputBytes = ResolveMaxOutputBytes(configRW.ValueRO.MaxOutputBytes, maxBytesValue);
            configRW.ValueRW.Enabled = 1;
            configRW.ValueRW.Version++;
            _initialized = true;
        }

        private static FixedString128Bytes GenerateRunId()
        {
            FixedString128Bytes id = default;
            var guid = Guid.NewGuid().ToString("N");
            for (int i = 0; i < guid.Length && i < id.Capacity; i++)
            {
                id.Append(guid[i]);
            }
            return id;
        }

        private static string BuildDefaultPath(string runId)
        {
            var basePath = Application.persistentDataPath;
            if (string.IsNullOrEmpty(basePath))
            {
                basePath = ".";
            }

            return Path.Combine(basePath, "telemetry", $"telemetry_{runId}.ndjson");
        }

        private static string GetEnv(string key)
        {
            return global::System.Environment.GetEnvironmentVariable(key);
        }

        private static bool IsTruthy(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            value = value.Trim();
            return value.Equals("1", StringComparison.OrdinalIgnoreCase)
                || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || value.Equals("on", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseFlags(string value, out TelemetryExportFlags flags)
        {
            flags = TelemetryExportFlags.None;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            if (int.TryParse(value, out var numeric))
            {
                flags = (TelemetryExportFlags)numeric;
                return true;
            }

            var parts = value.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return false;
            }

            foreach (var part in parts)
            {
                var token = part.Trim();
                if (Enum.TryParse(token, true, out TelemetryExportFlags parsed))
                {
                    flags |= parsed;
                    continue;
                }

                if (TryMapFlagAlias(token, out parsed))
                {
                    flags |= parsed;
                }
            }

            return flags != TelemetryExportFlags.None;
        }

        private static bool TryMapFlagAlias(string token, out TelemetryExportFlags flag)
        {
            flag = TelemetryExportFlags.None;
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            token = token.Trim().ToLowerInvariant();
            switch (token)
            {
                case "metrics":
                    flag = TelemetryExportFlags.IncludeTelemetryMetrics;
                    return true;
                case "frame":
                case "frametiming":
                    flag = TelemetryExportFlags.IncludeFrameTiming;
                    return true;
                case "behavior":
                    flag = TelemetryExportFlags.IncludeBehaviorTelemetry;
                    return true;
                case "replay":
                    flag = TelemetryExportFlags.IncludeReplayEvents;
                    return true;
                case "events":
                case "telemetryevents":
                    flag = TelemetryExportFlags.IncludeTelemetryEvents;
                    return true;
                case "proofs":
                    flag = TelemetryExportFlags.IncludeTelemetryEvents;
                    return true;
            }

            return false;
        }

        private static uint ResolveCadence(uint currentCadence, string cadenceValue)
        {
            var cadence = currentCadence > 0 ? currentCadence : 30u;
            if (!string.IsNullOrEmpty(cadenceValue) && uint.TryParse(cadenceValue, out var parsed) && parsed > 0)
            {
                cadence = parsed;
            }

            return cadence;
        }

        private static TelemetryExportLod ResolveLod(TelemetryExportLod currentLod, string lodValue)
        {
            var lod = currentLod;
            if (string.IsNullOrEmpty(lodValue))
            {
                return lod;
            }

            if (byte.TryParse(lodValue, out var numeric))
            {
                lod = (TelemetryExportLod)numeric;
                return lod;
            }

            var token = lodValue.Trim().ToLowerInvariant();
            switch (token)
            {
                case "minimal":
                    return TelemetryExportLod.Minimal;
                case "standard":
                    return TelemetryExportLod.Standard;
                case "full":
                    return TelemetryExportLod.Full;
                default:
                    return lod;
            }
        }

        private static TelemetryExportLevel ResolveLevel(string levelValue)
        {
            if (string.IsNullOrEmpty(levelValue))
            {
                return TelemetryExportLevel.Unspecified;
            }

            var token = levelValue.Trim().ToLowerInvariant();
            switch (token)
            {
                case "summary":
                case "thin":
                case "minimal":
                    return TelemetryExportLevel.Summary;
                case "full":
                case "verbose":
                    return TelemetryExportLevel.Full;
                default:
                    return TelemetryExportLevel.Unspecified;
            }
        }

        private static TelemetryExportLod ResolveLodFromLevel(TelemetryExportLod currentLod, TelemetryExportLevel level)
        {
            return level switch
            {
                TelemetryExportLevel.Summary => TelemetryExportLod.Minimal,
                TelemetryExportLevel.Full => TelemetryExportLod.Full,
                _ => currentLod
            };
        }

        private static TelemetryLoopFlags ResolveLoops(TelemetryLoopFlags currentLoops, string loopsValue)
        {
            var loops = currentLoops == TelemetryLoopFlags.None ? TelemetryLoopFlags.All : currentLoops;
            if (string.IsNullOrEmpty(loopsValue))
            {
                return loops;
            }

            if (int.TryParse(loopsValue, out var numeric))
            {
                return (TelemetryLoopFlags)numeric;
            }

            var parts = loopsValue.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return loops;
            }

            loops = TelemetryLoopFlags.None;
            foreach (var part in parts)
            {
                var token = part.Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(token))
                {
                    continue;
                }

                switch (token)
                {
                    case "all":
                        loops |= TelemetryLoopFlags.All;
                        break;
                    case "extract":
                        loops |= TelemetryLoopFlags.Extract;
                        break;
                    case "logistics":
                        loops |= TelemetryLoopFlags.Logistics;
                        break;
                    case "construction":
                        loops |= TelemetryLoopFlags.Construction;
                        break;
                    case "exploration":
                        loops |= TelemetryLoopFlags.Exploration;
                        break;
                    case "combat":
                        loops |= TelemetryLoopFlags.Combat;
                        break;
                }
            }

            if (loops == TelemetryLoopFlags.None)
            {
                loops = TelemetryLoopFlags.All;
            }

            return loops;
        }

        private static ulong ResolveMaxOutputBytes(ulong currentMaxBytes, string maxBytesValue)
        {
            if (string.IsNullOrEmpty(maxBytesValue))
            {
                return currentMaxBytes;
            }

            if (ulong.TryParse(maxBytesValue, out var parsed))
            {
                return parsed;
            }

            return currentMaxBytes;
        }

        private enum TelemetryExportLevel : byte
        {
            Unspecified = 0,
            Summary = 1,
            Full = 2
        }

        private static ushort ResolveMaxEvents(ushort currentMax, string maxEventsValue)
        {
            var maxEvents = currentMax > 0 ? currentMax : (ushort)64;
            if (!string.IsNullOrEmpty(maxEventsValue) && int.TryParse(maxEventsValue, out var parsed) && parsed > 0)
            {
                maxEvents = (ushort)Math.Min(parsed, ushort.MaxValue);
            }

            return maxEvents;
        }
    }
}
