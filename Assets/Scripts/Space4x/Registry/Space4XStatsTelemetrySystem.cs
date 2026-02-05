using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Publishes telemetry metrics for stat influences on gameplay systems.
    /// Tracks how stats affect various gameplay outcomes for tuning and debugging.
    /// Disabled by default - enable only when stat telemetry debugging is needed.
    /// </summary>
    [DisableAutoCreation]
    [BurstCompile]
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XTelemetryBootstrapSystem))]
    public partial struct Space4XStatsTelemetrySystem : ISystem
    {
        private static readonly FixedString64Bytes MetricCommandAvg = "space4x.stats.command.avg";
        private static readonly FixedString64Bytes MetricTacticsAvg = "space4x.stats.tactics.avg";
        private static readonly FixedString64Bytes MetricLogisticsAvg = "space4x.stats.logistics.avg";
        private static readonly FixedString64Bytes MetricDiplomacyAvg = "space4x.stats.diplomacy.avg";
        private static readonly FixedString64Bytes MetricEngineeringAvg = "space4x.stats.engineering.avg";
        private static readonly FixedString64Bytes MetricResolveAvg = "space4x.stats.resolve.avg";
        private static readonly FixedString64Bytes MetricPhysiqueAvg = "space4x.stats.physique.avg";
        private static readonly FixedString64Bytes MetricFinesseAvg = "space4x.stats.finesse.avg";
        private static readonly FixedString64Bytes MetricWillAvg = "space4x.stats.will.avg";

        private ComponentLookup<IndividualStats> _statsLookup;
        private ComponentLookup<PhysiqueFinesseWill> _physiqueLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<TelemetryStream>();
            
            _statsLookup = state.GetComponentLookup<IndividualStats>(true);
            _physiqueLookup = state.GetComponentLookup<PhysiqueFinesseWill>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            // Only publish telemetry every N ticks to avoid spam
            if (timeState.Tick % 60 != 0) // Every 60 ticks (~1 second at 60 FPS)
            {
                return;
            }

            _statsLookup.Update(ref state);
            _physiqueLookup.Update(ref state);

            if (!SystemAPI.TryGetSingletonBuffer<TelemetryMetric>(out var telemetryBuffer))
            {
                return;
            }

            // Aggregate stat influences across entities
            float totalCommand = 0f;
            float totalTactics = 0f;
            float totalLogistics = 0f;
            float totalDiplomacy = 0f;
            float totalEngineering = 0f;
            float totalResolve = 0f;
            float totalPhysique = 0f;
            float totalFinesse = 0f;
            float totalWill = 0f;
            int entityCount = 0;

            foreach (var (stats, entity) in SystemAPI.Query<RefRO<IndividualStats>>().WithEntityAccess())
            {
                totalCommand += stats.ValueRO.Command;
                totalTactics += stats.ValueRO.Tactics;
                totalLogistics += stats.ValueRO.Logistics;
                totalDiplomacy += stats.ValueRO.Diplomacy;
                totalEngineering += stats.ValueRO.Engineering;
                totalResolve += stats.ValueRO.Resolve;
                entityCount++;
            }

            int physiqueCount = 0;
            foreach (var (physique, entity) in SystemAPI.Query<RefRO<PhysiqueFinesseWill>>().WithEntityAccess())
            {
                totalPhysique += physique.ValueRO.Physique;
                totalFinesse += physique.ValueRO.Finesse;
                totalWill += physique.ValueRO.Will;
                physiqueCount++;
            }

            // Publish aggregated metrics
            if (entityCount > 0)
            {
                var avgCommand = totalCommand / entityCount;
                var avgTactics = totalTactics / entityCount;
                var avgLogistics = totalLogistics / entityCount;
                var avgDiplomacy = totalDiplomacy / entityCount;
                var avgEngineering = totalEngineering / entityCount;
                var avgResolve = totalResolve / entityCount;

                telemetryBuffer.AddMetric(MetricCommandAvg, avgCommand, TelemetryMetricUnit.None);
                telemetryBuffer.AddMetric(MetricTacticsAvg, avgTactics, TelemetryMetricUnit.None);
                telemetryBuffer.AddMetric(MetricLogisticsAvg, avgLogistics, TelemetryMetricUnit.None);
                telemetryBuffer.AddMetric(MetricDiplomacyAvg, avgDiplomacy, TelemetryMetricUnit.None);
                telemetryBuffer.AddMetric(MetricEngineeringAvg, avgEngineering, TelemetryMetricUnit.None);
                telemetryBuffer.AddMetric(MetricResolveAvg, avgResolve, TelemetryMetricUnit.None);
            }

            if (physiqueCount > 0)
            {
                var avgPhysique = totalPhysique / physiqueCount;
                var avgFinesse = totalFinesse / physiqueCount;
                var avgWill = totalWill / physiqueCount;

                telemetryBuffer.AddMetric(MetricPhysiqueAvg, avgPhysique, TelemetryMetricUnit.None);
                telemetryBuffer.AddMetric(MetricFinesseAvg, avgFinesse, TelemetryMetricUnit.None);
                telemetryBuffer.AddMetric(MetricWillAvg, avgWill, TelemetryMetricUnit.None);
            }
        }
    }
}
