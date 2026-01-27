using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Economy;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Economy
{
    /// <summary>
    /// Computes a dynamic price multiplier based on inventory fill level and recent inflow/outflow trends.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BatchInventorySystem))]
    public partial struct BatchPricingSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BatchInventory>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused
                || !SystemAPI.TryGetSingleton<RewindState>(out var rewindState)
                || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var cfg = SystemAPI.TryGetSingleton<BatchPricingConfig>(out var config)
                ? config
                : BatchPricingConfig.CreateDefault();
            var smoothing = math.clamp(cfg.TrendSmoothing * math.max(1f, timeState.CurrentSpeedMultiplier), 0f, 1f);

            foreach (var (inventory, pricing) in SystemAPI.Query<RefRO<BatchInventory>, RefRW<BatchPricingState>>())
            {
                var inv = inventory.ValueRO;
                var fill = inv.MaxCapacity > 0f ? math.saturate(inv.TotalUnits / inv.MaxCapacity) : 0f;
                var normalizedDelta = inv.MaxCapacity > 0f
                    ? math.clamp((inv.TotalUnits - pricing.ValueRO.LastUnits) / math.max(1f, inv.MaxCapacity), -cfg.MaxDeltaFraction, cfg.MaxDeltaFraction)
                    : 0f;

                var smoothedDelta = math.lerp(pricing.ValueRO.SmoothedDelta, normalizedDelta, smoothing);
                var smoothedFill = math.lerp(pricing.ValueRO.SmoothedFill, fill, smoothing);

                float multiplier;
                if (smoothedFill <= cfg.LowFillThreshold)
                {
                    multiplier = cfg.MaxMultiplier;
                }
                else if (smoothedFill >= cfg.HighFillThreshold)
                {
                    multiplier = cfg.MinMultiplier;
                }
                else
                {
                    var t = math.saturate((smoothedFill - cfg.LowFillThreshold) / math.max(0.0001f, cfg.HighFillThreshold - cfg.LowFillThreshold));
                    multiplier = math.lerp(cfg.MaxMultiplier, cfg.MinMultiplier, t);
                }

                var demandPressure = math.max(0.1f, 1f + (-smoothedDelta * cfg.Elasticity));
                pricing.ValueRW.LastPriceMultiplier = math.clamp(multiplier * demandPressure, cfg.MinMultiplier, cfg.MaxMultiplier);
                pricing.ValueRW.LastUpdateTick = timeState.Tick;
                pricing.ValueRW.SmoothedDelta = smoothedDelta;
                pricing.ValueRW.LastUnits = inv.TotalUnits;
                pricing.ValueRW.SmoothedFill = smoothedFill;
            }
        }
    }
}
