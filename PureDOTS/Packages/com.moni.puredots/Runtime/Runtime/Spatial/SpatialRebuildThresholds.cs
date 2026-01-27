using Unity.Entities;

namespace PureDOTS.Runtime.Spatial
{
    /// <summary>
    /// Configurable thresholds that control when the spatial grid rebuilds partially vs. fully.
    /// Attach to the spatial grid config entity or create as a singleton for global defaults.
    /// </summary>
    public struct SpatialRebuildThresholds : IComponentData
    {
        /// <summary>
        /// Maximum number of dirty operations before forcing a full rebuild (default: 1024).
        /// When dirty ops exceed this count, a full rebuild is triggered regardless of ratio.
        /// </summary>
        public int MaxDirtyOpsForPartialRebuild;

        /// <summary>
        /// Maximum ratio of dirty operations to total entries before forcing a full rebuild (default: 0.35f).
        /// When dirtyRatio >= this value, a full rebuild is triggered.
        /// Range: 0.0 to 1.0 (representing 0% to 100%).
        /// </summary>
        public float MaxDirtyRatioForPartialRebuild;

        /// <summary>
        /// Minimum total entry count required before partial rebuild logic applies (default: 100).
        /// Below this count, full rebuilds are always used for simplicity.
        /// </summary>
        public int MinEntryCountForPartialRebuild;

        /// <summary>
        /// Creates default thresholds suitable for most scenarios.
        /// </summary>
        public static SpatialRebuildThresholds CreateDefaults()
        {
            return new SpatialRebuildThresholds
            {
                MaxDirtyOpsForPartialRebuild = 1024,
                MaxDirtyRatioForPartialRebuild = 0.35f,
                MinEntryCountForPartialRebuild = 100
            };
        }
    }
}

