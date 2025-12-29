using PureDOTS.Runtime.Research;
using PureDOTS.Runtime.Telemetry;
using Unity.Burst;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Publishes research counters to telemetry.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(ResearchTransferProcessingSystem))]
    public partial struct Space4XResearchTelemetrySystem : ISystem
    {
        private EntityQuery _telemetryQuery;
        private EntityQuery _researchTelemetryQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TelemetryStream>();
            state.RequireForUpdate<ResearchTelemetry>();

            _telemetryQuery = SystemAPI.QueryBuilder()
                .WithAll<TelemetryStream>()
                .Build();

            _researchTelemetryQuery = SystemAPI.QueryBuilder()
                .WithAll<ResearchTelemetry>()
                .Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var telemetryEntity = _telemetryQuery.GetSingletonEntity();
            var researchEntity = _researchTelemetryQuery.GetSingletonEntity();

            var metrics = state.EntityManager.GetComponentData<ResearchTelemetry>(researchEntity);
            var buffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);

            buffer.AddMetric("space4x.research.harvest.count", (float)metrics.CompletedHarvests, TelemetryMetricUnit.Custom);
            buffer.AddMetric("space4x.research.bandwidth.loss", metrics.TotalLoss, TelemetryMetricUnit.Custom);
        }
    }
}
