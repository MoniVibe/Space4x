using System;
using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Telemetry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Runtime.Scenarios
{
    /// <summary>
    /// Serializable scenario definition for headless or in-editor runs.
    /// This shape is intentionally simple so it can be produced from JSON/blob assets.
    /// </summary>
    [Serializable]
    public class ScenarioDefinitionData
    {
        public string scenarioId = "scenario.default";
        public uint seed = 1;
        public int runTicks = 600;
        public ScenarioEntityCountData[] entityCounts = Array.Empty<ScenarioEntityCountData>();
        public ScenarioInputCommandData[] inputCommands = Array.Empty<ScenarioInputCommandData>();
        public ScenarioAssertionData[] assertions = Array.Empty<ScenarioAssertionData>();
        public BehaviorScenarioOverrideData behavior = null;
        public TelemetryScenarioOverrideData telemetry = null;
    }

    [Serializable]
    public class ScenarioEntityCountData
    {
        public string registryId = string.Empty;
        public int count = 0;
    }

    [Serializable]
    public class ScenarioInputCommandData
    {
        public int tick = 0;
        public string commandId = string.Empty;
        public string payload = string.Empty;
    }

    [Serializable]
    public class BehaviorScenarioOverrideData
    {
        public BehaviorScenarioMindData mind = new();
        public BehaviorScenarioGatherData gatherDeliver = new();
        public BehaviorScenarioCarrierData carrier = new();
        public BehaviorScenarioHazardData hazardDodge = new();
        public BehaviorScenarioMovementData movement = new();
        public BehaviorScenarioTelemetryData telemetry = new();
    }

    [Serializable]
    public class BehaviorScenarioMindData
    {
        public int mindCadenceTicks = -1;
        public int aggregateCadenceTicks = -1;
        public int gatherBehaviorBudgetTicks = -1;
        public int hazardBehaviorBudgetTicks = -1;
        public int goalChurnWindowTicks = -1;
        public int goalChurnMaxChanges = -1;
    }

    [Serializable]
    public class BehaviorScenarioGatherData
    {
        public float defaultGatherRatePerSecond = -1f;
        public float carryCapacityOverride = -1f;
        public float returnThresholdPercent = -1f;
        public float storehouseSearchRadius = -1f;
        public float dropoffCooldownSeconds = -1f;
    }

    [Serializable]
    public class BehaviorScenarioCarrierData
    {
        public int depositCadenceTicks = -1;
        public float storehouseBufferRatio = -1f;
        public int carrierIdleTimeoutTicks = -1;
    }

    [Serializable]
    public class BehaviorScenarioHazardData
    {
        public int raycastCooldownTicks = -1;
        public int sampleCount = -1;
        public float highUrgencyThreshold = -1f;
        public int oscillationWindowTicks = -1;
        public int oscillationMaxTransitions = -1;
        public int dodgeDistanceTargetMm = -1;
    }

    [Serializable]
    public class BehaviorScenarioMovementData
    {
        public float arrivalDistance = -1f;
        public float avoidanceBlendWeight = -1f;
        public float throttleRampSeconds = -1f;
    }

    [Serializable]
    public class BehaviorScenarioTelemetryData
    {
        public int aggregateCadenceTicks = -1;
    }

    [Serializable]
    public class TelemetryScenarioOverrideData
    {
        public int enabled = -1;
        public string path = null;
        public string runId = null;
        public string[] flags = null;
        public int cadenceTicks = -1;
        public string lod = null;
        public string[] loops = null;
        public int maxEventsPerTick = -1;
    }

    /// <summary>
    /// Native representation used during scenario execution.
    /// Dispose after use to release temporary allocations.
    /// </summary>
    public struct ResolvedScenario : IDisposable
    {
        public FixedString64Bytes ScenarioId;
        public uint Seed;
        public int RunTicks;
        public NativeList<ScenarioEntityCount> EntityCounts;
        public NativeList<ScenarioInputCommand> InputCommands;
        public NativeList<ScenarioAssertion> Assertions;
        public bool HasBehaviorOverride;
        public BehaviorScenarioOverride BehaviorOverride;
        public bool HasTelemetryOverride;
        public TelemetryScenarioOverride TelemetryOverride;

        public void Dispose()
        {
            if (EntityCounts.IsCreated)
            {
                EntityCounts.Dispose();
            }

            if (InputCommands.IsCreated)
            {
                InputCommands.Dispose();
            }

            if (Assertions.IsCreated)
            {
                Assertions.Dispose();
            }
        }
    }

    public struct ScenarioEntityCount
    {
        public FixedString64Bytes RegistryId;
        public int Count;
    }

    public struct ScenarioInputCommand
    {
        public int Tick;
        public FixedString64Bytes CommandId;
        public FixedString64Bytes Payload;
    }

    public static class ScenarioRunner
    {
        /// <summary>
        /// Stub type alias for ScenarioDefinitionData.
        /// Use ScenarioDefinitionData directly instead.
        /// </summary>
        [Obsolete("Use ScenarioDefinitionData instead")]
        public class ScenarioData : ScenarioDefinitionData
        {
        }

        /// <summary>
        /// Parse JSON text into the serializable scenario data.
        /// </summary>
        public static bool TryParse(string json, out ScenarioDefinitionData data, out FixedString128Bytes error)
        {
            error = default;
            data = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Scenario JSON was empty";
                return false;
            }

            try
            {
                data = UnityEngine.JsonUtility.FromJson<ScenarioDefinitionData>(json);
            }
            catch (Exception ex)
            {
                error = $"Failed to parse scenario JSON: {ex.Message}";
                return false;
            }

            if (data == null)
            {
                error = "Scenario JSON produced no data";
                return false;
            }

            return ValidateData(data, out error);
        }

        /// <summary>
        /// Build a native scenario representation suitable for deterministic execution.
        /// </summary>
        public static bool TryBuild(in ScenarioDefinitionData data, Allocator allocator, out ResolvedScenario scenario, out FixedString128Bytes error)
        {
            scenario = default;
            if (!ValidateData(data, out error))
            {
                return false;
            }

            scenario = new ResolvedScenario
            {
                ScenarioId = new FixedString64Bytes(data.scenarioId ?? "scenario.unnamed"),
                Seed = data.seed,
                RunTicks = math.max(1, data.runTicks),
                EntityCounts = new NativeList<ScenarioEntityCount>(allocator),
                InputCommands = new NativeList<ScenarioInputCommand>(allocator),
                Assertions = new NativeList<ScenarioAssertion>(allocator),
                HasBehaviorOverride = data.behavior != null,
                BehaviorOverride = data.behavior != null
                    ? ConvertBehaviorOverride(data.behavior)
                    : BehaviorScenarioOverride.CreateSentinel(),
                HasTelemetryOverride = data.telemetry != null,
                TelemetryOverride = data.telemetry != null
                    ? ConvertTelemetryOverride(data.telemetry)
                    : TelemetryScenarioOverride.CreateSentinel()
            };

            if (data.entityCounts != null)
            {
                foreach (var entry in data.entityCounts)
                {
                    if (string.IsNullOrWhiteSpace(entry.registryId) || entry.count <= 0)
                    {
                        continue;
                    }

                    scenario.EntityCounts.Add(new ScenarioEntityCount
                    {
                        RegistryId = new FixedString64Bytes(entry.registryId),
                        Count = entry.count
                    });
                }
            }

            if (data.inputCommands != null)
            {
                foreach (var command in data.inputCommands)
                {
                    if (command.tick < 0 || string.IsNullOrWhiteSpace(command.commandId))
                    {
                        continue;
                    }

                    scenario.InputCommands.Add(new ScenarioInputCommand
                    {
                        Tick = command.tick,
                        CommandId = new FixedString64Bytes(command.commandId),
                        Payload = new FixedString64Bytes(command.payload ?? string.Empty)
                    });
                }
            }

            if (data.assertions != null)
            {
                foreach (var assertionData in data.assertions)
                {
                    if (string.IsNullOrWhiteSpace(assertionData.metricId))
                    {
                        continue;
                    }

                    scenario.Assertions.Add(new ScenarioAssertion
                    {
                        MetricId = new FixedString64Bytes(assertionData.metricId),
                        Operator = assertionData.op,
                        ExpectedValue = assertionData.expectedValue,
                        Description = new FixedString128Bytes(assertionData.description ?? string.Empty)
                    });
                }
            }

            return true;
        }

        private static bool ValidateData(ScenarioDefinitionData data, out FixedString128Bytes error)
        {
            error = default;

            if (data.runTicks <= 0)
            {
                error = "Scenario runTicks must be > 0";
                return false;
            }

            if (string.IsNullOrWhiteSpace(data.scenarioId))
            {
                error = "Scenario must have a scenarioId";
                return false;
            }

            return true;
        }

        private static BehaviorScenarioOverride ConvertBehaviorOverride(BehaviorScenarioOverrideData data)
        {
            var overrides = BehaviorScenarioOverride.CreateSentinel();
            if (data == null)
            {
                return overrides;
            }

            if (data.mind != null)
            {
                overrides.Mind.MindCadenceTicks = data.mind.mindCadenceTicks;
                overrides.Mind.AggregateCadenceTicks = data.mind.aggregateCadenceTicks;
                overrides.Mind.GatherBehaviorBudgetTicks = data.mind.gatherBehaviorBudgetTicks;
                overrides.Mind.HazardBehaviorBudgetTicks = data.mind.hazardBehaviorBudgetTicks;
                overrides.Mind.GoalChurnWindowTicks = data.mind.goalChurnWindowTicks;
                overrides.Mind.GoalChurnMaxChanges = data.mind.goalChurnMaxChanges;
            }

            if (data.gatherDeliver != null)
            {
                overrides.GatherDeliver.DefaultGatherRatePerSecond = data.gatherDeliver.defaultGatherRatePerSecond;
                overrides.GatherDeliver.CarryCapacityOverride = data.gatherDeliver.carryCapacityOverride;
                overrides.GatherDeliver.ReturnThresholdPercent = data.gatherDeliver.returnThresholdPercent;
                overrides.GatherDeliver.StorehouseSearchRadius = data.gatherDeliver.storehouseSearchRadius;
                overrides.GatherDeliver.DropoffCooldownSeconds = data.gatherDeliver.dropoffCooldownSeconds;
            }

            if (data.carrier != null)
            {
                overrides.Carrier.DepositCadenceTicks = data.carrier.depositCadenceTicks;
                overrides.Carrier.StorehouseBufferRatio = data.carrier.storehouseBufferRatio;
                overrides.Carrier.CarrierIdleTimeoutTicks = data.carrier.carrierIdleTimeoutTicks;
            }

            if (data.hazardDodge != null)
            {
                overrides.HazardDodge.RaycastCooldownTicks = data.hazardDodge.raycastCooldownTicks;
                overrides.HazardDodge.SampleCount = data.hazardDodge.sampleCount;
                overrides.HazardDodge.HighUrgencyThreshold = data.hazardDodge.highUrgencyThreshold;
                overrides.HazardDodge.OscillationWindowTicks = data.hazardDodge.oscillationWindowTicks;
                overrides.HazardDodge.OscillationMaxTransitions = data.hazardDodge.oscillationMaxTransitions;
                overrides.HazardDodge.DodgeDistanceTargetMm = data.hazardDodge.dodgeDistanceTargetMm;
            }

            if (data.movement != null)
            {
                overrides.Movement.ArrivalDistance = data.movement.arrivalDistance;
                overrides.Movement.AvoidanceBlendWeight = data.movement.avoidanceBlendWeight;
                overrides.Movement.ThrottleRampSeconds = data.movement.throttleRampSeconds;
            }

            if (data.telemetry != null)
            {
                overrides.Telemetry.AggregateCadenceTicks = data.telemetry.aggregateCadenceTicks;
            }

            return overrides;
        }

        private static TelemetryScenarioOverride ConvertTelemetryOverride(TelemetryScenarioOverrideData data)
        {
            var result = TelemetryScenarioOverride.CreateSentinel();
            if (data == null)
            {
                return result;
            }

            if (data.enabled >= 0)
            {
                result.EnabledOverride = (sbyte)(data.enabled > 0 ? 1 : 0);
            }

            if (!string.IsNullOrWhiteSpace(data.path))
            {
                result.OutputPath = new FixedString512Bytes(data.path);
            }

            if (!string.IsNullOrWhiteSpace(data.runId))
            {
                result.RunId = new FixedString128Bytes(data.runId);
            }

            if (data.flags != null && TryParseFlags(data.flags, out var flags))
            {
                result.Flags = flags;
            }

            if (data.cadenceTicks > 0)
            {
                result.CadenceTicks = (uint)data.cadenceTicks;
            }

            if (!string.IsNullOrWhiteSpace(data.lod) && TryParseLod(data.lod, out var lod))
            {
                result.LodOverride = (sbyte)lod;
            }

            if (data.loops != null && TryParseLoops(data.loops, out var loops))
            {
                result.Loops = loops;
            }

            if (data.maxEventsPerTick > 0)
            {
                result.MaxEventsPerTick = (ushort)math.min(data.maxEventsPerTick, ushort.MaxValue);
            }

            return result;
        }

        private static bool TryParseFlags(string[] tokens, out TelemetryExportFlags flags)
        {
            flags = TelemetryExportFlags.None;
            if (tokens == null || tokens.Length == 0)
            {
                return false;
            }

            foreach (var raw in tokens)
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                var token = raw.Trim();
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
                case "proofs":
                    flag = TelemetryExportFlags.IncludeTelemetryEvents;
                    return true;
            }

            return false;
        }

        private static bool TryParseLod(string raw, out TelemetryExportLod lod)
        {
            lod = TelemetryExportLod.Minimal;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            if (byte.TryParse(raw, out var numeric))
            {
                lod = (TelemetryExportLod)numeric;
                return true;
            }

            var token = raw.Trim().ToLowerInvariant();
            switch (token)
            {
                case "minimal":
                    lod = TelemetryExportLod.Minimal;
                    return true;
                case "standard":
                    lod = TelemetryExportLod.Standard;
                    return true;
                case "full":
                    lod = TelemetryExportLod.Full;
                    return true;
            }

            return false;
        }

        private static bool TryParseLoops(string[] tokens, out TelemetryLoopFlags loops)
        {
            loops = TelemetryLoopFlags.None;
            if (tokens == null || tokens.Length == 0)
            {
                return false;
            }

            foreach (var raw in tokens)
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                var token = raw.Trim().ToLowerInvariant();
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
                    case "rewind":
                        loops |= TelemetryLoopFlags.Rewind;
                        break;
                    case "time":
                        loops |= TelemetryLoopFlags.Time;
                        break;
                }
            }

            return loops != TelemetryLoopFlags.None;
        }
    }
}
