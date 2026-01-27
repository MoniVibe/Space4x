using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Perception
{
    /// <summary>
    /// Stealth level - categorical stealth states.
    /// Maps to VisibilityState from Detection namespace but kept separate for Perception namespace consistency.
    /// </summary>
    public enum StealthLevel : byte
    {
        Exposed = 0,        // Fully visible (0% stealth bonus)
        Concealed = 1,      // Behind cover, in crowds (+25% stealth bonus)
        Hidden = 2,         // Actively sneaking (+50% stealth bonus)
        Invisible = 3       // Magical/tech invisibility (+75% stealth bonus)
    }

    /// <summary>
    /// Stealth modifiers - environmental and equipment modifiers affecting stealth effectiveness.
    /// </summary>
    public struct StealthModifiers : IComponentData
    {
        /// <summary>Light level modifier (-0.3 to +0.3). Negative = harder to hide in bright light.</summary>
        public float LightModifier;

        /// <summary>Terrain modifier (-0.2 to +0.25). Positive = easier to hide in forests/fog.</summary>
        public float TerrainModifier;

        /// <summary>Movement speed penalty (-0.4 to +0.1). Running = harder to hide.</summary>
        public float MovementModifier;

        /// <summary>Equipment bonus (cloaks, boots, etc.).</summary>
        public float EquipmentModifier;

        /// <summary>Last tick modifiers were updated.</summary>
        public uint LastUpdateTick;

        public static StealthModifiers Default => new StealthModifiers
        {
            LightModifier = 0f,
            TerrainModifier = 0f,
            MovementModifier = 0f,
            EquipmentModifier = 0f,
            LastUpdateTick = 0u
        };
    }

    /// <summary>
    /// Stealth check result - outcome of stealth vs perception check.
    /// </summary>
    public enum StealthCheckResult : byte
    {
        RemainsUndetected = 0,  // Stealth check won, entity remains hidden
        Suspicious = 1,         // Perception check won by <30, guard becomes suspicious
        Spotted = 2,            // Perception check won by 30-60, entity spotted
        Exposed = 3             // Perception check won by >60, identity revealed
    }

    /// <summary>
    /// Stealth profile - aggregates stealth state and current modifiers.
    /// Used for quick access to effective stealth rating.
    /// </summary>
    public struct StealthProfile : IComponentData
    {
        /// <summary>Current stealth level.</summary>
        public StealthLevel Level;

        /// <summary>Base stealth rating (from skills/stats).</summary>
        public float BaseRating;

        /// <summary>Effective stealth rating (base + modifiers).</summary>
        public float EffectiveRating;

        /// <summary>Last tick profile was updated.</summary>
        public uint LastUpdateTick;

        public static StealthProfile Default => new StealthProfile
        {
            Level = StealthLevel.Exposed,
            BaseRating = 0f,
            EffectiveRating = 0f,
            LastUpdateTick = 0u
        };
    }

    /// <summary>
    /// Environmental stealth context - cached environmental modifiers at a position.
    /// Used to avoid recalculating light/terrain for every entity.
    /// </summary>
    public struct EnvironmentalStealthContext : IComponentData
    {
        /// <summary>Light level at position (0-1, 0 = pitch black, 1 = full daylight).</summary>
        public float LightLevel;

        /// <summary>Terrain type (game-specific enum, 0 = open field, etc.).</summary>
        public byte TerrainType;

        /// <summary>Cached stealth modifier from environment.</summary>
        public float CachedModifier;

        /// <summary>Last tick context was updated.</summary>
        public uint LastUpdateTick;

        public static EnvironmentalStealthContext Default => new EnvironmentalStealthContext
        {
            LightLevel = 0.5f,
            TerrainType = 0,
            CachedModifier = 0f,
            LastUpdateTick = 0u
        };
    }
}

