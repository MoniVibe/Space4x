using Unity.Entities;

namespace PureDOTS.Runtime.Social
{
    /// <summary>
    /// Types of grudges.
    /// </summary>
    public enum GrudgeType : byte
    {
        None = 0,
        
        // Personal (1-9)
        Insult = 1,           // Minor slight
        Theft = 2,            // Took something
        Assault = 3,          // Physical harm
        Betrayal = 4,         // Broken trust
        Murder = 5,           // Killed loved one
        Humiliation = 6,      // Public embarrassment
        Abandonment = 7,      // Left in time of need
        
        // Professional (10-19)
        Demotion = 10,        // Career harm
        Sabotage = 11,        // Damaged work
        CreditStolen = 12,    // Took credit for work
        Exploitation = 13,    // Unfair treatment
        Blackmail = 14,       // Coerced
        
        // Factional (20-29)
        WarCrime = 20,        // Atrocity against faction
        TerritoryLoss = 21,   // Took land/space
        EconomicHarm = 22,    // Trade war damage
        CulturalOffense = 23, // Disrespected traditions
        Genocide = 24         // Mass killing of faction members
    }

    /// <summary>
    /// Severity of a grudge.
    /// </summary>
    public enum GrudgeSeverity : byte
    {
        Forgotten = 0,        // Decayed to nothing
        Minor = 1,            // Annoyance (intensity 1-25)
        Moderate = 2,         // Resentment (26-50)
        Serious = 3,          // Hatred (51-75)
        Vendetta = 4          // Blood feud (76-100)
    }

    /// <summary>
    /// A grudge held against another entity.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct EntityGrudge : IBufferElementData
    {
        public Entity OffenderEntity;      // Who wronged them
        public GrudgeType Type;
        public byte Intensity;             // 0-100, decays over time
        public GrudgeSeverity Severity;    // Derived from intensity
        public uint OriginTick;            // When grudge formed
        public uint LastRenewedTick;       // When intensity was refreshed
        public bool IsInherited;           // Passed down from family/faction
        public bool IsPublic;              // Known to others (affects reputation)
    }

    /// <summary>
    /// Configuration for grudge system.
    /// </summary>
    public struct GrudgeConfig : IComponentData
    {
        public float DecayRatePerDay;      // How fast grudges fade (0.5 = lose 0.5 intensity/day)
        public byte MinIntensityForAction; // Threshold for hostile action (default 50)
        public byte VendettaThreshold;     // When it becomes blood feud (default 75)
        public float InheritanceDecay;     // How much intensity children inherit (0.5 = 50%)
        public bool AllowForgiveness;      // Can grudges be resolved diplomatically
        public uint TicksPerDay;           // For decay calculation
    }

    /// <summary>
    /// Entity's grudge behavior traits.
    /// </summary>
    public struct GrudgeBehavior : IComponentData
    {
        public byte Vengefulness;          // 0-100, how likely to hold grudges
        public byte Forgiveness;           // 0-100, how fast grudges decay
        public bool SeeksRevenge;          // Will actively pursue vendetta targets
        public byte RevengeThreshold;      // Intensity needed to seek revenge
        public byte MemoryStrength;        // How well they remember slights
    }

    /// <summary>
    /// Request to add a grudge.
    /// </summary>
    public struct AddGrudgeRequest : IComponentData
    {
        public Entity VictimEntity;
        public Entity OffenderEntity;
        public GrudgeType Type;
        public byte BaseIntensity;
        public bool IsPublic;
    }

    /// <summary>
    /// Event when a grudge escalates to vendetta.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct VendettaEvent : IBufferElementData
    {
        public Entity OffenderEntity;
        public GrudgeType Type;
        public uint Tick;
    }

    /// <summary>
    /// Event when revenge is sought.
    /// </summary>
    public struct SeekingRevengeTag : IComponentData
    {
        public Entity TargetEntity;
        public GrudgeType GrudgeType;
        public byte Intensity;
    }
}

