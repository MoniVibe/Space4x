using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Telemetry.Analytics
{
    /// <summary>
    /// Historical telemetry data point.
    /// </summary>
    [InternalBufferCapacity(64)]
    public struct TelemetryHistory : IBufferElementData
    {
        public FixedString32Bytes MetricId;
        public float Value;
        public uint Tick;
        public uint SessionId;
    }

    /// <summary>
    /// Calculated trend for a metric.
    /// </summary>
    public struct TelemetryTrend : IComponentData
    {
        public FixedString32Bytes MetricId;
        public float CurrentValue;
        public float AverageValue;
        public float MinValue;
        public float MaxValue;
        public float Slope;                        // Rate of change
        public float Variance;
        public uint SampleCount;
        public uint WindowStartTick;
        public uint WindowEndTick;
    }

    /// <summary>
    /// Configuration for anomaly detection.
    /// </summary>
    public struct TelemetryAnomalyConfig : IComponentData
    {
        public float DeviationThreshold;           // Standard deviations for anomaly
        public uint MinSamplesForDetection;
        public uint CooldownTicks;                 // Min ticks between alerts
        public bool TrackPositiveAnomalies;        // Above average
        public bool TrackNegativeAnomalies;        // Below average
    }

    /// <summary>
    /// Detected anomaly in telemetry.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct TelemetryAnomaly : IBufferElementData
    {
        public FixedString32Bytes MetricId;
        public float ExpectedValue;
        public float ActualValue;
        public float DeviationScore;               // How many std devs
        public uint DetectedTick;
        public bool IsPositive;                    // Above or below expected
        public bool IsAcknowledged;
    }

    /// <summary>
    /// Balance metric for game design analysis.
    /// </summary>
    public struct BalanceMetric : IComponentData
    {
        public FixedString32Bytes MetricId;
        public FixedString32Bytes Category;        // "economy", "combat", "progression"
        
        public float TargetValue;                  // Designer intended value
        public float CurrentValue;
        public float Deviation;                    // Current - Target
        public float DeviationPercent;
        
        public float HistoricalAverage;
        public float HistoricalMin;
        public float HistoricalMax;
        
        public uint LastUpdatedTick;
        public bool NeedsAttention;                // Deviation > threshold
    }

    /// <summary>
    /// Player action for behavior tracking.
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct PlayerAction : IBufferElementData
    {
        public FixedString32Bytes ActionType;      // "build", "attack", "trade", "explore"
        public FixedString32Bytes TargetType;      // What was targeted
        public Entity TargetEntity;
        public float3 Position;
        public uint Tick;
        public float Duration;                     // How long action took
        public bool WasSuccessful;
    }

    /// <summary>
    /// Player session summary.
    /// </summary>
    public struct PlayerSession : IComponentData
    {
        public uint SessionId;
        public uint StartTick;
        public uint EndTick;
        public uint TotalActions;
        
        // Action breakdown
        public uint BuildActions;
        public uint CombatActions;
        public uint TradeActions;
        public uint ExploreActions;
        
        // Performance
        public float AverageActionsPerMinute;
        public float PeakActionsPerMinute;
        
        // Outcomes
        public float FinalScore;
        public bool WonGame;
        public FixedString32Bytes EndReason;       // "victory", "defeat", "quit"
    }

    /// <summary>
    /// Aggregated metric for analytics.
    /// </summary>
    public struct AggregatedMetric : IComponentData
    {
        public FixedString32Bytes MetricId;
        public FixedString32Bytes AggregationType; // "sum", "avg", "max", "min"
        
        public float Value;
        public uint SampleCount;
        public uint WindowTicks;
        public uint LastAggregatedTick;
    }

    /// <summary>
    /// Correlation between two metrics.
    /// </summary>
    public struct MetricCorrelation : IComponentData
    {
        public FixedString32Bytes MetricAId;
        public FixedString32Bytes MetricBId;
        public float CorrelationCoefficient;       // -1 to 1
        public float Significance;                 // Statistical significance
        public uint SampleCount;
        public uint LastCalculatedTick;
    }

    /// <summary>
    /// Request to calculate trends.
    /// </summary>
    public struct TrendCalculationRequest : IComponentData
    {
        public FixedString32Bytes MetricId;
        public uint WindowTicks;
        public uint RequestTick;
    }

    /// <summary>
    /// Request to check balance.
    /// </summary>
    public struct BalanceCheckRequest : IComponentData
    {
        public FixedString32Bytes Category;        // Empty = all categories
        public uint RequestTick;
    }

    /// <summary>
    /// Analytics configuration.
    /// </summary>
    public struct AnalyticsConfig : IComponentData
    {
        public uint HistoryRetentionTicks;         // How long to keep history
        public uint TrendWindowTicks;              // Window for trend calculation
        public uint AnomalyCheckInterval;          // How often to check for anomalies
        public uint BalanceCheckInterval;          // How often to check balance
        public bool EnablePlayerTracking;
        public bool EnableAnomalyDetection;
        public bool EnableBalanceAnalysis;
    }
}

