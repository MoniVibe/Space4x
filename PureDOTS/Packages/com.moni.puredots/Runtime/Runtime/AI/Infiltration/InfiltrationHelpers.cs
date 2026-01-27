using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;
using Unity.Burst;

namespace PureDOTS.Runtime.AI.Infiltration
{
    /// <summary>
    /// Static helpers for infiltration and espionage calculations.
    /// </summary>
    [BurstCompile]
    public static class InfiltrationHelpers
    {
        /// <summary>
        /// Default counter-intelligence configuration.
        /// </summary>
        public static CounterIntelligence DefaultCounterIntel => new CounterIntelligence
        {
            DetectionRate = 0.01f,
            SuspicionGrowth = 0.05f,
            SuspicionDecay = 0.01f,
            InvestigationPower = 0.5f,
            SecurityLevel = 5
        };

        /// <summary>
        /// Calculates infiltration progress rate.
        /// </summary>
        public static float CalculateProgressRate(
            InfiltrationLevel currentLevel,
            InfiltrationMethod method,
            float coverStrength,
            float targetSecurityLevel)
        {
            // Higher levels progress slower
            float levelPenalty = 1f / (1 + (int)currentLevel);
            
            // Method effectiveness varies
            float methodBonus = GetMethodEffectiveness(method, targetSecurityLevel);
            
            // Good cover helps
            float coverBonus = 0.5f + coverStrength * 0.5f;
            
            // Security slows progress
            float securityPenalty = 1f - (targetSecurityLevel * 0.08f);
            
            return levelPenalty * methodBonus * coverBonus * math.max(0.1f, securityPenalty);
        }

        /// <summary>
        /// Gets method effectiveness against security level.
        /// </summary>
        public static float GetMethodEffectiveness(InfiltrationMethod method, float securityLevel)
        {
            return method switch
            {
                InfiltrationMethod.Hacking => securityLevel < 5 ? 1.2f : 0.6f,
                InfiltrationMethod.Cultural => 1.0f,
                InfiltrationMethod.Blackmail => 0.8f + securityLevel * 0.05f,
                InfiltrationMethod.Bribery => securityLevel < 3 ? 1.3f : 0.5f,
                InfiltrationMethod.Celebrity => 1.1f,
                InfiltrationMethod.Conscription => 0.9f,
                InfiltrationMethod.Seduction => 1.0f,
                InfiltrationMethod.Forgery => securityLevel < 6 ? 1.1f : 0.4f,
                _ => 1.0f
            };
        }

        /// <summary>
        /// Calculates suspicion gain from activity.
        /// </summary>
        public static float CalculateSuspicionGain(
            float activityRisk,
            float coverStrength,
            float counterIntelPower,
            float currentSuspicion)
        {
            // Riskier activities generate more suspicion
            float baseSuspicion = activityRisk * 0.1f;
            
            // Good cover reduces suspicion gain
            float coverReduction = coverStrength * 0.7f;
            
            // Counterintel amplifies suspicion growth
            float counterIntelBonus = 1f + counterIntelPower * 0.5f;
            
            // Already suspicious = more scrutiny
            float scrutinyBonus = 1f + currentSuspicion * 0.3f;
            
            return math.max(0, baseSuspicion * (1f - coverReduction) * counterIntelBonus * scrutinyBonus);
        }

        /// <summary>
        /// Calculates natural suspicion decay.
        /// </summary>
        public static float CalculateSuspicionDecay(
            float currentSuspicion,
            float timeSinceActivity,
            float coverStrength,
            float baseDecayRate)
        {
            // Suspicion decays over time when agent is inactive
            float timeDecay = baseDecayRate * timeSinceActivity * 0.01f;
            
            // Good cover accelerates decay
            float coverBonus = 1f + coverStrength * 0.5f;
            
            return math.min(currentSuspicion, currentSuspicion * timeDecay * coverBonus);
        }

        /// <summary>
        /// Performs detection check against agent.
        /// </summary>
        public static bool PerformDetectionCheck(
            float suspicion,
            float coverStrength,
            float detectionRate,
            float investigationPower,
            uint seed)
        {
            float roll = DeterministicRandom(seed) / (float)uint.MaxValue;
            
            // Base detection from suspicion
            float detectionChance = suspicion * detectionRate;
            
            // Cover provides protection
            float coverProtection = coverStrength * 0.6f;
            
            // Active investigation boosts detection
            float investigationBonus = investigationPower * 0.3f;
            
            float finalChance = math.saturate(detectionChance * (1f - coverProtection) + investigationBonus);
            
            return roll < finalChance;
        }

        /// <summary>
        /// Calculates extraction success chance.
        /// </summary>
        public static float CalculateExtractionChance(
            in ExtractionPlan plan,
            float suspicionLevel,
            float counterIntelPower,
            bool hasActiveInvestigation)
        {
            // Base from plan quality
            float baseChance = plan.PlanQuality * 0.08f + 0.2f;
            
            // Suspicion makes extraction harder
            float suspicionPenalty = suspicionLevel * 0.4f;
            
            // Counter-intel blocks escape routes
            float counterIntelPenalty = counterIntelPower * 0.2f;
            
            // Active investigation is very dangerous
            float investigationPenalty = hasActiveInvestigation ? 0.3f : 0;
            
            // Contacts help
            float contactBonus = plan.ExfilContactEntity != Entity.Null ? 0.15f : 0;
            
            return math.saturate(baseChance - suspicionPenalty - counterIntelPenalty - investigationPenalty + contactBonus);
        }

        /// <summary>
        /// Calculates intel value based on level and freshness.
        /// </summary>
        public static float CalculateIntelValue(
            InfiltrationLevel gatheredAt,
            uint currentTick,
            uint gatheredTick,
            float stalenessFactor)
        {
            // Higher level intel is more valuable
            float levelValue = (int)gatheredAt * 0.2f;
            
            // Intel goes stale over time
            uint age = currentTick - gatheredTick;
            float freshness = math.exp(-age * stalenessFactor);
            
            return levelValue * freshness;
        }

        /// <summary>
        /// Checks if agent should level up infiltration.
        /// </summary>
        public static bool ShouldLevelUp(in InfiltrationState state, float progressThreshold)
        {
            return state.Progress >= progressThreshold && 
                   state.Level < InfiltrationLevel.Subverted &&
                   state.IsExposed == 0;
        }

        /// <summary>
        /// Gets intel types available at each infiltration level.
        /// </summary>
        public static int GetAvailableIntelTypes(InfiltrationLevel level)
        {
            return level switch
            {
                InfiltrationLevel.Contact => 1,
                InfiltrationLevel.Embedded => 3,
                InfiltrationLevel.Trusted => 6,
                InfiltrationLevel.Influential => 10,
                InfiltrationLevel.Subverted => 15,
                _ => 0
            };
        }

        /// <summary>
        /// Calculates cover identity degradation.
        /// </summary>
        public static float CalculateCoverDegradation(
            in CoverIdentity cover,
            uint currentTick,
            float suspicion,
            bool hasBeenQuestioned)
        {
            // Cover degrades over time
            uint age = currentTick - cover.CreatedTick;
            float ageDegradation = age * 0.00001f;
            
            // Suspicion accelerates degradation
            float suspicionDegradation = suspicion * 0.1f;
            
            // Questioning damages cover
            float questioningDamage = hasBeenQuestioned ? 0.1f : 0;
            
            return ageDegradation + suspicionDegradation + questioningDamage;
        }

        /// <summary>
        /// Updates infiltration state.
        /// </summary>
        public static InfiltrationState UpdateInfiltration(
            in InfiltrationState current,
            float progressDelta,
            float suspicionDelta,
            uint currentTick)
        {
            var result = current;
            result.Progress = math.saturate(current.Progress + progressDelta);
            result.Suspicion = math.saturate(current.Suspicion + suspicionDelta);
            result.LastActivityTick = currentTick;
            return result;
        }

        /// <summary>
        /// Levels up infiltration.
        /// </summary>
        public static InfiltrationState LevelUp(in InfiltrationState current)
        {
            var result = current;
            result.Level = (InfiltrationLevel)((int)current.Level + 1);
            result.Progress = 0;
            return result;
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
    }
}

