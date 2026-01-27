using PureDOTS.Runtime.Space;
using PureDOTS.Runtime.Components;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Systems.Space
{
    /// <summary>
    /// Emits lightweight console telemetry for module health/repair/power to make ScenarioRunner smoke observable without HUD.
    /// Logs at most once per 60 ticks or when queue/over-budget states change.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CarrierModuleTelemetrySystem))]
    public partial class CarrierModuleConsoleTelemetrySystem : SystemBase
    {
        private const string EnableEnvVar = "PUREDOTS_CARRIER_MODULE_CONSOLE";
        private bool _loggingEnabled;

        protected override void OnCreate()
        {
            RequireForUpdate<CarrierModuleTelemetry>();
            RequireForUpdate<TimeState>();

            if (Application.isBatchMode)
            {
                Enabled = false;
                return;
            }

            _loggingEnabled = IsLoggingEnabled();
            if (!_loggingEnabled)
            {
                Enabled = false;
            }
        }

        protected override void OnUpdate()
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            var telemetry = SystemAPI.GetSingleton<CarrierModuleTelemetry>();

            if (!SystemAPI.TryGetSingletonRW<CarrierModuleTelemetryLogState>(out var logStateRw))
            {
                var entity = EntityManager.CreateEntity(typeof(CarrierModuleTelemetryLogState));
                logStateRw = SystemAPI.GetSingletonRW<CarrierModuleTelemetryLogState>();
            }

            ref var logState = ref logStateRw.ValueRW;
            var tick = time.Tick;
            var shouldLog = telemetry.RepairTicketCount != logState.LastTicketCount
                            || telemetry.AnyOverBudget != logState.LastOverBudget
                            || tick - logState.LastLoggedTick >= 60;

            if (!shouldLog)
            {
                return;
            }

            logState.LastLoggedTick = tick;
            logState.LastTicketCount = telemetry.RepairTicketCount;
            logState.LastOverBudget = telemetry.AnyOverBudget;

            UnityEngine.Debug.Log($"[ModuleTelemetry] carriers={telemetry.CarrierCount} modules: damaged={telemetry.DamagedModules} destroyed={telemetry.DestroyedModules} tickets={telemetry.RepairTicketCount} power(draw/gen/net)=({telemetry.TotalPowerDraw:F1}/{telemetry.TotalPowerGeneration:F1}/{telemetry.NetPower:F1}) overBudget={telemetry.AnyOverBudget}");
        }

        private static bool IsLoggingEnabled()
        {
            var value = global::System.Environment.GetEnvironmentVariable(EnableEnvVar);
            return IsTruthy(value);
        }

        private static bool IsTruthy(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            value = value.Trim();
            return value.Equals("1", System.StringComparison.OrdinalIgnoreCase)
                || value.Equals("true", System.StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", System.StringComparison.OrdinalIgnoreCase)
                || value.Equals("on", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
