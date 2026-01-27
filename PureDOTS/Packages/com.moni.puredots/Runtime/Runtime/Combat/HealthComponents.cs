using Unity.Entities;

namespace PureDOTS.Runtime.Combat
{
    /// <summary>
    /// Health component - tracks current and maximum health.
    /// Used for both Godgame (villagers, creatures) and Space4X (hull points).
    /// </summary>
    public struct Health : IComponentData
    {
        /// <summary>
        /// Current health points. 0 = dead.
        /// </summary>
        public float Current;

        /// <summary>
        /// Maximum health points (base + modifiers).
        /// </summary>
        public float Max;

        /// <summary>
        /// Convenience alias for maximum health to match call sites.
        /// </summary>
        public float MaxHealth
        {
            get => Max;
            set => Max = value;
        }

        /// <summary>
        /// Health regeneration per second (0 = no regen).
        /// </summary>
        public float RegenRate;

        /// <summary>
        /// Tick when last damage was taken (for regen delay).
        /// </summary>
        public uint LastDamageTick;
    }

    /// <summary>
    /// Shield component - tracks shield points that absorb damage before health.
    /// Primarily for Space4X, but can be used for magic shields in Godgame.
    /// </summary>
    public struct Shield : IComponentData
    {
        /// <summary>
        /// Current shield points.
        /// </summary>
        public float Current;

        /// <summary>
        /// Maximum shield points.
        /// </summary>
        public float Max;

        /// <summary>
        /// Shield recharge rate per second (0 = no recharge).
        /// </summary>
        public float RechargeRate;

        /// <summary>
        /// Delay in seconds before recharge starts after taking damage.
        /// </summary>
        public float RechargeDelay;

        /// <summary>
        /// Tick when last damage was taken (for recharge delay).
        /// </summary>
        public uint LastDamageTick;
    }

    /// <summary>
    /// Death state component - marks an entity as dead.
    /// </summary>
    public struct DeathState : IComponentData
    {
        /// <summary>
        /// Whether the entity is dead.
        /// </summary>
        public bool IsDead;

        /// <summary>
        /// Tick when death occurred.
        /// </summary>
        public uint DeathTick;

        /// <summary>
        /// Entity that killed this entity (Entity.Null if environmental/death from time).
        /// </summary>
        public Entity KillerEntity;

        /// <summary>
        /// Type of damage that caused death.
        /// </summary>
        public DamageType KillingBlowType;
    }

    /// <summary>
    /// Armor component - reduces incoming physical damage.
    /// Can be attached to entities or equipment.
    /// </summary>
    public struct ArmorValue : IComponentData
    {
        /// <summary>
        /// Armor value (flat reduction or percentage, depending on system).
        /// </summary>
        public float Value;

        /// <summary>
        /// Armor type (affects effectiveness vs different damage types).
        /// </summary>
        public ArmorType Type;
    }

    /// <summary>
    /// Armor type enum - affects damage reduction effectiveness.
    /// </summary>
    public enum ArmorType : byte
    {
        None = 0,
        Light = 1,    // Cloth, leather
        Medium = 2,   // Chainmail, composite
        Heavy = 3,    // Plate, powered armor
        Magical = 4   // Energy shields, wards
    }

    /// <summary>
    /// Resistance component - percentage reduction vs specific damage types.
    /// </summary>
    public struct Resistance : IComponentData
    {
        /// <summary>
        /// Resistance to physical damage (0-1, where 1 = 100% reduction).
        /// </summary>
        public float Physical;

        /// <summary>
        /// Resistance to fire damage (0-1).
        /// </summary>
        public float Fire;

        /// <summary>
        /// Resistance to cold damage (0-1).
        /// </summary>
        public float Cold;

        /// <summary>
        /// Resistance to lightning damage (0-1).
        /// </summary>
        public float Lightning;

        /// <summary>
        /// Resistance to poison damage (0-1).
        /// </summary>
        public float Poison;
    }
}

