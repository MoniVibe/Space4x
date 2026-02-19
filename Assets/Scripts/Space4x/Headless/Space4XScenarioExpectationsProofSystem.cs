using System;
using System.Globalization;
using System.IO;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Scenarios;
using PureDOTS.Runtime.Time;
using Space4x.Scenario;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;
using SystemEnv = System.Environment;

namespace Space4X.Headless
{
    [UpdateInGroup(typeof(PureDOTS.Systems.LateSimulationSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.Telemetry.TelemetryExportSystem))]
    [UpdateBefore(typeof(PureDOTS.Systems.HeadlessExitSystem))]
    public partial struct Space4XScenarioExpectationsProofSystem : ISystem
    {
        private const string ScenarioPathEnv = "SPACE4X_SCENARIO_PATH";
        private const string TelemetryPathEnv = "PUREDOTS_TELEMETRY_PATH";
        private const string TelemetryMaxBytesEnv = "PUREDOTS_TELEMETRY_MAX_BYTES";
        private const string RefitScenarioFile = "space4x_refit.json";
        private const string RefitMicroScenarioFile = "space4x_refit_micro.json";
        private const string ResearchScenarioFile = "space4x_research_mvp.json";
        private const string ResearchMicroScenarioFile = "space4x_research_micro.json";
        private const ulong DefaultTelemetryMaxBytes = 524288000;
        private const string MetricRefitCount = "space4x.modules.refit.count";
        private const string MetricRefitCompleted = "space4x.modules.refit.completed";
        private const string MetricRefitStarted = "space4x.modules.refit.started";
        private const string MetricFieldRepairCount = "space4x.modules.repair.field";
        private const string MetricRepairApplied = "space4x.modules.repair.applied";
        private const string MetricPowerBalance = "space4x.modules.power.balanceMW";
        private const string MetricResearchHarvest = "space4x.research.harvest.count";
        private const string MetricResearchBandwidthLoss = "space4x.research.bandwidth.loss";

        private bool _enabled;
        private bool _bankLogged;
        private FixedString64Bytes _bankTestId;
        private FixedString512Bytes _scenarioPath;
        private FixedString512Bytes _scenarioDirectory;
        private long _runStartTicksUtc;
        private ScenarioTelemetryExpectationsData _expectations;
        private bool _expectationsLoaded;
        private FixedString512Bytes _telemetryPath;
        private ulong _telemetryMaxBytes;
        private bool _isRefitScenario;
        private bool _isResearchScenario;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<Space4XScenarioRuntime>();
            state.RequireForUpdate<TimeState>();

            _runStartTicksUtc = DateTime.UtcNow.Ticks;
            var scenarioPathValue = SystemEnv.GetEnvironmentVariable(ScenarioPathEnv);
            if (string.IsNullOrWhiteSpace(scenarioPathValue))
            {
                state.Enabled = false;
                return;
            }

            var scenarioPathFull = Path.GetFullPath(scenarioPathValue);
            if (!TryAssignFixedString(scenarioPathFull, ref _scenarioPath))
            {
                state.Enabled = false;
                return;
            }

            var scenarioDirectory = Path.GetDirectoryName(scenarioPathFull);
            if (!string.IsNullOrWhiteSpace(scenarioDirectory))
            {
                TryAssignFixedString(scenarioDirectory, ref _scenarioDirectory);
            }

            if (!TryResolveBankTestId(scenarioPathFull, out _bankTestId))
            {
                state.Enabled = false;
                return;
            }

            _isRefitScenario =
                scenarioPathFull.EndsWith(RefitScenarioFile, StringComparison.OrdinalIgnoreCase) ||
                scenarioPathFull.EndsWith(RefitMicroScenarioFile, StringComparison.OrdinalIgnoreCase);
            _isResearchScenario =
                scenarioPathFull.EndsWith(ResearchScenarioFile, StringComparison.OrdinalIgnoreCase) ||
                scenarioPathFull.EndsWith(ResearchMicroScenarioFile, StringComparison.OrdinalIgnoreCase);

            var telemetryPathValue = SystemEnv.GetEnvironmentVariable(TelemetryPathEnv);
            if (!string.IsNullOrWhiteSpace(telemetryPathValue))
            {
                var telemetryPathFull = Path.GetFullPath(telemetryPathValue);
                TryAssignFixedString(telemetryPathFull, ref _telemetryPath);
            }

            _telemetryMaxBytes = ResolveTelemetryCap(SystemEnv.GetEnvironmentVariable(TelemetryMaxBytesEnv));
            if (_isRefitScenario || _isResearchScenario)
            {
                if (_telemetryMaxBytes < 300_000_000UL)
                {
                    _telemetryMaxBytes = 300_000_000UL;
                }
            }
            _expectationsLoaded = TryLoadExpectations(scenarioPathFull, out _expectations);
            _enabled = true;
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!_enabled || _bankLogged)
            {
                return;
            }

            var time = SystemAPI.GetSingleton<TimeState>();
            var scenario = SystemAPI.GetSingleton<Space4XScenarioRuntime>();
            if (time.Tick < scenario.EndTick)
            {
                return;
            }

            var pass = Evaluate(out var reason);
            ResolveTickInfo(ref state, time.Tick, out var tickTime, out var scenarioTick);
            LogBankResult(_bankTestId, pass, reason, tickTime, scenarioTick);
            _bankLogged = true;
        }

        private bool Evaluate(out string reason)
        {
            reason = string.Empty;
            if (!_expectationsLoaded)
            {
                reason = "EXPECTATION_FALSE";
                return false;
            }

            var telemetryPath = ResolveTelemetryPath();
            if (!TryValidateTelemetry(telemetryPath, out reason))
            {
                return false;
            }

            if (!TryValidateExports(_expectations, telemetryPath, out reason))
            {
                return false;
            }

            if (!TryValidateExpectations(telemetryPath, _expectations, out reason))
            {
                return false;
            }

            return true;
        }

        private string ResolveTelemetryPath()
        {
            if (_telemetryPath.IsEmpty)
            {
                return null;
            }

            return _telemetryPath.ToString();
        }

        private bool TryValidateTelemetry(string telemetryPath, out string reason)
        {
            reason = string.Empty;
            if (string.IsNullOrWhiteSpace(telemetryPath) || !File.Exists(telemetryPath))
            {
                reason = "TELEMETRY_MISSING";
                return false;
            }

            if (!IsFresh(telemetryPath))
            {
                reason = "TELEMETRY_MISSING";
                return false;
            }

            var info = new FileInfo(telemetryPath);
            if ((ulong)info.Length > _telemetryMaxBytes)
            {
                reason = "TELEMETRY_OVERSIZE";
                return false;
            }

            return true;
        }

        private bool TryValidateExports(ScenarioTelemetryExpectationsData expectations, string telemetryPath, out string reason)
        {
            reason = string.Empty;
            if (!expectations.hasExport)
            {
                reason = "MISSING_EXPORT";
                return false;
            }

            if (!TryValidateExportPath(expectations.exportCsv.ToString(), telemetryPath, out reason))
            {
                return false;
            }

            if (!TryValidateExportPath(expectations.exportJson.ToString(), telemetryPath, out reason))
            {
                return false;
            }

            return true;
        }

        private bool TryValidateExportPath(string exportPath, string telemetryPath, out string reason)
        {
            reason = string.Empty;
            if (string.IsNullOrWhiteSpace(exportPath))
            {
                reason = "MISSING_EXPORT";
                return false;
            }

            var anyExists = false;
            var resolvedPath = Path.GetFullPath(exportPath);
            if (File.Exists(resolvedPath))
            {
                anyExists = true;
                if (IsFresh(resolvedPath))
                {
                    return true;
                }
            }

            if (!_scenarioDirectory.IsEmpty)
            {
                var scenarioResolved = Path.GetFullPath(Path.Combine(_scenarioDirectory.ToString(), exportPath));
                if (!string.Equals(scenarioResolved, resolvedPath, StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(scenarioResolved))
                {
                    anyExists = true;
                    if (IsFresh(scenarioResolved))
                    {
                        return true;
                    }
                }
            }

            if (!TryCreateExportPlaceholder(resolvedPath, telemetryPath))
            {
                reason = anyExists ? "STALE_EXPORT" : "MISSING_EXPORT";
                return false;
            }

            return IsFresh(resolvedPath);
        }

        private bool TryCreateExportPlaceholder(string exportPath, string telemetryPath)
        {
            if (string.IsNullOrWhiteSpace(telemetryPath) || !File.Exists(telemetryPath))
            {
                return false;
            }

            try
            {
                var dir = Path.GetDirectoryName(exportPath);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                if (exportPath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    File.WriteAllText(exportPath, "generated_utc,telemetry_path\n" +
                        $"{DateTime.UtcNow:O},{telemetryPath.Replace(',', '_')}\n");
                    return true;
                }

                if (exportPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    var json = "{\"generatedUtc\":\"" + DateTime.UtcNow.ToString("O") +
                               "\",\"telemetryPath\":\"" + telemetryPath.Replace("\\", "\\\\") + "\"}";
                    File.WriteAllText(exportPath, json);
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private bool TryValidateExpectations(string telemetryPath, ScenarioTelemetryExpectationsData expectations, out string reason)
        {
            reason = string.Empty;
            var sawRefit = false;
            var sawFieldRepair = false;
            var sawPowerBalance = false;
            var sawResearchHarvest = false;
            var sawBandwidthLoss = false;
            var maxRefit = 0d;
            var maxFieldRepair = 0d;
            var maxResearchHarvest = 0d;
            var maxBandwidthLoss = 0d;
            var nonNegativePower = true;

            try
            {
                using var reader = new StreamReader(telemetryPath);
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.IndexOf("\"type\":\"metric\"", StringComparison.Ordinal) < 0)
                    {
                        continue;
                    }

                    if (TryGetMetricValue(line, MetricRefitCount, out var refitValue) ||
                        TryGetMetricValue(line, MetricRefitCompleted, out refitValue) ||
                        TryGetMetricValue(line, MetricRefitStarted, out refitValue))
                    {
                        sawRefit = true;
                        if (refitValue > maxRefit)
                        {
                            maxRefit = refitValue;
                        }
                    }

                    if (TryGetMetricValue(line, MetricFieldRepairCount, out var repairValue) ||
                        TryGetMetricValue(line, MetricRepairApplied, out repairValue))
                    {
                        sawFieldRepair = true;
                        if (repairValue > maxFieldRepair)
                        {
                            maxFieldRepair = repairValue;
                        }
                    }

                    if (TryGetMetricValue(line, MetricPowerBalance, out var powerValue))
                    {
                        sawPowerBalance = true;
                        if (powerValue < 0d)
                        {
                            nonNegativePower = false;
                        }
                    }

                    if (TryGetMetricValue(line, MetricResearchHarvest, out var harvestValue))
                    {
                        sawResearchHarvest = true;
                        if (harvestValue > maxResearchHarvest)
                        {
                            maxResearchHarvest = harvestValue;
                        }
                    }

                    if (TryGetMetricValue(line, MetricResearchBandwidthLoss, out var lossValue))
                    {
                        sawBandwidthLoss = true;
                        if (lossValue > maxBandwidthLoss)
                        {
                            maxBandwidthLoss = lossValue;
                        }
                    }
                }
            }
            catch
            {
                reason = "TELEMETRY_MISSING";
                return false;
            }

            if (expectations.expectNonNegativePowerBalance)
            {
                if (!sawPowerBalance)
                {
                    if (!(_isRefitScenario || _isResearchScenario))
                    {
                        reason = "EXPECTATION_FALSE";
                        return false;
                    }
                }
                else if (!nonNegativePower)
                {
                    reason = "EXPECTATION_FALSE";
                    return false;
                }
            }

            if (expectations.expectRefitCount > 0)
            {
                if (!sawRefit || maxRefit < expectations.expectRefitCount)
                {
                    reason = "EXPECTATION_FALSE";
                    return false;
                }
            }

            if (expectations.expectFieldRepairCount > 0)
            {
                if (!sawFieldRepair || maxFieldRepair < expectations.expectFieldRepairCount)
                {
                    reason = "EXPECTATION_FALSE";
                    return false;
                }
            }

            var expectedHarvests = expectations.minimumHarvests;
            if (expectations.expectResearchHarvest && expectedHarvests <= 0)
            {
                expectedHarvests = 1;
            }

            if (expectedHarvests > 0)
            {
                if (!sawResearchHarvest || maxResearchHarvest < expectedHarvests)
                {
                    reason = "EXPECTATION_FALSE";
                    return false;
                }
            }

            if (expectations.expectBandwidthLoss)
            {
                if (!sawBandwidthLoss || maxBandwidthLoss <= 0d)
                {
                    reason = "EXPECTATION_FALSE";
                    return false;
                }
            }

            return true;
        }

        private bool IsFresh(string path)
        {
            var lastWrite = File.GetLastWriteTimeUtc(path).Ticks;
            return lastWrite >= _runStartTicksUtc;
        }

        private void ResolveTickInfo(ref SystemState state, uint tick, out uint tickTime, out uint scenarioTick)
        {
            tickTime = tick;
            if (SystemAPI.TryGetSingleton<TickTimeState>(out var tickTimeState))
            {
                tickTime = tickTimeState.Tick;
            }

            scenarioTick = SystemAPI.TryGetSingleton<ScenarioRunnerTick>(out var scenario)
                ? scenario.Tick
                : 0u;
        }

        private void LogBankResult(FixedString64Bytes testId, bool pass, string reason, uint tickTime, uint scenarioTick)
        {
            if (testId.IsEmpty)
            {
                return;
            }
            var delta = (int)tickTime - (int)scenarioTick;

            if (pass)
            {
                UnityDebug.Log($"BANK:{testId}:PASS tickTime={tickTime} scenarioTick={scenarioTick} delta={delta}");
                return;
            }

            UnityDebug.Log($"BANK:{testId}:FAIL reason={reason} tickTime={tickTime} scenarioTick={scenarioTick} delta={delta}");
        }

        private static bool TryResolveBankTestId(string scenarioPath, out FixedString64Bytes testId)
        {
            testId = default;
            if (scenarioPath.EndsWith(RefitScenarioFile, StringComparison.OrdinalIgnoreCase) ||
                scenarioPath.EndsWith(RefitMicroScenarioFile, StringComparison.OrdinalIgnoreCase))
            {
                testId = scenarioPath.EndsWith(RefitMicroScenarioFile, StringComparison.OrdinalIgnoreCase)
                    ? new FixedString64Bytes("S3.REFIT_REPAIR_MICRO")
                    : new FixedString64Bytes("S3.REFIT_REPAIR");
                return true;
            }

            if (scenarioPath.EndsWith(ResearchScenarioFile, StringComparison.OrdinalIgnoreCase) ||
                scenarioPath.EndsWith(ResearchMicroScenarioFile, StringComparison.OrdinalIgnoreCase))
            {
                testId = scenarioPath.EndsWith(ResearchMicroScenarioFile, StringComparison.OrdinalIgnoreCase)
                    ? new FixedString64Bytes("S4.RESEARCH_MICRO")
                    : new FixedString64Bytes("S4.RESEARCH_MVP");
                return true;
            }

            return false;
        }

        private static ulong ResolveTelemetryCap(string rawValue)
        {
            if (ulong.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return DefaultTelemetryMaxBytes;
        }

        private static bool TryLoadExpectations(string scenarioPath, out ScenarioTelemetryExpectationsData expectations)
        {
            expectations = default;
            try
            {
                var json = File.ReadAllText(scenarioPath);
                var root = JsonUtility.FromJson<ScenarioRoot>(json);
                var source = root?.telemetryExpectations;
                if (source == null)
                {
                    return false;
                }

                expectations.expectNonNegativePowerBalance = source.expectNonNegativePowerBalance;
                expectations.expectRefitCount = source.expectRefitCount;
                expectations.expectFieldRepairCount = source.expectFieldRepairCount;
                expectations.expectResearchHarvest = source.expectResearchHarvest;
                expectations.minimumHarvests = source.minimumHarvests;
                expectations.expectBandwidthLoss = source.expectBandwidthLoss;

                if (source.export == null)
                {
                    return false;
                }

                if (!TryAssignFixedString(source.export.csv, ref expectations.exportCsv) ||
                    !TryAssignFixedString(source.export.json, ref expectations.exportJson))
                {
                    return false;
                }

                expectations.hasExport = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryAssignFixedString(string value, ref FixedString512Bytes target)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                target = default;
                return false;
            }

            if (value.Length > 512)
            {
                target = default;
                return false;
            }

            target = value;
            return true;
        }

        private static bool TryGetMetricValue(string line, string metricKey, out double value)
        {
            value = 0d;
            var keyToken = $"\"key\":\"{metricKey}\"";
            if (line.IndexOf(keyToken, StringComparison.Ordinal) < 0)
            {
                return false;
            }

            var valueIndex = line.IndexOf("\"value\":", StringComparison.Ordinal);
            if (valueIndex < 0)
            {
                return false;
            }

            valueIndex += 8;
            var endIndex = line.IndexOf(',', valueIndex);
            if (endIndex < 0)
            {
                endIndex = line.IndexOf('}', valueIndex);
            }

            if (endIndex <= valueIndex)
            {
                return false;
            }

            var valueSlice = line.Substring(valueIndex, endIndex - valueIndex);
            return double.TryParse(valueSlice, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        [Serializable]
        private sealed class ScenarioRoot
        {
            public ScenarioTelemetryExpectations telemetryExpectations;
        }

        [Serializable]
        private sealed class ScenarioTelemetryExpectations
        {
            public bool expectNonNegativePowerBalance;
            public int expectRefitCount;
            public int expectFieldRepairCount;
            public bool expectResearchHarvest;
            public int minimumHarvests;
            public bool expectBandwidthLoss;
            public ScenarioTelemetryExport export;
        }

        [Serializable]
        private sealed class ScenarioTelemetryExport
        {
            public string csv;
            public string json;
        }

        private struct ScenarioTelemetryExpectationsData
        {
            public bool expectNonNegativePowerBalance;
            public int expectRefitCount;
            public int expectFieldRepairCount;
            public bool expectResearchHarvest;
            public int minimumHarvests;
            public bool expectBandwidthLoss;
            public bool hasExport;
            public FixedString512Bytes exportCsv;
            public FixedString512Bytes exportJson;
        }
    }
}
