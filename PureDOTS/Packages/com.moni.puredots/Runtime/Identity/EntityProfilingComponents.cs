// Entity profiling components
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Stats;

namespace PureDOTS.Runtime.Identity
{
    /// <summary>
    /// Entity profile - tracks archetype assignment and profile source.
    /// </summary>
    public struct EntityProfile : IComponentData
    {
        public FixedString64Bytes ArchetypeName;
        public EntityProfileSource Source;
        public uint CreatedTick;
        public byte IsResolved;
    }

    /// <summary>
    /// Profile application source.
    /// </summary>
    public enum EntityProfileSource : byte
    {
        Template = 0,
        Generated = 1,
        PlayerCreated = 2,
        Scenario = 3,
        Custom = 4
    }

    /// <summary>
    /// Profile application state - tracks which phases have been completed.
    /// </summary>
    public struct ProfileApplicationState : IComponentData
    {
        public ProfileApplicationPhase Phase;
        public uint LastUpdatedTick;
        public byte NeedsRecalculation;
    }

    /// <summary>
    /// Profile application phases.
    /// </summary>
    public enum ProfileApplicationPhase : byte
    {
        None = 0,
        ArchetypeAssigned = 1,
        StatsApplied = 2,
        AlignmentApplied = 3,
        PersonalityApplied = 4,
        GameSpecificApplied = 5,
        Complete = 6
    }

    /// <summary>
    /// Opt-out component: entities with this component will be skipped by EntityProfilingBootstrapSystem.
    /// Add this to entities that have manually created profiling components and should not be auto-profiled.
    /// </summary>
    public struct SkipEntityProfiling : IComponentData
    {
    }

    /// <summary>
    /// Godgame villager profile - complete profile for Godgame villagers.
    /// Note: This is a data container; actual components are applied separately.
    /// </summary>
    public struct VillagerProfileData : IComponentData
    {
        // Note: IndividualStats, SocialStats, etc. are applied as separate components
        // This struct is just for passing profile data during creation
        public float BasePhysique;
        public float BaseFinesse;
        public float BaseWill;
        public float BaseWisdom;
        public float InitialFame;
        public float InitialWealth;
        public float InitialReputation;
        public float InitialGlory;
        public float MoralAxisLean;
        public float OrderAxisLean;
        public float PurityAxisLean;
        public float VengefulForgiving;
        public float CravenBold;
        public float CooperativeCompetitive;
        public float WarlikePeaceful;
    }

    /// <summary>
    /// Space4X individual profile - complete profile for Space4X individuals.
    /// Note: This is a data container; actual components are applied separately.
    /// </summary>
    public struct IndividualProfileData : IComponentData
    {
        // Note: IndividualStats, OfficerStats, etc. are applied as separate components
        // This struct is just for passing profile data during creation
        
        // Base attributes
        public float BasePhysique;
        public float BaseFinesse;
        public float BaseWill;
        
        // Alignment leans
        public float MoralAxisLean;
        public float OrderAxisLean;
        public float PurityAxisLean;
        
        // Personality axes
        public float VengefulForgiving;
        public float CravenBold;
        
        // Space4X officer stats (0-100)
        public half Command;
        public half Tactics;
        public half Logistics;
        public half Diplomacy;
        public half Engineering;
        public half Resolve;
        
        // Initial expertise types and service traits (max 4 entries each)
        // Format: expertise type string (e.g., "CarrierCommand"), service trait ID (e.g., "ReactorWhisperer")
        public FixedList128Bytes<FixedString32Bytes> InitialExpertiseTypes;
        public FixedList128Bytes<FixedString32Bytes> InitialServiceTraits;
    }
}

