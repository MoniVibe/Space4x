using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Combat
{
    /// <summary>
    /// Damage event buffer element - queued damage to be applied.
    /// Processed by DamageApplicationSystem.
    /// </summary>
    public struct DamageEvent : IBufferElementData
    {
        /// <summary>
        /// Entity that caused the damage (attacker, projectile source, etc.).
        /// </summary>
        public Entity SourceEntity;

        /// <summary>
        /// Entity receiving the damage.
        /// </summary>
        public Entity TargetEntity;

        /// <summary>
        /// Raw damage amount before reductions.
        /// </summary>
        public float RawDamage;

        /// <summary>
        /// Type of damage (affects resistances, armor effectiveness).
        /// </summary>
        public DamageType Type;

        /// <summary>
        /// Tick when damage was calculated (for determinism).
        /// </summary>
        public uint Tick;

        /// <summary>
        /// Flags for special damage properties.
        /// </summary>
        public DamageFlags Flags;
    }

    /// <summary>
    /// Damage type enum - affects how damage is reduced.
    /// </summary>
    public enum DamageType : byte
    {
        Physical = 0,
        Fire = 1,
        Cold = 2,
        Lightning = 3,
        Poison = 4,
        True = 5  // Bypasses all reductions
    }

    /// <summary>
    /// Damage flags for special properties.
    /// </summary>
    [System.Flags]
    public enum DamageFlags : byte
    {
        None = 0,
        Critical = 1 << 0,      // Critical hit (multiplier applied)
        Pierce = 1 << 1,        // Ignores armor
        IgnoreShield = 1 << 2,  // Bypasses shields
        DOT = 1 << 3,           // Damage over time (from buff/debuff)
        Lethal = 1 << 4,        // Cannot be reduced below lethal threshold
        AoE = 1 << 5,           // Area of effect damage
        Chain = 1 << 6          // Chain/splash damage
    }

    /// <summary>
    /// Heal event buffer element - queued healing to be applied.
    /// </summary>
    public struct HealEvent : IBufferElementData
    {
        /// <summary>
        /// Entity that caused the healing (spell caster, medic, etc.).
        /// </summary>
        public Entity SourceEntity;

        /// <summary>
        /// Entity receiving the healing.
        /// </summary>
        public Entity TargetEntity;

        /// <summary>
        /// Amount of healing to apply.
        /// </summary>
        public float Amount;

        /// <summary>
        /// Tick when healing was calculated (for determinism).
        /// </summary>
        public uint Tick;
    }

    /// <summary>
    /// Death event - emitted when an entity dies.
    /// Used for presentation, cleanup, and game logic.
    /// </summary>
    public struct DeathEvent : IBufferElementData
    {
        /// <summary>
        /// Entity that died.
        /// </summary>
        public Entity DeadEntity;

        /// <summary>
        /// Entity that killed it (Entity.Null if environmental/death from time).
        /// </summary>
        public Entity KillerEntity;

        /// <summary>
        /// Type of damage that caused death.
        /// </summary>
        public DamageType KillingBlowType;

        /// <summary>
        /// Tick when death occurred.
        /// </summary>
        public uint DeathTick;
    }
}

