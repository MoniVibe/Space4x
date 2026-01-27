using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;
using Unity.Burst;

namespace PureDOTS.Runtime.Expertise
{
    /// <summary>
    /// Static helpers for expertise and mastery calculations.
    /// </summary>
    [BurstCompile]
    public static class ExpertiseHelpers
    {
        /// <summary>
        /// Default expertise configuration.
        /// </summary>
        public static ExpertiseConfig DefaultConfig => new ExpertiseConfig
        {
            NoviceThreshold = 0,
            ApprenticeThreshold = 100,
            JourneymanThreshold = 500,
            ExpertThreshold = 2000,
            MasterThreshold = 8000,
            GrandmasterThreshold = 25000,
            LegendThreshold = 100000
        };

        /// <summary>
        /// Calculates XP gain from activity with modifiers.
        /// </summary>
        public static float CalculateXPGain(
            in XPActivity activity,
            byte inclination,
            float mentorBonus,
            float focusBonus)
        {
            // Base XP from activity
            float xp = activity.BaseXP;
            
            // Difficulty scales XP (harder = more)
            xp *= activity.DifficultyModifier;
            
            // Success quality
            xp *= activity.SuccessModifier;
            
            // Inclination bonus (1-10 -> 0.5-1.5x)
            float inclinationMod = 0.5f + inclination * 0.1f;
            xp *= inclinationMod;
            
            // Mentor bonus (stacks)
            xp *= 1f + mentorBonus;
            
            // Focus bonus from player priorities
            xp *= 1f + focusBonus;
            
            return xp;
        }

        /// <summary>
        /// Gets mastery tier from XP amount.
        /// </summary>
        public static MasteryTier GetMasteryTier(float xp, in ExpertiseConfig config)
        {
            if (xp >= config.LegendThreshold) return MasteryTier.Legend;
            if (xp >= config.GrandmasterThreshold) return MasteryTier.Grandmaster;
            if (xp >= config.MasterThreshold) return MasteryTier.Master;
            if (xp >= config.ExpertThreshold) return MasteryTier.Expert;
            if (xp >= config.JourneymanThreshold) return MasteryTier.Journeyman;
            if (xp >= config.ApprenticeThreshold) return MasteryTier.Apprentice;
            return MasteryTier.Novice;
        }

        /// <summary>
        /// Calculates progress to next tier (0-1).
        /// </summary>
        public static float GetTierProgress(float xp, MasteryTier currentTier, in ExpertiseConfig config)
        {
            float currentThreshold = GetThreshold(currentTier, config);
            float nextThreshold = GetThreshold((MasteryTier)((int)currentTier + 1), config);
            
            if (nextThreshold <= currentThreshold) return 1f; // Max tier
            
            return math.saturate((xp - currentThreshold) / (nextThreshold - currentThreshold));
        }

        /// <summary>
        /// Gets XP threshold for tier.
        /// </summary>
        public static float GetThreshold(MasteryTier tier, in ExpertiseConfig config)
        {
            return tier switch
            {
                MasteryTier.Novice => config.NoviceThreshold,
                MasteryTier.Apprentice => config.ApprenticeThreshold,
                MasteryTier.Journeyman => config.JourneymanThreshold,
                MasteryTier.Expert => config.ExpertThreshold,
                MasteryTier.Master => config.MasterThreshold,
                MasteryTier.Grandmaster => config.GrandmasterThreshold,
                MasteryTier.Legend => config.LegendThreshold,
                _ => 0
            };
        }

        /// <summary>
        /// Allocates XP from pool to expertise entries.
        /// </summary>
        public static void AllocateXP(
            ref XPPool pool,
            ref DynamicBuffer<ExpertiseEntry> expertise,
            in XPAllocationPrefs prefs,
            in ExpertiseConfig config,
            uint currentTick)
        {
            if (pool.UnallocatedXP <= 0) return;
            
            float toAllocate = pool.UnallocatedXP;
            pool.UnallocatedXP = 0;
            
            // Find focus entries
            int primaryIdx = -1;
            int secondaryIdx = -1;
            float totalWeight = 0;
            
            for (int i = 0; i < expertise.Length; i++)
            {
                if (expertise[i].Category == prefs.PrimaryFocus) primaryIdx = i;
                if (expertise[i].Category == prefs.SecondaryFocus) secondaryIdx = i;
                
                // Weight by inclination if following aptitude
                float weight = prefs.FollowAptitude != 0 ? expertise[i].Inclination : 5f;
                totalWeight += weight;
            }
            
            // Distribute XP
            for (int i = 0; i < expertise.Length; i++)
            {
                var entry = expertise[i];
                float share;
                
                if (i == primaryIdx)
                {
                    share = toAllocate * prefs.FocusWeight;
                }
                else if (i == secondaryIdx)
                {
                    share = toAllocate * prefs.FocusWeight * 0.5f;
                }
                else
                {
                    float weight = prefs.FollowAptitude != 0 ? entry.Inclination : 5f;
                    float remainingRatio = math.max(0, 1f - prefs.FocusWeight * 1.5f);
                    share = toAllocate * remainingRatio * (weight / math.max(1f, totalWeight));
                }
                
                entry.CurrentXP += share;
                entry.TotalXP += share;
                entry.RecentGain += share;
                entry.Tier = GetMasteryTier(entry.CurrentXP, config);
                entry.LastActivityTick = currentTick;
                expertise[i] = entry;
            }
        }

        /// <summary>
        /// Calculates teaching effectiveness.
        /// </summary>
        public static float CalculateTeachingBonus(
            in MentoringCapability mentor,
            MasteryTier mentorTier,
            MasteryTier studentTier)
        {
            // Can't teach what you barely know
            if (mentorTier < mentor.MinTierToTeach) return 0;
            
            // Teaching quality base
            float bonus = mentor.TeachingQuality;
            
            // Gap between mentor and student matters
            int tierGap = (int)mentorTier - (int)studentTier;
            float gapBonus = math.min(0.5f, tierGap * 0.1f);
            
            return bonus + gapBonus;
        }

        /// <summary>
        /// Calculates XP decay for unused expertise.
        /// </summary>
        public static float CalculateDecay(
            float currentXP,
            uint ticksSinceActivity,
            MasteryTier tier)
        {
            // Higher tiers decay slower
            float tierResistance = 1f - (int)tier * 0.1f;
            
            // Base decay rate
            float decayRate = 0.0001f * math.max(0.1f, tierResistance);
            
            // More XP = more to lose, but slower rate
            float decayAmount = currentXP * decayRate * ticksSinceActivity;
            
            // Never decay below tier threshold
            return math.max(0, decayAmount);
        }

        /// <summary>
        /// Checks if entity can mentor in category.
        /// </summary>
        public static bool CanMentor(
            in DynamicBuffer<ExpertiseEntry> expertise,
            ExpertiseCategory category,
            in MentoringCapability capability)
        {
            for (int i = 0; i < expertise.Length; i++)
            {
                if (expertise[i].Category == category &&
                    expertise[i].Tier >= capability.MinTierToTeach)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Gets best expertise category for entity.
        /// </summary>
        public static ExpertiseCategory GetBestExpertise(
            in DynamicBuffer<ExpertiseEntry> expertise,
            out MasteryTier tier,
            out float xp)
        {
            tier = MasteryTier.Novice;
            xp = 0;
            ExpertiseCategory best = ExpertiseCategory.Combat;
            
            for (int i = 0; i < expertise.Length; i++)
            {
                if (expertise[i].CurrentXP > xp)
                {
                    xp = expertise[i].CurrentXP;
                    tier = expertise[i].Tier;
                    best = expertise[i].Category;
                }
            }
            
            return best;
        }

        /// <summary>
        /// Adds XP from completed activity.
        /// </summary>
        public static void AddActivityXP(
            ref XPPool pool,
            in XPActivity activity)
        {
            // Route to appropriate pool
            int cat = (int)activity.Category;
            if (cat < 4)
                pool.CombatXP += activity.BaseXP;
            else if (cat < 8)
                pool.CraftXP += activity.BaseXP;
            else if (cat < 12)
                pool.SocialXP += activity.BaseXP;
            else
                pool.SpecialXP += activity.BaseXP;
            
            pool.UnallocatedXP += activity.BaseXP;
        }

        /// <summary>
        /// Gets expertise entry for category.
        /// </summary>
        public static bool TryGetExpertise(
            in DynamicBuffer<ExpertiseEntry> expertise,
            ExpertiseCategory category,
            out ExpertiseEntry entry)
        {
            entry = default;
            for (int i = 0; i < expertise.Length; i++)
            {
                if (expertise[i].Category == category)
                {
                    entry = expertise[i];
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Adds or updates expertise entry.
        /// </summary>
        public static void AddOrUpdateExpertise(
            ref DynamicBuffer<ExpertiseEntry> expertise,
            ExpertiseCategory category,
            float xpToAdd,
            byte inclination,
            in ExpertiseConfig config,
            uint currentTick)
        {
            for (int i = 0; i < expertise.Length; i++)
            {
                if (expertise[i].Category == category)
                {
                    var entry = expertise[i];
                    entry.CurrentXP += xpToAdd;
                    entry.TotalXP += xpToAdd;
                    entry.RecentGain += xpToAdd;
                    entry.Tier = GetMasteryTier(entry.CurrentXP, config);
                    entry.LastActivityTick = currentTick;
                    expertise[i] = entry;
                    return;
                }
            }
            
            // Not found, add new
            expertise.Add(new ExpertiseEntry
            {
                Category = category,
                CurrentXP = xpToAdd,
                TotalXP = xpToAdd,
                Tier = GetMasteryTier(xpToAdd, config),
                Inclination = inclination,
                RecentGain = xpToAdd,
                LastActivityTick = currentTick
            });
        }

        /// <summary>
        /// Resets recent gain tracking (call at end of session).
        /// </summary>
        public static void ResetRecentGains(ref DynamicBuffer<ExpertiseEntry> expertise)
        {
            for (int i = 0; i < expertise.Length; i++)
            {
                var entry = expertise[i];
                entry.RecentGain = 0;
                expertise[i] = entry;
            }
        }

        /// <summary>
        /// Gets total XP across all expertise.
        /// </summary>
        public static float GetTotalXP(in DynamicBuffer<ExpertiseEntry> expertise)
        {
            float total = 0;
            for (int i = 0; i < expertise.Length; i++)
            {
                total += expertise[i].TotalXP;
            }
            return total;
        }
    }
}

