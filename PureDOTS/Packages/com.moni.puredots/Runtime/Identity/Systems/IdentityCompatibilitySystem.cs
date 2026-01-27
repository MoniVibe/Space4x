using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Identity
{
    /// <summary>
    /// Calculates compatibility between entities based on all four identity layers:
    /// Alignment, Outlook, Personality, and Might/Magic Affinity.
    /// Used by relations system for initial relation calculation.
    /// </summary>
    [BurstCompile]
    public static class IdentityCompatibility
    {
        /// <summary>
        /// Calculate full compatibility score from all four layers.
        /// Returns value typically in range -100 to +100, but can exceed for extreme matches/mismatches.
        /// </summary>
        [BurstCompile]
        public static float CalculateCompatibility(
            in EntityAlignment align1, in EntityOutlook outlook1, in PersonalityAxes personality1, in MightMagicAffinity affinity1,
            in EntityAlignment align2, in EntityOutlook outlook2, in PersonalityAxes personality2, in MightMagicAffinity affinity2)
        {
            float total = 0f;

            total += CalculateAlignmentCompatibility(in align1, in align2);
            total += CalculateOutlookCompatibility(in outlook1, in outlook2);
            total += CalculatePersonalityCompatibility(in personality1, in personality2);
            total += CalculateMightMagicCompatibility(in affinity1, in affinity2);

            return total;
        }

        /// <summary>
        /// Alignment compatibility: uses formulas from Generalized_Alignment_Framework.md
        /// </summary>
        [BurstCompile]
        public static float CalculateAlignmentCompatibility(in EntityAlignment align1, in EntityAlignment align2)
        {
            float total = 0f;

            // Moral axis (Good ↔ Evil)
            float moralDelta = math.abs(align1.Moral - align2.Moral);
            bool bothGood = align1.Moral > 30f && align2.Moral > 30f;
            bool bothEvil = align1.Moral < -30f && align2.Moral < -30f;
            bool opposite = (align1.Moral > 30f && align2.Moral < -30f) || (align1.Moral < -30f && align2.Moral > 30f);

            if (bothGood)
                total += 20f - (moralDelta * 0.2f);
            else if (bothEvil)
                total += 15f - (moralDelta * 0.15f);
            else if (opposite)
                total -= moralDelta * 0.3f;

            // Order axis (Lawful ↔ Chaotic)
            float orderDelta = math.abs(align1.Order - align2.Order);
            bool bothLawful = align1.Order > 30f && align2.Order > 30f;
            bool bothChaotic = align1.Order < -30f && align2.Order < -30f;
            bool orderOpposite = (align1.Order > 30f && align2.Order < -30f) || (align1.Order < -30f && align2.Order > 30f);

            if (bothLawful)
                total += 15f - (orderDelta * 0.1f);
            else if (bothChaotic)
            {
                // Chaotic unpredictable: random factor
                var rng = new Unity.Mathematics.Random((uint)UnityEngine.Time.time);
                total += rng.NextFloat(-10f, 10f);
            }
            else if (orderOpposite)
                total -= orderDelta * 0.2f;

            // Purity axis (Pure ↔ Corrupt)
            float purityDelta = math.abs(align1.Purity - align2.Purity);
            bool bothPure = align1.Purity > 30f && align2.Purity > 30f;
            bool bothCorrupt = align1.Purity < -30f && align2.Purity < -30f;
            bool purityOpposite = (align1.Purity > 30f && align2.Purity < -30f) || (align1.Purity < -30f && align2.Purity > 30f);

            if (bothPure)
                total += 10f - (purityDelta * 0.05f);
            else if (bothCorrupt)
                total -= purityDelta * 0.1f; // Corrupt compete
            else if (purityOpposite)
            {
                // Pure sees corrupt negatively, corrupt doesn't care as much
                if (align1.Purity > 30f)
                    total -= purityDelta * 0.15f;
                else
                    total -= purityDelta * 0.05f;
            }

            return total;
        }

        /// <summary>
        /// Outlook compatibility: matching outlooks give bonuses, opposing give penalties.
        /// </summary>
        [BurstCompile]
        public static float CalculateOutlookCompatibility(in EntityOutlook outlook1, in EntityOutlook outlook2)
        {
            float total = 0f;

            // Check all combinations for matches
            total += GetOutlookMatchBonus(outlook1.Primary, outlook2.Primary, 1.0f);
            total += GetOutlookMatchBonus(outlook1.Primary, outlook2.Secondary, 0.7f);
            total += GetOutlookMatchBonus(outlook1.Primary, outlook2.Tertiary, 0.5f);
            total += GetOutlookMatchBonus(outlook1.Secondary, outlook2.Primary, 0.7f);
            total += GetOutlookMatchBonus(outlook1.Secondary, outlook2.Secondary, 0.5f);
            total += GetOutlookMatchBonus(outlook1.Secondary, outlook2.Tertiary, 0.3f);
            total += GetOutlookMatchBonus(outlook1.Tertiary, outlook2.Primary, 0.5f);
            total += GetOutlookMatchBonus(outlook1.Tertiary, outlook2.Secondary, 0.3f);
            total += GetOutlookMatchBonus(outlook1.Tertiary, outlook2.Tertiary, 0.2f);

            // Check for oppositions
            total += GetOutlookOppositionPenalty(outlook1.Primary, outlook2.Primary);
            total += GetOutlookOppositionPenalty(outlook1.Primary, outlook2.Secondary);
            total += GetOutlookOppositionPenalty(outlook1.Secondary, outlook2.Primary);

            return total;
        }

        [BurstCompile]
        private static float GetOutlookMatchBonus(OutlookType a, OutlookType b, float weight)
        {
            if (a == OutlookType.None || b == OutlookType.None || a != b)
                return 0f;

            return a switch
            {
                OutlookType.Warlike => 15f * weight,
                OutlookType.Peaceful => 10f * weight,
                OutlookType.Spiritual => 12f * weight,
                OutlookType.Materialistic => 8f * weight,
                OutlookType.Scholarly => 10f * weight,
                OutlookType.Pragmatic => 8f * weight,
                OutlookType.Xenophobic => 15f * weight,
                OutlookType.Egalitarian => 10f * weight,
                OutlookType.Authoritarian => 5f * weight, // Compete for dominance
                _ => 0f
            };
        }

        [BurstCompile]
        private static float GetOutlookOppositionPenalty(OutlookType a, OutlookType b)
        {
            if (a == OutlookType.None || b == OutlookType.None)
                return 0f;

            // Warlike vs Peaceful
            if ((a == OutlookType.Warlike && b == OutlookType.Peaceful) ||
                (a == OutlookType.Peaceful && b == OutlookType.Warlike))
                return -15f;

            // Spiritual vs Materialistic (special: debates can increase relation)
            if ((a == OutlookType.Spiritual && b == OutlookType.Materialistic) ||
                (a == OutlookType.Materialistic && b == OutlookType.Spiritual))
                return -10f;

            // Xenophobic vs Egalitarian
            if ((a == OutlookType.Xenophobic && b == OutlookType.Egalitarian) ||
                (a == OutlookType.Egalitarian && b == OutlookType.Xenophobic))
                return -20f;

            // Authoritarian vs Egalitarian
            if ((a == OutlookType.Authoritarian && b == OutlookType.Egalitarian) ||
                (a == OutlookType.Egalitarian && b == OutlookType.Authoritarian))
                return -18f;

            return 0f;
        }

        /// <summary>
        /// Personality compatibility: similar personalities get bonuses, opposites get penalties.
        /// </summary>
        [BurstCompile]
        public static float CalculatePersonalityCompatibility(in PersonalityAxes pers1, in PersonalityAxes pers2)
        {
            float total = 0f;

            // Forgiving trait: gives benefit of doubt
            if (pers1.VengefulForgiving > 60f)
                total += 10f;
            if (pers2.VengefulForgiving > 60f)
                total += 10f;

            // Vengeful trait: suspicious
            if (pers1.VengefulForgiving < -60f)
                total -= 5f;
            if (pers2.VengefulForgiving < -60f)
                total -= 5f;

            // Bold + Bold: mutual respect
            if (pers1.CravenBold > 60f && pers2.CravenBold > 60f)
                total += 8f;

            // Craven + Craven: mutual understanding
            if (pers1.CravenBold < -60f && pers2.CravenBold < -60f)
                total += 5f;

            // Bold + Craven: friction
            if (pers1.CravenBold > 60f && pers2.CravenBold < -60f)
                total -= 8f;
            if (pers1.CravenBold < -60f && pers2.CravenBold > 60f)
                total -= 5f;

            return total;
        }

        /// <summary>
        /// Might/Magic compatibility: matching affinity gives small bonus, opposing gives small penalty.
        /// </summary>
        [BurstCompile]
        public static float CalculateMightMagicCompatibility(in MightMagicAffinity aff1, in MightMagicAffinity aff2)
        {
            float axisDelta = math.abs(aff1.Axis - aff2.Axis);

            // If both have strong commitment, compatibility matters more
            float strengthMultiplier = (aff1.Strength + aff2.Strength) * 0.5f;

            // Matching affinity (within 30 points)
            if (axisDelta < 30f)
                return 5f * strengthMultiplier;

            // Opposing affinity (more than 60 points apart)
            if (axisDelta > 60f)
                return -3f * strengthMultiplier;

            // Middle range: neutral
            return 0f;
        }
    }
}

