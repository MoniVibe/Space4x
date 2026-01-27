using Unity.Entities;

namespace PureDOTS.Runtime.Config
{
    /// <summary>
    /// Runtime component storing threading configuration.
    /// Baked from PureDotsRuntimeConfig asset.
    /// </summary>
    public struct ThreadingSettingsConfig : IComponentData
    {
        public int OverrideWorkerCount; // 0 = use Unity default
        public bool EnableColdThrottling;
        public int HistorySnapshotCadence; // Every N frames
        public float ColdPathTimeBudget; // Max time per frame (seconds)
        public bool BurstCompileSynchronously;
    }
}

