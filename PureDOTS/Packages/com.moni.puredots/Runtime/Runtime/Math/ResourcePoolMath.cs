using Unity.Mathematics;
using PureDOTS.Runtime.Resources;

namespace PureDOTS.Runtime.Math
{
    /// <summary>
    /// Shared deterministic math helpers for rechargeable resource pools.
    /// </summary>
    public static class ResourcePoolMath
    {
        public static void AccumulateModifier(ref ResourcePoolModifier accumulator, in ResourcePoolModifier modifier)
        {
            accumulator.AdditiveMax += modifier.AdditiveMax;
            accumulator.MultiplicativeMax *= modifier.MultiplicativeMax <= 0f ? 1f : modifier.MultiplicativeMax;
            accumulator.AdditiveRegenPerSecond += modifier.AdditiveRegenPerSecond;
            accumulator.MultiplicativeRegen *= modifier.MultiplicativeRegen <= 0f ? 1f : modifier.MultiplicativeRegen;
        }

        public static float ResolveModifiedMax(float baseMax, float additiveMax, float multiplicativeMax)
        {
            var safeBase = math.max(0f, baseMax);
            var safeMult = math.max(0.01f, multiplicativeMax);
            return math.max(0f, (safeBase + additiveMax) * safeMult);
        }

        public static float ResolveModifiedRate(float baseRate, float additiveRate, float multiplicativeRate)
        {
            var safeBase = math.max(0f, baseRate);
            var safeMult = math.max(0f, multiplicativeRate);
            return math.max(0f, (safeBase + additiveRate) * safeMult);
        }

        public static float ClampCurrent(float current, float maxValue)
        {
            return math.clamp(current, 0f, math.max(0f, maxValue));
        }

        public static float Regen(float current, float maxValue, float regenPerSecond, float deltaSeconds)
        {
            var maxClamped = math.max(0f, maxValue);
            var regen = math.max(0f, regenPerSecond) * math.max(0f, deltaSeconds);
            return math.min(maxClamped, math.max(0f, current) + regen);
        }

        public static bool CanSpend(float current, float amount)
        {
            return math.max(0f, current) >= math.max(0f, amount);
        }

        public static bool TrySpend(ref float current, float amount)
        {
            var spend = math.max(0f, amount);
            if (!CanSpend(current, spend))
            {
                return false;
            }

            current = math.max(0f, current - spend);
            return true;
        }

        public static float ResolveSpendCost(float amount, float costMultiplier)
        {
            return math.max(0f, amount) * math.max(0f, costMultiplier);
        }
    }
}
