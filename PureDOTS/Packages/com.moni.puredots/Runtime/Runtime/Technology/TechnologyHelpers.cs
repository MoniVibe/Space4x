using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;
using Unity.Burst;

namespace PureDOTS.Runtime.Technology
{
    /// <summary>
    /// Static helpers for technology and research calculations.
    /// </summary>
    [BurstCompile]
    public static class TechnologyHelpers
    {
        /// <summary>
        /// Default technology configuration.
        /// </summary>
        public static TechConfig DefaultConfig => new TechConfig
        {
            BaseResearchCostPerTier = 100f,
            TierCostMultiplier = 1.5f,
            KnowledgeToResearchRatio = 0.1f,
            TransferSpeedModifier = 1f,
            AllowTechRegression = 0
        };

        /// <summary>
        /// Calculates research cost for a tier.
        /// </summary>
        public static float CalculateTierCost(byte tier, in TechConfig config)
        {
            return config.BaseResearchCostPerTier * math.pow(config.TierCostMultiplier, tier);
        }

        /// <summary>
        /// Calculates research rate with modifiers.
        /// </summary>
        public static float CalculateResearchRate(
            float baseRate,
            float efficiencyModifier,
            float facilityBonus,
            float knowledgeContribution,
            in TechConfig config)
        {
            float rate = baseRate * efficiencyModifier;
            rate += facilityBonus;
            rate += knowledgeContribution * config.KnowledgeToResearchRatio;
            return math.max(0f, rate);
        }

        /// <summary>
        /// Checks if a project can be started.
        /// </summary>
        public static bool CanStartProject(
            in ResearchProject project,
            byte currentTier,
            in DynamicBuffer<TechPrerequisite> prerequisites)
        {
            if (currentTier < project.RequiredTier)
                return false;

            // Check all prerequisites
            for (int i = 0; i < prerequisites.Length; i++)
            {
                if (prerequisites[i].IsMet == 0)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Advances research on a project.
        /// </summary>
        public static bool AdvanceResearch(
            ref ResearchProject project,
            float researchPoints)
        {
            if (project.IsCompleted != 0)
                return false;

            project.CurrentProgress += researchPoints;
            
            if (project.CurrentProgress >= project.TotalResearchCost)
            {
                project.IsCompleted = 1;
                project.IsActive = 0;
                return true; // Project completed
            }

            return false;
        }

        /// <summary>
        /// Gets progress percentage for a project.
        /// </summary>
        public static float GetProgressPercent(in ResearchProject project)
        {
            if (project.TotalResearchCost <= 0)
                return 1f;
            return math.saturate(project.CurrentProgress / project.TotalResearchCost);
        }

        /// <summary>
        /// Checks if tier advancement is possible.
        /// </summary>
        public static bool CanAdvanceTier(
            in TechLevel techLevel,
            in KnowledgePool knowledge,
            in TechConfig config)
        {
            // Check knowledge supports next tier
            if (techLevel.CurrentTier >= knowledge.MaxTierSupported)
                return false;

            // Check research progress
            float costForNextTier = CalculateTierCost((byte)(techLevel.CurrentTier + 1), config);
            return techLevel.ResearchProgress >= costForNextTier;
        }

        /// <summary>
        /// Advances to next tech tier.
        /// </summary>
        public static TechLevel AdvanceTier(in TechLevel current, uint currentTick, in TechConfig config)
        {
            float costForNextTier = CalculateTierCost((byte)(current.CurrentTier + 1), config);
            
            return new TechLevel
            {
                CurrentTier = (byte)(current.CurrentTier + 1),
                ResearchProgress = current.ResearchProgress - costForNextTier,
                TierUnlockedTick = currentTick
            };
        }

        /// <summary>
        /// Calculates tech transfer progress.
        /// </summary>
        public static float CalculateTransferProgress(
            float currentProgress,
            float transferRate,
            byte sourceTier,
            byte targetTier,
            in TechConfig config)
        {
            // Harder to transfer higher tech
            float tierDiff = sourceTier - targetTier;
            float difficultyMod = 1f / (1f + tierDiff * 0.2f);
            
            return currentProgress + transferRate * config.TransferSpeedModifier * difficultyMod;
        }

        /// <summary>
        /// Applies knowledge decay.
        /// </summary>
        public static KnowledgePool ApplyKnowledgeDecay(in KnowledgePool current, float deltaTime)
        {
            var result = current;
            result.AccumulatedKnowledge = math.max(0f, 
                current.AccumulatedKnowledge - current.KnowledgeDecayRate * deltaTime);
            return result;
        }

        /// <summary>
        /// Adds knowledge from education/discovery.
        /// </summary>
        public static void AddKnowledge(ref KnowledgePool pool, float amount)
        {
            pool.AccumulatedKnowledge += amount;
        }

        /// <summary>
        /// Gets specialty bonus for a category.
        /// </summary>
        public static float GetSpecialtyBonus(
            FixedString32Bytes specialty,
            FixedString32Bytes category,
            float bonusMultiplier)
        {
            if (specialty.Equals(category))
                return bonusMultiplier;
            return 1f;
        }

        /// <summary>
        /// Finds active research project.
        /// </summary>
        public static bool TryGetActiveProject(
            in DynamicBuffer<ResearchProject> projects,
            out ResearchProject active,
            out int index)
        {
            for (int i = 0; i < projects.Length; i++)
            {
                if (projects[i].IsActive != 0 && projects[i].IsCompleted == 0)
                {
                    active = projects[i];
                    index = i;
                    return true;
                }
            }

            active = default;
            index = -1;
            return false;
        }

        /// <summary>
        /// Unlocks a recipe.
        /// </summary>
        public static void UnlockRecipe(
            ref DynamicBuffer<RecipeUnlock> unlocks,
            FixedString64Bytes recipeId,
            FixedString64Bytes unlockedBy,
            uint tick)
        {
            // Check if already unlocked
            for (int i = 0; i < unlocks.Length; i++)
            {
                if (unlocks[i].RecipeId.Equals(recipeId))
                    return;
            }

            unlocks.Add(new RecipeUnlock
            {
                RecipeId = recipeId,
                UnlockedBy = unlockedBy,
                UnlockedTick = tick
            });
        }

        /// <summary>
        /// Checks if a recipe is unlocked.
        /// </summary>
        public static bool IsRecipeUnlocked(in DynamicBuffer<RecipeUnlock> unlocks, FixedString64Bytes recipeId)
        {
            for (int i = 0; i < unlocks.Length; i++)
            {
                if (unlocks[i].RecipeId.Equals(recipeId))
                    return true;
            }
            return false;
        }
    }
}

