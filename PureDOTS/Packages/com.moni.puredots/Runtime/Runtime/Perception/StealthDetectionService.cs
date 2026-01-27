using Unity.Burst;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace PureDOTS.Runtime.Perception
{
    /// <summary>
    /// Service for stealth detection calculations.
    /// Burst-compatible static methods for stealth vs perception checks.
    /// </summary>
    [BurstCompile]
    public static class StealthDetectionService
    {
        /// <summary>
        /// Calculate stealth level bonus multiplier (0%, 25%, 50%, 75%).
        /// </summary>
        public static float GetStealthLevelBonus(StealthLevel level)
        {
            return level switch
            {
                StealthLevel.Exposed => 0f,
                StealthLevel.Concealed => 0.25f,
                StealthLevel.Hidden => 0.5f,
                StealthLevel.Invisible => 0.75f,
                _ => 0f
            };
        }

        /// <summary>
        /// Calculate effective stealth rating with all modifiers applied.
        /// </summary>
        /// <param name="baseStealth">Base stealth rating (0-100).</param>
        /// <param name="level">Current stealth level.</param>
        /// <param name="modifiers">Environmental and equipment modifiers.</param>
        /// <param name="movementSpeed">Current movement speed (m/s).</param>
        /// <param name="lightLevel">Light level at position (0-1, 0 = dark, 1 = bright).</param>
        /// <param name="terrainBonus">Terrain bonus modifier.</param>
        /// <returns>Effective stealth rating (0-100+).</returns>
        [BurstCompile]
        public static float CalculateEffectiveStealth(
            float baseStealth,
            StealthLevel level,
            in StealthModifiers modifiers,
            float movementSpeed,
            float lightLevel,
            float terrainBonus)
        {
            // Base stealth rating
            float effective = baseStealth;

            // Stealth level bonus (0%, 25%, 50%, 75%)
            float levelBonus = GetStealthLevelBonus(level);
            effective += baseStealth * levelBonus;

            // Light modifier (bright light = harder to hide)
            // Light level 0 (dark) = +0.3 bonus, light level 1 (bright) = -0.3 penalty
            float lightMod = modifiers.LightModifier;
            if (math.abs(lightMod) < 0.001f)
            {
                // Calculate from light level if modifier not set
                lightMod = (0.5f - lightLevel) * 0.6f; // -0.3 to +0.3 range
            }
            effective += baseStealth * lightMod;

            // Terrain modifier
            float terrainMod = modifiers.TerrainModifier;
            if (math.abs(terrainMod) < 0.001f)
            {
                terrainMod = terrainBonus;
            }
            effective += baseStealth * terrainMod;

            // Movement penalty (running = harder to hide)
            float movementMod = modifiers.MovementModifier;
            if (math.abs(movementMod) < 0.001f)
            {
                // Calculate from movement speed
                // Stationary (0 m/s) = +0.1, Walking (2 m/s) = -0.15, Running (5+ m/s) = -0.4
                if (movementSpeed < 0.1f)
                {
                    movementMod = 0.1f; // Stationary bonus
                }
                else if (movementSpeed < 2f)
                {
                    movementMod = 0f; // Sneaking, no penalty
                }
                else if (movementSpeed < 5f)
                {
                    movementMod = -0.15f; // Walking penalty
                }
                else
                {
                    movementMod = -0.4f; // Running penalty
                }
            }
            effective += baseStealth * movementMod;

            // Equipment bonus
            effective += modifiers.EquipmentModifier;

            // Clamp to reasonable range (0-150, allowing for bonuses)
            return math.max(0f, effective);
        }

        /// <summary>
        /// Get environmental modifiers for a position.
        /// </summary>
        /// <param name="lightLevel">Light level at position (0-1).</param>
        /// <param name="terrainType">Terrain type (game-specific enum).</param>
        /// <param name="movementSpeed">Current movement speed (m/s).</param>
        /// <returns>StealthModifiers with calculated values.</returns>
        [BurstCompile]
        public static void GetEnvironmentalModifiers(
            float lightLevel,
            byte terrainType,
            float movementSpeed,
            out StealthModifiers modifiers)
        {
            modifiers = StealthModifiers.Default;

            // Light modifier: bright light = harder to hide
            // Light level 0 (dark) = +0.3, light level 1 (bright) = -0.3
            modifiers.LightModifier = (0.5f - lightLevel) * 0.6f;

            // Terrain modifier based on terrain type
            // 0 = open field (-0.2), 1 = urban (+0.0), 2 = forest (+0.2), 3 = fog (+0.25)
            modifiers.TerrainModifier = terrainType switch
            {
                0 => -0.2f,  // Open field
                1 => 0f,     // Urban street
                2 => 0.2f,   // Forest/woods
                3 => 0.25f,  // Dense fog
                _ => 0f
            };

            // Movement modifier
            if (movementSpeed < 0.1f)
            {
                modifiers.MovementModifier = 0.1f; // Stationary bonus
            }
            else if (movementSpeed < 2f)
            {
                modifiers.MovementModifier = 0f; // Sneaking
            }
            else if (movementSpeed < 5f)
            {
                modifiers.MovementModifier = -0.15f; // Walking penalty
            }
            else
            {
                modifiers.MovementModifier = -0.4f; // Running penalty
            }

        }

        /// <summary>
        /// Roll stealth check vs perception check.
        /// </summary>
        /// <param name="stealthRating">Effective stealth rating (0-100+).</param>
        /// <param name="perceptionRating">Perception rating (0-100+).</param>
        /// <param name="alertnessModifier">Alertness modifier (0-1, higher = more alert).</param>
        /// <param name="randomSeed">Random seed index for deterministic checks (will be normalized via CreateFromIndex).</param>
        /// <returns>StealthCheckResult indicating outcome.</returns>
        [BurstCompile]
        public static StealthCheckResult RollStealthCheck(
            float stealthRating,
            float perceptionRating,
            float alertnessModifier,
            uint randomSeed)
        {
            // Use CreateFromIndex to ensure proper seed normalization (odd, non-zero)
            // This prevents crashes in deterministic builds when seed is zero or even
            var rng = Random.CreateFromIndex(randomSeed);

            // Roll d100 for stealth check
            float stealthRoll = rng.NextFloat(0f, 100f);
            float stealthCheck = stealthRoll + stealthRating;

            // Roll d100 for perception check
            float perceptionRoll = rng.NextFloat(0f, 100f);
            float perceptionCheck = perceptionRoll + perceptionRating + (alertnessModifier * 20f); // Alertness adds up to +20

            // Calculate margin
            float margin = perceptionCheck - stealthCheck;

            // Determine result based on margin
            if (margin < 0f)
            {
                // Stealth wins
                return StealthCheckResult.RemainsUndetected;
            }
            else if (margin < 30f)
            {
                // Perception wins by <30: suspicious
                return StealthCheckResult.Suspicious;
            }
            else if (margin < 60f)
            {
                // Perception wins by 30-60: spotted
                return StealthCheckResult.Spotted;
            }
            else
            {
                // Perception wins by >60: exposed
                return StealthCheckResult.Exposed;
            }
        }

        /// <summary>
        /// Calculate confidence penalty based on stealth check result.
        /// Used to modify PerceivedEntity.Confidence when target is stealthed.
        /// </summary>
        /// <param name="baseConfidence">Base detection confidence (0-1).</param>
        /// <param name="checkResult">Result of stealth check.</param>
        /// <returns>Modified confidence (0-1).</returns>
        [BurstCompile]
        public static float ApplyStealthConfidencePenalty(float baseConfidence, StealthCheckResult checkResult)
        {
            return checkResult switch
            {
                StealthCheckResult.RemainsUndetected => 0f,           // Not detected
                StealthCheckResult.Suspicious => baseConfidence * 0.3f, // Low confidence (something there)
                StealthCheckResult.Spotted => baseConfidence * 0.7f,   // Medium confidence (spotted but uncertain)
                StealthCheckResult.Exposed => baseConfidence,          // Full confidence (exposed)
                _ => baseConfidence
            };
        }
    }
}

