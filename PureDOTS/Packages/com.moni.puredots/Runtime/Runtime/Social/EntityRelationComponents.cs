using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Social
{
    /// <summary>
    /// Types of relationships between entities.
    /// </summary>
    public enum RelationType : byte
    {
        None = 0,
        
        // Personal relationships (1-9)
        Stranger = 1,
        Acquaintance = 2,
        Friend = 3,
        CloseFriend = 4,
        BestFriend = 5,
        
        // Negative personal (10-19)
        Rival = 10,
        Enemy = 11,
        Nemesis = 12,
        Grudge = 13,
        
        // Family relationships (20-29)
        Parent = 20,
        Child = 21,
        Sibling = 22,
        Spouse = 23,
        Grandparent = 24,
        Grandchild = 25,
        Cousin = 26,
        InLaw = 27,
        
        // Professional relationships (30-39)
        Mentor = 30,
        Student = 31,
        Colleague = 32,
        Superior = 33,
        Subordinate = 34,
        BusinessPartner = 35,
        
        // Faction relationships (40-49)
        Ally = 40,
        Neutral = 41,
        Hostile = 42,
        AtWar = 43,
        Vassal = 44,
        Overlord = 45,
        
        // Romantic (50-59)
        Crush = 50,
        Courting = 51,
        Betrothed = 52,
        Lover = 53,
        ExPartner = 54
    }

    /// <summary>
    /// Outcome of an interaction for relation changes.
    /// </summary>
    public enum InteractionOutcome : byte
    {
        Neutral = 0,
        Positive = 1,
        VeryPositive = 2,
        Negative = 3,
        VeryNegative = 4,
        Hostile = 5,
        Intimate = 6,
        Professional = 7
    }

    /// <summary>
    /// Relationship data between two entities.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct EntityRelation : IBufferElementData
    {
        public Entity OtherEntity;
        public RelationType Type;
        public sbyte Intensity;           // -100 (hatred) to +100 (love)
        public ushort InteractionCount;   // Times interacted
        public uint FirstMetTick;
        public uint LastInteractionTick;
        public byte Trust;                // 0-100 reliability score
        public byte Familiarity;          // 0-100 how well they know each other
        public byte Respect;              // 0-100 respect level
        public byte Fear;                 // 0-100 fear level
    }

    /// <summary>
    /// Configuration for relationship system.
    /// </summary>
    public struct RelationConfig : IComponentData
    {
        public float DecayRatePerDay;     // How fast unused relations fade
        public sbyte MinIntensity;        // Floor for decay (-50 default)
        public sbyte MaxIntensity;        // Ceiling for intensity (100 default)
        public byte FamiliarityPerInteraction;  // Familiarity gained per interaction
        public byte TrustPerPositiveInteraction;
        public byte TrustLossPerNegative;
        public float DecayCheckInterval;  // Ticks between decay checks
    }

    /// <summary>
    /// Request to record an interaction between entities.
    /// </summary>
    public struct RecordInteractionRequest : IComponentData
    {
        public Entity EntityA;
        public Entity EntityB;
        public InteractionOutcome Outcome;
        public sbyte IntensityChange;
        public sbyte TrustChange;
        public bool IsMutual;             // Apply to both entities
    }

    /// <summary>
    /// Event emitted when a relationship changes significantly.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct RelationChangedEvent : IBufferElementData
    {
        public Entity OtherEntity;
        public RelationType OldType;
        public RelationType NewType;
        public sbyte OldIntensity;
        public sbyte NewIntensity;
        public uint Tick;
    }

    /// <summary>
    /// Event emitted when entities first meet.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct FirstMeetingEvent : IBufferElementData
    {
        public Entity OtherEntity;
        public sbyte InitialImpression;
        public uint Tick;
    }

    /// <summary>
    /// Social standing/reputation of an entity.
    /// </summary>
    public struct SocialStanding : IComponentData
    {
        public sbyte Reputation;          // -100 to 100
        public byte Influence;            // 0-100
        public byte Notoriety;            // 0-100 (fame, good or bad)
        public ushort TotalRelations;     // Number of relations
        public ushort PositiveRelations;  // Number of positive relations
        public ushort NegativeRelations;  // Number of negative relations
    }
}
