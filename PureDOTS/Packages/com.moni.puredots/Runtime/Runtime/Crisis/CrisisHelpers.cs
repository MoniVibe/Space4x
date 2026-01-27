using Unity.Mathematics;
using Unity.Entities;
using Unity.Burst;
using Unity.Collections;

namespace PureDOTS.Runtime.Crisis
{
    /// <summary>
    /// Static helpers for crisis lifecycle management.
    /// </summary>
    [BurstCompile]
    public static class CrisisHelpers
    {
        /// <summary>
        /// Updates crisis tracker with contributions.
        /// </summary>
        public static void UpdateTracker(
            ref CrisisTracker tracker,
            in DynamicBuffer<CrisisTrigger> triggers,
            float deltaTime,
            uint currentTick)
        {
            // Calculate contribution from triggers
            float totalContribution = 0;
            for (int i = 0; i < triggers.Length; i++)
            {
                if (triggers[i].IsMet != 0)
                {
                    float overflow = triggers[i].CurrentValue - triggers[i].ThresholdValue;
                    totalContribution += math.max(0, overflow) * triggers[i].ContributionWeight;
                }
            }
            
            // Apply growth and decay
            float growth = (tracker.GrowthRate + totalContribution) * deltaTime;
            float decay = tracker.DecayRate * deltaTime;
            
            tracker.AccumulatedValue = math.saturate(tracker.AccumulatedValue + growth - decay);
            tracker.LastUpdateTick = currentTick;
        }

        /// <summary>
        /// Checks if crisis should advance phase.
        /// </summary>
        public static CrisisPhase CheckPhaseAdvancement(
            in CrisisState crisis,
            uint currentTick,
            float phaseTimeMultiplier)
        {
            uint ticksInPhase = currentTick - crisis.PhaseStartTick;
            float timeThreshold = GetPhaseTimeThreshold(crisis.Phase) * phaseTimeMultiplier;
            
            if (ticksInPhase < timeThreshold)
                return crisis.Phase;
            
            return crisis.Phase switch
            {
                CrisisPhase.Dormant => CrisisPhase.Seeding,
                CrisisPhase.Seeding => CrisisPhase.Foreshadowing,
                CrisisPhase.Foreshadowing => CrisisPhase.Emergence,
                CrisisPhase.Emergence => CrisisPhase.Escalation,
                CrisisPhase.Escalation => CrisisPhase.Climax,
                CrisisPhase.Climax => CrisisPhase.Resolution,
                CrisisPhase.Resolution => CrisisPhase.Aftermath,
                _ => crisis.Phase
            };
        }

        private static float GetPhaseTimeThreshold(CrisisPhase phase)
        {
            return phase switch
            {
                CrisisPhase.Seeding => 5000,
                CrisisPhase.Foreshadowing => 3000,
                CrisisPhase.Emergence => 2000,
                CrisisPhase.Escalation => 4000,
                CrisisPhase.Climax => 2000,
                CrisisPhase.Resolution => 3000,
                _ => 1000
            };
        }

        /// <summary>
        /// Calculates intensity change this tick.
        /// </summary>
        public static float CalculateIntensityChange(
            in CrisisState crisis,
            float externalPressure,
            float playerMitigation)
        {
            float baseChange = crisis.Phase switch
            {
                CrisisPhase.Seeding => 0.001f,
                CrisisPhase.Foreshadowing => 0.002f,
                CrisisPhase.Emergence => 0.005f,
                CrisisPhase.Escalation => 0.01f,
                CrisisPhase.Climax => 0.005f,
                CrisisPhase.Resolution => -0.01f,
                CrisisPhase.Aftermath => -0.02f,
                _ => 0
            };
            
            // External pressure accelerates
            baseChange *= 1f + externalPressure;
            
            // Player mitigation slows or reverses
            baseChange -= playerMitigation;
            
            return baseChange;
        }

        /// <summary>
        /// Gets best resolution path.
        /// </summary>
        public static int GetBestResolutionPath(
            in DynamicBuffer<ResolutionPath> paths,
            float availableResources,
            float availableTime)
        {
            int bestIdx = -1;
            float bestScore = -1;
            
            for (int i = 0; i < paths.Length; i++)
            {
                if (paths[i].ResourceCost > availableResources) continue;
                if (paths[i].TimeRequired > availableTime) continue;
                
                float score = paths[i].SuccessChance * paths[i].IntensityReduction;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIdx = i;
                }
            }
            
            return bestIdx;
        }

        /// <summary>
        /// Applies resolution attempt.
        /// </summary>
        public static bool AttemptResolution(
            ref CrisisState crisis,
            in ResolutionPath path,
            uint seed)
        {
            var rng = new Random(seed);
            bool success = rng.NextFloat(0, 1) <= path.SuccessChance;
            
            if (success)
            {
                crisis.Intensity = math.max(0, crisis.Intensity - path.IntensityReduction);
                if (crisis.Intensity <= 0.1f)
                    crisis.Phase = CrisisPhase.Aftermath;
            }
            
            return success;
        }

        /// <summary>
        /// Checks and applies escalation milestones.
        /// </summary>
        public static void CheckEscalationMilestones(
            in CrisisState crisis,
            ref DynamicBuffer<EscalationMilestone> milestones,
            uint currentTick)
        {
            for (int i = 0; i < milestones.Length; i++)
            {
                var milestone = milestones[i];
                if (milestone.WasReached == 0 && crisis.Intensity >= milestone.IntensityThreshold)
                {
                    milestone.WasReached = 1;
                    milestone.ReachedTick = currentTick;
                    milestones[i] = milestone;
                }
            }
        }

        /// <summary>
        /// Generates aftermath effects based on crisis outcome.
        /// </summary>
        public static void GenerateAftermath(
            ref DynamicBuffer<AftermathEffect> effects,
            in CrisisState crisis,
            float resolutionQuality,
            uint currentTick)
        {
            // Worse crises leave worse aftermath
            float magnitude = crisis.MaxIntensity * (1f - resolutionQuality);
            
            effects.Add(new AftermathEffect
            {
                EffectType = "reconstruction",
                Magnitude = magnitude,
                DurationTicks = (uint)(10000 * magnitude),
                AppliedTick = currentTick,
                IsPositive = 0
            });
            
            // Some positive effects from surviving crisis
            if (resolutionQuality > 0.5f)
            {
                effects.Add(new AftermathEffect
                {
                    EffectType = "resilience",
                    Magnitude = resolutionQuality * 0.5f,
                    DurationTicks = 20000,
                    AppliedTick = currentTick,
                    IsPositive = 1
                });
            }
        }

        /// <summary>
        /// Calculates scope expansion chance.
        /// </summary>
        public static bool ShouldExpandScope(
            in CrisisState crisis,
            float proximityFactor,
            uint seed)
        {
            if (crisis.Phase < CrisisPhase.Escalation)
                return false;
            
            float expandChance = crisis.Intensity * 0.1f * proximityFactor;
            var rng = new Random(seed);
            return rng.NextFloat(0, 1) < expandChance;
        }
    }
}

