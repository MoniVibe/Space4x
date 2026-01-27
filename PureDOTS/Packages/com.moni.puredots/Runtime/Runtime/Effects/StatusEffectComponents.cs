using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Effects
{
    /// <summary>
    /// Types of status effects that can be applied to entities.
    /// </summary>
    public enum StatusEffectType : byte
    {
        None = 0,
        
        // Damage over time (1-9)
        Poison = 1,
        Bleed = 2,
        Burn = 3,
        Freeze = 4,
        Irradiated = 5,
        Corrode = 6,
        Suffocate = 7,
        
        // Crowd control (10-19)
        Stun = 10,
        Slow = 11,
        Root = 12,
        Silence = 13,
        Blind = 14,
        Disarm = 15,
        Fear = 16,
        Charm = 17,
        Sleep = 18,
        Confuse = 19,
        
        // Buffs (20-29)
        Haste = 20,
        Shield = 21,
        Regen = 22,
        Inspired = 23,
        Empowered = 24,
        Fortified = 25,
        Focused = 26,
        Invisible = 27,
        Blessed = 28,
        
        // Debuffs (30-39)
        Weakness = 30,
        Vulnerability = 31,
        Exhaustion = 32,
        Demoralized = 33,
        Cursed = 34,
        Marked = 35,
        Exposed = 36,
        Fragile = 37,
        
        // Special (40-49)
        Invulnerable = 40,
        Coma = 41,
        MentalBreakdown = 42,
        Berserk = 43,
        Phased = 44,
        Ethereal = 45,
        Petrified = 46,
        
        // Environmental (50-59)
        Wet = 50,
        Chilled = 51,
        Heated = 52,
        Electrified = 53,
        Diseased = 54,
        Intoxicated = 55
    }

    /// <summary>
    /// Determines how effects interact when applied multiple times.
    /// </summary>
    public enum StackBehavior : byte
    {
        Replace,        // New effect replaces old
        Refresh,        // Reset duration, keep stacks
        Stack,          // Add stacks up to max
        StackDuration,  // Extend duration
        Ignore          // Don't apply if already present
    }

    /// <summary>
    /// Category of effect for filtering and immunity checks.
    /// </summary>
    public enum StatusEffectCategory : byte
    {
        None = 0,
        Physical = 1,
        Magical = 2,
        Mental = 3,
        Environmental = 4,
        Divine = 5,
        Curse = 6
    }

    /// <summary>
    /// Active status effect on an entity.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ActiveStatusEffect : IBufferElementData
    {
        public StatusEffectType Type;
        public StatusEffectCategory Category;
        public StackBehavior Behavior;
        public float Duration;           // Remaining duration in seconds (< 0 = permanent)
        public float Value;              // Effect magnitude (damage/heal per tick, slow %, etc.)
        public float TickTimer;          // Time until next tick
        public float TickInterval;       // Time between ticks
        public byte Stacks;              // Current stack count
        public byte MaxStacks;           // Maximum stacks
        public Entity SourceEntity;      // Who applied it
        public uint AppliedTick;
    }

    /// <summary>
    /// Request to apply a status effect to an entity.
    /// </summary>
    public struct ApplyStatusEffectRequest : IComponentData
    {
        public Entity TargetEntity;
        public StatusEffectType Type;
        public StatusEffectCategory Category;
        public StackBehavior Behavior;
        public float Duration;
        public float Value;
        public float TickInterval;
        public byte MaxStacks;
        public Entity SourceEntity;
    }

    /// <summary>
    /// Request to remove a status effect from an entity.
    /// </summary>
    public struct RemoveStatusEffectRequest : IComponentData
    {
        public Entity TargetEntity;
        public StatusEffectType Type;
        public bool RemoveAllStacks;
    }

    /// <summary>
    /// Immunity to specific status effect types.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct StatusEffectImmunity : IBufferElementData
    {
        public StatusEffectType Type;
        public StatusEffectCategory Category;  // If Type is None, immune to entire category
        public float Duration;                 // Remaining duration (< 0 = permanent)
        public uint AppliedTick;
    }

    /// <summary>
    /// Configuration for status effect system.
    /// </summary>
    public struct StatusEffectConfig : IComponentData
    {
        public float DefaultTickInterval;     // Default time between DoT/HoT ticks (1s)
        public byte MaxEffectsPerEntity;      // Prevent effect spam (default 16)
        public float CleanseChance;           // Base chance for cleanse effects
        public bool AllowStackOverflow;       // If true, stacks beyond max refresh duration
    }

    /// <summary>
    /// Event emitted when a status effect is applied.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct StatusEffectAppliedEvent : IBufferElementData
    {
        public StatusEffectType Type;
        public Entity SourceEntity;
        public float Value;
        public byte Stacks;
        public uint Tick;
    }

    /// <summary>
    /// Event emitted when a status effect expires or is removed.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct StatusEffectRemovedEvent : IBufferElementData
    {
        public StatusEffectType Type;
        public bool WasDispelled;
        public bool WasExpired;
        public uint Tick;
    }

    /// <summary>
    /// Event emitted when a status effect ticks (DoT/HoT).
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct StatusEffectTickEvent : IBufferElementData
    {
        public StatusEffectType Type;
        public float Value;
        public byte Stacks;
        public uint Tick;
    }
}

