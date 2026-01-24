using System.Globalization;
using System.IO;
using PureDOTS.Environment;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Time;
using Space4x.Scenario;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Headless
{
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.Telemetry.TelemetryExportSystem))]
    [UpdateBefore(typeof(PureDOTS.Systems.HeadlessExitSystem))]
    public partial class Space4XHeadlessAsteroidDigGateSystem : SystemBase
    {
        private const string SessionDirEnv = "SPACE4X_DIG_GATE_SESSION_DIR";
        private const string DefaultSessionDir = @"C:\polish\queue\reports\session_20260108_workblock";
        private const string SummaryFileName = "asteroid_dig_gate_summary.json";

        private byte _done;
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

            var miningProof = System.Environment.GetEnvironmentVariable("SPACE4X_HEADLESS_MINING_PROOF");
            if (string.Equals(miningProof, "0", StringComparison.OrdinalIgnoreCase))
            {
                Enabled = false;
                return;
            }

            RequireForUpdate<TimeState>();
            RequireForUpdate<Space4XScenarioRuntime>();
            RequireForUpdate<TerrainModificationQueue>();

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

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var scenario = SystemAPI.GetSingleton<Space4XScenarioRuntime>();
            if (timeState.Tick < scenario.EndTick)
            {
                return;
            }

            _done = 1;

            var digOccurred = false;
            if (SystemAPI.TryGetSingletonEntity<TerrainModificationQueue>(out var queueEntity) &&
                EntityManager.HasBuffer<TerrainModificationEvent>(queueEntity))
            {
                var events = EntityManager.GetBuffer<TerrainModificationEvent>(queueEntity);
                for (int i = 0; i < events.Length; i++)
                {
                    if (events[i].ClearedVoxels > 0)
                    {
                        digOccurred = true;
                        break;
                    }
                }
            }

            if (digOccurred)
            {
                return;
            }

            WriteFailureSummary(timeState.Tick);
            HeadlessExitUtility.Request(EntityManager, timeState.Tick, Space4XHeadlessDiagnostics.TestFailExitCode);
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
