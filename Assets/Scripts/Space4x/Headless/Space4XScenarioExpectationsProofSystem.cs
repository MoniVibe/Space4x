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
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.Telemetry.TelemetryExportSystem))]
    [UpdateBefore(typeof(PureDOTS.Systems.HeadlessExitSystem))]
    public partial struct Space4XScenarioExpectationsProofSystem : ISystem
    {
        private const string ScenarioPathEnv = "SPACE4X_SCENARIO_PATH";
        private const string TelemetryPathEnv = "PUREDOTS_TELEMETRY_PATH";
        private const string TelemetryMaxBytesEnv = "PUREDOTS_TELEMETRY_MAX_BYTES";
        private const string RefitScenarioFile = "space4x_refit.json";
        private const string ResearchScenarioFile = "space4x_research_mvp.json";
        private const ulong DefaultTelemetryMaxBytes = 52428800;
        private const string MetricRefitCount = "space4x.modules.refit.count";
        private const string MetricFieldRepairCount = "space4x.modules.repair.field";
        private const string MetricPowerBalance = "space4x.modules.power.balanceMW";
        private const string MetricResearchHarvest = "space4x.research.harvest.count";
        private const string MetricResearchBandwidthLoss = "space4x.research.bandwidth.loss";

        private bool _enabled;
        private bool _bankLogged;
        private FixedString64Bytes _bankTestId;
        private string _scenarioPath;
        private string _scenarioDirectory;
        private DateTime _runStartUtc;
        private ScenarioTelemetryExpectations _expectations;
        private bool _expectationsLoaded;
        private string _telemetryPath;
        private ulong _telemetryMaxBytes;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<Space4XScenarioRuntime>();
            state.RequireForUpdate<TimeState>();

            _runStartUtc = DateTime.UtcNow;
            _scenarioPath = SystemEnv.GetEnvironmentVariable(ScenarioPathEnv);
            if (string.IsNullOrWhiteSpace(_scenarioPath))
            {
                state.Enabled = false;
                return;
            }

            _scenarioPath = Path.GetFullPath(_scenarioPath);
            _scenarioDirectory = Path.GetDirectoryName(_scenarioPath);
            if (!TryResolveBankTestId(_scenarioPath, out _bankTestId))
            {
                state.Enabled = false;
                return;
            }

            _telemetryPath = SystemEnv.GetEnvironmentVariable(TelemetryPathEnv);
            _telemetryMaxBytes = ResolveTelemetryCap(SystemEnv.GetEnvironmentVariable(TelemetryMaxBytesEnv));
            _expectationsLoaded = TryLoadExpectations(_scenarioPath, out _expectations);
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
            LogBankResult(ref state, _bankTestId, pass, reason, time.Tick);
            _bankLogged = true;
        }

        private bool Evaluate(out string reason)
        {
            reason = string.Empty;
            if (!_expectationsLoaded || _expectations == null)
            {
                reason = "EXPECTATION_FALSE";
                return false;
            }

            var telemetryPath = ResolveTelemetryPath();
            if (!TryValidateTelemetry(telemetryPath, out reason))
            {
                return false;
            }

            if (!TryValidateExports(_expectations.export, out reason))
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
            if (string.IsNullOrWhiteSpace(_telemetryPath))
            {
                return null;
            }

            return Path.GetFullPath(_telemetryPath);
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

        private bool TryValidateExports(ScenarioTelemetryExport export, out string reason)
        {
            reason = string.Empty;
            if (export == null)
            {
                reason = "MISSING_EXPORT";
                return false;
            }

            if (!TryValidateExportPath(export.csv, out reason))
            {
                return false;
            }

            if (!TryValidateExportPath(export.json, out reason))
            {
                return false;
            }

            return true;
        }

        private bool TryValidateExportPath(string exportPath, out string reason)
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

            if (!string.IsNullOrWhiteSpace(_scenarioDirectory))
            {
                var scenarioResolved = Path.GetFullPath(Path.Combine(_scenarioDirectory, exportPath));
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

            reason = anyExists ? "STALE_EXPORT" : "MISSING_EXPORT";
            return false;
        }

        private bool TryValidateExpectations(string telemetryPath, ScenarioTelemetryExpectations expectations, out string reason)
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

                    if (TryGetMetricValue(line, MetricRefitCount, out var refitValue))
                    {
                        sawRefit = true;
                        if (refitValue > maxRefit)
                        {
                            maxRefit = refitValue;
                        }
                    }

                    if (TryGetMetricValue(line, MetricFieldRepairCount, out var repairValue))
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
                if (!sawPowerBalance || !nonNegativePower)
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
            var lastWrite = File.GetLastWriteTimeUtc(path);
            return lastWrite >= _runStartUtc;
        }

        private void LogBankResult(ref SystemState state, FixedString64Bytes testId, bool pass, string reason, uint tick)
        {
            if (testId.IsEmpty)
            {
                return;
            }

            var tickTime = tick;
            if (SystemAPI.TryGetSingleton<TickTimeState>(out var tickTimeState))
            {
                tickTime = tickTimeState.Tick;
            }

            var scenarioTick = SystemAPI.TryGetSingleton<ScenarioRunnerTick>(out var scenario)
                ? scenario.Tick
                : 0u;
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
            if (scenarioPath.EndsWith(RefitScenarioFile, StringComparison.OrdinalIgnoreCase))
            {
                testId = new FixedString64Bytes("S3.REFIT_REPAIR");
                return true;
            }

            if (scenarioPath.EndsWith(ResearchScenarioFile, StringComparison.OrdinalIgnoreCase))
            {
                testId = new FixedString64Bytes("S4.RESEARCH_MVP");
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

        private static bool TryLoadExpectations(string scenarioPath, out ScenarioTelemetryExpectations expectations)
        {
            expectations = null;
            try
            {
                var json = File.ReadAllText(scenarioPath);
                var root = JsonUtility.FromJson<ScenarioRoot>(json);
                expectations = root?.telemetryExpectations;
                return expectations != null;
            }
            catch
            {
                return false;
            }
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
    }
}
