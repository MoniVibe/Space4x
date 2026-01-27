using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;
using Unity.Burst;

namespace PureDOTS.Runtime.Telemetry.Analytics
{
    /// <summary>
    /// Static helpers for telemetry analytics.
    /// </summary>
    [BurstCompile]
    public static class AnalyticsHelpers
    {
        private static readonly FixedString32Bytes BuildActionId = "build";
        private static readonly FixedString32Bytes AttackActionId = "attack";
        private static readonly FixedString32Bytes DefendActionId = "defend";
        private static readonly FixedString32Bytes TradeActionId = "trade";
        private static readonly FixedString32Bytes ExploreActionId = "explore";

        /// <summary>
        /// Default analytics configuration.
        /// </summary>
        public static AnalyticsConfig DefaultConfig => new AnalyticsConfig
        {
            HistoryRetentionTicks = 216000,    // 1 hour at 60 ticks/sec
            TrendWindowTicks = 3600,           // 1 minute
            AnomalyCheckInterval = 600,        // Every 10 seconds
            BalanceCheckInterval = 1800,       // Every 30 seconds
            EnablePlayerTracking = true,
            EnableAnomalyDetection = true,
            EnableBalanceAnalysis = true
        };

        /// <summary>
        /// Default anomaly detection configuration.
        /// </summary>
        public static TelemetryAnomalyConfig DefaultAnomalyConfig => new TelemetryAnomalyConfig
        {
            DeviationThreshold = 2.5f,         // 2.5 standard deviations
            MinSamplesForDetection = 30,
            CooldownTicks = 600,
            TrackPositiveAnomalies = true,
            TrackNegativeAnomalies = true
        };

        /// <summary>
        /// Calculates trend from history buffer.
        /// </summary>
        public static TelemetryTrend CalculateTrend(
            in DynamicBuffer<TelemetryHistory> history,
            FixedString32Bytes metricId,
            uint windowStartTick,
            uint windowEndTick)
        {
            var trend = new TelemetryTrend
            {
                MetricId = metricId,
                WindowStartTick = windowStartTick,
                WindowEndTick = windowEndTick,
                MinValue = float.MaxValue,
                MaxValue = float.MinValue
            };

            float sum = 0f;
            float sumSquared = 0f;
            int count = 0;

            // First pass: calculate sum, min, max
            for (int i = 0; i < history.Length; i++)
            {
                var entry = history[i];
                if (!entry.MetricId.Equals(metricId))
                    continue;
                if (entry.Tick < windowStartTick || entry.Tick > windowEndTick)
                    continue;

                sum += entry.Value;
                sumSquared += entry.Value * entry.Value;
                trend.MinValue = math.min(trend.MinValue, entry.Value);
                trend.MaxValue = math.max(trend.MaxValue, entry.Value);
                count++;

                if (count == 1 || entry.Tick == windowEndTick)
                {
                    trend.CurrentValue = entry.Value;
                }
            }

            if (count == 0)
            {
                trend.MinValue = 0;
                trend.MaxValue = 0;
                return trend;
            }

            trend.SampleCount = (uint)count;
            trend.AverageValue = sum / count;
            trend.Variance = (sumSquared / count) - (trend.AverageValue * trend.AverageValue);

            // Calculate slope using linear regression
            if (count >= 2)
            {
                float sumX = 0f, sumY = 0f, sumXY = 0f, sumX2 = 0f;
                int idx = 0;

                for (int i = 0; i < history.Length; i++)
                {
                    var entry = history[i];
                    if (!entry.MetricId.Equals(metricId))
                        continue;
                    if (entry.Tick < windowStartTick || entry.Tick > windowEndTick)
                        continue;

                    float x = idx;
                    float y = entry.Value;
                    sumX += x;
                    sumY += y;
                    sumXY += x * y;
                    sumX2 += x * x;
                    idx++;
                }

                float denominator = count * sumX2 - sumX * sumX;
                if (math.abs(denominator) > 0.0001f)
                {
                    trend.Slope = (count * sumXY - sumX * sumY) / denominator;
                }
            }

            return trend;
        }

        /// <summary>
        /// Detects anomalies in recent data.
        /// </summary>
        public static bool DetectAnomaly(
            in TelemetryTrend trend,
            float currentValue,
            in TelemetryAnomalyConfig config,
            out TelemetryAnomaly anomaly)
        {
            anomaly = default;

            if (trend.SampleCount < config.MinSamplesForDetection)
                return false;

            float stdDev = math.sqrt(math.max(0, trend.Variance));
            if (stdDev < 0.0001f)
                return false;

            float deviation = currentValue - trend.AverageValue;
            float deviationScore = deviation / stdDev;

            bool isPositive = deviation > 0;

            if (isPositive && !config.TrackPositiveAnomalies)
                return false;
            if (!isPositive && !config.TrackNegativeAnomalies)
                return false;

            if (math.abs(deviationScore) >= config.DeviationThreshold)
            {
                anomaly = new TelemetryAnomaly
                {
                    MetricId = trend.MetricId,
                    ExpectedValue = trend.AverageValue,
                    ActualValue = currentValue,
                    DeviationScore = deviationScore,
                    IsPositive = isPositive,
                    IsAcknowledged = false
                };
                return true;
            }

            return false;
        }

        /// <summary>
        /// Updates balance metric.
        /// </summary>
        public static void UpdateBalanceMetric(
            ref BalanceMetric metric,
            float currentValue,
            uint currentTick)
        {
            metric.CurrentValue = currentValue;
            metric.Deviation = currentValue - metric.TargetValue;
            
            if (math.abs(metric.TargetValue) > 0.0001f)
            {
                metric.DeviationPercent = metric.Deviation / metric.TargetValue * 100f;
            }

            // Update historical
            metric.HistoricalMin = math.min(metric.HistoricalMin, currentValue);
            metric.HistoricalMax = math.max(metric.HistoricalMax, currentValue);
            
            // Running average
            float alpha = 0.1f; // Smoothing factor
            metric.HistoricalAverage = metric.HistoricalAverage * (1f - alpha) + currentValue * alpha;

            metric.LastUpdatedTick = currentTick;
            metric.NeedsAttention = math.abs(metric.DeviationPercent) > 20f; // 20% threshold
        }

        /// <summary>
        /// Calculates correlation between two metrics.
        /// </summary>
        public static float CalculateCorrelation(
            in DynamicBuffer<TelemetryHistory> history,
            FixedString32Bytes metricA,
            FixedString32Bytes metricB,
            uint windowStartTick,
            uint windowEndTick)
        {
            // Collect paired samples
            NativeList<float2> pairs = new NativeList<float2>(Allocator.Temp);

            for (int i = 0; i < history.Length; i++)
            {
                var entryA = history[i];
                if (!entryA.MetricId.Equals(metricA))
                    continue;
                if (entryA.Tick < windowStartTick || entryA.Tick > windowEndTick)
                    continue;

                // Find matching B entry at same tick
                for (int j = 0; j < history.Length; j++)
                {
                    var entryB = history[j];
                    if (!entryB.MetricId.Equals(metricB))
                        continue;
                    if (entryB.Tick == entryA.Tick)
                    {
                        pairs.Add(new float2(entryA.Value, entryB.Value));
                        break;
                    }
                }
            }

            if (pairs.Length < 3)
            {
                pairs.Dispose();
                return 0f;
            }

            // Calculate Pearson correlation
            float sumA = 0f, sumB = 0f, sumAB = 0f, sumA2 = 0f, sumB2 = 0f;
            int n = pairs.Length;

            for (int i = 0; i < n; i++)
            {
                sumA += pairs[i].x;
                sumB += pairs[i].y;
                sumAB += pairs[i].x * pairs[i].y;
                sumA2 += pairs[i].x * pairs[i].x;
                sumB2 += pairs[i].y * pairs[i].y;
            }

            pairs.Dispose();

            float numerator = n * sumAB - sumA * sumB;
            float denominator = math.sqrt((n * sumA2 - sumA * sumA) * (n * sumB2 - sumB * sumB));

            if (math.abs(denominator) < 0.0001f)
                return 0f;

            return math.clamp(numerator / denominator, -1f, 1f);
        }

        /// <summary>
        /// Records a player action.
        /// </summary>
        public static void RecordPlayerAction(
            ref DynamicBuffer<PlayerAction> actions,
            FixedString32Bytes actionType,
            FixedString32Bytes targetType,
            Entity target,
            float3 position,
            uint tick,
            bool successful)
        {
            if (actions.Length >= actions.Capacity)
            {
                actions.RemoveAt(0);
            }

            actions.Add(new PlayerAction
            {
                ActionType = actionType,
                TargetType = targetType,
                TargetEntity = target,
                Position = position,
                Tick = tick,
                WasSuccessful = successful
            });
        }

        /// <summary>
        /// Updates session statistics.
        /// </summary>
        public static void UpdateSessionStats(
            ref PlayerSession session,
            in DynamicBuffer<PlayerAction> actions,
            uint currentTick)
        {
            session.EndTick = currentTick;
            session.TotalActions = (uint)actions.Length;

            // Count action types
            session.BuildActions = 0;
            session.CombatActions = 0;
            session.TradeActions = 0;
            session.ExploreActions = 0;

            for (int i = 0; i < actions.Length; i++)
            {
                var action = actions[i];
                if (action.ActionType.Equals(BuildActionId))
                    session.BuildActions++;
                else if (action.ActionType.Equals(AttackActionId) || 
                         action.ActionType.Equals(DefendActionId))
                    session.CombatActions++;
                else if (action.ActionType.Equals(TradeActionId))
                    session.TradeActions++;
                else if (action.ActionType.Equals(ExploreActionId))
                    session.ExploreActions++;
            }

            // Calculate actions per minute
            uint durationTicks = currentTick - session.StartTick;
            float durationMinutes = durationTicks / 3600f; // Assuming 60 ticks/sec
            
            if (durationMinutes > 0.01f)
            {
                session.AverageActionsPerMinute = session.TotalActions / durationMinutes;
            }
        }

        /// <summary>
        /// Prunes old history entries.
        /// </summary>
        public static void PruneHistory(
            ref DynamicBuffer<TelemetryHistory> history,
            uint currentTick,
            uint retentionTicks)
        {
            uint cutoffTick = currentTick > retentionTicks ? currentTick - retentionTicks : 0;

            for (int i = history.Length - 1; i >= 0; i--)
            {
                if (history[i].Tick < cutoffTick)
                {
                    history.RemoveAt(i);
                }
            }
        }
    }
}

