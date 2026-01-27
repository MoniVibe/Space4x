using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Telemetry
{
    /// <summary>
    /// Performance metrics collected during scale scenarios.
    /// Singleton component updated by ScaleTestMetricsSystem.
    /// </summary>
    public struct ScaleTestMetrics : IComponentData
    {
        // Entity counts per archetype
        public int VillagerCount;
        public int ResourceChunkCount;
        public int ProjectileCount;
        public int CarrierCount;
        public int AggregateCount;
        public int TotalEntityCount;

        // Timing (milliseconds)
        public float TotalTickTime;
        public float MovementSystemTime;
        public float AISystemTime;
        public float SpatialGridTime;
        public float RegistryUpdateTime;

        // Rates
        public int PathfindingOpsPerTick;
        public int AIDecisionsPerTick;
        public int CollisionChecksPerTick;
        public int SpatialQueriesPerTick;

        // Memory (bytes)
        public long TotalMemoryBytes;
        public long ChunkMemoryBytes;

        // Tick info
        public uint CurrentTick;
        public float AverageTickTime;
        public float MaxTickTime;
        public float MinTickTime;
        public float P95TickTime;
        public float P99TickTime;

        // Sample tracking
        public int SampleCount;
        public float SumTickTime;
        public float SumSquaredTickTime;
    }

    /// <summary>
    /// Configuration for scale test metrics collection.
    /// </summary>
    public struct ScaleTestMetricsConfig : IComponentData
    {
        /// <summary>
        /// Ticks between metric samples.
        /// </summary>
        public uint SampleInterval;

        /// <summary>
        /// Ticks between log outputs.
        /// </summary>
        public uint LogInterval;

        /// <summary>
        /// Whether to collect per-system timing.
        /// </summary>
        public byte CollectSystemTimings;

        /// <summary>
        /// Whether to collect memory statistics.
        /// </summary>
        public byte CollectMemoryStats;

        /// <summary>
        /// Target tick time in milliseconds (for budget validation).
        /// </summary>
        public float TargetTickTimeMs;

        /// <summary>
        /// Target memory in megabytes (for budget validation).
        /// </summary>
        public float TargetMemoryMB;

        /// <summary>
        /// Enable LOD debug logging (counts entities per LOD level).
        /// </summary>
        public byte EnableLODDebug;

        /// <summary>
        /// Enable aggregate debug logging (aggregate counts and summaries).
        /// </summary>
        public byte EnableAggregateDebug;
    }

    /// <summary>
    /// Debug metrics for LOD system (optional, only collected if EnableLODDebug is set).
    /// </summary>
    public struct LODDebugMetrics : IComponentData
    {
        public int LOD0Count;  // Full detail
        public int LOD1Count;  // Reduced detail
        public int LOD2Count;  // Impostor
        public int LOD3Count;  // Hidden
        public int ShouldRenderCount;
        public int CulledCount;
        public float AverageCameraDistance;
        public float AverageImportanceScore;
    }

    /// <summary>
    /// Debug metrics for aggregate system (optional, only collected if EnableAggregateDebug is set).
    /// </summary>
    public struct AggregateDebugMetrics : IComponentData
    {
        public int AggregateCount;
        public int TotalMemberCount;
        public int AverageMembersPerAggregate;
        public int MinMembersPerAggregate;
        public int MaxMembersPerAggregate;
        public float AverageTotalHealth;
        public float AverageTotalStrength;
        public uint LastAggregationUpdateTick;
    }

    /// <summary>
    /// Buffer element for storing tick time samples (for percentile calculation).
    /// </summary>
    [InternalBufferCapacity(100)]
    public struct TickTimeSample : IBufferElementData
    {
        public float TickTimeMs;
        public uint Tick;
    }

    /// <summary>
    /// Scale test result summary for reporting.
    /// </summary>
    public struct ScaleTestResult : IComponentData
    {
        public FixedString64Bytes ScenarioId;
        public uint TotalTicks;
        public int TotalEntities;
        public float AverageTickTimeMs;
        public float MaxTickTimeMs;
        public float P95TickTimeMs;
        public float P99TickTimeMs;
        public long PeakMemoryBytes;
        public byte PassedBudget; // 1 = passed, 0 = failed
        public uint CompletedTick;
    }

    /// <summary>
    /// Performance budget thresholds for validation.
    /// </summary>
    public struct PerformanceBudget : IComponentData
    {
        /// <summary>
        /// Maximum allowed tick time in milliseconds.
        /// </summary>
        public float MaxTickTimeMs;

        /// <summary>
        /// Maximum allowed memory in megabytes.
        /// </summary>
        public float MaxMemoryMB;

        /// <summary>
        /// Maximum components on hot entities.
        /// </summary>
        public int MaxHotComponents;

        /// <summary>
        /// Maximum size of hot components in bytes.
        /// </summary>
        public int MaxHotComponentSize;

        /// <summary>
        /// Movement system budget in milliseconds.
        /// </summary>
        public float MovementBudgetMs;

        /// <summary>
        /// AI system budget in milliseconds.
        /// </summary>
        public float AIBudgetMs;

        /// <summary>
        /// Spatial grid budget in milliseconds.
        /// </summary>
        public float SpatialBudgetMs;

        /// <summary>
        /// Registry update budget in milliseconds.
        /// </summary>
        public float RegistryBudgetMs;

        /// <summary>
        /// Creates default budgets for 100k entity scale.
        /// </summary>
        public static PerformanceBudget Default100k => new PerformanceBudget
        {
            MaxTickTimeMs = 33.33f, // 30 FPS
            MaxMemoryMB = 2048f,
            MaxHotComponents = 12,
            MaxHotComponentSize = 128,
            MovementBudgetMs = 5f,
            AIBudgetMs = 8f,
            SpatialBudgetMs = 3f,
            RegistryBudgetMs = 2f
        };

        /// <summary>
        /// Creates default budgets for 10k entity scale.
        /// </summary>
        public static PerformanceBudget Default10k => new PerformanceBudget
        {
            MaxTickTimeMs = 16.67f, // 60 FPS
            MaxMemoryMB = 512f,
            MaxHotComponents = 12,
            MaxHotComponentSize = 128,
            MovementBudgetMs = 2f,
            AIBudgetMs = 4f,
            SpatialBudgetMs = 1f,
            RegistryBudgetMs = 1f
        };
    }
}

