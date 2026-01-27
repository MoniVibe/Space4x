using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Buffs
{
    /// <summary>
    /// Buffer element representing an active buff/debuff on an entity.
    /// Multiple instances of the same buff can exist if stacking is allowed.
    /// </summary>
    public struct ActiveBuff : IBufferElementData
    {
        /// <summary>
        /// Buff identifier from catalog.
        /// </summary>
        public FixedString64Bytes BuffId;

        /// <summary>
        /// Entity that applied this buff (Entity.Null if self-applied or unknown).
        /// </summary>
        public Entity SourceEntity;

        /// <summary>
        /// Remaining duration in seconds (0 = permanent until dispelled).
        /// </summary>
        public float RemainingDuration;

        /// <summary>
        /// Time since last periodic effect tick (for TickInterval tracking).
        /// </summary>
        public float TimeSinceLastTick;

        /// <summary>
        /// Current number of stacks (1 = base, increases with stacking).
        /// </summary>
        public byte CurrentStacks;

        /// <summary>
        /// Tick when buff was applied (for determinism and debugging).
        /// </summary>
        public uint AppliedTick;
    }

    /// <summary>
    /// Aggregated stat modifiers from all active buffs (cached for performance).
    /// Systems should update this when buffs are added/removed/expired.
    /// </summary>
    public struct BuffStatCache : IComponentData
    {
        // Combat stats
        public float DamageFlat;
        public float DamagePercent;
        public float AttackSpeedFlat;
        public float AttackSpeedPercent;
        public float ArmorFlat;
        public float ArmorPercent;
        public float HealthFlat;
        public float HealthPercent;
        public float MaxHealthFlat;
        public float MaxHealthPercent;
        public float HealthRegenFlat;
        public float ManaFlat;
        public float ManaPercent;
        public float MaxManaFlat;
        public float MaxManaPercent;
        public float ManaRegenFlat;
        public float StaminaFlat;
        public float StaminaPercent;
        public float MaxStaminaFlat;
        public float MaxStaminaPercent;
        public float StaminaRegenFlat;

        // Movement stats
        public float SpeedFlat;
        public float SpeedPercent;
        public float JumpHeightFlat;
        public float JumpHeightPercent;

        // Skill/Attribute stats
        public float SkillGainRateFlat;
        public float SkillGainRatePercent;
        public float XPGainRateFlat;
        public float XPGainRatePercent;

        // Space4X specific
        public float PowerGenerationFlat;
        public float PowerGenerationPercent;
        public float PowerDrawFlat;
        public float PowerDrawPercent;
        public float MiningRateFlat;
        public float MiningRatePercent;
        public float RepairRateFlat;
        public float RepairRatePercent;
        public float FireRateFlat;
        public float FireRatePercent;
        public float AccuracyFlat;
        public float AccuracyPercent;

        // Godgame specific
        public float MoodFlat;
        public float MoodPercent;
        public float FaithFlat;
        public float FaithPercent;
        public float WorshipRateFlat;
        public float WorshipRatePercent;

        /// <summary>
        /// Tick when cache was last updated (for change detection).
        /// </summary>
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Request to apply a buff to an entity (processed by BuffApplicationSystem).
    /// </summary>
    public struct BuffApplicationRequest : IBufferElementData
    {
        /// <summary>
        /// Buff identifier from catalog.
        /// </summary>
        public FixedString64Bytes BuffId;

        /// <summary>
        /// Entity applying the buff (for tracking/attribution).
        /// </summary>
        public Entity SourceEntity;

        /// <summary>
        /// Duration override in seconds (0 = use buff's BaseDuration).
        /// </summary>
        public float DurationOverride;

        /// <summary>
        /// Number of stacks to apply (default 1).
        /// </summary>
        public byte StacksToApply;
    }

    /// <summary>
    /// Request to remove/dispel buffs from an entity.
    /// </summary>
    public struct BuffDispelRequest : IBufferElementData
    {
        /// <summary>
        /// Buff identifier to dispel (empty = dispel all buffs).
        /// </summary>
        public FixedString64Bytes BuffId;

        /// <summary>
        /// If true, only dispel debuffs. If false, dispel matching buffs.
        /// </summary>
        public bool DebuffsOnly;
    }

    /// <summary>
    /// Event emitted when a buff is applied (for UI/audio/visual feedback).
    /// </summary>
    public struct BuffAppliedEvent : IBufferElementData
    {
        public FixedString64Bytes BuffId;
        public Entity TargetEntity;
        public Entity SourceEntity;
        public byte StacksApplied;
        public uint Tick;
    }

    /// <summary>
    /// Event emitted when a buff expires or is removed.
    /// </summary>
    public struct BuffRemovedEvent : IBufferElementData
    {
        public FixedString64Bytes BuffId;
        public Entity TargetEntity;
        public bool Expired; // true if expired naturally, false if dispelled
        public uint Tick;
    }
}

