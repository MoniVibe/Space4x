using System;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Resource
{
    /// <summary>
    /// Quality tiers used by both Godgame and Space4X resource loops.
    /// </summary>
    public enum ResourceQualityTier : byte
    {
        Unknown = 0,
        Poor = 1,
        Common = 2,
        Uncommon = 3,
        Rare = 4,
        Epic = 5,
        Legendary = 6,
        Relic = 7
    }

    /// <summary>
    /// Utility helpers for converting skill/quality to deterministic scalars shared by both games.
    /// </summary>
    public static class ResourceQualityUtility
    {
        private static readonly ushort[] s_TierUpperBounds =
        {
            0, 49, 99, 199, 399, 499, 549, 600
        };

        private static readonly float[] s_TierSkillRequirements =
        {
            0f, // Unknown
            0f, // Poor
            0f, // Common
            20f, // Uncommon
            40f, // Rare
            70f, // Epic
            90f, // Legendary
            120f // Relic
        };

        /// <summary>
        /// Returns the quality tier for a given 1-600 quality value.
        /// </summary>
        public static ResourceQualityTier DetermineTier(ushort quality)
        {
            for (byte tier = 1; tier < s_TierUpperBounds.Length; tier++)
            {
                if (quality <= s_TierUpperBounds[tier])
                {
                    return (ResourceQualityTier)tier;
                }
            }

            return ResourceQualityTier.Relic;
        }

        /// <summary>
        /// Returns the recommended minimum skill to reliably harvest the supplied tier.
        /// Used for AI heuristics and lesson requirement checks.
        /// </summary>
        public static float GetRecommendedSkill(ResourceQualityTier tier)
        {
            var index = (int)math.clamp((int)tier, 0, s_TierSkillRequirements.Length - 1);
            return s_TierSkillRequirements[index];
        }

        /// <summary>
        /// Deterministic multiplier applied to harvest/processing durations given a worker skill.
        /// </summary>
        public static float GetHarvestTimeMultiplier(float skill)
        {
            if (skill <= 20f)
            {
                // Lerp between 20x at skill 0 and 10x at skill 20
                return math.lerp(20f, 10f, math.saturate(skill / 20f));
            }

            if (skill <= 40f)
            {
                // 10x -> 0.5x
                var t = (skill - 20f) / 20f;
                return math.lerp(10f, 0.5f, t);
            }

            if (skill <= 70f)
            {
                var t = (skill - 40f) / 30f;
                return math.lerp(0.5f, 0.4f, t);
            }

            if (skill <= 100f)
            {
                var t = (skill - 70f) / 30f;
                return math.lerp(0.4f, 0.2f, t);
            }

            // Diminishing returns past 100
            var overSkill = math.max(0f, skill - 100f);
            var decay = math.max(0f, 0.2f * (1f - 0.1f * math.log(1f + overSkill)));
            return math.max(0.1f, decay);
        }

        /// <summary>
        /// Applies skill + lesson bonuses to a target quality band.
        /// </summary>
        public static ushort ClampQualityWithSkill(ushort desiredQuality, float skill, float lessonBonus, bool hasLessonForTier)
        {
            var result = desiredQuality;
            var tier = DetermineTier(desiredQuality);
            var requirement = GetRecommendedSkill(tier);
            var cappedQuality = desiredQuality;

            if (!hasLessonForTier && tier >= ResourceQualityTier.Legendary)
            {
                cappedQuality = (ushort)math.min((int)desiredQuality, (int)s_TierUpperBounds[(int)ResourceQualityTier.Legendary]);
            }

            if (skill + lessonBonus < requirement)
            {
                var ratio = math.saturate((skill + lessonBonus) / math.max(1f, requirement));
                cappedQuality = (ushort)math.round(math.lerp(100f, cappedQuality, ratio));
            }

            result = (ushort)math.clamp(cappedQuality + lessonBonus, 1f, 600f);
            return result;
        }

        public static ushort BlendQuality(ushort existingQuality, float existingAmount, ushort incomingQuality, float incomingAmount)
        {
            var total = existingAmount + incomingAmount;
            if (total <= 0f)
            {
                return incomingQuality;
            }

            var weighted = (existingQuality * existingAmount) + (incomingQuality * incomingAmount);
            return (ushort)math.clamp(math.round(weighted / total), 1f, 600f);
        }
    }
}
