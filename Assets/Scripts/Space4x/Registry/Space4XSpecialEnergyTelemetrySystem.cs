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
        private static readonly FixedString64Bytes ShipSamplesMetricKey =
            new FixedString64Bytes("space4x.special_energy.ship.samples");
        private static readonly FixedString64Bytes ShipAvgRatioMetricKey =
            new FixedString64Bytes("space4x.special_energy.ship.avg_ratio");
        private static readonly FixedString64Bytes ShipMinRatioMetricKey =
            new FixedString64Bytes("space4x.special_energy.ship.min_ratio");
        private static readonly FixedString64Bytes ShipMaxRatioMetricKey =
            new FixedString64Bytes("space4x.special_energy.ship.max_ratio");
        private static readonly FixedString64Bytes SpendTickTotalMetricKey =
            new FixedString64Bytes("space4x.special_energy.spend.tick_total");
        private static readonly FixedString64Bytes SpendFailedTotalMetricKey =
            new FixedString64Bytes("space4x.special_energy.spend.failed_total");

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TelemetryStream>();
            state.RequireForUpdate<TelemetryExportConfig>();
            state.RequireForUpdate<ShipSpecialEnergyState>();
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
            metrics.AddMetric(ShipSamplesMetricKey, shipCount, TelemetryMetricUnit.Count);
            metrics.AddMetric(ShipAvgRatioMetricKey, avgRatio, TelemetryMetricUnit.Ratio);
            metrics.AddMetric(ShipMinRatioMetricKey, ratioMin, TelemetryMetricUnit.Ratio);
            metrics.AddMetric(ShipMaxRatioMetricKey, ratioMax, TelemetryMetricUnit.Ratio);
            metrics.AddMetric(SpendTickTotalMetricKey, spentTickTotal, TelemetryMetricUnit.Custom);
            metrics.AddMetric(SpendFailedTotalMetricKey, failedSpendTotal, TelemetryMetricUnit.Count);
        }
    }
}
