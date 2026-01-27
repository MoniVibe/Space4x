using Unity.Entities;

namespace PureDOTS.Runtime.Telemetry
{
    /// <summary>
    /// High-level performance budgets used for HUD display and editor assertions.
    /// The values are conservative defaults and can be tuned per project via authoring or runtime config.
    /// </summary>
    public struct PerformanceBudgetSettings : IComponentData
    {
        /// <summary>Maximum budget for a fixed tick in milliseconds.</summary>
        public float FixedTickBudgetMs;
        /// <summary>Expected footprint for the snapshot ring buffer (bytes).</summary>
        public int SnapshotRingBudgetBytes;
        /// <summary>Expected footprint for the command ring buffer (bytes).</summary>
        public int CommandRingBudgetBytes;
        /// <summary>Maximum active presentation companions allowed.</summary>
        public int CompanionBudget;
    }

    public static class PerformanceBudgetDefaults
    {
        public const float FixedTickBudgetMs = 8f;
        public const int SnapshotRingBudgetBytes = 512 * 1024;
        public const int CommandRingBudgetBytes = 256 * 1024;
        public const int CompanionBudget = 512;

        public static PerformanceBudgetSettings CreateDefault() => new PerformanceBudgetSettings
        {
            FixedTickBudgetMs = FixedTickBudgetMs,
            SnapshotRingBudgetBytes = SnapshotRingBudgetBytes,
            CommandRingBudgetBytes = CommandRingBudgetBytes,
            CompanionBudget = CompanionBudget
        };
    }
}
