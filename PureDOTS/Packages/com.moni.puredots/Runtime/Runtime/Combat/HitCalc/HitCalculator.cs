using Unity.Burst;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Combat.HitCalc
{
    /// <summary>
    /// Input parameters for hit calculation.
    /// </summary>
    public struct HitCalculationInput
    {
        /// <summary>
        /// Base accuracy (0-1).
        /// </summary>
        public float BaseAccuracy;

        /// <summary>
        /// Attacker bonus to accuracy (additive).
        /// </summary>
        public float AttackerBonus;

        /// <summary>
        /// Defender's dodge rating (0-1).
        /// </summary>
        public float DefenderDodge;

        /// <summary>
        /// Defender's armor value.
        /// </summary>
        public float DefenderArmor;

        /// <summary>
        /// Base damage before reductions.
        /// </summary>
        public float BaseDamage;

        /// <summary>
        /// Type of damage (affects armor effectiveness).
        /// </summary>
        public DamageType DamageType;

        /// <summary>
        /// Critical hit chance (0-1).
        /// </summary>
        public float CritChance;

        /// <summary>
        /// Critical hit damage multiplier.
        /// </summary>
        public float CritMultiplier;

        /// <summary>
        /// Armor penetration (0-1, percentage of armor ignored).
        /// </summary>
        public float ArmorPenetration;
    }

    /// <summary>
    /// Result of hit calculation.
    /// </summary>
    public struct HitCalculationResult
    {
        /// <summary>
        /// Whether the attack hit.
        /// </summary>
        public bool Hit;

        /// <summary>
        /// Whether the hit was critical.
        /// </summary>
        public bool Critical;

        /// <summary>
        /// Final damage after all modifiers.
        /// </summary>
        public float FinalDamage;

        /// <summary>
        /// Damage reduced by armor.
        /// </summary>
        public float DamageReduced;

        /// <summary>
        /// Damage reduced by dodge (grazing hit).
        /// </summary>
        public float DamageDodged;

        /// <summary>
        /// The roll that determined hit (for debugging).
        /// </summary>
        public float HitRoll;

        /// <summary>
        /// The roll that determined crit (for debugging).
        /// </summary>
        public float CritRoll;
    }

    /// <summary>
    /// Armor type for damage reduction calculations.
    /// </summary>
    public enum ArmorType : byte
    {
        /// <summary>
        /// No armor - full damage.
        /// </summary>
        None = 0,

        /// <summary>
        /// Light armor - good vs physical, weak vs magic.
        /// </summary>
        Light = 1,

        /// <summary>
        /// Medium armor - balanced protection.
        /// </summary>
        Medium = 2,

        /// <summary>
        /// Heavy armor - excellent vs physical, weak vs fire.
        /// </summary>
        Heavy = 3,

        /// <summary>
        /// Magic armor - good vs magic, weak vs physical.
        /// </summary>
        Magical = 4
    }

    /// <summary>
    /// Static hit calculation functions.
    /// Pure functions for Burst-compatible combat resolution.
    /// </summary>
    public static class HitCalculator
    {
        /// <summary>
        /// Performs full hit calculation.
        /// </summary>
        public static HitCalculationResult Calculate(in HitCalculationInput input, uint seed)
        {
            var result = new HitCalculationResult
            {
                Hit = false,
                Critical = false,
                FinalDamage = 0f,
                DamageReduced = 0f,
                DamageDodged = 0f
            };

            // Calculate hit chance
            float hitChance = input.BaseAccuracy + input.AttackerBonus - input.DefenderDodge;
            hitChance = math.saturate(hitChance);

            // Roll for hit
            result.HitRoll = GetDeterministicRandom(seed, 0);

            if (result.HitRoll > hitChance)
            {
                // Miss
                return result;
            }

            result.Hit = true;

            // Roll for critical
            float critChance = input.CritChance;
            result.CritRoll = GetDeterministicRandom(seed, 1);

            if (result.CritRoll < critChance)
            {
                result.Critical = true;
            }

            // Calculate base damage
            float damage = input.BaseDamage;

            // Apply critical multiplier
            if (result.Critical)
            {
                float critMult = input.CritMultiplier > 0 ? input.CritMultiplier : 2f;
                damage *= critMult;
            }

            // Calculate effective armor (after penetration)
            float effectiveArmor = input.DefenderArmor * (1f - input.ArmorPenetration);

            // Apply armor reduction
            float armorReduction = CalculateArmorReduction(damage, effectiveArmor, input.DamageType);
            result.DamageReduced = armorReduction;

            // Final damage
            result.FinalDamage = math.max(0f, damage - armorReduction);

            return result;
        }

        /// <summary>
        /// Calculates damage reduction from armor.
        /// </summary>
        public static float CalculateArmorReduction(float damage, float armor, DamageType damageType)
        {
            if (armor <= 0f)
            {
                return 0f;
            }

            // Get armor effectiveness based on damage type
            float effectiveness = GetArmorEffectiveness(damageType);

            // Diminishing returns formula: reduction = armor / (armor + k)
            // where k determines how fast diminishing returns kick in
            float k = 100f; // Higher k = slower diminishing returns
            float reductionPercent = (armor * effectiveness) / (armor * effectiveness + k);

            return damage * reductionPercent;
        }

        /// <summary>
        /// Applies armor reduction with armor type consideration.
        /// </summary>
        public static float ApplyArmorReduction(float damage, float armor, ArmorType armorType, DamageType damageType)
        {
            float typeMultiplier = GetArmorTypeMultiplier(armorType, damageType);
            float effectiveArmor = armor * typeMultiplier;

            return CalculateArmorReduction(damage, effectiveArmor, damageType);
        }

        /// <summary>
        /// Rolls for critical hit.
        /// </summary>
        public static bool RollCritical(float critChance, uint seed)
        {
            float roll = GetDeterministicRandom(seed, 2);
            return roll < critChance;
        }

        /// <summary>
        /// Gets armor effectiveness against damage type.
        /// </summary>
        public static float GetArmorEffectiveness(DamageType damageType)
        {
            return damageType switch
            {
                DamageType.Physical => 1.0f,   // Full effectiveness
                DamageType.Fire => 0.7f,       // Reduced effectiveness
                DamageType.Cold => 0.8f,       // Slightly reduced
                DamageType.Lightning => 0.6f,  // Significantly reduced
                DamageType.Poison => 0.3f,     // Mostly bypasses armor
                DamageType.True => 0.0f,       // Ignores armor
                _ => 1.0f
            };
        }

        /// <summary>
        /// Gets armor type multiplier against damage type.
        /// </summary>
        public static float GetArmorTypeMultiplier(ArmorType armorType, DamageType damageType)
        {
            return (armorType, damageType) switch
            {
                // Light armor
                (ArmorType.Light, DamageType.Physical) => 0.8f,
                (ArmorType.Light, DamageType.Fire) => 0.6f,
                (ArmorType.Light, DamageType.Cold) => 0.7f,
                (ArmorType.Light, DamageType.Lightning) => 0.5f,
                (ArmorType.Light, DamageType.Poison) => 0.4f,

                // Medium armor
                (ArmorType.Medium, DamageType.Physical) => 1.0f,
                (ArmorType.Medium, DamageType.Fire) => 0.8f,
                (ArmorType.Medium, DamageType.Cold) => 0.8f,
                (ArmorType.Medium, DamageType.Lightning) => 0.7f,
                (ArmorType.Medium, DamageType.Poison) => 0.5f,

                // Heavy armor
                (ArmorType.Heavy, DamageType.Physical) => 1.3f,
                (ArmorType.Heavy, DamageType.Fire) => 0.6f,  // Conducts heat
                (ArmorType.Heavy, DamageType.Cold) => 0.9f,
                (ArmorType.Heavy, DamageType.Lightning) => 0.4f, // Conducts electricity
                (ArmorType.Heavy, DamageType.Poison) => 0.6f,

                // Magic armor
                (ArmorType.Magical, DamageType.Physical) => 0.5f,
                (ArmorType.Magical, DamageType.Fire) => 1.2f,
                (ArmorType.Magical, DamageType.Cold) => 1.2f,
                (ArmorType.Magical, DamageType.Lightning) => 1.2f,
                (ArmorType.Magical, DamageType.Poison) => 1.0f,

                // No armor or true damage
                (ArmorType.None, _) => 0f,
                (_, DamageType.True) => 0f,

                _ => 1.0f
            };
        }

        /// <summary>
        /// Calculates grazing hit damage (partial dodge).
        /// </summary>
        public static float CalculateGrazingDamage(float baseDamage, float hitRoll, float hitChance)
        {
            // If hit roll is within 10% of miss threshold, it's a grazing hit
            float grazingThreshold = hitChance - 0.1f;
            if (hitRoll > grazingThreshold && hitRoll <= hitChance)
            {
                // Reduce damage based on how close to miss
                float grazingFactor = (hitChance - hitRoll) / 0.1f;
                return baseDamage * (0.5f + 0.5f * grazingFactor);
            }

            return baseDamage;
        }

        /// <summary>
        /// Deterministic random number generator.
        /// </summary>
        private static float GetDeterministicRandom(uint seed, int offset)
        {
            uint hash = seed * 1103515245 + 12345 + (uint)offset * 31;
            hash = (hash >> 16) ^ hash;
            hash *= 0x85ebca6b;
            hash = (hash >> 13) ^ hash;
            hash *= 0xc2b2ae35;
            hash = (hash >> 16) ^ hash;
            return (hash % 10000) / 10000f;
        }
    }
}

