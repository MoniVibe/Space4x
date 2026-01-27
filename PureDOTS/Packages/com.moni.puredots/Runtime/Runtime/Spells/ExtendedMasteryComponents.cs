using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Spells
{
    /// <summary>
    /// Extended spell mastery tracking (0-400%).
    /// Replaces/supplements LearnedSpell.MasteryLevel (0-255) with float precision.
    /// </summary>
    public struct ExtendedSpellMastery : IBufferElementData
    {
        /// <summary>
        /// Spell identifier from catalog.
        /// </summary>
        public FixedString64Bytes SpellId;

        /// <summary>
        /// Mastery progress (0.0 = 0%, 1.0 = 100%, 4.0 = 400%).
        /// </summary>
        public float MasteryProgress;

        /// <summary>
        /// Number of times this spell was observed being cast.
        /// </summary>
        public uint ObservationCount;

        /// <summary>
        /// Number of practice attempts (including failed casts).
        /// </summary>
        public uint PracticeAttempts;

        /// <summary>
        /// Number of successful casts.
        /// </summary>
        public uint SuccessfulCasts;

        /// <summary>
        /// Number of failed casts (fizzled/interrupted).
        /// </summary>
        public uint FailedCasts;

        /// <summary>
        /// Unlocked signatures (bitflags for 200%, 300%, 400% milestones).
        /// </summary>
        public SpellSignatureFlags Signatures;

        /// <summary>
        /// Spell ID this was hybridized with (for 400% milestone).
        /// Empty if not hybridized.
        /// </summary>
        public FixedString64Bytes HybridWithSpellId;

        /// <summary>
        /// Tick when mastery was last updated.
        /// </summary>
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Mastery milestone thresholds.
    /// </summary>
    public enum SpellMasteryMilestone : byte
    {
        Observing = 0,      // 0-20%: Can only observe
        Practicing = 1,     // 20-50%: Can practice (low success)
        Casting = 2,        // 50-100%: Success rate scales up
        Mastered = 3,       // 100%: 100% success rate
        Signature1 = 4,     // 200%: First signature unlocked
        Signature2 = 5,     // 300%: Second signature unlocked
        Signature3 = 6      // 400%: Third signature + hybridization
    }

    /// <summary>
    /// Flags for unlocked signatures (bitflags).
    /// </summary>
    [System.Flags]
    public enum SpellSignatureFlags : byte
    {
        None = 0,
        Signature1Unlocked = 1 << 0,  // 200% milestone
        Signature2Unlocked = 1 << 1,  // 300% milestone
        Signature3Unlocked = 1 << 2,  // 400% milestone
        HybridizationUnlocked = 1 << 3 // Can hybridize at 400%
    }

    /// <summary>
    /// Utility for mastery calculations.
    /// </summary>
    public static class SpellMasteryUtility
    {
        /// <summary>
        /// Get success chance based on mastery progress.
        /// - 0-20%: Cannot cast (0%)
        /// - 20-50%: Practice mode (0-50% success)
        /// - 50-100%: Success rate scales 50-100%
        /// - 100%+: Always succeeds (100%)
        /// </summary>
        public static float GetSuccessChance(float masteryProgress)
        {
            if (masteryProgress < 0.2f) return 0f;
            if (masteryProgress < 0.5f) return (masteryProgress - 0.2f) * 1.67f; // (0.5-0.2) * 1.67 = 0.5
            if (masteryProgress < 1.0f) return 0.5f + (masteryProgress - 0.5f); // 0.5 + 0.5 = 1.0
            return 1f;
        }

        /// <summary>
        /// Get current milestone based on mastery progress.
        /// </summary>
        public static SpellMasteryMilestone GetMilestone(float masteryProgress)
        {
            if (masteryProgress >= 4.0f) return SpellMasteryMilestone.Signature3;
            if (masteryProgress >= 3.0f) return SpellMasteryMilestone.Signature2;
            if (masteryProgress >= 2.0f) return SpellMasteryMilestone.Signature1;
            if (masteryProgress >= 1.0f) return SpellMasteryMilestone.Mastered;
            if (masteryProgress >= 0.5f) return SpellMasteryMilestone.Casting;
            if (masteryProgress >= 0.2f) return SpellMasteryMilestone.Practicing;
            return SpellMasteryMilestone.Observing;
        }

        /// <summary>
        /// Check if entity can attempt to cast at current mastery.
        /// </summary>
        public static bool CanAttemptCast(float masteryProgress)
        {
            return masteryProgress >= 0.2f;
        }

        /// <summary>
        /// Check if entity can practice (attempt with low success).
        /// </summary>
        public static bool CanPractice(float masteryProgress)
        {
            return masteryProgress >= 0.2f && masteryProgress < 1.0f;
        }
    }
}

