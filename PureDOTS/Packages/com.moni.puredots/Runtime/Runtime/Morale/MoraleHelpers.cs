using Unity.Mathematics;
using Unity.Entities;
using Unity.Collections;
using Unity.Burst;

namespace PureDOTS.Runtime.Morale
{
    /// <summary>
    /// Static helpers for morale calculations.
    /// </summary>
    [BurstCompile]
    public static class MoraleHelpers
    {
        /// <summary>
        /// Default morale configuration.
        /// </summary>
        public static MoraleConfig DefaultConfig => new MoraleConfig
        {
            DespairThreshold = 200f,
            UnhappyThreshold = 400f,
            CheerfulThreshold = 600f,
            ElatedThreshold = 800f,
            MaxMorale = 1000f,
            BreakdownCheckInterval = 600,  // Every 10 seconds at 60 ticks/sec
            BurnoutCheckInterval = 1200,   // Every 20 seconds
            BaseBreakdownChance = 0.1f,
            BaseBurnoutChance = 0.05f,
            ModifierDecayRate = 1f
        };

        /// <summary>
        /// Gets morale band from current morale value.
        /// </summary>
        public static MoraleBand GetBand(float morale, in MoraleConfig config)
        {
            if (morale >= config.ElatedThreshold)
                return MoraleBand.Elated;
            if (morale >= config.CheerfulThreshold)
                return MoraleBand.Cheerful;
            if (morale >= config.UnhappyThreshold)
                return MoraleBand.Stable;
            if (morale >= config.DespairThreshold)
                return MoraleBand.Unhappy;
            return MoraleBand.Despair;
        }

        /// <summary>
        /// Gets work speed modifier for a morale band.
        /// </summary>
        public static float GetWorkSpeedModifier(MoraleBand band)
        {
            return band switch
            {
                MoraleBand.Despair => -0.40f,
                MoraleBand.Unhappy => -0.15f,
                MoraleBand.Stable => 0f,
                MoraleBand.Cheerful => 0.10f,
                MoraleBand.Elated => 0.15f,
                _ => 0f
            };
        }

        /// <summary>
        /// Gets initiative modifier for a morale band.
        /// </summary>
        public static float GetInitiativeModifier(MoraleBand band)
        {
            return band switch
            {
                MoraleBand.Despair => -0.40f,
                MoraleBand.Unhappy => -0.20f,
                MoraleBand.Stable => 0f,
                MoraleBand.Cheerful => 0.10f,
                MoraleBand.Elated => 0.25f,
                _ => 0f
            };
        }

        /// <summary>
        /// Gets combat modifier for a morale band.
        /// </summary>
        public static float GetCombatModifier(MoraleBand band)
        {
            return band switch
            {
                MoraleBand.Despair => -0.30f,
                MoraleBand.Unhappy => -0.15f,
                MoraleBand.Stable => 0f,
                MoraleBand.Cheerful => 0.10f,
                MoraleBand.Elated => 0.20f,
                _ => 0f
            };
        }

        /// <summary>
        /// Gets social modifier for a morale band.
        /// </summary>
        public static float GetSocialModifier(MoraleBand band)
        {
            return band switch
            {
                MoraleBand.Despair => -0.25f,
                MoraleBand.Unhappy => -0.10f,
                MoraleBand.Stable => 0f,
                MoraleBand.Cheerful => 0.10f,
                MoraleBand.Elated => 0.15f,
                _ => 0f
            };
        }

        /// <summary>
        /// Gets breakdown risk percentage for despair band.
        /// </summary>
        public static byte GetBreakdownRisk(float morale, in MoraleConfig config)
        {
            if (morale >= config.DespairThreshold)
                return 0;
            
            // Risk increases as morale approaches 0
            float riskPercent = (1f - morale / config.DespairThreshold) * 100f;
            return (byte)math.clamp(riskPercent, 0, 100);
        }

        /// <summary>
        /// Gets burnout risk percentage for elated band.
        /// </summary>
        public static byte GetBurnoutRisk(float morale, in MoraleConfig config)
        {
            if (morale < config.ElatedThreshold)
                return 0;
            
            // Risk increases as morale approaches max
            float overElated = morale - config.ElatedThreshold;
            float maxOver = config.MaxMorale - config.ElatedThreshold;
            float riskPercent = (overElated / maxOver) * 100f;
            return (byte)math.clamp(riskPercent, 0, 100);
        }

        /// <summary>
        /// Calculates total modifier from all active modifiers.
        /// </summary>
        public static float CalculateTotalModifier(in DynamicBuffer<MoraleModifier> buffer)
        {
            float total = 0;
            for (int i = 0; i < buffer.Length; i++)
            {
                total += buffer[i].Magnitude;
            }
            return total;
        }

        /// <summary>
        /// Calculates total modifier by category.
        /// </summary>
        public static float CalculateModifierByCategory(in DynamicBuffer<MoraleModifier> buffer, MoraleModifierCategory category)
        {
            float total = 0;
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Category == category)
                {
                    total += buffer[i].Magnitude;
                }
            }
            return total;
        }

        /// <summary>
        /// Applies or updates a modifier in the buffer.
        /// </summary>
        public static void ApplyModifier(
            ref DynamicBuffer<MoraleModifier> buffer,
            FixedString32Bytes modifierId,
            MoraleModifierCategory category,
            short magnitude,
            uint durationTicks,
            uint decayHalfLife,
            uint currentTick)
        {
            // Check if modifier already exists
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].ModifierId.Equals(modifierId))
                {
                    // Update existing modifier
                    var mod = buffer[i];
                    mod.Magnitude = magnitude;
                    mod.RemainingTicks = durationTicks;
                    mod.DecayHalfLife = decayHalfLife;
                    mod.AppliedTick = currentTick;
                    buffer[i] = mod;
                    return;
                }
            }

            // Add new modifier
            buffer.Add(new MoraleModifier
            {
                ModifierId = modifierId,
                Category = category,
                Magnitude = magnitude,
                RemainingTicks = durationTicks,
                DecayHalfLife = decayHalfLife,
                AppliedTick = currentTick
            });
        }

        /// <summary>
        /// Removes a modifier by ID.
        /// </summary>
        public static bool RemoveModifier(ref DynamicBuffer<MoraleModifier> buffer, FixedString32Bytes modifierId)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].ModifierId.Equals(modifierId))
                {
                    buffer.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Calculates total memory contribution to morale.
        /// </summary>
        public static float CalculateMemoryContribution(in DynamicBuffer<MoraleMemory> buffer)
        {
            float total = 0;
            for (int i = 0; i < buffer.Length; i++)
            {
                total += buffer[i].CurrentMagnitude;
            }
            return total;
        }

        /// <summary>
        /// Checks if entity should have a breakdown.
        /// </summary>
        public static bool ShouldBreakdown(byte breakdownRisk, uint seed, in MoraleConfig config)
        {
            if (breakdownRisk == 0) return false;
            
            float chance = (breakdownRisk / 100f) * config.BaseBreakdownChance;
            float roll = DeterministicRandom(seed) / (float)uint.MaxValue;
            return roll < chance;
        }

        /// <summary>
        /// Checks if entity should burn out.
        /// </summary>
        public static bool ShouldBurnout(byte burnoutRisk, uint seed, in MoraleConfig config)
        {
            if (burnoutRisk == 0) return false;
            
            float chance = (burnoutRisk / 100f) * config.BaseBurnoutChance;
            float roll = DeterministicRandom(seed) / (float)uint.MaxValue;
            return roll < chance;
        }

        /// <summary>
        /// Updates entity morale modifiers based on band.
        /// </summary>
        public static void UpdateModifiers(ref EntityMorale morale)
        {
            morale.WorkSpeedModifier = GetWorkSpeedModifier(morale.Band);
            morale.InitiativeModifier = GetInitiativeModifier(morale.Band);
            morale.CombatModifier = GetCombatModifier(morale.Band);
            morale.SocialModifier = GetSocialModifier(morale.Band);
        }

        /// <summary>
        /// Creates default entity morale.
        /// </summary>
        public static EntityMorale CreateDefault(float startingMorale = 500f)
        {
            var config = DefaultConfig;
            var band = GetBand(startingMorale, config);
            
            return new EntityMorale
            {
                CurrentMorale = startingMorale,
                Band = band,
                PreviousBand = band,
                WorkSpeedModifier = GetWorkSpeedModifier(band),
                InitiativeModifier = GetInitiativeModifier(band),
                CombatModifier = GetCombatModifier(band),
                SocialModifier = GetSocialModifier(band),
                BreakdownRisk = GetBreakdownRisk(startingMorale, config),
                BurnoutRisk = GetBurnoutRisk(startingMorale, config)
            };
        }

        /// <summary>
        /// Decays a memory magnitude based on half-life.
        /// </summary>
        public static short DecayMemory(short currentMagnitude, uint ticksSinceFormed, uint decayHalfLife)
        {
            if (decayHalfLife == 0) return currentMagnitude; // Permanent
            
            float halfLives = ticksSinceFormed / (float)decayHalfLife;
            float decayFactor = math.pow(0.5f, halfLives);
            return (short)(currentMagnitude * decayFactor);
        }

        /// <summary>
        /// Simple deterministic random for breakdown/burnout checks.
        /// </summary>
        private static uint DeterministicRandom(uint seed)
        {
            seed ^= seed << 13;
            seed ^= seed >> 17;
            seed ^= seed << 5;
            return seed;
        }

        /// <summary>
        /// Gets a description of the morale band.
        /// </summary>
        public static FixedString32Bytes GetBandDescription(MoraleBand band)
        {
            return band switch
            {
                MoraleBand.Despair => new FixedString32Bytes("Despairing"),
                MoraleBand.Unhappy => new FixedString32Bytes("Unhappy"),
                MoraleBand.Stable => new FixedString32Bytes("Stable"),
                MoraleBand.Cheerful => new FixedString32Bytes("Cheerful"),
                MoraleBand.Elated => new FixedString32Bytes("Elated"),
                _ => new FixedString32Bytes("Unknown")
            };
        }
    }
}

