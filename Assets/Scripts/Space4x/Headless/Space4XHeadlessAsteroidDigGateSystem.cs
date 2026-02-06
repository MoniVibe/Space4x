using System;
using System.Globalization;
using System;
using System.IO;
using PureDOTS.Environment;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Time;
using Space4x.Scenario;
using Unity.Entities;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Headless
{
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.Telemetry.TelemetryExportSystem))]
    [UpdateBefore(typeof(Space4XHeadlessDiagnosticsSystem))]
    [UpdateBefore(typeof(PureDOTS.Systems.HeadlessExitSystem))]
    public partial class Space4XHeadlessAsteroidDigGateSystem : SystemBase
    {
        private const string SessionDirEnv = "SPACE4X_DIG_GATE_SESSION_DIR";
        private const string DefaultSessionDir = @"C:\polish\queue\reports\session_20260108_workblock";
        private const string SummaryFileName = "asteroid_dig_gate_summary.json";
        private const string ScenarioPathEnv = "SPACE4X_SCENARIO_PATH";
        private const string ScenarioSourcePathEnv = "SPACE4X_SCENARIO_SOURCE_PATH";
        private const string MiningProofEnv = "SPACE4X_HEADLESS_MINING_PROOF";
        private const string FtlMicroScenarioFile = "space4x_ftl_micro.json";
        private const string CollisionMicroScenarioFile = "space4x_collision_micro.json";
        private const string DogfightScenarioFile = "space4x_dogfight_headless.json";

        private byte _done;
        private byte _sawDig;
        private int _lastEventCount;
        private string _summaryPath;

        // Compile-time guard: UpdateInGroup discovery expects ComponentSystemBase here.
        private static ComponentSystemBase SystemBaseGuard(Space4XHeadlessAsteroidDigGateSystem system) => system;

        protected override void OnCreate()
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                Enabled = false;
                return;
            }

            RequireForUpdate<TimeState>();
            RequireForUpdate<Space4XScenarioRuntime>();
            RequireForUpdate<TerrainModificationQueue>();

            var miningProof = System.Environment.GetEnvironmentVariable(MiningProofEnv);
            if (string.IsNullOrWhiteSpace(miningProof) ||
                miningProof.Trim().Equals("0", StringComparison.OrdinalIgnoreCase) ||
                miningProof.Trim().Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                Enabled = false;
                return;
            }

            var scenarioPath = System.Environment.GetEnvironmentVariable(ScenarioSourcePathEnv);
            if (string.IsNullOrWhiteSpace(scenarioPath))
            {
                scenarioPath = System.Environment.GetEnvironmentVariable(ScenarioPathEnv);
            }
            if (!string.IsNullOrWhiteSpace(scenarioPath) &&
                (scenarioPath.EndsWith(FtlMicroScenarioFile, StringComparison.OrdinalIgnoreCase) ||
                 scenarioPath.EndsWith(CollisionMicroScenarioFile, StringComparison.OrdinalIgnoreCase) ||
                 scenarioPath.EndsWith(DogfightScenarioFile, StringComparison.OrdinalIgnoreCase)))
            {
                Enabled = false;
                return;
            }

            var sessionDir = System.Environment.GetEnvironmentVariable(SessionDirEnv);
            if (string.IsNullOrWhiteSpace(sessionDir))
            {
                sessionDir = DefaultSessionDir;
            }

            _summaryPath = Path.Combine(Path.GetFullPath(sessionDir), SummaryFileName);
        }

        protected override void OnUpdate()
        {
            if (_done != 0)
            {
                return;
            }

            TrackDigEvents();

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var scenario = SystemAPI.GetSingleton<Space4XScenarioRuntime>();
            if (timeState.Tick < scenario.EndTick)
            {
                return;
            }

            _done = 1;

            if (_sawDig != 0)
            {
                return;
            }

            var observed = $"saw_dig={_sawDig} events_seen={_lastEventCount}";
            Space4XHeadlessDiagnostics.ReportInvariant(
                "INV-DIG-GATE",
                "No terrain dig events observed before scenario end.",
                observed,
                "saw_dig=1");
            WriteFailureSummary(timeState.Tick);
            HeadlessExitUtility.Request(EntityManager, timeState.Tick, Space4XHeadlessDiagnostics.TestFailExitCode);
        }

        private void TrackDigEvents()
        {
            if (_sawDig != 0)
            {
                return;
            }

            if (!SystemAPI.TryGetSingletonEntity<TerrainModificationQueue>(out var queueEntity) ||
                !EntityManager.HasBuffer<TerrainModificationEvent>(queueEntity))
            {
                return;
            }

            var events = EntityManager.GetBuffer<TerrainModificationEvent>(queueEntity);
            if (events.Length < _lastEventCount)
            {
                _lastEventCount = 0;
            }

            for (int i = _lastEventCount; i < events.Length; i++)
            {
                if (events[i].ClearedVoxels > 0)
                {
                    _sawDig = 1;
                    break;
                }
            }

            _lastEventCount = events.Length;
        }

        private void WriteFailureSummary(uint tick)
        {
            if (string.IsNullOrWhiteSpace(_summaryPath))
            {
                return;
            }

            try
            {
                var dir = Path.GetDirectoryName(_summaryPath);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var json = "{\"status\":\"FAIL\",\"reason\":\"NO_TERRAIN_DIG\",\"tick\":" +
                           tick.ToString(CultureInfo.InvariantCulture) + "}";
                File.WriteAllText(_summaryPath, json);
            }
            catch
            {
            }
        }
    }
}
