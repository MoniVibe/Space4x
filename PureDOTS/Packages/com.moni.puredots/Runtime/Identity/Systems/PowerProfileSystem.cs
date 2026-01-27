using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Identity
{
    /// <summary>
    /// Power profile system: might/magic affinity affects doctrine, tactics, and tech/spell preferences.
    /// Provides helper methods for game systems to query power preferences.
    /// </summary>
    [BurstCompile]
    public partial struct PowerProfileSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // Framework system - games implement specific logic
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // This system provides helper methods for other systems
        }

        /// <summary>
        /// Get preference weight for a power type based on might/magic affinity.
        /// </summary>
        /// <param name="affinity">Entity's might/magic affinity</param>
        /// <param name="powerType">Type of power (Might or Magic)</param>
        /// <returns>Preference weight (0..1). Higher = more preferred.</returns>
        [BurstCompile]
        public static float GetPowerPreference(in MightMagicAffinity affinity, PowerType powerType)
        {
            float normalizedAxis = math.clamp(affinity.Axis / 100f, -1f, 1f);

            return powerType switch
            {
                PowerType.Might => (1f - normalizedAxis) * 0.5f + 0.5f, // -100 → 1.0, +100 → 0.0
                PowerType.Magic => (normalizedAxis + 1f) * 0.5f,         // -100 → 0.0, +100 → 1.0
                PowerType.Hybrid => 1f - math.abs(normalizedAxis),       // Middle → 1.0, extremes → 0.0
                _ => 0.5f
            };
        }

        /// <summary>
        /// Check if entity prefers might-based approaches.
        /// </summary>
        [BurstCompile]
        public static bool PrefersMight(in MightMagicAffinity affinity)
        {
            return affinity.Axis < -30f && affinity.Strength > 0.5f;
        }

        /// <summary>
        /// Check if entity prefers magic-based approaches.
        /// </summary>
        [BurstCompile]
        public static bool PrefersMagic(in MightMagicAffinity affinity)
        {
            return affinity.Axis > 30f && affinity.Strength > 0.5f;
        }

        /// <summary>
        /// Check if entity is hybrid (uses both might and magic).
        /// </summary>
        [BurstCompile]
        public static bool IsHybrid(in MightMagicAffinity affinity)
        {
            return math.abs(affinity.Axis) <= 30f || affinity.Strength < 0.5f;
        }

        /// <summary>
        /// Get group power preference from aggregate power profile.
        /// </summary>
        [BurstCompile]
        public static float GetGroupPowerPreference(in AggregatePowerProfile profile, PowerType powerType)
        {
            float normalizedAxis = math.clamp(profile.AvgMightMagicAxis / 100f, -1f, 1f);

            return powerType switch
            {
                PowerType.Might => (1f - normalizedAxis) * 0.5f + 0.5f,
                PowerType.Magic => (normalizedAxis + 1f) * 0.5f,
                PowerType.Hybrid => profile.MagitechBlend, // Use magitech blend for hybrid preference
                _ => 0.5f
            };
        }
    }

    /// <summary>
    /// Power type classification.
    /// </summary>
    public enum PowerType : byte
    {
        Might,   // Physical, tech, brute force
        Magic,   // Mystical, psionic, divine
        Hybrid   // Combination of both
    }
}

