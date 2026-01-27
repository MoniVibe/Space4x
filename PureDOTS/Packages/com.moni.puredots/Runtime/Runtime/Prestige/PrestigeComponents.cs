using Unity.Entities;
using Unity.Collections;

namespace PureDOTS.Runtime.Prestige
{
    /// <summary>
    /// Type of reputation context.
    /// </summary>
    public enum ReputationType : byte
    {
        General = 0,        // Overall reputation
        Military = 1,       // Combat prowess
        Economic = 2,       // Trade reliability
        Diplomatic = 3,     // Trustworthiness
        Scientific = 4,     // Research achievements
        Cultural = 5,       // Cultural influence
        Criminal = 6        // Infamy (negative)
    }

    /// <summary>
    /// Prestige tier unlocks.
    /// </summary>
    public enum PrestigeTier : byte
    {
        Unknown = 0,        // 0-99
        Known = 1,          // 100-499
        Notable = 2,        // 500-1999
        Renowned = 3,       // 2000-7999
        Famous = 4,         // 8000-24999
        Legendary = 5,      // 25000-99999
        Mythic = 6          // 100000+
    }

    /// <summary>
    /// Main prestige tracking component.
    /// </summary>
    public struct Prestige : IComponentData
    {
        public float CurrentPrestige;
        public float LifetimePrestige;     // Total ever earned
        public PrestigeTier Tier;
        public float PeakPrestige;         // Highest ever reached
        public uint LastGainTick;
        public uint LastDecayTick;
        public float DecayRate;            // Per tick decay
    }

    /// <summary>
    /// Reputation with different audiences.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ReputationScore : IBufferElementData
    {
        public Entity AudienceEntity;      // Who's opinion (null = global)
        public ReputationType Type;
        public float Score;                // -100 to +100
        public float Volatility;           // How fast it changes
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Stress/strain on prestige.
    /// </summary>
    public struct PrestigeStress : IComponentData
    {
        public float CurrentStress;        // 0-1
        public float StressThreshold;      // When crisis triggers
        public float RecoveryRate;         // Natural stress reduction
        public uint LastStressEventTick;
        public byte InCrisis;
    }

    /// <summary>
    /// Prestige unlock requirement.
    /// </summary>
    public struct PrestigeUnlock
    {
        public FixedString64Bytes UnlockId;
        public PrestigeTier RequiredTier;
        public ReputationType RequiredRepType;
        public float RequiredRepScore;
        public byte RequiresBothPrestigeAndRep;
    }

    /// <summary>
    /// Prestige event that modifies prestige/reputation.
    /// </summary>
    public struct PrestigeEvent : IComponentData
    {
        public FixedString32Bytes EventType;
        public float PrestigeChange;
        public float ReputationChange;
        public ReputationType AffectedRepType;
        public Entity SourceEntity;
        public uint OccurredTick;
        public byte IsPositive;
    }

    /// <summary>
    /// Notoriety tracking (negative reputation).
    /// </summary>
    public struct Notoriety : IComponentData
    {
        public float InfamyLevel;          // 0-100
        public float HeatLevel;            // Active pursuit/attention
        public float BountyValue;          // Price on head
        public uint LastCrimesTick;
        public byte IsOutlaw;
    }
}

