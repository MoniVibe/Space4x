using Unity.Mathematics;
using Unity.Entities;
using Unity.Burst;
using Unity.Collections;

namespace PureDOTS.Runtime.Lifecycle
{
    /// <summary>
    /// Static helpers for lifecycle and metamorphosis calculations.
    /// </summary>
    [BurstCompile]
    public static class LifecycleHelpers
    {
        /// <summary>
        /// Calculates stage progress based on age and configuration.
        /// </summary>
        public static float CalculateStageProgress(
            in LifecycleState state,
            in LifecycleConfig config,
            uint currentTick)
        {
            float ticksInStage = currentTick - state.StageEnteredTick;
            float stageDuration = GetStageDuration(state.CurrentStage, config);

            if (stageDuration <= 0)
                return 1f;

            return math.saturate(ticksInStage / stageDuration);
        }

        /// <summary>
        /// Gets the duration of a specific stage.
        /// </summary>
        public static float GetStageDuration(LifecycleStage stage, in LifecycleConfig config)
        {
            return stage switch
            {
                LifecycleStage.Nascent => 100f, // Brief initialization
                LifecycleStage.Seed => 500f,   // Dormant period
                LifecycleStage.Juvenile => config.JuvenileDuration,
                LifecycleStage.Mature => config.MatureDuration,
                LifecycleStage.Elder => config.ElderDuration,
                LifecycleStage.Decaying => config.DecayDuration,
                LifecycleStage.Dormant => float.MaxValue, // Until triggered
                _ => 1000f
            };
        }

        /// <summary>
        /// Attempts to advance to the next stage.
        /// </summary>
        public static bool TryAdvanceStage(
            ref LifecycleState state,
            in LifecycleConfig config,
            uint currentTick,
            out LifecycleStage newStage)
        {
            newStage = state.CurrentStage;

            if (state.IsFrozen != 0 || state.CanAdvance == 0)
                return false;

            if (state.StageProgress < 1f)
                return false;

            // Determine next stage based on type
            var nextStage = GetNextStage(state.CurrentStage, config.Type, config.MaxStage);

            if (nextStage == state.CurrentStage)
                return false;

            state.CurrentStage = nextStage;
            state.StageProgress = 0f;
            state.StageEnteredTick = currentTick;
            state.StageCount++;
            newStage = nextStage;

            return true;
        }

        /// <summary>
        /// Gets the next stage in progression.
        /// </summary>
        private static LifecycleStage GetNextStage(
            LifecycleStage current,
            LifecycleType type,
            byte maxStage)
        {
            var nextOrdinal = (byte)current + 1;

            if (type == LifecycleType.Terminal)
            {
                if (nextOrdinal > maxStage || current == LifecycleStage.Decaying)
                    return current;
            }
            else if (type == LifecycleType.Cyclical)
            {
                if (current == LifecycleStage.Decaying)
                    return LifecycleStage.Seed; // Cycle back
            }

            return current switch
            {
                LifecycleStage.Nascent => LifecycleStage.Seed,
                LifecycleStage.Seed => LifecycleStage.Juvenile,
                LifecycleStage.Juvenile => LifecycleStage.Mature,
                LifecycleStage.Mature => LifecycleStage.Elder,
                LifecycleStage.Elder => LifecycleStage.Decaying,
                LifecycleStage.Decaying => LifecycleStage.Decaying, // Terminal
                LifecycleStage.Dormant => LifecycleStage.Seed,
                _ => current
            };
        }

        /// <summary>
        /// Gets metamorphosis target based on evolution paths.
        /// </summary>
        public static bool GetMetamorphosisTarget(
            in DynamicBuffer<EvolutionPath> paths,
            out FixedString64Bytes targetTypeId)
        {
            targetTypeId = default;

            for (int i = 0; i < paths.Length; i++)
            {
                var path = paths[i];
                if (path.IsUnlocked != 0 && path.IsSelected != 0)
                {
                    targetTypeId = path.TargetTypeId;
                    return true;
                }
            }

            // If no selection, pick first unlocked
            for (int i = 0; i < paths.Length; i++)
            {
                if (paths[i].IsUnlocked != 0)
                {
                    targetTypeId = paths[i].TargetTypeId;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Updates metamorphosis progress.
        /// </summary>
        public static void UpdateMetamorphosis(
            ref Metamorphosis meta,
            float deltaTime,
            uint currentTick)
        {
            if (meta.IsTransforming == 0)
                return;

            if (meta.TransformDuration <= 0)
            {
                meta.TransformProgress = 1f;
                return;
            }

            float elapsed = currentTick - meta.TransformStartTick;
            meta.TransformProgress = math.saturate(elapsed / meta.TransformDuration);
        }

        /// <summary>
        /// Starts metamorphosis process.
        /// </summary>
        public static void StartMetamorphosis(
            ref Metamorphosis meta,
            in FixedString64Bytes targetTypeId,
            float duration,
            uint currentTick)
        {
            meta.TargetTypeId = targetTypeId;
            meta.TransformDuration = duration;
            meta.TransformStartTick = currentTick;
            meta.TransformProgress = 0f;
            meta.IsTransforming = 1;
        }

        /// <summary>
        /// Checks if metamorphosis is complete.
        /// </summary>
        public static bool IsMetamorphosisComplete(in Metamorphosis meta)
        {
            return meta.IsTransforming != 0 && meta.TransformProgress >= 1f;
        }

        /// <summary>
        /// Updates evolution path progress.
        /// </summary>
        public static void UpdateEvolutionProgress(
            ref DynamicBuffer<EvolutionPath> paths,
            ref DynamicBuffer<EvolutionRequirement> requirements)
        {
            for (int i = 0; i < paths.Length; i++)
            {
                var path = paths[i];
                if (path.IsUnlocked != 0)
                    continue;

                // Check if requirements met
                bool allMet = true;
                float totalProgress = 0f;
                int reqCount = 0;

                for (int j = 0; j < requirements.Length; j++)
                {
                    var req = requirements[j];
                    if (!req.RequirementId.Equals(path.RequirementId))
                        continue;

                    reqCount++;
                    if (req.IsMet == 0)
                    {
                        allMet = false;
                        totalProgress += req.CurrentAmount / math.max(1f, req.RequiredAmount);
                    }
                    else
                    {
                        totalProgress += 1f;
                    }
                }

                path.UnlockProgress = reqCount > 0 ? totalProgress / reqCount : 0f;
                path.IsUnlocked = (byte)(allMet ? 1 : 0);
                paths[i] = path;
            }
        }

        /// <summary>
        /// Calculates aging effects based on lifecycle state.
        /// </summary>
        public static AgingEffects CalculateAgingEffects(
            in LifecycleState state,
            in LifecycleConfig config)
        {
            var effects = new AgingEffects
            {
                VitalityModifier = 1f,
                WisdomModifier = 1f,
                StrengthModifier = 1f,
                FertilityModifier = 1f,
                DecayRate = 0f
            };

            switch (state.CurrentStage)
            {
                case LifecycleStage.Nascent:
                case LifecycleStage.Seed:
                    effects.VitalityModifier = 0.5f;
                    effects.StrengthModifier = 0.1f;
                    effects.FertilityModifier = 0f;
                    break;

                case LifecycleStage.Juvenile:
                    float juvenileProgress = state.StageProgress;
                    effects.VitalityModifier = math.lerp(0.7f, 1f, juvenileProgress);
                    effects.StrengthModifier = math.lerp(0.3f, 0.8f, juvenileProgress);
                    effects.WisdomModifier = 0.5f;
                    effects.FertilityModifier = juvenileProgress > 0.8f ? 0.5f : 0f;
                    break;

                case LifecycleStage.Mature:
                    effects.VitalityModifier = 1f;
                    effects.StrengthModifier = 1f;
                    effects.WisdomModifier = math.lerp(0.7f, 1f, state.StageProgress);
                    effects.FertilityModifier = 1f;
                    break;

                case LifecycleStage.Elder:
                    float elderProgress = state.StageProgress;
                    effects.VitalityModifier = math.lerp(1f, 0.6f, elderProgress);
                    effects.StrengthModifier = math.lerp(0.9f, 0.5f, elderProgress);
                    effects.WisdomModifier = math.lerp(1f, 1.3f, elderProgress);
                    effects.FertilityModifier = math.lerp(0.5f, 0f, elderProgress);
                    break;

                case LifecycleStage.Decaying:
                    float decayProgress = state.StageProgress;
                    effects.VitalityModifier = math.lerp(0.5f, 0.1f, decayProgress);
                    effects.StrengthModifier = math.lerp(0.4f, 0.1f, decayProgress);
                    effects.WisdomModifier = 1.2f;
                    effects.FertilityModifier = 0f;
                    effects.DecayRate = config.ProgressRate * 2f;
                    break;
            }

            return effects;
        }

        /// <summary>
        /// Updates ascension progress.
        /// </summary>
        public static void UpdateAscension(
            ref AscensionState ascension,
            float progressDelta)
        {
            if (ascension.AscensionLevel >= ascension.MaxAscensionLevel)
                return;

            ascension.AscensionProgress += progressDelta;
        }

        /// <summary>
        /// Attempts to ascend to next level.
        /// </summary>
        public static bool TryAscend(
            ref AscensionState ascension,
            uint currentTick)
        {
            if (ascension.AscensionLevel >= ascension.MaxAscensionLevel)
                return false;

            if (ascension.AscensionProgress < ascension.AscensionThreshold)
                return false;

            if (ascension.IsAscending != 0)
                return false;

            ascension.AscensionLevel++;
            ascension.AscensionProgress = 0f;
            ascension.LastAscensionTick = currentTick;
            ascension.AscensionThreshold *= 1.5f; // Exponential scaling

            return true;
        }

        /// <summary>
        /// Checks if can reproduce based on state.
        /// </summary>
        public static bool CanReproduce(
            in LifecycleState lifecycle,
            in ReproductionState reproduction,
            uint currentTick)
        {
            if (reproduction.CanReproduce == 0)
                return false;

            if (lifecycle.TotalAge < reproduction.MaturityAge)
                return false;

            if (reproduction.OffspringCount >= reproduction.MaxOffspring)
                return false;

            if (currentTick - reproduction.LastReproductionTick < reproduction.ReproductionCooldown)
                return false;

            if (reproduction.IsPregnant != 0)
                return false;

            // Check stage eligibility
            return lifecycle.CurrentStage == LifecycleStage.Mature ||
                   lifecycle.CurrentStage == LifecycleStage.Elder;
        }

        /// <summary>
        /// Calculates death chance for mortality check.
        /// </summary>
        public static float CalculateDeathChance(
            in LifecycleState lifecycle,
            in MortalityConfig mortality)
        {
            if (mortality.CanDieOfAge == 0)
                return 0f;

            if (lifecycle.TotalAge < mortality.MinimumAge)
                return 0f;

            float ageRatio = lifecycle.TotalAge / math.max(1f, mortality.NaturalLifespan);

            // Low chance until past lifespan
            if (ageRatio < 1f)
                return mortality.DeathChancePerTick * ageRatio * 0.1f;

            // Increasing chance after expected lifespan
            float overtime = ageRatio - 1f;
            return math.min(0.1f, mortality.DeathChancePerTick * (1f + overtime * 2f));
        }

        /// <summary>
        /// Creates default lifecycle config.
        /// </summary>
        public static LifecycleConfig CreateDefaultConfig()
        {
            return new LifecycleConfig
            {
                Type = LifecycleType.Linear,
                AdvanceTrigger = StageTrigger.Age,
                JuvenileDuration = 5000f,
                MatureDuration = 20000f,
                ElderDuration = 10000f,
                DecayDuration = 2000f,
                ProgressRate = 1f,
                MaxStage = (byte)LifecycleStage.Decaying
            };
        }

        /// <summary>
        /// Creates default mortality config.
        /// </summary>
        public static MortalityConfig CreateDefaultMortalityConfig()
        {
            return new MortalityConfig
            {
                NaturalLifespan = 40000f,
                LifespanVariance = 5000f,
                DeathChancePerTick = 0.00001f,
                MinimumAge = 10000f,
                CanDieOfAge = 1,
                CanResurrect = 0,
                LeavesCorpse = 1
            };
        }

        /// <summary>
        /// Gets stage display name.
        /// </summary>
        public static FixedString32Bytes GetStageName(LifecycleStage stage)
        {
            return stage switch
            {
                LifecycleStage.Nascent => "Nascent",
                LifecycleStage.Seed => "Seed",
                LifecycleStage.Juvenile => "Juvenile",
                LifecycleStage.Mature => "Mature",
                LifecycleStage.Elder => "Elder",
                LifecycleStage.Decaying => "Decaying",
                LifecycleStage.Dormant => "Dormant",
                LifecycleStage.Transformed => "Transformed",
                _ => "Unknown"
            };
        }
    }
}

