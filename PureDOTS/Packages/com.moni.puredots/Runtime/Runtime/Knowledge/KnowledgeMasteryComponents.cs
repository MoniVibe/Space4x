using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Knowledge
{
    /// <summary>
    /// Buffer tracking mastery tiers for individual lessons.
    /// Extends the basic VillagerKnowledge lesson progress with tier tracking.
    /// </summary>
    public struct LessonMastery : IBufferElementData
    {
        /// <summary>
        /// Lesson identifier.
        /// </summary>
        public FixedString64Bytes LessonId;

        /// <summary>
        /// Current mastery tier.
        /// </summary>
        public MasteryTier Tier;

        /// <summary>
        /// Progress within current tier (0-1).
        /// </summary>
        public float TierProgress;

        /// <summary>
        /// Normalized progress toward the next tier (0-1), used for decay smoothing.
        /// </summary>
        public float Progress;

        /// <summary>
        /// Total XP accumulated for this lesson.
        /// </summary>
        public float TotalXp;

        /// <summary>
        /// Tick when mastery was last updated.
        /// </summary>
        public uint LastProgressTick;
    }

    /// <summary>
    /// Mastery tier levels (matches traditional crafting/skill tiers).
    /// </summary>
    public enum MasteryTier : byte
    {
        /// <summary>
        /// No mastery established.
        /// </summary>
        None = 255,

        /// <summary>
        /// Just started learning (0-20% progress).
        /// </summary>
        Novice = 0,

        /// <summary>
        /// Basic understanding (20-40% progress).
        /// </summary>
        Apprentice = 1,

        /// <summary>
        /// Competent practitioner (40-60% progress).
        /// </summary>
        Journeyman = 2,

        /// <summary>
        /// High skill (60-80% progress).
        /// </summary>
        Expert = 3,

        /// <summary>
        /// Peak mastery (80-100% progress).
        /// </summary>
        Master = 4,

        /// <summary>
        /// Legendary mastery (100%+ with exceptional achievements).
        /// </summary>
        Grandmaster = 5
    }

    /// <summary>
    /// Utility for mastery tier calculations.
    /// </summary>
    public static class MasteryTierUtility
    {
        /// <summary>
        /// Progress thresholds for each tier.
        /// </summary>
        public static readonly float[] TierThresholds = { 0f, 0.2f, 0.4f, 0.6f, 0.8f, 1.0f };

        /// <summary>
        /// Get mastery tier from total progress (0-1).
        /// </summary>
        public static MasteryTier GetTierFromProgress(float progress)
        {
            if (progress >= 1.0f) return MasteryTier.Grandmaster;
            if (progress >= 0.8f) return MasteryTier.Master;
            if (progress >= 0.6f) return MasteryTier.Expert;
            if (progress >= 0.4f) return MasteryTier.Journeyman;
            if (progress >= 0.2f) return MasteryTier.Apprentice;
            return MasteryTier.Novice;
        }

        /// <summary>
        /// Get progress within current tier (0-1).
        /// </summary>
        public static float GetProgressWithinTier(float totalProgress)
        {
            var tier = GetTierFromProgress(totalProgress);
            var tierIndex = (int)tier;
            if (tierIndex >= TierThresholds.Length - 1) return 1f;

            var tierStart = TierThresholds[tierIndex];
            var tierEnd = TierThresholds[tierIndex + 1];
            var tierRange = tierEnd - tierStart;

            if (tierRange <= 0f) return 1f;
            return (totalProgress - tierStart) / tierRange;
        }

        /// <summary>
        /// Get display name for mastery tier.
        /// </summary>
        public static FixedString32Bytes GetTierName(MasteryTier tier)
        {
            return tier switch
            {
                MasteryTier.None => "None",
                MasteryTier.Novice => "Novice",
                MasteryTier.Apprentice => "Apprentice",
                MasteryTier.Journeyman => "Journeyman",
                MasteryTier.Expert => "Expert",
                MasteryTier.Master => "Master",
                MasteryTier.Grandmaster => "Grandmaster",
                _ => "Unknown"
            };
        }
    }

    /// <summary>
    /// Event raised when an entity achieves a new mastery tier.
    /// </summary>
    public struct MasteryAchievedEvent : IBufferElementData
    {
        public FixedString64Bytes LessonId;
        public MasteryTier NewTier;
        public MasteryTier PreviousTier;
        public uint AchievedTick;
    }

    /// <summary>
    /// Bonus from achieving mastery in a lesson.
    /// </summary>
    public struct MasteryBonus : IBufferElementData
    {
        public FixedString64Bytes LessonId;
        public MasteryTier RequiredTier;
        public MasteryBonusType BonusType;
        public float BonusValue;
        public FixedString64Bytes TargetId;  // Skill, stat, or ability affected
    }

    /// <summary>
    /// Type of bonus from mastery.
    /// </summary>
    public enum MasteryBonusType : byte
    {
        SkillBonus = 0,
        StatBonus = 1,
        YieldBonus = 2,
        SpeedBonus = 3,
        QualityBonus = 4,
        UnlockAbility = 5,
        UnlockRecipe = 6
    }
}

