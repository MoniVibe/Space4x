using PureDOTS.Runtime.Telemetry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Publishes module maintenance counters to telemetry.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XFieldRepairSystem))]
    public partial struct Space4XModuleMaintenanceTelemetrySystem : ISystem
    {
        private static readonly FixedString64Bytes MetricRefitStarted = "space4x.modules.refit.started";
        private static readonly FixedString64Bytes MetricRefitCompleted = "space4x.modules.refit.completed";
        private static readonly FixedString64Bytes MetricRefitWork = "space4x.modules.refit.work";
        private static readonly FixedString64Bytes MetricRepairApplied = "space4x.modules.repair.applied";
        private static readonly FixedString64Bytes MetricFailures = "space4x.modules.failures";
        private static readonly FixedString64Bytes MetricLastTick = "space4x.modules.lastTick";

        private EntityQuery _telemetryQuery;
        private EntityQuery _maintenanceQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TelemetryStream>();
            state.RequireForUpdate<ModuleMaintenanceTelemetry>();

            _telemetryQuery = SystemAPI.QueryBuilder()
                .WithAll<TelemetryStream>()
                .Build();

            _maintenanceQuery = SystemAPI.QueryBuilder()
                .WithAll<ModuleMaintenanceTelemetry>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var telemetryEntity = _telemetryQuery.GetSingletonEntity();
            var maintenanceEntity = _maintenanceQuery.GetSingletonEntity();

            var metrics = state.EntityManager.GetComponentData<ModuleMaintenanceTelemetry>(maintenanceEntity);
            var buffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);

            buffer.AddMetric(MetricRefitStarted, metrics.RefitStarted, TelemetryMetricUnit.Custom);
            buffer.AddMetric(MetricRefitCompleted, metrics.RefitCompleted, TelemetryMetricUnit.Custom);
            buffer.AddMetric(MetricRefitWork, metrics.RefitWorkApplied, TelemetryMetricUnit.Custom);
            buffer.AddMetric(MetricRepairApplied, metrics.RepairApplied, TelemetryMetricUnit.Custom);
            buffer.AddMetric(MetricFailures, metrics.Failures, TelemetryMetricUnit.Custom);
            buffer.AddMetric(MetricLastTick, metrics.LastUpdateTick, TelemetryMetricUnit.Custom);
        }
    }
}
