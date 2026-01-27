using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Aggregates
{
    /// <summary>
    /// Identity data for a band (stable fields such as name/purpose).
    /// Split from stats so that parallel jobs never need to touch FixedStrings.
    /// </summary>
    public struct BandIdentity : IComponentData
    {
        public FixedString64Bytes BandName;
        public BandPurpose Purpose;
        public Entity LeaderEntity;
        public uint FormationTick;
    }

    /// <summary>
    /// Frequently updated aggregate stats derived from members.
    /// Kept separate from identity to avoid NativeText write hazards.
    /// </summary>
    public struct BandAggregateStats : IComponentData
    {
        public ushort MemberCount;
        public float AverageMorale;
        public float AverageEnergy;
        public float AverageStrength;
    }
    
    public enum BandPurpose : byte
    {
        Military_Warband,
        Military_Defense,
        Military_Mercenary,
        Logistics_Caravan,
        Logistics_Construction,
        Logistics_Repair,
        Civilian_Merchant,
        Civilian_Entertainer,
        Civilian_Artisan,
        Civilian_Missionary,
        Civilian_Adventuring,
        Work_Hunting,
        Work_Mining,
        Work_Logging,
        Custom
    }
    
    /// <summary>
    /// Buffer of band members (attached to Band entity).
    /// </summary>
    [InternalBufferCapacity(20)]
    public struct BandMember : IBufferElementData
    {
        public Entity MemberEntity;
        public uint JoinedTick;
        public BandRole Role;
        public bool IsDoubleAgent; // Spy from another faction
    }
    
    public enum BandRole : byte
    {
        Leader,
        Combatant,
        Healer,
        Blacksmith,
        Scout,
        Entertainer,
        Merchant,
        Laborer,
        Specialist
    }
    
    /// <summary>
    /// Attached to individual entities who are band members.
    /// Reference back to their band.
    /// </summary>
    public struct BandMembership : IComponentData
    {
        public Entity BandEntity;
        public uint JoinedTick;
        public BandRole Role;
    }
    
    /// <summary>
    /// Tracks shared experiences between band members.
    /// </summary>
    [InternalBufferCapacity(10)]
    public struct SharedExperience : IBufferElementData
    {
        public SharedExperienceType Type;
        public uint OccurredTick;
        public Entity WitnessedWith; // Other entity in the experience
    }
    
    public enum SharedExperienceType : byte
    {
        CombatVictory,
        CombatDefeat,
        MemberDeath,
        ResourceCrisis,
        QuestCompleted,
        LongJourney,
        Betrayal,
        Rescue
    }
    
    /// <summary>
    /// Band formation candidate (2+ entities considering forming band).
    /// </summary>
    public struct BandFormationCandidate : IComponentData
    {
        public Entity InitiatorEntity;
        public FixedString128Bytes SharedGoal;
        public uint ProposedTick;
        public byte ProspectiveMemberCount;
    }
    
    [InternalBufferCapacity(10)]
    public struct BandFormationProspect : IBufferElementData
    {
        public Entity ProspectEntity;
        public bool HasAccepted;
    }
    
    /// <summary>
    /// Join request sent from band leader to potential member.
    /// </summary>
    public struct BandJoinRequest : IComponentData
    {
        public Entity BandEntity;
        public Entity LeaderEntity;
        public Entity TargetEntity;
        public BandRole OfferedRole;
        public uint RequestTick;
        public uint ExpirationTick;
    }
    
    /// <summary>
    /// Band's current goal/objective.
    /// </summary>
    public struct BandGoal : IComponentData
    {
        public BandGoalType Type;
        public float3 TargetLocation; // If location-based goal
        public Entity TargetEntity;   // If entity-based goal (hunt creature, protect person)
        public uint DeadlineTick;     // Optional deadline
    }
    
    public enum BandGoalType : byte
    {
        Travel_To_Location,
        Hunt_Creature,
        Escort_Entity,
        Defend_Location,
        Trade_Route,
        Build_Structure,
        Explore_Region,
        Complete_Quest,
        Find_Resources
    }
    
    /// <summary>
    /// Band evolution state tracking.
    /// </summary>
    public struct BandEvolutionState : IComponentData
    {
        public bool HasFamilies;
        public bool OriginalGoalCompleted;
        public uint TimeAsRoamingVillage; // Ticks spent as roaming village
        public bool HasSettlementPlans;
        public bool HasGuildBacking;
        public Entity BackingGuildEntity; // If guild formed
    }
}

