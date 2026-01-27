using Unity.Entities;

namespace PureDOTS.Runtime.Social
{
    /// <summary>
    /// Types of loyalty targets.
    /// </summary>
    public enum LoyaltyTarget : byte
    {
        None = 0,
        
        // Organizational (1-9)
        Village = 1,          // Local settlement
        Band = 2,             // Military unit
        Faction = 3,          // Nation/corporation
        Fleet = 4,            // Space4X fleet
        Colony = 5,           // Space4X colony
        Guild = 6,            // Professional organization
        
        // Personal (10-19)
        Leader = 10,          // Specific leader entity
        Family = 11,          // Blood relatives
        Religion = 12,        // Faith/doctrine
        Ideology = 13,        // Political belief
        Mentor = 14,          // Teacher/guide
        
        // Abstract (20-29)
        Homeland = 20,        // Place of origin
        Tradition = 21,       // Cultural practices
        Cause = 22            // Movement/ideal
    }

    /// <summary>
    /// Loyalty state levels.
    /// </summary>
    public enum LoyaltyState : byte
    {
        Traitor = 0,          // Actively working against (0-19)
        Disloyal = 1,         // Susceptible to defection (20-39)
        Neutral = 2,          // No strong feelings (40-59)
        Loyal = 3,            // Committed (60-79)
        Fanatic = 4           // Will die for cause (80-100)
    }

    /// <summary>
    /// Entity's primary loyalty.
    /// </summary>
    public struct EntityLoyalty : IComponentData
    {
        public Entity PrimaryTarget;       // Main loyalty target (village/fleet/faction)
        public LoyaltyTarget TargetType;
        public byte Loyalty;               // 0-100
        public LoyaltyState State;         // Derived from loyalty value
        public byte NaturalLoyalty;        // Innate tendency (some are naturally loyal)
        public float DesertionRisk;        // Current chance to desert (0-1)
        public uint LastLoyaltyChangeTick;
    }

    /// <summary>
    /// Secondary loyalties (can have multiple).
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct SecondaryLoyalty : IBufferElementData
    {
        public Entity Target;
        public LoyaltyTarget TargetType;
        public byte Loyalty;
        public LoyaltyState State;
    }

    /// <summary>
    /// Configuration for loyalty system.
    /// </summary>
    public struct LoyaltyConfig : IComponentData
    {
        public float BaseDesertionThreshold;  // Loyalty below which desertion possible (30)
        public float MutinyThreshold;         // Average loyalty for mutiny check (25)
        public float FanaticThreshold;        // Loyalty for fanatic bonuses (80)
        public float LoyaltyDecayRate;        // Per-day decay without reinforcement
        public float HardshipPenalty;         // Loyalty loss per hardship event
        public float VictoryBonus;            // Loyalty gain per victory
        public uint TicksPerDay;              // For decay calculation
    }

    /// <summary>
    /// Calculated modifiers from loyalty.
    /// </summary>
    public struct LoyaltyModifiers : IComponentData
    {
        public float MoraleBonus;             // From high loyalty
        public float SacrificeWillingness;    // Chance to take damage for ally
        public float BribeResistance;         // Resistance to corruption
        public float PropagandaResistance;    // Resistance to enemy propaganda
        public float ConscriptionCap;         // Max conscription rate accepted
    }

    /// <summary>
    /// Loyalty event types.
    /// </summary>
    public enum LoyaltyEventType : byte
    {
        None = 0,
        Victory = 1,          // Won battle/achieved goal
        Defeat = 2,           // Lost battle
        Hardship = 3,         // Famine, disaster
        Betrayal = 4,         // Leader betrayed
        Miracle = 5,          // Divine intervention
        Reward = 6,           // Given recognition
        Punishment = 7,       // Unfair treatment
        Propaganda = 8,       // Enemy influence
        Inspiration = 9       // Inspiring speech/action
    }

    /// <summary>
    /// Event that affects loyalty.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct LoyaltyEvent : IBufferElementData
    {
        public LoyaltyEventType Type;
        public sbyte Magnitude;           // -100 to +100
        public uint Tick;
        public Entity SourceEntity;       // Who caused it
    }

    /// <summary>
    /// Desertion event.
    /// </summary>
    public struct DesertionEvent : IComponentData
    {
        public byte LoyaltyAtDesertion;
        public float HardshipLevel;
        public uint Tick;
    }

    /// <summary>
    /// Mutiny event (group).
    /// </summary>
    public struct MutinyEvent : IComponentData
    {
        public Entity GroupEntity;        // Fleet/band that mutinied
        public float AverageLoyalty;
        public uint Tick;
    }
}

