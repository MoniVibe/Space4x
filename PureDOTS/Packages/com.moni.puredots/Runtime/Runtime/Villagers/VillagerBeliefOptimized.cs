using Unity.Entities;

namespace PureDOTS.Runtime.Villagers
{
    /// <summary>
    /// Optimized belief component using byte index instead of FixedString.
    /// Size: 6 bytes (vs 72 bytes for VillagerBelief with FixedString64Bytes).
    /// 
    /// The deity ID is stored as an index into a DeityCatalog blob asset.
    /// This component is hot-path friendly for belief/worship systems.
    /// </summary>
    public struct VillagerBeliefOptimized : IComponentData
    {
        /// <summary>
        /// Index into DeityCatalog blob asset (0-255 deities supported).
        /// Use DeityCatalog.GetDeityId(index) to retrieve actual ID string.
        /// </summary>
        public byte PrimaryDeityIndex;

        /// <summary>
        /// Belief strength (0-255 mapped to 0.0-1.0).
        /// Use FaithNormalized property for 0-1 float value.
        /// </summary>
        public byte Faith;

        /// <summary>
        /// Worship progress (0-255 mapped to 0.0-1.0).
        /// Use WorshipProgressNormalized property for 0-1 float value.
        /// </summary>
        public byte WorshipProgress;

        /// <summary>
        /// Reserved for future flags or secondary deity index.
        /// </summary>
        public byte Flags;

        /// <summary>
        /// Tick when belief was last updated (for staleness checks).
        /// </summary>
        public ushort LastUpdateTick;

        // Convenience accessors
        public float FaithNormalized => Faith / 255f;
        public float WorshipProgressNormalized => WorshipProgress / 255f;

        public void SetFaith(float value)
        {
            Faith = (byte)(Unity.Mathematics.math.saturate(value) * 255f);
        }

        public void SetWorshipProgress(float value)
        {
            WorshipProgress = (byte)(Unity.Mathematics.math.saturate(value) * 255f);
        }
    }

    /// <summary>
    /// Flags for VillagerBeliefOptimized.
    /// </summary>
    public static class VillagerBeliefFlags
    {
        public const byte None = 0;
        public const byte HasSecondaryDeity = 1 << 0;
        public const byte IsDevout = 1 << 1;
        public const byte IsFaltering = 1 << 2;
        public const byte HasWorship = 1 << 3;
    }
}

