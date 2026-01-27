using Unity.Mathematics;
using Unity.Entities;
using Unity.Burst;

namespace PureDOTS.Runtime.Needs
{
    /// <summary>
    /// Static helpers for needs calculations.
    /// </summary>
    [BurstCompile]
    public static class NeedsHelpers
    {
        /// <summary>
        /// Default needs configuration.
        /// </summary>
        public static NeedsConfig DefaultConfig => new NeedsConfig
        {
            SatisfiedThreshold = 80f,
            NormalThreshold = 50f,
            ConcernedThreshold = 25f,
            UrgentThreshold = 10f,
            WorkingDecayMult = 2.5f,
            IdleDecayMult = 0.5f,
            SleepRegenMult = 5f,
            EatingRegenMult = 3f,
            CriticalPerformancePenalty = 0.5f,
            UrgentPerformancePenalty = 0.25f
        };

        /// <summary>
        /// Gets urgency level from current/max values.
        /// </summary>
        public static NeedUrgency GetUrgency(float current, float max, in NeedsConfig config)
        {
            if (max <= 0) return NeedUrgency.Satisfied;
            
            float percent = (current / max) * 100f;
            
            if (percent >= config.SatisfiedThreshold)
                return NeedUrgency.Satisfied;
            if (percent >= config.NormalThreshold)
                return NeedUrgency.Normal;
            if (percent >= config.ConcernedThreshold)
                return NeedUrgency.Concerned;
            if (percent >= config.UrgentThreshold)
                return NeedUrgency.Urgent;
            
            return NeedUrgency.Critical;
        }

        /// <summary>
        /// Gets decay rate multiplier based on activity.
        /// </summary>
        public static float GetDecayMultiplier(ActivityState activity, in NeedsConfig config)
        {
            return activity switch
            {
                ActivityState.Idle => config.IdleDecayMult,
                ActivityState.Working => config.WorkingDecayMult,
                ActivityState.Resting => 0.25f,
                ActivityState.Sleeping => 0.1f,
                ActivityState.Eating => 0.5f,
                ActivityState.Socializing => 0.75f,
                ActivityState.Exercising => 3f,
                ActivityState.Traveling => 1.5f,
                ActivityState.Combat => 4f,
                _ => 1f
            };
        }

        /// <summary>
        /// Gets regen rate multiplier based on activity.
        /// </summary>
        public static float GetRegenMultiplier(ActivityState activity, NeedType needType, in NeedsConfig config)
        {
            // Sleep regenerates fatigue/energy
            if (activity == ActivityState.Sleeping && 
                (needType == NeedType.Fatigue || needType == NeedType.Health))
            {
                return config.SleepRegenMult;
            }
            
            // Eating regenerates hunger
            if (activity == ActivityState.Eating && needType == NeedType.Hunger)
            {
                return config.EatingRegenMult;
            }
            
            // Socializing regenerates social need
            if (activity == ActivityState.Socializing && needType == NeedType.Social)
            {
                return 3f;
            }
            
            // Resting provides moderate regen
            if (activity == ActivityState.Resting)
            {
                return 2f;
            }
            
            return 1f;
        }

        /// <summary>
        /// Calculates effective decay rate based on activity.
        /// </summary>
        public static float GetEffectiveDecayRate(float baseRate, ActivityState activity, in NeedsConfig config)
        {
            return baseRate * GetDecayMultiplier(activity, config);
        }

        /// <summary>
        /// Calculates effective regen rate based on activity.
        /// </summary>
        public static float GetEffectiveRegenRate(float baseRate, ActivityState activity, NeedType needType, in NeedsConfig config)
        {
            return baseRate * GetRegenMultiplier(activity, needType, config);
        }

        /// <summary>
        /// Checks if entity should seek to satisfy a need.
        /// </summary>
        public static bool ShouldSeekNeed(NeedUrgency urgency)
        {
            return urgency >= NeedUrgency.Concerned;
        }

        /// <summary>
        /// Gets priority for seeking a need (higher = more urgent).
        /// </summary>
        public static int GetSeekPriority(NeedUrgency urgency)
        {
            return urgency switch
            {
                NeedUrgency.Critical => 100,
                NeedUrgency.Urgent => 75,
                NeedUrgency.Concerned => 50,
                NeedUrgency.Normal => 25,
                NeedUrgency.Satisfied => 0,
                _ => 0
            };
        }

        /// <summary>
        /// Gets performance penalty from needs state.
        /// </summary>
        public static float GetPerformancePenalty(in EntityNeeds needs, in NeedsConfig config)
        {
            float penalty = 0f;
            
            // Health penalty
            penalty += GetUrgencyPenalty(needs.HealthUrgency, config);
            
            // Energy penalty
            penalty += GetUrgencyPenalty(needs.EnergyUrgency, config);
            
            // Morale penalty (half weight)
            penalty += GetUrgencyPenalty(needs.MoraleUrgency, config) * 0.5f;
            
            return math.clamp(penalty, 0f, 0.9f); // Max 90% penalty
        }

        /// <summary>
        /// Gets penalty for a specific urgency level.
        /// </summary>
        public static float GetUrgencyPenalty(NeedUrgency urgency, in NeedsConfig config)
        {
            return urgency switch
            {
                NeedUrgency.Critical => config.CriticalPerformancePenalty,
                NeedUrgency.Urgent => config.UrgentPerformancePenalty,
                NeedUrgency.Concerned => config.UrgentPerformancePenalty * 0.5f,
                _ => 0f
            };
        }

        /// <summary>
        /// Applies need decay for a time delta.
        /// </summary>
        public static void ApplyDecay(ref EntityNeeds needs, float deltaTime, ActivityState activity, in NeedsConfig config)
        {
            float decayMult = GetDecayMultiplier(activity, config);
            
            needs.Energy = math.max(0, needs.Energy - needs.EnergyDecayRate * decayMult * deltaTime);
            needs.Morale = math.max(0, needs.Morale - needs.MoraleDecayRate * decayMult * deltaTime);
            
            // Update urgencies
            needs.EnergyUrgency = GetUrgency(needs.Energy, needs.MaxEnergy, config);
            needs.MoraleUrgency = GetUrgency(needs.Morale, needs.MaxMorale, config);
            needs.HealthUrgency = GetUrgency(needs.Health, needs.MaxHealth, config);
        }

        /// <summary>
        /// Applies need regeneration for a time delta.
        /// </summary>
        public static void ApplyRegen(ref EntityNeeds needs, float deltaTime, ActivityState activity, in NeedsConfig config)
        {
            // Health regen (only when resting/sleeping)
            if (activity == ActivityState.Resting || activity == ActivityState.Sleeping)
            {
                float healthRegen = needs.HealthRegenRate * GetRegenMultiplier(activity, NeedType.Health, config);
                needs.Health = math.min(needs.MaxHealth, needs.Health + healthRegen * deltaTime);
            }
            
            // Energy regen (when sleeping or eating)
            if (activity == ActivityState.Sleeping || activity == ActivityState.Eating || activity == ActivityState.Resting)
            {
                float energyRegen = needs.EnergyRegenRate * GetRegenMultiplier(activity, NeedType.Hunger, config);
                needs.Energy = math.min(needs.MaxEnergy, needs.Energy + energyRegen * deltaTime);
            }
            
            // Morale regen (when socializing or resting)
            if (activity == ActivityState.Socializing || activity == ActivityState.Resting)
            {
                float moraleRegen = needs.MoraleRegenRate * GetRegenMultiplier(activity, NeedType.Social, config);
                needs.Morale = math.min(needs.MaxMorale, needs.Morale + moraleRegen * deltaTime);
            }
            
            // Update urgencies
            needs.EnergyUrgency = GetUrgency(needs.Energy, needs.MaxEnergy, config);
            needs.MoraleUrgency = GetUrgency(needs.Morale, needs.MaxMorale, config);
            needs.HealthUrgency = GetUrgency(needs.Health, needs.MaxHealth, config);
        }

        /// <summary>
        /// Satisfies a specific need by amount.
        /// </summary>
        public static void SatisfyNeed(ref EntityNeeds needs, NeedType needType, float amount, in NeedsConfig config)
        {
            switch (needType)
            {
                case NeedType.Health:
                    needs.Health = math.min(needs.MaxHealth, needs.Health + amount);
                    needs.HealthUrgency = GetUrgency(needs.Health, needs.MaxHealth, config);
                    break;
                    
                case NeedType.Hunger:
                case NeedType.Fatigue:
                    needs.Energy = math.min(needs.MaxEnergy, needs.Energy + amount);
                    needs.EnergyUrgency = GetUrgency(needs.Energy, needs.MaxEnergy, config);
                    break;
                    
                case NeedType.Social:
                case NeedType.Entertainment:
                case NeedType.Purpose:
                    needs.Morale = math.min(needs.MaxMorale, needs.Morale + amount);
                    needs.MoraleUrgency = GetUrgency(needs.Morale, needs.MaxMorale, config);
                    break;
            }
        }

        /// <summary>
        /// Gets the most urgent need from an entity.
        /// </summary>
        public static NeedType GetMostUrgentNeed(in EntityNeeds needs)
        {
            NeedUrgency maxUrgency = NeedUrgency.Satisfied;
            NeedType mostUrgent = NeedType.None;
            
            if (needs.HealthUrgency > maxUrgency)
            {
                maxUrgency = needs.HealthUrgency;
                mostUrgent = NeedType.Health;
            }
            
            if (needs.EnergyUrgency > maxUrgency)
            {
                maxUrgency = needs.EnergyUrgency;
                mostUrgent = NeedType.Hunger; // Energy represents hunger/fatigue
            }
            
            if (needs.MoraleUrgency > maxUrgency)
            {
                mostUrgent = NeedType.Social; // Morale represents social/purpose
            }
            
            return mostUrgent;
        }

        /// <summary>
        /// Creates default entity needs.
        /// </summary>
        public static EntityNeeds CreateDefault(float maxHealth = 100f, float maxEnergy = 100f, float maxMorale = 100f)
        {
            return new EntityNeeds
            {
                Health = maxHealth,
                MaxHealth = maxHealth,
                Energy = maxEnergy,
                MaxEnergy = maxEnergy,
                Morale = maxMorale,
                MaxMorale = maxMorale,
                HealthUrgency = NeedUrgency.Satisfied,
                EnergyUrgency = NeedUrgency.Satisfied,
                MoraleUrgency = NeedUrgency.Satisfied,
                EnergyDecayRate = 1f,   // 1 per second
                MoraleDecayRate = 0.5f, // 0.5 per second
                HealthRegenRate = 0.5f,
                EnergyRegenRate = 2f,
                MoraleRegenRate = 1f
            };
        }

        /// <summary>
        /// Finds a specific need in a buffer.
        /// </summary>
        public static bool TryGetNeed(in DynamicBuffer<NeedEntry> buffer, NeedType type, out NeedEntry need)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Type == type)
                {
                    need = buffer[i];
                    return true;
                }
            }
            
            need = default;
            return false;
        }

        /// <summary>
        /// Updates a specific need in a buffer.
        /// </summary>
        public static bool UpdateNeed(ref DynamicBuffer<NeedEntry> buffer, NeedType type, float newValue, uint tick, in NeedsConfig config)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Type == type)
                {
                    var entry = buffer[i];
                    entry.Current = math.clamp(newValue, 0, entry.Max);
                    entry.Urgency = GetUrgency(entry.Current, entry.Max, config);
                    entry.LastUpdateTick = tick;
                    
                    if (entry.Urgency == NeedUrgency.Satisfied)
                        entry.LastSatisfiedTick = tick;
                    
                    buffer[i] = entry;
                    return true;
                }
            }
            return false;
        }
    }
}

