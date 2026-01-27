using Unity.Entities;
using Unity.Collections;

namespace PureDOTS.Runtime.Stats
{
    /// <summary>
    /// Aggregate statistics for a group of values.
    /// </summary>
    public struct AggregateStats : IComponentData
    {
        public float Mean;
        public float WeightedMean;
        public float Min;
        public float Max;
        public float Variance;
        public float StandardDeviation;
        public int Count;
        public float TotalWeight;
        public uint LastCalculatedTick;
    }

    /// <summary>
    /// Identifies the weakest point in a chain/group.
    /// </summary>
    public struct BottleneckEntry : IComponentData
    {
        public ushort ResourceTypeId;      // Which resource is bottlenecked
        public Entity BottleneckEntity;    // Which entity is the bottleneck
        public float BottleneckValue;      // Current value at bottleneck
        public float ThresholdValue;       // What it should be
        public float DeficitRatio;         // How far below threshold (0-1)
        public byte Severity;              // 0 = minor, 1 = moderate, 2 = critical
        public uint DetectedTick;
    }

    /// <summary>
    /// Buffer of bottlenecks in priority order.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct BottleneckBuffer : IBufferElementData
    {
        public BottleneckEntry Entry;
    }

    /// <summary>
    /// Composition of a group (counts by category).
    /// </summary>
    public struct GroupComposition : IComponentData
    {
        public int TotalCount;
        public int ActiveCount;            // Not disabled/dead
        public int HealthyCount;           // Above health threshold
        public int DamagedCount;           // Below health threshold
        public int CriticalCount;          // Below critical threshold
        public float HealthyRatio;         // Healthy / Total
        public float ReadinessScore;       // Weighted readiness
    }

    /// <summary>
    /// Per-category count for detailed composition.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct CompositionCategory : IBufferElementData
    {
        public FixedString32Bytes CategoryId;
        public int Count;
        public float Weight;               // Importance/contribution weight
        public float AverageValue;         // Average stat for this category
    }

    /// <summary>
    /// Configuration for aggregate calculations.
    /// </summary>
    public struct AggregateConfig : IComponentData
    {
        public float HealthyThreshold;     // % to be considered healthy
        public float CriticalThreshold;    // % to be considered critical
        public float BottleneckThreshold;  // % below which is bottleneck
        public byte WeightByImportance;    // Weight by entity importance
        public byte IncludeInactive;       // Include inactive entities
    }

    /// <summary>
    /// Single value contribution to aggregate.
    /// </summary>
    public struct StatContribution
    {
        public float Value;
        public float Weight;
        public byte IsActive;
        public byte Category;
    }
}

