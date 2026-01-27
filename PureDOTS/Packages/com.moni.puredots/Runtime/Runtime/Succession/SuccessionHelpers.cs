using Unity.Mathematics;
using Unity.Entities;
using Unity.Burst;
using Unity.Collections;

namespace PureDOTS.Runtime.Succession
{
    /// <summary>
    /// Static helpers for succession and inheritance calculations.
    /// </summary>
    [BurstCompile]
    public static class SuccessionHelpers
    {
        /// <summary>
        /// Selects heir based on succession type.
        /// </summary>
        public static Entity SelectHeir(
            in SuccessionRules rules,
            in DynamicBuffer<HeirCandidate> candidates,
            uint seed)
        {
            if (candidates.Length == 0)
                return Entity.Null;

            return rules.Type switch
            {
                SuccessionType.Primogeniture => SelectByPriority(candidates, ascending: true),
                SuccessionType.Ultimogeniture => SelectByPriority(candidates, ascending: false),
                SuccessionType.Designated => SelectDesignated(candidates),
                SuccessionType.Meritocratic => SelectBySuitability(candidates),
                SuccessionType.Random => SelectRandom(candidates, seed),
                SuccessionType.Elective => Entity.Null, // Requires voting system
                SuccessionType.Seniority => SelectByPriority(candidates, ascending: true),
                _ => Entity.Null
            };
        }

        private static Entity SelectByPriority(
            in DynamicBuffer<HeirCandidate> candidates,
            bool ascending)
        {
            Entity best = Entity.Null;
            byte bestPriority = ascending ? byte.MaxValue : byte.MinValue;
            
            for (int i = 0; i < candidates.Length; i++)
            {
                if (ascending && candidates[i].Priority < bestPriority)
                {
                    bestPriority = candidates[i].Priority;
                    best = candidates[i].CandidateEntity;
                }
                else if (!ascending && candidates[i].Priority > bestPriority)
                {
                    bestPriority = candidates[i].Priority;
                    best = candidates[i].CandidateEntity;
                }
            }
            return best;
        }

        private static Entity SelectDesignated(
            in DynamicBuffer<HeirCandidate> candidates)
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i].IsDesignated != 0)
                    return candidates[i].CandidateEntity;
            }
            return candidates.Length > 0 ? candidates[0].CandidateEntity : Entity.Null;
        }

        private static Entity SelectBySuitability(
            in DynamicBuffer<HeirCandidate> candidates)
        {
            Entity best = Entity.Null;
            float bestSuitability = -1;
            
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i].Suitability > bestSuitability)
                {
                    bestSuitability = candidates[i].Suitability;
                    best = candidates[i].CandidateEntity;
                }
            }
            return best;
        }

        private static Entity SelectRandom(
            in DynamicBuffer<HeirCandidate> candidates,
            uint seed)
        {
            if (candidates.Length == 0) return Entity.Null;
            var rng = new Random(seed);
            int index = rng.NextInt(0, candidates.Length);
            return candidates[index].CandidateEntity;
        }

        /// <summary>
        /// Calculates expertise inheritance amount.
        /// </summary>
        public static float CalculateExpertiseInheritance(
            float deceasedExpertise,
            float heirExpertise,
            float inheritanceRate,
            float bloodlineBonus)
        {
            // Base inheritance
            float inherited = deceasedExpertise * inheritanceRate;
            
            // Bloodline bonus
            inherited *= 1f + bloodlineBonus;
            
            // Diminishing returns if heir already skilled
            float diminishing = 1f / (1f + heirExpertise * 0.1f);
            inherited *= diminishing;
            
            return inherited;
        }

        /// <summary>
        /// Calculates asset inheritance with taxes/fees.
        /// </summary>
        public static float CalculateAssetInheritance(
            float assetValue,
            float inheritanceTaxRate,
            float claimStrength)
        {
            // Tax reduces inheritance
            float afterTax = assetValue * (1f - inheritanceTaxRate);
            
            // Weak claims lose more
            float claimFactor = 0.5f + claimStrength * 0.5f;
            
            return afterTax * claimFactor;
        }

        /// <summary>
        /// Checks if succession crisis should trigger.
        /// </summary>
        public static bool ShouldTriggerCrisis(
            in DynamicBuffer<HeirCandidate> candidates,
            float claimDisputeThreshold)
        {
            if (candidates.Length <= 1)
                return false;
            
            // Check for competing strong claims
            int strongClaims = 0;
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i].Claim >= claimDisputeThreshold)
                    strongClaims++;
            }
            
            return strongClaims > 1;
        }

        /// <summary>
        /// Calculates succession crisis intensity.
        /// </summary>
        public static float CalculateCrisisIntensity(
            in DynamicBuffer<HeirCandidate> candidates)
        {
            if (candidates.Length <= 1)
                return 0;
            
            // More claimants = more intense
            float countFactor = math.min(1f, candidates.Length * 0.2f);
            
            // Similar claims = more contested
            float avgClaim = 0;
            for (int i = 0; i < candidates.Length; i++)
                avgClaim += candidates[i].Claim;
            avgClaim /= candidates.Length;
            
            float claimVariance = 0;
            for (int i = 0; i < candidates.Length; i++)
            {
                float diff = candidates[i].Claim - avgClaim;
                claimVariance += diff * diff;
            }
            claimVariance /= candidates.Length;
            
            // Low variance = more intense (close race)
            float contestedFactor = 1f - math.sqrt(claimVariance);
            
            return countFactor * contestedFactor;
        }

        /// <summary>
        /// Adds chronicle entry for succession.
        /// </summary>
        public static void RecordSuccession(
            ref DynamicBuffer<ChronicleEntry> chronicle,
            Entity successorEntity,
            uint currentTick)
        {
            chronicle.Add(new ChronicleEntry
            {
                EventType = "succession",
                RelatedEntity = successorEntity,
                Significance = 0.8f,
                OccurredTick = currentTick
            });
        }

        /// <summary>
        /// Distributes inheritance items to heir.
        /// </summary>
        public static void ProcessInheritance(
            in DynamicBuffer<InheritanceItem> items,
            Entity heirEntity,
            out float totalValueInherited,
            out int itemsInherited)
        {
            totalValueInherited = 0;
            itemsInherited = 0;
            
            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                float actualValue = item.Value * item.TransferEfficiency;
                totalValueInherited += actualValue;
                itemsInherited++;
            }
        }

        /// <summary>
        /// Calculates claim strength from relationships.
        /// </summary>
        public static float CalculateClaimStrength(
            bool isBloodline,
            byte generationalDistance,
            float legitimacy,
            bool isDesignated)
        {
            float claim = 0;
            
            // Blood relation is primary
            if (isBloodline)
            {
                claim = 1f / (1f + generationalDistance * 0.5f);
            }
            else
            {
                claim = 0.3f; // Non-blood claims are weaker
            }
            
            // Legitimacy affects claim
            claim *= legitimacy;
            
            // Designation is a strong boost
            if (isDesignated)
                claim = math.max(claim, 0.8f);
            
            return math.saturate(claim);
        }
    }
}

