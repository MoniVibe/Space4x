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
            metrics.AddMetric(new FixedString64Bytes("space4x.special_energy.ship.samples"), shipCount, TelemetryMetricUnit.Count);
            metrics.AddMetric(new FixedString64Bytes("space4x.special_energy.ship.avg_ratio"), avgRatio, TelemetryMetricUnit.Ratio);
            metrics.AddMetric(new FixedString64Bytes("space4x.special_energy.ship.min_ratio"), ratioMin, TelemetryMetricUnit.Ratio);
            metrics.AddMetric(new FixedString64Bytes("space4x.special_energy.ship.max_ratio"), ratioMax, TelemetryMetricUnit.Ratio);
            metrics.AddMetric(new FixedString64Bytes("space4x.special_energy.spend.tick_total"), spentTickTotal, TelemetryMetricUnit.Custom);
            metrics.AddMetric(new FixedString64Bytes("space4x.special_energy.spend.failed_total"), failedSpendTotal, TelemetryMetricUnit.Count);
        }
    }
}
