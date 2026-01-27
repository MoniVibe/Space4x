using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Telemetry;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Telemetry
{
    /// <summary>
    /// Ensures mandatory loop metrics exist, filling missing values with zeroes.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [UpdateBefore(typeof(TelemetryExportSystem))]
    public partial struct TelemetryLoopDefaultsSystem : ISystem
    {
        private FixedString64Bytes _extractWorkersKey;
        private FixedString64Bytes _extractOutputKey;
        private FixedString64Bytes _extractBufferKey;
        private FixedString64Bytes _extractNodesKey;
        private FixedString64Bytes _logisticsInTransitKey;
        private FixedString64Bytes _logisticsThroughputKey;
        private FixedString64Bytes _logisticsUtilizationKey;
        private FixedString64Bytes _logisticsBacklogKey;
        private FixedString64Bytes _constructionSitesKey;
        private FixedString64Bytes _constructionProgressKey;
        private FixedString64Bytes _constructionStalledKey;
        private FixedString64Bytes _explorationScoutsKey;
        private FixedString64Bytes _explorationCoverageKey;
        private FixedString64Bytes _explorationFreshnessKey;
        private FixedString64Bytes _combatEngagementsKey;
        private FixedString64Bytes _combatLossesKey;
        private FixedString64Bytes _combatMoraleKey;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TelemetryStream>();
            state.RequireForUpdate<TelemetryExportConfig>();
            state.RequireForUpdate<TimeState>();

            _extractWorkersKey = "loop.extract.activeWorkers";
            _extractOutputKey = "loop.extract.outputPerTick";
            _extractBufferKey = "loop.extract.buffer";
            _extractNodesKey = "loop.extract.nodes.active";
            _logisticsInTransitKey = "loop.logistics.inTransit";
            _logisticsThroughputKey = "loop.logistics.throughput";
            _logisticsUtilizationKey = "loop.logistics.storage.utilization";
            _logisticsBacklogKey = "loop.logistics.backlog";
            _constructionSitesKey = "loop.construction.activeSites";
            _constructionProgressKey = "loop.construction.progressPerTick";
            _constructionStalledKey = "loop.construction.stalled";
            _explorationScoutsKey = "loop.exploration.activeScouts";
            _explorationCoverageKey = "loop.exploration.coverage";
            _explorationFreshnessKey = "loop.exploration.intelFreshness";
            _combatEngagementsKey = "loop.combat.engagements.active";
            _combatLossesKey = "loop.combat.lossesPerTick";
            _combatMoraleKey = "loop.combat.morale.avg";
        }

        public void OnDestroy(ref SystemState state) { }

        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<TelemetryExportConfig>();
            if (config.Enabled == 0 || (config.Flags & TelemetryExportFlags.IncludeTelemetryMetrics) == 0)
            {
                return;
            }

            var cadence = config.CadenceTicks > 0 ? config.CadenceTicks : 30u;
            var tick = SystemAPI.GetSingleton<TimeState>().Tick;
            if (tick % cadence != 0)
            {
                return;
            }

            var telemetryEntity = SystemAPI.GetSingletonEntity<TelemetryStream>();
            if (!state.EntityManager.HasBuffer<TelemetryMetric>(telemetryEntity))
            {
                return;
            }

            var buffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);
            if ((config.Loops & TelemetryLoopFlags.Extract) != 0)
            {
                EnsureMetric(ref buffer, _extractWorkersKey, TelemetryMetricUnit.Count);
                EnsureMetric(ref buffer, _extractOutputKey, TelemetryMetricUnit.Custom);
                EnsureMetric(ref buffer, _extractBufferKey, TelemetryMetricUnit.Custom);
                EnsureMetric(ref buffer, _extractNodesKey, TelemetryMetricUnit.Count);
            }

            if ((config.Loops & TelemetryLoopFlags.Logistics) != 0)
            {
                EnsureMetric(ref buffer, _logisticsInTransitKey, TelemetryMetricUnit.Count);
                EnsureMetric(ref buffer, _logisticsThroughputKey, TelemetryMetricUnit.Custom);
                EnsureMetric(ref buffer, _logisticsUtilizationKey, TelemetryMetricUnit.Ratio);
                EnsureMetric(ref buffer, _logisticsBacklogKey, TelemetryMetricUnit.Count);
            }

            if ((config.Loops & TelemetryLoopFlags.Construction) != 0)
            {
                EnsureMetric(ref buffer, _constructionSitesKey, TelemetryMetricUnit.Count);
                EnsureMetric(ref buffer, _constructionProgressKey, TelemetryMetricUnit.Custom);
                EnsureMetric(ref buffer, _constructionStalledKey, TelemetryMetricUnit.Count);
            }

            if ((config.Loops & TelemetryLoopFlags.Exploration) != 0)
            {
                EnsureMetric(ref buffer, _explorationScoutsKey, TelemetryMetricUnit.Count);
                EnsureMetric(ref buffer, _explorationCoverageKey, TelemetryMetricUnit.Ratio);
                EnsureMetric(ref buffer, _explorationFreshnessKey, TelemetryMetricUnit.Custom);
            }

            if ((config.Loops & TelemetryLoopFlags.Combat) != 0)
            {
                EnsureMetric(ref buffer, _combatEngagementsKey, TelemetryMetricUnit.Count);
                EnsureMetric(ref buffer, _combatLossesKey, TelemetryMetricUnit.Custom);
                EnsureMetric(ref buffer, _combatMoraleKey, TelemetryMetricUnit.Ratio);
            }
        }

        private static void EnsureMetric(ref DynamicBuffer<TelemetryMetric> buffer, in FixedString64Bytes key, TelemetryMetricUnit unit)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Key.Equals(key))
                {
                    return;
                }
            }

            buffer.Add(new TelemetryMetric
            {
                Key = key,
                Value = 0f,
                Unit = unit
            });
        }
    }
}
