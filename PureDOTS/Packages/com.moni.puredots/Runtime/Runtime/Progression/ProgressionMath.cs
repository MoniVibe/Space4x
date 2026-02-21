using Unity.Mathematics;

namespace PureDOTS.Runtime.Progression
{
    /// <summary>
    /// Shared deterministic helpers for skill/proficiency accumulation and milestone checks.
    /// </summary>
    public static class ProgressionMath
    {
        public static float ResolveLearningMultiplier(float normalizedStat01, float minMultiplier, float maxMultiplier)
        {
            var stat01 = math.saturate(normalizedStat01);
            var min = math.max(0f, minMultiplier);
            var max = math.max(min, maxMultiplier);
            return math.lerp(min, max, stat01);
        }

        public static float ResolveSkill01FromPractice(
            float practiceSeconds,
            float secondsToMastery,
            float wisdom01,
            float aptitude01,
            float wisdomMultiplierMin,
            float wisdomMultiplierMax,
            float aptitudeMultiplierMin,
            float aptitudeMultiplierMax)
        {
            var safePractice = math.max(0f, practiceSeconds);
            var wisdomFactor = ResolveLearningMultiplier(wisdom01, wisdomMultiplierMin, wisdomMultiplierMax);
            var aptitudeFactor = ResolveLearningMultiplier(aptitude01, aptitudeMultiplierMin, aptitudeMultiplierMax);
            var effectiveSeconds = safePractice * wisdomFactor * aptitudeFactor;
            return ResolveSkill01(effectiveSeconds, secondsToMastery);
        }

        public static float ResolveSkill01(float effectivePracticeSeconds, float secondsToMastery)
        {
            var safePractice = math.max(0f, effectivePracticeSeconds);
            var mastery = math.max(1e-5f, secondsToMastery);
            return math.saturate(safePractice / mastery);
        }

        public static void AccumulatePositive(ref float accumulator, float delta)
        {
            if (delta <= 0f)
            {
                return;
            }

            accumulator += delta;
        }

        public static float ResolveLinearMilestoneThreshold(float baseThreshold, float perMilestoneStep, int milestoneIndex)
        {
            var index = math.max(0, milestoneIndex);
            var baseValue = math.max(0f, baseThreshold);
            var step = math.max(0f, perMilestoneStep);
            return baseValue + step * index;
        }

        public static int ResolveLinearMilestoneCount(float totalValue, float baseThreshold, float perMilestoneStep, int maxMilestones = 32)
        {
            var value = math.max(0f, totalValue);
            var cap = math.max(0, maxMilestones);
            var count = 0;
            for (var i = 0; i < cap; i++)
            {
                var threshold = ResolveLinearMilestoneThreshold(baseThreshold, perMilestoneStep, i);
                if (value + 1e-5f < threshold)
                {
                    break;
                }

                count++;
            }

            return count;
        }
    }
}
