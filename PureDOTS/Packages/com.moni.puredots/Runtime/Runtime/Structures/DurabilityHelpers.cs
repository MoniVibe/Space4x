using Unity.Mathematics;
using Unity.Entities;
using Unity.Burst;

namespace PureDOTS.Runtime.Structures
{
    /// <summary>
    /// Static helpers for durability calculations.
    /// </summary>
    [BurstCompile]
    public static class DurabilityHelpers
    {
        /// <summary>
        /// Default durability configuration.
        /// </summary>
        public static DurabilityConfig DefaultConfig => new DurabilityConfig
        {
            DamagedEfficiencyPenalty = 0.25f,
            CriticalEfficiencyPenalty = 0.5f,
            NaturalDecayRate = 0.1f,
            AutoQueueRepair = true,
            RepairCostMultiplier = 1f,
            TicksPerDay = 86400
        };

        /// <summary>
        /// Gets durability state from current/max values.
        /// </summary>
        public static DurabilityState GetState(float current, float max)
        {
            if (max <= 0) return DurabilityState.Destroyed;
            
            float percent = current / max;
            
            if (percent <= 0) return DurabilityState.Destroyed;
            if (percent < 0.25f) return DurabilityState.Critical;
            if (percent < 0.50f) return DurabilityState.Damaged;
            if (percent < 0.75f) return DurabilityState.Worn;
            if (percent < 1.0f) return DurabilityState.Good;
            return DurabilityState.Pristine;
        }

        /// <summary>
        /// Gets efficiency penalty based on durability state.
        /// </summary>
        public static float GetEfficiencyPenalty(in StructureDurability durability, in DurabilityConfig config)
        {
            float percent = durability.MaxDurability > 0 
                ? durability.CurrentDurability / durability.MaxDurability 
                : 0;
            
            if (percent >= durability.DamagedThreshold)
                return 0f;
            
            if (percent >= durability.CriticalThreshold)
            {
                // Linear interpolation between damaged and critical
                float t = (durability.DamagedThreshold - percent) / (durability.DamagedThreshold - durability.CriticalThreshold);
                return math.lerp(0f, config.DamagedEfficiencyPenalty, t);
            }
            
            // Below critical threshold
            float critT = (durability.CriticalThreshold - percent) / durability.CriticalThreshold;
            return math.lerp(config.DamagedEfficiencyPenalty, config.CriticalEfficiencyPenalty, critT);
        }

        /// <summary>
        /// Applies damage to structure.
        /// </summary>
        public static void ApplyDamage(ref StructureDurability durability, float damage, uint currentTick, in DurabilityConfig config)
        {
            durability.CurrentDurability = math.max(0, durability.CurrentDurability - damage);
            durability.State = GetState(durability.CurrentDurability, durability.MaxDurability);
            durability.EfficiencyPenalty = GetEfficiencyPenalty(durability, config);
            durability.LastDamageTick = currentTick;
            durability.NeedsRepair = durability.CurrentDurability < durability.MaxDurability;
        }

        /// <summary>
        /// Repairs structure.
        /// </summary>
        public static void Repair(ref StructureDurability durability, float amount, uint currentTick, in DurabilityConfig config)
        {
            durability.CurrentDurability = math.min(durability.MaxDurability, durability.CurrentDurability + amount);
            durability.State = GetState(durability.CurrentDurability, durability.MaxDurability);
            durability.EfficiencyPenalty = GetEfficiencyPenalty(durability, config);
            durability.LastRepairTick = currentTick;
            durability.NeedsRepair = durability.CurrentDurability < durability.MaxDurability;
        }

        /// <summary>
        /// Applies natural decay over time.
        /// </summary>
        public static void ApplyDecay(ref StructureDurability durability, uint ticksElapsed, in DurabilityConfig config)
        {
            if (config.NaturalDecayRate <= 0) return;
            
            float daysElapsed = ticksElapsed / (float)config.TicksPerDay;
            float decay = daysElapsed * config.NaturalDecayRate;
            
            durability.CurrentDurability = math.max(0, durability.CurrentDurability - decay);
            durability.State = GetState(durability.CurrentDurability, durability.MaxDurability);
            durability.EfficiencyPenalty = GetEfficiencyPenalty(durability, config);
            durability.NeedsRepair = durability.CurrentDurability < durability.MaxDurability;
        }

        /// <summary>
        /// Calculates repair cost.
        /// </summary>
        public static float CalculateRepairCost(in StructureDurability durability, float baseCost, in DurabilityConfig config)
        {
            float damagePercent = 1f - (durability.CurrentDurability / durability.MaxDurability);
            return baseCost * damagePercent * config.RepairCostMultiplier;
        }

        /// <summary>
        /// Checks if structure is functional.
        /// </summary>
        public static bool IsFunctional(in StructureDurability durability)
        {
            return durability.State != DurabilityState.Destroyed;
        }

        /// <summary>
        /// Gets efficiency multiplier (1 - penalty).
        /// </summary>
        public static float GetEfficiencyMultiplier(in StructureDurability durability)
        {
            return 1f - durability.EfficiencyPenalty;
        }

        /// <summary>
        /// Creates default structure durability.
        /// </summary>
        public static StructureDurability CreateDefault(float maxDurability = 100f)
        {
            return new StructureDurability
            {
                CurrentDurability = maxDurability,
                MaxDurability = maxDurability,
                State = DurabilityState.Pristine,
                DamagedThreshold = 0.5f,
                CriticalThreshold = 0.25f,
                EfficiencyPenalty = 0f,
                NeedsRepair = false
            };
        }

        /// <summary>
        /// Gets repair priority (higher = more urgent).
        /// </summary>
        public static int GetRepairPriority(in StructureDurability durability)
        {
            return durability.State switch
            {
                DurabilityState.Critical => 100,
                DurabilityState.Damaged => 75,
                DurabilityState.Worn => 50,
                DurabilityState.Good => 25,
                _ => 0
            };
        }

        /// <summary>
        /// Checks if durability is below a threshold.
        /// </summary>
        public static bool IsBelowThreshold(in StructureDurability durability, float threshold)
        {
            if (durability.MaxDurability <= 0) return true;
            return (durability.CurrentDurability / durability.MaxDurability) < threshold;
        }
    }
}

