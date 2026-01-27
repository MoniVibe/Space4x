using Unity.Mathematics;
using Unity.Entities;
using Unity.Burst;

namespace PureDOTS.Runtime.Prestige
{
    /// <summary>
    /// Static helpers for prestige and reputation calculations.
    /// </summary>
    [BurstCompile]
    public static class PrestigeHelpers
    {
        /// <summary>
        /// Gets prestige tier from value.
        /// </summary>
        public static PrestigeTier GetPrestigeTier(float prestige)
        {
            if (prestige >= 100000) return PrestigeTier.Mythic;
            if (prestige >= 25000) return PrestigeTier.Legendary;
            if (prestige >= 8000) return PrestigeTier.Famous;
            if (prestige >= 2000) return PrestigeTier.Renowned;
            if (prestige >= 500) return PrestigeTier.Notable;
            if (prestige >= 100) return PrestigeTier.Known;
            return PrestigeTier.Unknown;
        }

        /// <summary>
        /// Calculates prestige decay.
        /// </summary>
        public static float CalculateDecay(
            float currentPrestige,
            float decayRate,
            PrestigeTier tier,
            uint ticksSinceGain)
        {
            // Higher tiers decay slower
            float tierResist = 1f - (int)tier * 0.1f;
            
            // Recent gains protect against decay
            float recentGainProtection = math.exp(-ticksSinceGain * 0.0001f);
            
            float effectiveDecay = decayRate * tierResist * (1f - recentGainProtection);
            return currentPrestige * effectiveDecay;
        }

        /// <summary>
        /// Adds prestige with modifiers.
        /// </summary>
        public static float AddPrestige(
            ref Prestige prestige,
            float amount,
            float multiplier,
            uint currentTick)
        {
            float gained = amount * multiplier;
            prestige.CurrentPrestige += gained;
            prestige.LifetimePrestige += gained;
            prestige.LastGainTick = currentTick;
            
            if (prestige.CurrentPrestige > prestige.PeakPrestige)
                prestige.PeakPrestige = prestige.CurrentPrestige;
            
            prestige.Tier = GetPrestigeTier(prestige.CurrentPrestige);
            return gained;
        }

        /// <summary>
        /// Modifies reputation score.
        /// </summary>
        public static void ModifyReputation(
            ref DynamicBuffer<ReputationScore> scores,
            Entity audience,
            ReputationType type,
            float change,
            uint currentTick)
        {
            for (int i = 0; i < scores.Length; i++)
            {
                if (scores[i].AudienceEntity == audience && scores[i].Type == type)
                {
                    var score = scores[i];
                    score.Score = math.clamp(score.Score + change * score.Volatility, -100, 100);
                    score.LastUpdateTick = currentTick;
                    scores[i] = score;
                    return;
                }
            }
            
            // Add new reputation entry
            scores.Add(new ReputationScore
            {
                AudienceEntity = audience,
                Type = type,
                Score = math.clamp(change, -100, 100),
                Volatility = 1f,
                LastUpdateTick = currentTick
            });
        }

        /// <summary>
        /// Gets reputation with specific audience.
        /// </summary>
        public static float GetReputation(
            in DynamicBuffer<ReputationScore> scores,
            Entity audience,
            ReputationType type)
        {
            for (int i = 0; i < scores.Length; i++)
            {
                if (scores[i].AudienceEntity == audience && scores[i].Type == type)
                    return scores[i].Score;
            }
            return 0; // Neutral if unknown
        }

        /// <summary>
        /// Checks if unlock requirements are met.
        /// </summary>
        public static bool MeetsUnlockRequirements(
            in Prestige prestige,
            in DynamicBuffer<ReputationScore> scores,
            in PrestigeUnlock unlock)
        {
            bool meetsPrestige = prestige.Tier >= unlock.RequiredTier;
            
            bool meetsRep = true;
            if (unlock.RequiredRepScore > 0)
            {
                float repScore = GetReputation(scores, Entity.Null, unlock.RequiredRepType);
                meetsRep = repScore >= unlock.RequiredRepScore;
            }
            
            if (unlock.RequiresBothPrestigeAndRep != 0)
                return meetsPrestige && meetsRep;
            else
                return meetsPrestige || meetsRep;
        }

        /// <summary>
        /// Applies stress to prestige.
        /// </summary>
        public static void ApplyStress(
            ref PrestigeStress stress,
            float amount,
            uint currentTick)
        {
            stress.CurrentStress = math.saturate(stress.CurrentStress + amount);
            stress.LastStressEventTick = currentTick;
            
            if (stress.CurrentStress >= stress.StressThreshold)
                stress.InCrisis = 1;
        }

        /// <summary>
        /// Updates stress recovery.
        /// </summary>
        public static void UpdateStressRecovery(
            ref PrestigeStress stress,
            float deltaTime)
        {
            if (stress.InCrisis != 0) return; // No recovery during crisis
            
            float recovery = stress.RecoveryRate * deltaTime;
            stress.CurrentStress = math.max(0, stress.CurrentStress - recovery);
        }

        /// <summary>
        /// Calculates infamy from crimes.
        /// </summary>
        public static float CalculateInfamyGain(
            float crimeSeverity,
            bool wasWitnessed,
            float victimImportance)
        {
            float baseInfamy = crimeSeverity * 10f;
            
            // Witnesses spread word
            if (wasWitnessed)
                baseInfamy *= 2f;
            
            // Important victims = more infamy
            baseInfamy *= 1f + victimImportance;
            
            return baseInfamy;
        }

        /// <summary>
        /// Updates heat level decay.
        /// </summary>
        public static void UpdateHeatDecay(
            ref Notoriety notoriety,
            uint ticksSinceLastCrime,
            float decayRate)
        {
            float decay = ticksSinceLastCrime * decayRate;
            notoriety.HeatLevel = math.max(0, notoriety.HeatLevel - decay);
            
            // Infamy decays slower
            notoriety.InfamyLevel = math.max(0, notoriety.InfamyLevel - decay * 0.1f);
        }

        /// <summary>
        /// Calculates bounty based on infamy.
        /// </summary>
        public static float CalculateBounty(float infamyLevel, float baseBountyMultiplier)
        {
            if (infamyLevel < 10) return 0;
            return infamyLevel * baseBountyMultiplier;
        }

        /// <summary>
        /// Gets aggregate reputation across all audiences.
        /// </summary>
        public static float GetAverageReputation(
            in DynamicBuffer<ReputationScore> scores,
            ReputationType type)
        {
            float total = 0;
            int count = 0;
            
            for (int i = 0; i < scores.Length; i++)
            {
                if (scores[i].Type == type)
                {
                    total += scores[i].Score;
                    count++;
                }
            }
            
            return count > 0 ? total / count : 0;
        }
    }
}

