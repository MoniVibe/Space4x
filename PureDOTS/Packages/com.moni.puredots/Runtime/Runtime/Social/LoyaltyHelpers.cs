using Unity.Mathematics;
using Unity.Entities;
using Unity.Burst;

namespace PureDOTS.Runtime.Social
{
    /// <summary>
    /// Static helpers for loyalty calculations.
    /// </summary>
    [BurstCompile]
    public static class LoyaltyHelpers
    {
        /// <summary>
        /// Default loyalty configuration.
        /// </summary>
        public static LoyaltyConfig DefaultConfig => new LoyaltyConfig
        {
            BaseDesertionThreshold = 30f,
            MutinyThreshold = 25f,
            FanaticThreshold = 80f,
            LoyaltyDecayRate = 0.1f,
            HardshipPenalty = 5f,
            VictoryBonus = 10f,
            TicksPerDay = 86400
        };

        /// <summary>
        /// Gets loyalty state from value.
        /// </summary>
        public static LoyaltyState GetState(byte loyalty)
        {
            if (loyalty < 20) return LoyaltyState.Traitor;
            if (loyalty < 40) return LoyaltyState.Disloyal;
            if (loyalty < 60) return LoyaltyState.Neutral;
            if (loyalty < 80) return LoyaltyState.Loyal;
            return LoyaltyState.Fanatic;
        }

        /// <summary>
        /// Gets desertion risk based on loyalty.
        /// </summary>
        public static float GetDesertionRisk(byte loyalty, in LoyaltyConfig config)
        {
            if (loyalty >= config.BaseDesertionThreshold)
                return 0f;
            
            // Risk increases as loyalty decreases below threshold
            float risk = (config.BaseDesertionThreshold - loyalty) / config.BaseDesertionThreshold;
            return math.clamp(risk, 0f, 1f);
        }

        /// <summary>
        /// Gets morale bonus from loyalty.
        /// </summary>
        public static float GetMoraleBonus(byte loyalty)
        {
            // -20% at 0 loyalty, +20% at 100 loyalty
            return (loyalty - 50f) / 250f;
        }

        /// <summary>
        /// Checks if entity will desert.
        /// </summary>
        public static bool WillDesert(byte loyalty, float hardshipLevel, uint seed, in LoyaltyConfig config)
        {
            float baseRisk = GetDesertionRisk(loyalty, config);
            if (baseRisk <= 0f) return false;
            
            // Hardship increases desertion chance
            float adjustedRisk = baseRisk * (1f + hardshipLevel);
            
            // Random check
            float roll = DeterministicRandom(seed) / (float)uint.MaxValue;
            return roll < adjustedRisk;
        }

        /// <summary>
        /// Checks if group will mutiny.
        /// </summary>
        public static bool WillMutiny(float averageLoyalty, uint seed, in LoyaltyConfig config)
        {
            if (averageLoyalty >= config.MutinyThreshold)
                return false;
            
            // Mutiny chance based on how far below threshold
            float mutinyChance = (config.MutinyThreshold - averageLoyalty) / config.MutinyThreshold * 0.1f;
            
            float roll = DeterministicRandom(seed) / (float)uint.MaxValue;
            return roll < mutinyChance;
        }

        /// <summary>
        /// Applies hardship to loyalty.
        /// </summary>
        public static void ApplyHardship(ref EntityLoyalty loyalty, float severity, in LoyaltyConfig config)
        {
            float penalty = config.HardshipPenalty * severity;
            loyalty.Loyalty = (byte)math.max(0, loyalty.Loyalty - (int)penalty);
            loyalty.State = GetState(loyalty.Loyalty);
            loyalty.DesertionRisk = GetDesertionRisk(loyalty.Loyalty, config);
        }

        /// <summary>
        /// Applies victory to loyalty.
        /// </summary>
        public static void ApplyVictory(ref EntityLoyalty loyalty, float magnitude, in LoyaltyConfig config)
        {
            float bonus = config.VictoryBonus * magnitude;
            loyalty.Loyalty = (byte)math.min(100, loyalty.Loyalty + (int)bonus);
            loyalty.State = GetState(loyalty.Loyalty);
            loyalty.DesertionRisk = GetDesertionRisk(loyalty.Loyalty, config);
        }

        /// <summary>
        /// Applies loyalty event.
        /// </summary>
        public static void ApplyLoyaltyEvent(ref EntityLoyalty loyalty, LoyaltyEventType eventType, sbyte magnitude, in LoyaltyConfig config)
        {
            int change = eventType switch
            {
                LoyaltyEventType.Victory => (int)(magnitude * 0.5f),
                LoyaltyEventType.Defeat => -(int)(math.abs(magnitude) * 0.3f),
                LoyaltyEventType.Hardship => -(int)(math.abs(magnitude) * 0.4f),
                LoyaltyEventType.Betrayal => -(int)(math.abs(magnitude) * 0.8f),
                LoyaltyEventType.Miracle => (int)(magnitude * 0.6f),
                LoyaltyEventType.Reward => (int)(magnitude * 0.4f),
                LoyaltyEventType.Punishment => -(int)(math.abs(magnitude) * 0.5f),
                LoyaltyEventType.Propaganda => -(int)(math.abs(magnitude) * 0.2f),
                LoyaltyEventType.Inspiration => (int)(magnitude * 0.3f),
                _ => 0
            };
            
            loyalty.Loyalty = (byte)math.clamp(loyalty.Loyalty + change, 0, 100);
            loyalty.State = GetState(loyalty.Loyalty);
            loyalty.DesertionRisk = GetDesertionRisk(loyalty.Loyalty, config);
        }

        /// <summary>
        /// Calculates loyalty modifiers.
        /// </summary>
        public static LoyaltyModifiers CalculateModifiers(byte loyalty, in LoyaltyConfig config)
        {
            var state = GetState(loyalty);
            
            return new LoyaltyModifiers
            {
                MoraleBonus = GetMoraleBonus(loyalty),
                SacrificeWillingness = state >= LoyaltyState.Loyal ? (loyalty - 60f) / 40f : 0f,
                BribeResistance = loyalty / 100f,
                PropagandaResistance = loyalty / 100f,
                ConscriptionCap = state >= LoyaltyState.Neutral ? 1f : 0.5f
            };
        }

        /// <summary>
        /// Gets fanatic bonuses.
        /// </summary>
        public static void GetFanaticBonuses(byte loyalty, in LoyaltyConfig config, out float damageResistance, out float moraleBonus)
        {
            damageResistance = 0f;
            moraleBonus = 0f;
            
            if (loyalty >= config.FanaticThreshold)
            {
                float fanaticLevel = (loyalty - config.FanaticThreshold) / (100f - config.FanaticThreshold);
                damageResistance = fanaticLevel * 0.2f;  // Up to 20% damage resistance
                moraleBonus = fanaticLevel * 0.3f;       // Up to 30% morale bonus
            }
        }

        /// <summary>
        /// Applies natural decay to loyalty.
        /// </summary>
        public static byte ApplyDecay(byte currentLoyalty, byte naturalLoyalty, uint ticksElapsed, in LoyaltyConfig config)
        {
            float daysElapsed = ticksElapsed / (float)config.TicksPerDay;
            float decay = daysElapsed * config.LoyaltyDecayRate;
            
            // Decay toward natural loyalty
            if (currentLoyalty > naturalLoyalty)
            {
                return (byte)math.max(naturalLoyalty, currentLoyalty - (int)decay);
            }
            else if (currentLoyalty < naturalLoyalty)
            {
                // Slowly recover toward natural loyalty
                return (byte)math.min(naturalLoyalty, currentLoyalty + (int)(decay * 0.5f));
            }
            
            return currentLoyalty;
        }

        /// <summary>
        /// Creates default entity loyalty.
        /// </summary>
        public static EntityLoyalty CreateDefault(Entity target = default, LoyaltyTarget targetType = LoyaltyTarget.None, byte startingLoyalty = 50)
        {
            var config = DefaultConfig;
            
            return new EntityLoyalty
            {
                PrimaryTarget = target,
                TargetType = targetType,
                Loyalty = startingLoyalty,
                State = GetState(startingLoyalty),
                NaturalLoyalty = startingLoyalty,
                DesertionRisk = GetDesertionRisk(startingLoyalty, config)
            };
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

