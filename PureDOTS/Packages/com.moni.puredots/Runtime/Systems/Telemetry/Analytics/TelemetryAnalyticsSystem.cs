using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using PureDOTS.Runtime.Telemetry.Analytics;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.Systems.Telemetry.Analytics
{
    /// <summary>
    /// System that maintains telemetry history.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct TelemetryHistorySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            // Prune old history
            foreach (var (history, config, entity) in 
                SystemAPI.Query<DynamicBuffer<TelemetryHistory>, RefRO<AnalyticsConfig>>()
                    .WithEntityAccess())
            {
                var historyBuffer = history;
                AnalyticsHelpers.PruneHistory(ref historyBuffer, currentTick, config.ValueRO.HistoryRetentionTicks);
            }
        }
    }

    /// <summary>
    /// System that calculates trends from telemetry.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TelemetryHistorySystem))]
    [BurstCompile]
    public partial struct TelemetryTrendSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Process trend calculation requests
            foreach (var (request, history, entity) in 
                SystemAPI.Query<RefRO<TrendCalculationRequest>, DynamicBuffer<TelemetryHistory>>()
                    .WithEntityAccess())
            {
                uint windowStart = currentTick > request.ValueRO.WindowTicks ? 
                    currentTick - request.ValueRO.WindowTicks : 0;

                var trend = AnalyticsHelpers.CalculateTrend(
                    history,
                    request.ValueRO.MetricId,
                    windowStart,
                    currentTick);

                ecb.AddComponent(entity, trend);
                ecb.RemoveComponent<TrendCalculationRequest>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// System that detects anomalies in telemetry.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TelemetryTrendSystem))]
    [BurstCompile]
    public partial struct AnomalyDetectionSystem : ISystem
    {
        private uint _lastCheckTick;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            _lastCheckTick = 0;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            // Get config
            var config = AnalyticsHelpers.DefaultConfig;
            var anomalyConfig = AnalyticsHelpers.DefaultAnomalyConfig;
            
            foreach (var configComp in SystemAPI.Query<RefRO<AnalyticsConfig>>())
            {
                config = configComp.ValueRO;
                break;
            }

            foreach (var anomalyConfigComp in SystemAPI.Query<RefRO<TelemetryAnomalyConfig>>())
            {
                anomalyConfig = anomalyConfigComp.ValueRO;
                break;
            }

            if (!config.EnableAnomalyDetection)
                return;

            if (currentTick - _lastCheckTick < config.AnomalyCheckInterval)
                return;

            _lastCheckTick = currentTick;

            // Check for anomalies
            foreach (var (trend, anomalies, history, entity) in 
                SystemAPI.Query<RefRO<TelemetryTrend>, DynamicBuffer<TelemetryAnomaly>, DynamicBuffer<TelemetryHistory>>()
                    .WithEntityAccess())
            {
                // Get most recent value
                float currentValue = trend.ValueRO.CurrentValue;

                if (AnalyticsHelpers.DetectAnomaly(trend.ValueRO, currentValue, anomalyConfig, out var anomaly))
                {
                    // Check cooldown
                    bool inCooldown = false;
                    for (int i = anomalies.Length - 1; i >= 0; i--)
                    {
                        if (anomalies[i].MetricId.Equals(anomaly.MetricId) &&
                            currentTick - anomalies[i].DetectedTick < anomalyConfig.CooldownTicks)
                        {
                            inCooldown = true;
                            break;
                        }
                    }

                    if (!inCooldown)
                    {
                        anomaly.DetectedTick = currentTick;
                        
                        // Remove old anomalies if at capacity
                        if (anomalies.Length >= anomalies.Capacity)
                        {
                            anomalies.RemoveAt(0);
                        }
                        
                        anomalies.Add(anomaly);
                    }
                }
            }
        }
    }

    /// <summary>
    /// System that analyzes game balance.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AnomalyDetectionSystem))]
    [BurstCompile]
    public partial struct BalanceAnalysisSystem : ISystem
    {
        private uint _lastCheckTick;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            _lastCheckTick = 0;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            // Get config
            var config = AnalyticsHelpers.DefaultConfig;
            foreach (var configComp in SystemAPI.Query<RefRO<AnalyticsConfig>>())
            {
                config = configComp.ValueRO;
                break;
            }

            if (!config.EnableBalanceAnalysis)
                return;

            if (currentTick - _lastCheckTick < config.BalanceCheckInterval)
                return;

            _lastCheckTick = currentTick;

            // Update balance metrics
            foreach (var (metric, trend, entity) in 
                SystemAPI.Query<RefRW<BalanceMetric>, RefRO<TelemetryTrend>>()
                    .WithEntityAccess())
            {
                if (metric.ValueRO.MetricId.Equals(trend.ValueRO.MetricId))
                {
                    AnalyticsHelpers.UpdateBalanceMetric(
                        ref metric.ValueRW,
                        trend.ValueRO.CurrentValue,
                        currentTick);
                }
            }
        }
    }

    /// <summary>
    /// System that tracks player behavior.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct PlayerBehaviorSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            // Get config
            var config = AnalyticsHelpers.DefaultConfig;
            foreach (var configComp in SystemAPI.Query<RefRO<AnalyticsConfig>>())
            {
                config = configComp.ValueRO;
                break;
            }

            if (!config.EnablePlayerTracking)
                return;

            // Update session stats
            foreach (var (session, actions, entity) in 
                SystemAPI.Query<RefRW<PlayerSession>, DynamicBuffer<PlayerAction>>()
                    .WithEntityAccess())
            {
                AnalyticsHelpers.UpdateSessionStats(ref session.ValueRW, actions, currentTick);
            }
        }
    }
}

