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
    public partial struct Space4XHeadlessAsteroidDigGateSystem : ISystem
    {
        private const string SessionDirEnv = "SPACE4X_DIG_GATE_SESSION_DIR";
        private const string DefaultSessionDir = @"C:\polish\queue\reports\session_20260108_workblock";
        private const string SummaryFileName = "asteroid_dig_gate_summary.json";

        private byte _done;
        private string _summaryPath;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XScenarioRuntime>();
            state.RequireForUpdate<TerrainModificationQueue>();

            var sessionDir = System.Environment.GetEnvironmentVariable(SessionDirEnv);
            if (string.IsNullOrWhiteSpace(sessionDir))
            {
                sessionDir = DefaultSessionDir;
            }

            _summaryPath = Path.Combine(Path.GetFullPath(sessionDir), SummaryFileName);
        }

        public void OnUpdate(ref SystemState state)
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
                state.EntityManager.HasBuffer<TerrainModificationEvent>(queueEntity))
            {
                var events = state.EntityManager.GetBuffer<TerrainModificationEvent>(queueEntity);
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
            HeadlessExitUtility.Request(state.EntityManager, timeState.Tick, Space4XHeadlessDiagnostics.TestFailExitCode);
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
