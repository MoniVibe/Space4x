using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Buffs
{
    /// <summary>
    /// Catalog of buff/debuff definitions baked from authoring.
    /// Shared between Godgame and Space4X for timed status effects.
    /// </summary>
    public struct BuffDefinitionBlob
    {
        public BlobArray<BuffEntry> Buffs;
    }

    /// <summary>
    /// Individual buff/debuff definition.
    /// </summary>
    public struct BuffEntry
    {
        /// <summary>
        /// Unique buff identifier (matches SpellEffect.BuffId).
        /// </summary>
        public FixedString64Bytes BuffId;

        /// <summary>
        /// Display name for UI.
        /// </summary>
        public FixedString64Bytes DisplayName;

        /// <summary>
        /// Category of buff (Buff, Debuff, Aura).
        /// </summary>
        public BuffCategory Category;

        /// <summary>
        /// How multiple instances of this buff stack.
        /// </summary>
        public StackBehavior Stacking;

        /// <summary>
        /// Maximum number of stacks allowed (1 = no stacking).
        /// </summary>
        public byte MaxStacks;

        /// <summary>
        /// Base duration in seconds (0 = permanent until dispelled).
        /// </summary>
        public float BaseDuration;

        /// <summary>
        /// Interval between periodic effect ticks in seconds (0 = no periodic effects).
        /// </summary>
        public float TickInterval;

        /// <summary>
        /// Stat modifiers applied by this buff.
        /// </summary>
        public BlobArray<BuffStatModifier> StatModifiers;

        /// <summary>
        /// Periodic effects that trigger at TickInterval.
        /// </summary>
        public BlobArray<BuffPeriodicEffect> PeriodicEffects;
    }

    /// <summary>
    /// Category of buff effect.
    /// </summary>
    public enum BuffCategory : byte
    {
        Buff = 0,      // Positive effect
        Debuff = 1,   // Negative effect
        Aura = 2       // Environmental effect (can affect multiple entities)
    }

    /// <summary>
    /// How multiple instances of the same buff stack.
    /// </summary>
    public enum StackBehavior : byte
    {
        Additive = 0,        // Stacks add together (e.g., +10 damage per stack)
        Multiplicative = 1,  // Stacks multiply (e.g., 1.1x damage per stack)
        Refresh = 2,         // New application refreshes duration, doesn't add stacks
        Replace = 3          // New application replaces old instance
    }

    /// <summary>
    /// Stat modifier applied by a buff.
    /// </summary>
    public struct BuffStatModifier
    {
        /// <summary>
        /// Which stat is modified.
        /// </summary>
        public StatTarget Stat;

        /// <summary>
        /// How the modifier is applied.
        /// </summary>
        public ModifierType Type;

        /// <summary>
        /// Modifier value (flat amount or percentage multiplier).
        /// </summary>
        public float Value;
    }

    /// <summary>
    /// Target stat for buff modifiers.
    /// </summary>
    public enum StatTarget : byte
    {
        // Combat stats
        Damage = 0,
        AttackSpeed = 1,
        Armor = 2,
        Health = 3,
        MaxHealth = 4,
        HealthRegen = 5,
        Mana = 6,
        MaxMana = 7,
        ManaRegen = 8,
        Stamina = 9,
        MaxStamina = 10,
        StaminaRegen = 11,

        // Movement stats
        Speed = 20,
        JumpHeight = 21,

        // Skill/Attribute stats
        SkillGainRate = 30,
        XPGainRate = 31,

        // Space4X specific
        PowerGeneration = 40,
        PowerDraw = 41,
        MiningRate = 42,
        RepairRate = 43,
        FireRate = 44,
        Accuracy = 45,

        // Godgame specific
        Mood = 50,
        Faith = 51,
        WorshipRate = 52
    }

    /// <summary>
    /// How a stat modifier is applied.
    /// </summary>
    public enum ModifierType : byte
    {
        Flat = 0,        // Add/subtract flat value (e.g., +10 damage)
        Percent = 1,     // Multiply by percentage (e.g., 1.2x = +20%)
        Override = 2     // Replace stat value entirely (rare)
    }

    /// <summary>
    /// Periodic effect that triggers at regular intervals while buff is active.
    /// </summary>
    public struct BuffPeriodicEffect
    {
        /// <summary>
        /// Type of periodic effect.
        /// </summary>
        public PeriodicEffectType Type;

        /// <summary>
        /// Value of the effect (damage amount, heal amount, etc.).
        /// </summary>
        public float Value;
    }

    /// <summary>
    /// Type of periodic effect.
    /// </summary>
    public enum PeriodicEffectType : byte
    {
        Damage = 0,      // Deal damage over time
        Heal = 1,        // Heal over time
        Mana = 2,        // Restore mana over time
        Stamina = 3,     // Restore stamina over time
        ResourceGrant = 4 // Grant resources over time
    }

    /// <summary>
    /// Singleton reference to buff catalog blob.
    /// </summary>
    public struct BuffCatalogRef : IComponentData
    {
        public BlobAssetReference<BuffDefinitionBlob> Blob;
    }
}

