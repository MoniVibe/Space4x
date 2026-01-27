using Unity.Burst;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Shared
{
    /// <summary>
    /// Burst-compiled utility functions for quality evaluation.
    /// All methods are deterministic and thread-safe.
    /// </summary>
    [BurstCompile]
    public static class QualityEval
    {
        /// <summary>
        /// Calculates quality Score01 (0-1) from input values using the formula blob.
        /// </summary>
        [BurstCompile]
        public static float Score01(
            ref QualityFormulaBlob formula,
            float purity01,
            float skill01,
            float station01,
            float recipe01)
        {
            ref var f = ref formula;
            var weightedSum =
                f.WMaterial * purity01 +
                f.WSkill * skill01 +
                f.WStation * station01 +
                f.WRecipe * recipe01 +
                f.Bias;

            return math.saturate(math.clamp(weightedSum, f.ClampMin, f.ClampMax));
        }

        /// <summary>
        /// Determines QualityTier from Score01 using tier cutoffs.
        /// Cutoffs are sorted ascending; returns the highest tier whose cutoff is met.
        /// </summary>
        [BurstCompile]
        public static QualityTier Tier(ref QualityFormulaBlob formula, float score01)
        {
            ref var cuts = ref formula.TierCutoffs01;
            if (cuts.Length == 0)
                return QualityTier.Poor;

            // Find highest tier whose cutoff is met
            int tierIndex = 0;
            for (int i = 0; i < cuts.Length; i++)
            {
                if (score01 >= cuts[i])
                {
                    tierIndex = i + 1;
                }
            }

            // Clamp to valid enum range
            return (QualityTier)math.clamp(tierIndex, 0, 4);
        }

        /// <summary>
        /// Samples a Curve1D at t01 (0-1) using linear interpolation between knots.
        /// Returns 1.0 if curve is empty or invalid.
        /// </summary>
        [BurstCompile]
        public static float SampleCurve(ref Curve1D curve, float t01)
        {
            ref var knots = ref curve.Knots;
            if (knots.Length == 0)
                return 1f;

            if (knots.Length == 1)
                return knots[0];

            float clampedT = math.saturate(t01);
            float floatIndex = clampedT * (knots.Length - 1);
            int i = (int)math.floor(floatIndex);
            int j = math.min(i + 1, knots.Length - 1);

            float a = knots[i];
            float b = knots[j];
            float fraction = floatIndex - i;

            return math.lerp(a, b, fraction);
        }

        /// <summary>
        /// Computes a deterministic provenance hash from input IDs and values.
        /// Used for audit trails and determinism verification.
        /// </summary>
        [BurstCompile]
        public static uint ComputeProvenanceHash(
            uint materialIdHash,
            float purity01,
            uint crafterIdHash,
            float skill01,
            uint stationIdHash,
            float station01,
            uint recipeIdHash,
            float recipe01)
        {
            // Combine all inputs into a stable hash
            uint hash = materialIdHash;
            hash = math.hash(new uint2(hash, math.asuint(purity01)));
            hash = math.hash(new uint2(hash, crafterIdHash));
            hash = math.hash(new uint2(hash, math.asuint(skill01)));
            hash = math.hash(new uint2(hash, stationIdHash));
            hash = math.hash(new uint2(hash, math.asuint(station01)));
            hash = math.hash(new uint2(hash, recipeIdHash));
            hash = math.hash(new uint2(hash, math.asuint(recipe01)));
            return hash;
        }
    }
}
