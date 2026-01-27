using Unity.Entities;
using Unity.Collections;

namespace PureDOTS.Runtime.Morale
{
    /// <summary>
    /// Morale bands representing discrete mood tiers.
    /// </summary>
    public enum MoraleBand : byte
    {
        Despair = 0,      // 0-199: -40% initiative, breakdown risk, health decay
        Unhappy = 1,      // 200-399: -15% work speed, social friction
        Stable = 2,       // 400-599: neutral baseline
        Cheerful = 3,     // 600-799: +10% work speed, +5% faith/loyalty gain
        Elated = 4        // 800-1000: +25% initiative, inspire allies, burnout risk
    }

    /// <summary>
    /// Categories of morale modifiers.
    /// </summary>
    public enum MoraleModifierCategory : byte
    {
        Needs = 0,          // Food, rest, hygiene
        Environment = 1,    // Weather, lighting, beauty, ship quality
        Relationships = 2,  // Friends, family, leadership
        Events = 3,         // Victories, disasters, miracles
        Health = 4,         // Injuries, illness
        Work = 5,           // Job satisfaction, achievements
        Faith = 6,          // Religious experiences
        Combat = 7          // Battle outcomes
    }

    /// <summary>
    /// Entity morale state with band and modifiers.
    /// </summary>
    public struct EntityMorale : IComponentData
    {
        public float CurrentMorale;        // 0-1000
        public MoraleBand Band;            // Derived from current morale
        
        // Calculated modifiers
        public float WorkSpeedModifier;    // -0.20 to +0.15
        public float InitiativeModifier;   // -0.40 to +0.25
        public float CombatModifier;       // -0.30 to +0.20
        public float SocialModifier;       // -0.25 to +0.15
        
        // Risk factors
        public byte BreakdownRisk;         // 0-100 (Despair band)
        public byte BurnoutRisk;           // 0-100 (Elated band)
        
        // Timestamps
        public uint LastBandChangeTick;
        public uint LastUpdateTick;
        
        // Previous band for transition detection
        public MoraleBand PreviousBand;
    }

    /// <summary>
    /// Active morale modifier affecting an entity.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct MoraleModifier : IBufferElementData
    {
        public FixedString32Bytes ModifierId;
        public MoraleModifierCategory Category;
        public short Magnitude;            // -100 to +100
        public uint RemainingTicks;        // 0 = permanent
        public uint DecayHalfLife;         // Ticks until half-magnitude
        public uint AppliedTick;
    }

    /// <summary>
    /// Long-term morale memory (triumphs, traumas).
    /// </summary>
    [InternalBufferCapacity(6)]
    public struct MoraleMemory : IBufferElementData
    {
        public FixedString32Bytes MemoryType;  // "trauma", "triumph", "betrayal"
        public short InitialMagnitude;
        public short CurrentMagnitude;
        public uint FormedTick;
        public uint DecayHalfLife;
        public Entity AssociatedEntity;
    }

    /// <summary>
    /// Configuration for morale system.
    /// </summary>
    public struct MoraleConfig : IComponentData
    {
        public float DespairThreshold;     // 200
        public float UnhappyThreshold;     // 400
        public float CheerfulThreshold;    // 600
        public float ElatedThreshold;      // 800
        public float MaxMorale;            // 1000
        
        // Breakdown/burnout check intervals
        public uint BreakdownCheckInterval;
        public uint BurnoutCheckInterval;
        
        // Base breakdown/burnout chances per check
        public float BaseBreakdownChance;  // 0.1 = 10% per check at max risk
        public float BaseBurnoutChance;    // 0.05 = 5% per check at max risk
        
        // Modifier decay rate
        public float ModifierDecayRate;    // Multiplier for decay
    }

    /// <summary>
    /// Event when morale band changes.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct MoraleBandChangedEvent : IBufferElementData
    {
        public MoraleBand OldBand;
        public MoraleBand NewBand;
        public float OldMorale;
        public float NewMorale;
        public uint Tick;
    }

    /// <summary>
    /// Event when entity has a breakdown.
    /// </summary>
    public struct MoraleBreakdownEvent : IComponentData
    {
        public MoraleBand TriggerBand;
        public float MoraleAtBreakdown;
        public uint Tick;
    }

    /// <summary>
    /// Event when entity burns out.
    /// </summary>
    public struct MoraleBurnoutEvent : IComponentData
    {
        public float MoraleAtBurnout;
        public uint Tick;
    }

    /// <summary>
    /// Request to apply a morale modifier.
    /// </summary>
    public struct ApplyMoraleModifierRequest : IComponentData
    {
        public Entity TargetEntity;
        public FixedString32Bytes ModifierId;
        public MoraleModifierCategory Category;
        public short Magnitude;
        public uint DurationTicks;
        public uint DecayHalfLife;
    }

    /// <summary>
    /// Request to add a morale memory.
    /// </summary>
    public struct AddMoraleMemoryRequest : IComponentData
    {
        public Entity TargetEntity;
        public FixedString32Bytes MemoryType;
        public short Magnitude;
        public uint DecayHalfLife;
        public Entity AssociatedEntity;
    }
}

