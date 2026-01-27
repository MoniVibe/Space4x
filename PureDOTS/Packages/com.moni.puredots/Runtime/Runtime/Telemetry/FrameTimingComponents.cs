using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Telemetry
{
    /// <summary>
    /// High-level system groups tracked by frame timing diagnostics.
    /// </summary>
    public enum FrameTimingGroup : byte
    {
        Unknown = 0,
        Time = 1,
        Environment = 2,
        Spatial = 3,
        AI = 4,
        Villager = 5,
        Resource = 6,
        Miracle = 7,
        Gameplay = 8,
        History = 9,
        Presentation = 10,
        Hand = 11,
        Camera = 12,  // Highest priority - runs first in simulation
        Transport = 13,
        Custom = 255
    }

    /// <summary>
    /// Flags describing additional context for a frame timing sample.
    /// </summary>
    public enum FrameTimingFlags : byte
    {
        None = 0,
        BudgetExceeded = 1 << 0,
        CatchUp = 1 << 1
    }

    /// <summary>
    /// Singleton describing the current frame timing stream version.
    /// </summary>
    public struct FrameTimingStream : IComponentData
    {
        public uint Version;
        public uint LastTick;
    }

    /// <summary>
    /// Captures the latest duration for a tracked system group.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct FrameTimingSample : IBufferElementData
    {
        public FrameTimingGroup Group;
        public float DurationMs;
        public float BudgetMs;
        public FrameTimingFlags Flags;
        public int SystemCount;
    }

    /// <summary>
    /// Aggregated allocation diagnostics surfaced alongside frame timings.
    /// </summary>
    public struct AllocationDiagnostics : IComponentData
    {
        public long TotalAllocatedBytes;
        public long TotalReservedBytes;
        public long TotalUnusedReservedBytes;
        public int GcCollectionsGeneration0;
        public int GcCollectionsGeneration1;
        public int GcCollectionsGeneration2;
    }

    /// <summary>
    /// Helper utilities for translating frame timing metadata to labels and budgets.
    /// </summary>
    public static class FrameTimingUtility
    {
        public static float GetBudgetMs(FrameTimingGroup group)
        {
            return group switch
            {
                FrameTimingGroup.Camera => 0.5f,  // High priority, must be fast
                FrameTimingGroup.Time => 0.5f,
                FrameTimingGroup.Environment => 2f,
                FrameTimingGroup.Spatial => 1f,
                FrameTimingGroup.AI => 1.5f,
                FrameTimingGroup.Villager => 2f,
                FrameTimingGroup.Resource => 1.5f,
                FrameTimingGroup.Miracle => 1.5f,
                FrameTimingGroup.Gameplay => 4f,
                FrameTimingGroup.Transport => 1.25f,
                FrameTimingGroup.History => 1f,
                FrameTimingGroup.Presentation => 1.5f,
                FrameTimingGroup.Hand => 0.75f,
                _ => 0f
            };
        }

        public static FixedString32Bytes GetGroupLabel(FrameTimingGroup group)
        {
            return group switch
            {
                FrameTimingGroup.Camera => "Camera",
                FrameTimingGroup.Time => "Time",
                FrameTimingGroup.Environment => "Environment",
                FrameTimingGroup.Spatial => "Spatial",
                FrameTimingGroup.AI => "AI",
                FrameTimingGroup.Villager => "Villager",
                FrameTimingGroup.Resource => "Resource",
                FrameTimingGroup.Miracle => "Miracle",
                FrameTimingGroup.Gameplay => "Gameplay",
                FrameTimingGroup.Transport => "Transport",
                FrameTimingGroup.History => "History",
                FrameTimingGroup.Presentation => "Presentation",
                FrameTimingGroup.Hand => "Hand",
                FrameTimingGroup.Custom => "Custom",
                _ => "Unknown"
            };
        }

        public static FixedString64Bytes GetMetricKey(FrameTimingGroup group)
        {
            return group switch
            {
                FrameTimingGroup.Camera => "timing.camera",
                FrameTimingGroup.Time => "timing.time",
                FrameTimingGroup.Environment => "timing.environment",
                FrameTimingGroup.Spatial => "timing.spatial",
                FrameTimingGroup.AI => "timing.ai",
                FrameTimingGroup.Villager => "timing.villager",
                FrameTimingGroup.Resource => "timing.resource",
                FrameTimingGroup.Miracle => "timing.miracle",
                FrameTimingGroup.Gameplay => "timing.gameplay",
                FrameTimingGroup.Transport => "timing.transport",
                FrameTimingGroup.History => "timing.history",
                FrameTimingGroup.Presentation => "timing.presentation",
                FrameTimingGroup.Hand => "timing.hand",
                FrameTimingGroup.Custom => "timing.custom",
                _ => "timing.unknown"
            };
        }
    }
}
