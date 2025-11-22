using PureDOTS.Runtime.Telemetry;
using Unity.Burst;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Publishes module maintenance counters to telemetry.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XFieldRepairSystem))]
    public partial struct Space4XModuleMaintenanceTelemetrySystem : ISystem
    {
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

            buffer.AddMetric("space4x.modules.refit.started", metrics.RefitStarted, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.modules.refit.completed", metrics.RefitCompleted, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.modules.refit.work", metrics.RefitWorkApplied, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.modules.repair.applied", metrics.RepairApplied, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.modules.failures", metrics.Failures, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.modules.lastTick", metrics.LastUpdateTick, TelemetryMetricUnit.Custom);
        }
    }
}
