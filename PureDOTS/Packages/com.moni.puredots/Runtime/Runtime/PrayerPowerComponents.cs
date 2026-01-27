using Unity.Entities;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Singleton component tracking prayer power/mana pool.
    /// Regenerated from worship, buildings, and natural sources.
    /// </summary>
    public struct PrayerPower : IComponentData
    {
        public float CurrentMana;
        public float MaxMana;
        public float RegenRate; // Mana per second
        public float LastRegenTick; // For deterministic regeneration
    }

    /// <summary>
    /// Component marking entities that generate prayer power (shrines, worshippers, etc.).
    /// </summary>
    public struct PrayerPowerSource : IComponentData
    {
        public float GenerationRate; // Mana per second generated
        public float Range; // Radius of influence
        public bool IsActive; // Can be disabled (e.g., shrine destroyed)
    }

    /// <summary>
    /// Component marking entities that consume prayer power (miracles, buildings, etc.).
    /// </summary>
    public struct PrayerPowerConsumer : IComponentData
    {
        public float ConsumptionRate; // Mana per second consumed (for sustained effects)
        public float OneTimeCost; // Mana cost for instant effects
        public bool RequiresPower; // If false, can operate without mana (for testing)
    }
}

