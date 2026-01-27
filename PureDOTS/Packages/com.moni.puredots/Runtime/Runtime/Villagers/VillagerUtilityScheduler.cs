using PureDOTS.Config;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Villagers
{
    /// <summary>
    /// Utility-based scheduler for villager needs and job priorities.
    /// Now includes personality-based action weighting (bold/craven, vengeful/forgiving).
    /// Based on Villager_Behavioral_Personality.md design.
    /// </summary>
    [BurstCompile]
    public struct VillagerUtilityScheduler
    {
        /// <summary>
        /// Calculates utility score for a need based on current value and archetype decay rate.
        /// Higher utility = more urgent need.
        /// </summary>
        [BurstCompile]
        public static float CalculateNeedUtility(float currentValue, float decayRate, float threshold = 0.3f)
        {
            // Low current value = high utility (urgent)
            float urgencyScore = 1f - math.clamp(currentValue, 0f, 1f);
            
            // High decay rate = higher utility (needs attention soon)
            float decayWeight = math.clamp(decayRate * 10f, 0f, 1f);
            
            // Threshold determines when need becomes critical
            float criticalBonus = currentValue < threshold ? 2f : 1f;
            
            return urgencyScore * (1f + decayWeight) * criticalBonus;
        }
        
        /// <summary>
        /// Calculates utility score for a job assignment based on archetype preferences and current state.
        /// </summary>
        [BurstCompile]
        public static float CalculateJobUtility(byte archetypeJobWeight, float distanceToJob, float currentEnergy)
        {
            // Higher archetype weight = higher base utility (0-100 -> 0-1)
            float baseUtility = archetypeJobWeight / 100f;
            
            // Closer distance = higher utility (normalize: 0-100m -> 1-0)
            float distanceUtility = math.clamp(1f - (distanceToJob / 100f), 0f, 1f);
            
            // Higher energy = can perform job better
            float energyUtility = math.clamp(currentEnergy / 100f, 0f, 1f);
            
            return baseUtility * (0.5f + distanceUtility * 0.3f + energyUtility * 0.2f);
        }
        
        /// <summary>
        /// Calculates personality-weighted action utility.
        /// Bold villagers prefer risky actions; Craven villagers avoid them.
        /// Vengeful villagers prioritize revenge actions; Forgiving villagers avoid them.
        /// </summary>
        [BurstCompile]
        public static float CalculatePersonalityWeight(
            VillagerActionType actionType,
            in VillagerBehavior behavior)
        {
            float weight = 1f; // Base weight
            
            // Bold/Craven modifiers
            switch (actionType)
            {
                case VillagerActionType.Combat:
                case VillagerActionType.Trade: // Risky frontier trading
                    // Bold villagers seek danger: +weight
                    // Craven villagers avoid danger: -weight
                    weight += behavior.BoldScore * 0.01f; // -1.0 to +1.0 modifier
                    break;
                    
                case VillagerActionType.Gather:
                case VillagerActionType.Build:
                case VillagerActionType.Craft:
                    // Safe jobs: Craven prefers, Bold avoids
                    weight -= behavior.BoldScore * 0.005f; // Smaller modifier for safe jobs
                    break;
            }
            
            // Vengeful/Forgiving modifiers
            // Note: Revenge actions would be handled by initiative-triggered autonomous decisions
            // This is for regular job selection, so forgiving villagers might prefer social actions
            
            return math.max(0.1f, weight); // Clamp to minimum weight
        }
        
        /// <summary>
        /// Selects the highest utility need action for a villager, weighted by personality.
        /// </summary>
        [BurstCompile]
        public static void SelectBestAction(
            ref VillagerNeeds needs,
            in VillagerArchetypeData archetype,
            in VillagerBehavior behavior,
            out VillagerActionType actionType,
            out float utilityScore)
        {
            float bestUtility = 0f;
            VillagerActionType bestAction = VillagerActionType.Idle;
            
            // Evaluate need-based actions
            float hungerUtility = CalculateNeedUtility(needs.HungerFloat / 100f, archetype.HungerDecayRate);
            float energyUtility = CalculateNeedUtility(needs.EnergyFloat / 100f, archetype.EnergyDecayRate);
            float moraleUtility = CalculateNeedUtility(needs.MoraleFloat / 100f, archetype.MoraleDecayRate);
            
            // Apply personality weights
            float hungerWeight = CalculatePersonalityWeight(VillagerActionType.SatisfyHunger, behavior);
            float restWeight = CalculatePersonalityWeight(VillagerActionType.Rest, behavior);
            float moraleWeight = CalculatePersonalityWeight(VillagerActionType.ImproveMorale, behavior);
            
            if (hungerUtility * hungerWeight > bestUtility)
            {
                bestUtility = hungerUtility * hungerWeight;
                bestAction = VillagerActionType.SatisfyHunger;
            }
            
            if (energyUtility * restWeight > bestUtility)
            {
                bestUtility = energyUtility * restWeight;
                bestAction = VillagerActionType.Rest;
            }
            
            if (moraleUtility * moraleWeight > bestUtility)
            {
                bestUtility = moraleUtility * moraleWeight;
                bestAction = VillagerActionType.ImproveMorale;
            }
            actionType = bestAction;
            utilityScore = bestUtility;
        }

        /// <summary>
        /// Looks up the archetype preference weight for a given job type so scheduling logic
        /// can compare work utility against needs utility.
        /// </summary>
        [BurstCompile]
        public static byte GetJobPreference(
            VillagerJob.JobType jobType,
            in VillagerArchetypeData archetype)
        {
            switch (jobType)
            {
                case VillagerJob.JobType.Farmer:
                case VillagerJob.JobType.Gatherer:
                    return archetype.GatherJobWeight;
                case VillagerJob.JobType.Builder:
                    return archetype.BuildJobWeight;
                case VillagerJob.JobType.Crafter:
                    return archetype.CraftJobWeight;
                case VillagerJob.JobType.Hunter:
                case VillagerJob.JobType.Guard:
                    return archetype.CombatJobWeight;
                case VillagerJob.JobType.Priest:
                case VillagerJob.JobType.Merchant:
                    return archetype.TradeJobWeight;
                default:
                    return archetype.GatherJobWeight;
            }
        }
    }
    
    /// <summary>
    /// Types of actions a villager can take.
    /// </summary>
    public enum VillagerActionType : byte
    {
        Idle,
        SatisfyHunger,
        Rest,
        ImproveMorale,
        Gather,
        Build,
        Craft,
        Combat,
        Trade
    }
}

