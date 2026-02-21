using PureDOTS.Runtime.Telemetry;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XSpecialEnergyTelemetrySystem : ISystem
    {
        private FixedString64Bytes _shipSamplesMetricKey;
        private FixedString64Bytes _shipAvgRatioMetricKey;
        private FixedString64Bytes _shipMinRatioMetricKey;
        private FixedString64Bytes _shipMaxRatioMetricKey;
        private FixedString64Bytes _spendTickTotalMetricKey;
        private FixedString64Bytes _spendFailedTotalMetricKey;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TelemetryStream>();
            state.RequireForUpdate<TelemetryExportConfig>();
            state.RequireForUpdate<ShipSpecialEnergyState>();

            // Initialize FixedString keys outside Burst static constructors.
            _shipSamplesMetricKey = new FixedString64Bytes("space4x.special_energy.ship.samples");
            _shipAvgRatioMetricKey = new FixedString64Bytes("space4x.special_energy.ship.avg_ratio");
            _shipMinRatioMetricKey = new FixedString64Bytes("space4x.special_energy.ship.min_ratio");
            _shipMaxRatioMetricKey = new FixedString64Bytes("space4x.special_energy.ship.max_ratio");
            _spendTickTotalMetricKey = new FixedString64Bytes("space4x.special_energy.spend.tick_total");
            _spendFailedTotalMetricKey = new FixedString64Bytes("space4x.special_energy.spend.failed_total");
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TelemetryExportConfig>(out var config) ||
                (config.Flags & TelemetryExportFlags.IncludeTelemetryMetrics) == 0)
            {
                return;
            }

            if (!SystemAPI.TryGetSingletonEntity<TelemetryStream>(out var telemetryEntity) ||
                !state.EntityManager.HasBuffer<TelemetryMetric>(telemetryEntity))
            {
                return;
            }

            var shipCount = 0;
            var ratioSum = 0f;
            var ratioMin = 1f;
            var ratioMax = 0f;
            var spentTickTotal = 0f;
            var failedSpendTotal = 0f;

            foreach (var energy in SystemAPI.Query<RefRO<ShipSpecialEnergyState>>())
            {
                shipCount++;
                var ratio = math.saturate(energy.ValueRO.Ratio);
                ratioSum += ratio;
                ratioMin = math.min(ratioMin, ratio);
                ratioMax = math.max(ratioMax, ratio);
                spentTickTotal += math.max(0f, energy.ValueRO.LastSpent);
                failedSpendTotal += energy.ValueRO.FailedSpendAttempts;
            }

            if (shipCount == 0)
            {
                return;
            }

            var avgRatio = ratioSum / shipCount;
            var metrics = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);
            metrics.AddMetric(_shipSamplesMetricKey, shipCount, TelemetryMetricUnit.Count);
            metrics.AddMetric(_shipAvgRatioMetricKey, avgRatio, TelemetryMetricUnit.Ratio);
            metrics.AddMetric(_shipMinRatioMetricKey, ratioMin, TelemetryMetricUnit.Ratio);
            metrics.AddMetric(_shipMaxRatioMetricKey, ratioMax, TelemetryMetricUnit.Ratio);
            metrics.AddMetric(_spendTickTotalMetricKey, spentTickTotal, TelemetryMetricUnit.Custom);
            metrics.AddMetric(_spendFailedTotalMetricKey, failedSpendTotal, TelemetryMetricUnit.Count);
        }
    }
}
