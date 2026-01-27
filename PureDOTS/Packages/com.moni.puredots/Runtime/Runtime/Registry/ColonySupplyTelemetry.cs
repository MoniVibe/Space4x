using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Registry
{
    /// <summary>
    /// Supply metrics snapshot for colony registries.
    /// Used by game projects to report supply/demand metrics to the shared telemetry system.
    /// </summary>
    public struct ColonySupplySnapshot : IComponentData
    {
        /// <summary>
        /// Total supply demand across all colonies.
        /// </summary>
        public float TotalSupplyDemand;

        /// <summary>
        /// Total supply shortage (demand - supply where demand > supply).
        /// </summary>
        public float TotalSupplyShortage;

        /// <summary>
        /// Average supply ratio across all colonies (0-1).
        /// </summary>
        public float AverageSupplyRatio;

        /// <summary>
        /// Number of colonies below the bottleneck threshold.
        /// </summary>
        public int BottleneckColonyCount;

        /// <summary>
        /// Number of colonies below the critical threshold.
        /// </summary>
        public int CriticalColonyCount;

        /// <summary>
        /// Total number of colonies in the snapshot.
        /// </summary>
        public int TotalColonyCount;

        /// <summary>
        /// Version incremented on each snapshot update.
        /// </summary>
        public uint Version;

        /// <summary>
        /// Tick when this snapshot was last updated.
        /// </summary>
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Static thresholds for colony supply classification.
    /// </summary>
    public static class ColonySupplyThresholds
    {
        /// <summary>
        /// Supply ratio threshold below which a colony is considered bottlenecked.
        /// Default: 60% supply/demand ratio.
        /// </summary>
        public const float BottleneckThreshold = 0.6f;

        /// <summary>
        /// Supply ratio threshold below which a colony is considered critical.
        /// Default: 30% supply/demand ratio.
        /// </summary>
        public const float CriticalThreshold = 0.3f;

        /// <summary>
        /// Classifies a supply ratio into a status category.
        /// </summary>
        /// <param name="supplyRatio">Supply ratio (0-1).</param>
        /// <returns>Classification status.</returns>
        public static ColonySupplyStatus Classify(float supplyRatio)
        {
            if (supplyRatio < CriticalThreshold)
            {
                return ColonySupplyStatus.Critical;
            }

            if (supplyRatio < BottleneckThreshold)
            {
                return ColonySupplyStatus.Bottleneck;
            }

            return ColonySupplyStatus.Healthy;
        }

        /// <summary>
        /// Classifies a supply ratio using custom thresholds.
        /// </summary>
        public static ColonySupplyStatus Classify(float supplyRatio, float bottleneckThreshold, float criticalThreshold)
        {
            if (supplyRatio < criticalThreshold)
            {
                return ColonySupplyStatus.Critical;
            }

            if (supplyRatio < bottleneckThreshold)
            {
                return ColonySupplyStatus.Bottleneck;
            }

            return ColonySupplyStatus.Healthy;
        }
    }

    /// <summary>
    /// Status classification for colony supply levels.
    /// </summary>
    public enum ColonySupplyStatus : byte
    {
        /// <summary>
        /// Supply ratio is above bottleneck threshold.
        /// </summary>
        Healthy = 0,

        /// <summary>
        /// Supply ratio is below bottleneck threshold but above critical.
        /// </summary>
        Bottleneck = 1,

        /// <summary>
        /// Supply ratio is below critical threshold.
        /// </summary>
        Critical = 2
    }

    /// <summary>
    /// Optional configuration for colony supply telemetry thresholds.
    /// Attach to the colony registry entity to override default thresholds.
    /// </summary>
    public struct ColonySupplyTelemetryConfig : IComponentData
    {
        /// <summary>
        /// Custom bottleneck threshold (0-1). Use 0 for default.
        /// </summary>
        public float BottleneckThreshold;

        /// <summary>
        /// Custom critical threshold (0-1). Use 0 for default.
        /// </summary>
        public float CriticalThreshold;

        /// <summary>
        /// Whether to publish telemetry metrics.
        /// </summary>
        public byte PublishTelemetry;

        public readonly float EffectiveBottleneckThreshold =>
            BottleneckThreshold > 0f ? BottleneckThreshold : ColonySupplyThresholds.BottleneckThreshold;

        public readonly float EffectiveCriticalThreshold =>
            CriticalThreshold > 0f ? CriticalThreshold : ColonySupplyThresholds.CriticalThreshold;

        public readonly bool ShouldPublishTelemetry => PublishTelemetry != 0;

        public static ColonySupplyTelemetryConfig CreateDefaults()
        {
            return new ColonySupplyTelemetryConfig
            {
                BottleneckThreshold = 0f, // Use default
                CriticalThreshold = 0f,   // Use default
                PublishTelemetry = 1
            };
        }
    }

    /// <summary>
    /// Telemetry metric keys for colony supply.
    /// </summary>
    public static class ColonySupplyTelemetryKeys
    {
        public static readonly FixedString64Bytes Demand = new FixedString64Bytes("registry.colonies.supply.demand");
        public static readonly FixedString64Bytes Shortage = new FixedString64Bytes("registry.colonies.supply.shortage");
        public static readonly FixedString64Bytes AvgRatio = new FixedString64Bytes("registry.colonies.supply.avgRatio");
        public static readonly FixedString64Bytes Bottleneck = new FixedString64Bytes("registry.colonies.supply.bottleneck");
        public static readonly FixedString64Bytes Critical = new FixedString64Bytes("registry.colonies.supply.critical");
        public static readonly FixedString64Bytes Total = new FixedString64Bytes("registry.colonies.supply.total");
    }

    /// <summary>
    /// Helper for accumulating colony supply metrics during registry rebuilds.
    /// </summary>
    public struct ColonySupplyAccumulator
    {
        public float TotalDemand;
        public float TotalSupply;
        public int TotalColonies;
        public int BottleneckCount;
        public int CriticalCount;

        private float _bottleneckThreshold;
        private float _criticalThreshold;

        public ColonySupplyAccumulator(float bottleneckThreshold = 0f, float criticalThreshold = 0f)
        {
            TotalDemand = 0f;
            TotalSupply = 0f;
            TotalColonies = 0;
            BottleneckCount = 0;
            CriticalCount = 0;
            _bottleneckThreshold = bottleneckThreshold > 0f ? bottleneckThreshold : ColonySupplyThresholds.BottleneckThreshold;
            _criticalThreshold = criticalThreshold > 0f ? criticalThreshold : ColonySupplyThresholds.CriticalThreshold;
        }

        /// <summary>
        /// Adds a colony's supply metrics to the accumulator.
        /// </summary>
        /// <param name="demand">Colony's total demand.</param>
        /// <param name="supply">Colony's total supply.</param>
        public void AddColony(float demand, float supply)
        {
            TotalColonies++;
            TotalDemand += demand;
            TotalSupply += supply;

            var ratio = demand > 0f ? math.saturate(supply / demand) : 1f;

            if (ratio < _criticalThreshold)
            {
                CriticalCount++;
            }
            else if (ratio < _bottleneckThreshold)
            {
                BottleneckCount++;
            }
        }

        /// <summary>
        /// Creates a supply snapshot from the accumulated metrics.
        /// </summary>
        public readonly ColonySupplySnapshot ToSnapshot(uint tick, uint version = 0)
        {
            var avgRatio = TotalDemand > 0f ? math.saturate(TotalSupply / TotalDemand) : 1f;
            var shortage = math.max(0f, TotalDemand - TotalSupply);

            return new ColonySupplySnapshot
            {
                TotalSupplyDemand = TotalDemand,
                TotalSupplyShortage = shortage,
                AverageSupplyRatio = avgRatio,
                BottleneckColonyCount = BottleneckCount,
                CriticalColonyCount = CriticalCount,
                TotalColonyCount = TotalColonies,
                Version = version,
                LastUpdateTick = tick
            };
        }

        /// <summary>
        /// Resets the accumulator for a new pass.
        /// </summary>
        public void Reset()
        {
            TotalDemand = 0f;
            TotalSupply = 0f;
            TotalColonies = 0;
            BottleneckCount = 0;
            CriticalCount = 0;
        }
    }
}

