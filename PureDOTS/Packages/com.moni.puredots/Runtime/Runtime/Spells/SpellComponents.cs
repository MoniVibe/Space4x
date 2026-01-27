using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Spells
{
    /// <summary>
    /// Buffer of spells an entity has learned.
    /// </summary>
    public struct LearnedSpell : IBufferElementData
    {
        /// <summary>
        /// Spell identifier from catalog.
        /// </summary>
        public FixedString64Bytes SpellId;

        /// <summary>
        /// Proficiency level (0-255).
        /// Higher proficiency = faster cast, lower cost, stronger effects.
        /// </summary>
        public byte MasteryLevel;

        /// <summary>
        /// Number of times cast (for statistics/progression).
        /// </summary>
        public uint TimesCast;

        /// <summary>
        /// Tick when spell was learned.
        /// </summary>
        public uint LearnedTick;

        /// <summary>
        /// Entity that taught this spell (Entity.Null if self-learned).
        /// </summary>
        public Entity TeacherEntity;
    }

    /// <summary>
    /// Current spell casting state for an entity.
    /// </summary>
    public struct SpellCastState : IComponentData
    {
        /// <summary>
        /// Currently casting/active spell (empty if idle).
        /// </summary>
        public FixedString64Bytes ActiveSpellId;

        /// <summary>
        /// Target entity for single-target spells.
        /// </summary>
        public Entity TargetEntity;

        /// <summary>
        /// Target position for ground/area spells.
        /// </summary>
        public float3 TargetPosition;

        /// <summary>
        /// Cast progress (0-1).
        /// </summary>
        public float CastProgress;

        /// <summary>
        /// Charge level for charged spells (0-1).
        /// </summary>
        public float ChargeLevel;

        /// <summary>
        /// Current phase of casting.
        /// </summary>
        public SpellCastPhase Phase;

        /// <summary>
        /// Tick when cast started.
        /// </summary>
        public uint CastStartTick;

        /// <summary>
        /// Flags for cast state.
        /// </summary>
        public SpellCastFlags Flags;
    }

    /// <summary>
    /// Phase of spell casting.
    /// </summary>
    public enum SpellCastPhase : byte
    {
        Idle = 0,
        Preparing = 1,    // Starting cast animation
        Casting = 2,      // Active cast in progress
        Channeling = 3,   // Maintaining channeled spell
        Releasing = 4,    // Releasing charged spell
        Cooldown = 5      // Post-cast cooldown
    }

    /// <summary>
    /// Flags for spell casting state.
    /// </summary>
    [System.Flags]
    public enum SpellCastFlags : byte
    {
        None = 0,
        Interruptible = 1 << 0,
        MovementLocked = 1 << 1,
        Concentrating = 1 << 2,
        OverchargeAllowed = 1 << 3
    }

    /// <summary>
    /// Spell cooldown tracking buffer.
    /// </summary>
    public struct SpellCooldown : IBufferElementData
    {
        public FixedString64Bytes SpellId;
        public float RemainingTime;
        public float TotalTime;
    }

    /// <summary>
    /// Mana/energy pool for spell casting.
    /// </summary>
    public struct SpellMana : IComponentData
    {
        /// <summary>
        /// Current mana amount.
        /// </summary>
        public float Current;

        /// <summary>
        /// Maximum mana capacity.
        /// </summary>
        public float Max;

        /// <summary>
        /// Mana regeneration per second.
        /// </summary>
        public float RegenRate;

        /// <summary>
        /// Bonus/penalty to mana costs (multiplier, 1.0 = normal).
        /// </summary>
        public float CostModifier;

        /// <summary>
        /// Tick of last mana update.
        /// </summary>
        public uint LastRegenTick;
    }

    /// <summary>
    /// Request to cast a spell (input from player or AI).
    /// </summary>
    public struct SpellCastRequest : IComponentData
    {
        public FixedString64Bytes SpellId;
        public Entity TargetEntity;
        public float3 TargetPosition;
        public uint RequestTick;
        public SpellCastRequestFlags Flags;
    }

    /// <summary>
    /// Flags for spell cast requests.
    /// </summary>
    [System.Flags]
    public enum SpellCastRequestFlags : byte
    {
        None = 0,
        QueueIfBusy = 1 << 0,
        ForceInterrupt = 1 << 1,
        AutoTarget = 1 << 2
    }

    /// <summary>
    /// Event raised when spell cast completes.
    /// </summary>
    public struct SpellCastEvent : IBufferElementData
    {
        public FixedString64Bytes SpellId;
        public Entity CasterEntity;
        public Entity TargetEntity;
        public float3 TargetPosition;
        public float EffectiveStrength;
        public uint CastTick;
        public SpellCastResult Result;
    }

    /// <summary>
    /// Result of a spell cast attempt.
    /// </summary>
    public enum SpellCastResult : byte
    {
        Success = 0,
        Interrupted = 1,
        OutOfMana = 2,
        OnCooldown = 3,
        OutOfRange = 4,
        InvalidTarget = 5,
        MissingPrerequisite = 6,
        Fizzled = 7  // Failed due to low mastery
    }

    /// <summary>
    /// Marker for entities that can cast spells.
    /// </summary>
    public struct SpellCaster : IComponentData
    {
        /// <summary>
        /// Primary casting attribute (affects power).
        /// </summary>
        public float CastingPower;

        /// <summary>
        /// Cast speed modifier (1.0 = normal).
        /// </summary>
        public float CastSpeedModifier;

        /// <summary>
        /// Cooldown reduction modifier (1.0 = normal).
        /// </summary>
        public float CooldownModifier;

        /// <summary>
        /// Number of spells that can be channeled simultaneously.
        /// </summary>
        public byte MaxChanneledSpells;

        /// <summary>
        /// Flags for caster capabilities.
        /// </summary>
        public SpellCasterFlags Flags;
    }

    /// <summary>
    /// Flags for spell caster capabilities.
    /// </summary>
    [System.Flags]
    public enum SpellCasterFlags : byte
    {
        None = 0,
        CanCastWhileMoving = 1 << 0,
        SilentCasting = 1 << 1,
        QuickDraw = 1 << 2,  // Reduced cast startup
        Resilient = 1 << 3   // Harder to interrupt
    }
}

