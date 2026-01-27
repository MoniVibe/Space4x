using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;
using Unity.Burst;

namespace PureDOTS.Runtime.Time.Branching
{
    /// <summary>
    /// Static helpers for timeline branching operations.
    /// </summary>
    [BurstCompile]
    public static class BranchingHelpers
    {
        /// <summary>
        /// Default time spine configuration.
        /// </summary>
        public static TimeSpineConfig DefaultConfig => new TimeSpineConfig
        {
            MaxConcurrentBranches = 4,
            MaxBranchDuration = 36000, // 10 minutes at 60 ticks/sec
            BranchGCInterval = 3600,   // Every minute
            DivergenceThreshold = 0.8f,
            AllowNestedBranches = false,
            AutoPruneLowPriority = true
        };

        /// <summary>
        /// Generates a unique branch ID.
        /// </summary>
        public static FixedString64Bytes GenerateBranchId(uint tick, uint seed)
        {
            uint hash = DeterministicRandom(seed ^ tick);
            var id = new FixedString64Bytes();
            id.Append((FixedString32Bytes)"branch_");
            id.Append(tick);
            id.Append((FixedString32Bytes)"_");
            id.Append((int)(hash % 10000));
            return id;
        }

        /// <summary>
        /// Creates a new branch from the current state.
        /// </summary>
        public static TimelineBranch CreateBranch(
            FixedString64Bytes branchId,
            FixedString64Bytes parentId,
            uint branchPointTick,
            byte priority = 1)
        {
            return new TimelineBranch
            {
                BranchId = branchId,
                ParentBranchId = parentId,
                BranchPointTick = branchPointTick,
                CurrentTick = branchPointTick,
                MaxTick = branchPointTick,
                IsMainTimeline = false,
                IsActive = true,
                IsFrozen = false,
                IsMarkedForMerge = false,
                DivergenceScore = 0f,
                Priority = priority
            };
        }

        /// <summary>
        /// Calculates divergence score between two branches.
        /// </summary>
        public static float CalculateDivergence(
            in BranchComparison comparison,
            float resourceWeight = 0.3f,
            float populationWeight = 0.3f,
            float militaryWeight = 0.2f,
            float happinessWeight = 0.2f)
        {
            float divergence = 0f;
            
            // Normalize differences
            divergence += math.abs(comparison.ResourceDifference) * resourceWeight;
            divergence += math.abs(comparison.PopulationDifference) * populationWeight;
            divergence += math.abs(comparison.MilitaryDifference) * militaryWeight;
            divergence += math.abs(comparison.HappinessDifference) * happinessWeight;
            
            // Entity divergence
            int totalEntities = comparison.EntitiesOnlyInA + comparison.EntitiesOnlyInB + comparison.EntitiesInBoth;
            if (totalEntities > 0)
            {
                float entityDivergence = (comparison.EntitiesOnlyInA + comparison.EntitiesOnlyInB + comparison.EntitiesDiverged) / (float)totalEntities;
                divergence = (divergence + entityDivergence) / 2f;
            }

            return math.clamp(divergence, 0f, 1f);
        }

        /// <summary>
        /// Calculates overall score for what-if result.
        /// </summary>
        public static float CalculateOverallScore(
            in WhatIfResult result,
            float resourceWeight = 0.25f,
            float populationWeight = 0.25f,
            float happinessWeight = 0.25f,
            float militaryWeight = 0.25f)
        {
            float score = 0f;
            
            // Use deltas for scoring
            score += result.ResourcesDelta * resourceWeight;
            score += result.PopulationDelta * populationWeight;
            score += result.HappinessDelta * happinessWeight;
            score += result.MilitaryDelta * militaryWeight;
            
            return score;
        }

        /// <summary>
        /// Determines if a branch should be frozen due to divergence.
        /// </summary>
        public static bool ShouldFreezeBranch(
            in TimelineBranch branch,
            in TimeSpineConfig config)
        {
            if (branch.IsFrozen)
                return false;

            if (branch.DivergenceScore > config.DivergenceThreshold)
                return true;

            uint duration = branch.CurrentTick - branch.BranchPointTick;
            if (duration > config.MaxBranchDuration)
                return true;

            return false;
        }

        /// <summary>
        /// Determines if a branch should be pruned.
        /// </summary>
        public static bool ShouldPruneBranch(
            in TimelineBranch branch,
            int currentBranchCount,
            in TimeSpineConfig config)
        {
            if (branch.IsMainTimeline)
                return false;

            if (branch.IsActive && !branch.IsFrozen)
                return false;

            // Prune low priority when at limit
            if (config.AutoPruneLowPriority && 
                currentBranchCount >= config.MaxConcurrentBranches &&
                branch.Priority == 0)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Applies a what-if modification conceptually (returns delta).
        /// </summary>
        public static float ApplyModification(
            in WhatIfModification mod,
            float currentValue)
        {
            // This is a simplified version - real implementation would
            // actually modify entity components
            return mod.ParameterValue - currentValue;
        }

        /// <summary>
        /// Creates a comparison request.
        /// </summary>
        public static ComparisonRequest CreateComparisonRequest(
            FixedString64Bytes branchA,
            FixedString64Bytes branchB,
            uint currentTick,
            bool detailed = false)
        {
            return new ComparisonRequest
            {
                BranchAId = branchA,
                BranchBId = branchB,
                DetailedComparison = detailed,
                RequestTick = currentTick
            };
        }

        /// <summary>
        /// Creates a merge request.
        /// </summary>
        public static BranchMergeRequest CreateMergeRequest(
            FixedString64Bytes source,
            FixedString64Bytes target,
            uint currentTick,
            bool fullMerge = true)
        {
            return new BranchMergeRequest
            {
                SourceBranchId = source,
                TargetBranchId = target,
                FullMerge = fullMerge,
                RequestTick = currentTick
            };
        }

        /// <summary>
        /// Validates a branch can be created.
        /// </summary>
        public static bool CanCreateBranch(
            int currentBranchCount,
            in TimeSpineConfig config,
            bool isNested)
        {
            if (currentBranchCount >= config.MaxConcurrentBranches)
                return false;

            if (isNested && !config.AllowNestedBranches)
                return false;

            return true;
        }

        /// <summary>
        /// Gets branch priority based on scenario type.
        /// </summary>
        public static byte GetScenarioPriority(FixedString64Bytes scenarioName)
        {
            // Higher priority for certain scenario types
            if (scenarioName.Length > 0)
            {
                // Check for priority keywords via fixed string comparison (Burst-safe)
                if (scenarioName.Equals((FixedString64Bytes)"critical") ||
                    scenarioName.Equals((FixedString64Bytes)"urgent"))
                    return 2;
                if (scenarioName.Equals((FixedString64Bytes)"test") ||
                    scenarioName.Equals((FixedString64Bytes)"experiment"))
                    return 0;
            }
            return 1; // Default priority
        }

        /// <summary>
        /// Simple deterministic random.
        /// </summary>
        private static uint DeterministicRandom(uint seed)
        {
            seed ^= seed << 13;
            seed ^= seed >> 17;
            seed ^= seed << 5;
            return seed;
        }

        /// <summary>
        /// Creates main timeline branch.
        /// </summary>
        public static TimelineBranch CreateMainTimeline()
        {
            return new TimelineBranch
            {
                BranchId = new FixedString64Bytes("main"),
                ParentBranchId = default,
                BranchPointTick = 0,
                CurrentTick = 0,
                MaxTick = 0,
                IsMainTimeline = true,
                IsActive = true,
                IsFrozen = false,
                IsMarkedForMerge = false,
                DivergenceScore = 0f,
                Priority = 2
            };
        }
    }
}

