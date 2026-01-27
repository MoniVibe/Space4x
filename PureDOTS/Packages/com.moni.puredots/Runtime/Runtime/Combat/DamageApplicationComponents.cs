using Unity.Entities;

namespace PureDOTS.Runtime.Combat
{
    /// <summary>
    /// Damage application result (HOT path).
    /// Apply damage/result that's already chosen.
    /// Subtract HP, apply armor/shield modifiers, check death.
    /// Light cooldown countdowns.
    /// </summary>
    public struct DamageApplication : IComponentData
    {
        /// <summary>
        /// Damage amount to apply (already calculated).
        /// </summary>
        public float DamageAmount;

        /// <summary>
        /// Source entity that dealt damage.
        /// </summary>
        public Entity SourceEntity;

        /// <summary>
        /// Damage type flags.
        /// </summary>
        public DamageTypeFlags DamageType;

        /// <summary>
        /// Tick when damage should be applied.
        /// </summary>
        public uint ApplyTick;
    }

    /// <summary>
    /// Damage type flags.
    /// </summary>
    [System.Flags]
    public enum DamageTypeFlags : byte
    {
        None = 0,
        Physical = 1 << 0,
        Fire = 1 << 1,
        Cold = 1 << 2,
        Poison = 1 << 3,
        Magic = 1 << 4
    }

    /// <summary>
    /// Health state snapshot for hot path reads.
    /// </summary>
    public struct HealthSnapshot : IComponentData
    {
        /// <summary>
        /// Current HP.
        /// </summary>
        public float CurrentHP;

        /// <summary>
        /// Maximum HP.
        /// </summary>
        public float MaxHP;

        /// <summary>
        /// Whether entity is dead (1) or alive (0).
        /// </summary>
        public byte IsDead;

        /// <summary>
        /// Tick when this snapshot was last updated.
        /// </summary>
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Cooldown state for abilities/actions (HOT path).
    /// Light cooldown countdowns.
    /// </summary>
    public struct CooldownState : IComponentData
    {
        /// <summary>
        /// Remaining cooldown ticks.
        /// </summary>
        public byte RemainingTicks;

        /// <summary>
        /// Whether ability is ready (1) or on cooldown (0).
        /// </summary>
        public byte IsReady;
    }
}

