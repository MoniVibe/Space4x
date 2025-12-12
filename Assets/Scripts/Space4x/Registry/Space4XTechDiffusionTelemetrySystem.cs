using PureDOTS.Runtime.Telemetry;
using Unity.Burst;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Publishes tech diffusion counters to telemetry for HUD/debug dashboards.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XTechDiffusionSystem))]
    public partial struct Space4XTechDiffusionTelemetrySystem : ISystem
    {
        private EntityQuery _telemetryQuery;
        private EntityQuery _diffusionTelemetryQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TelemetryStream>();
            state.RequireForUpdate<TechDiffusionTelemetry>();

            _telemetryQuery = SystemAPI.QueryBuilder()
                .WithAll<TelemetryStream>()
                .Build();

            _diffusionTelemetryQuery = SystemAPI.QueryBuilder()
                .WithAll<TechDiffusionTelemetry>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var telemetryEntity = _telemetryQuery.GetSingletonEntity();
            var diffusionEntity = _diffusionTelemetryQuery.GetSingletonEntity();

            var metrics = state.EntityManager.GetComponentData<TechDiffusionTelemetry>(diffusionEntity);
            var buffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);

            buffer.AddMetric("space4x.techdiffusion.active", metrics.ActiveDiffusions, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.techdiffusion.completed", metrics.CompletedUpgrades, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.techdiffusion.lastTick", metrics.LastUpdateTick, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.techdiffusion.lastUpgradeTick", metrics.LastUpgradeTick, TelemetryMetricUnit.Custom);
        }
    }
}
