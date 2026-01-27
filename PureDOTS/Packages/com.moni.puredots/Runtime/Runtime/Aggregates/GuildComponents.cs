using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Aggregates
{
    /// <summary>
    /// Core guild entity component.
    /// Guilds are aggregate entities similar to villages/bands.
    /// </summary>
    public struct Guild : IComponentData
    {
        public enum GuildType : byte
        {
            Heroes,
            Merchants,
            Scholars,
            Assassins,
            Artisans,
            Farmers,
            Mystics,
            Mages,          // Arcane specialists
            HolyOrder,      // Religious enforcement
            Rogues,         // Thieves and infiltrators
            Rebels          // Revolutionary factions
            // Extensible
        }
        
        public GuildType Type;
        public FixedString64Bytes GuildName; // "Lightbringers", "Shadowfang"
        public uint FoundedTick;
        
        // Home village (headquarters)
        public Entity HomeVillage;
        public float3 HeadquartersPosition;
        
        // Power metrics
        public ushort MemberCount;
        public float AverageMemberLevel;     // Combat skill average
        public uint TotalExperience;         // Cumulative XP from missions
        
        // Diplomatic state
        public byte ReputationScore;         // 0-100 general reputation
        public FixedString64Bytes CurrentMission; // "Hunt Demon Lord Korgath"
    }
    
    /// <summary>
    /// Guild membership roster.
    /// </summary>
    public struct GuildMember : IBufferElementData
    {
        public Entity VillagerEntity;       // Which villager is a member
        public uint JoinedTick;
        public ushort ExperienceContributed; // XP earned for guild
        public uint ContributionScore;      // Aggregate contribution weighting
        public byte Rank;                   // 0=Member, 1=Officer, 2=Master
        public bool IsOfficer;
        public bool IsGuildMaster;
    }
    
    /// <summary>
    /// Guild outlook set persisted after legacy alignment migration.
    /// </summary>
    public struct GuildOutlookSet : IComponentData
    {
        public byte Outlook1;
        public byte Outlook2;
        public byte Outlook3;
        public bool IsFanatic;
    }
    
    /// <summary>
    /// Guild leadership structure.
    /// </summary>
    public struct GuildLeadership : IComponentData
    {
        public enum GovernanceType : byte
        {
            Democratic,     // Members vote
            Authoritarian,  // Master rules absolutely
            Meritocratic,   // Highest skill leads
            Oligarchic      // Officers vote
        }
        
        public GovernanceType Governance;
        public Entity GuildMasterEntity;     // Current leader
        public uint MasterElectedTick;       // When elected/seized power
        
        // Officers
        public Entity QuartermasterEntity;
        public Entity RecruiterEntity;
        public Entity DiplomatEntity;
        public Entity WarMasterEntity;
        public Entity SpyMasterEntity;      // Only for espionage guilds
        
        // Voting state
        public bool VoteInProgress;
        public FixedString64Bytes VoteProposal; // "Declare war on Shadowfang"
        public uint VoteEndTick;
    }
    
    /// <summary>
    /// Active vote buffer for democratic guilds.
    /// </summary>
    public struct GuildVote : IBufferElementData
    {
        public Entity VoterEntity;
        public bool VotedYes;
        public bool VotedNo;
        public bool Abstained;
    }
    
    /// <summary>
    /// Guild embassies in other villages.
    /// </summary>
    public struct GuildEmbassy : IBufferElementData
    {
        public Entity VillageEntity;         // Which village hosts embassy
        public float3 EmbassyPosition;
        public Entity EmbassyBuildingEntity; // Physical building
        public uint EstablishedTick;
        
        // Embassy staff
        public Entity Representative1;
        public Entity Representative2;
        public Entity Representative3;
    }
    
    /// <summary>
    /// Inter-guild relations.
    /// </summary>
    public struct GuildRelation : IBufferElementData
    {
        public enum RelationType : byte
        {
            Neutral,
            Allied,
            Hostile,
            AtWar,
            Betrayed // Special state after alliance broken
        }
        
        public Entity OtherGuildEntity;
        public RelationType Relation;
        public sbyte TrustLevel;             // -100 to +100
        public uint RelationSinceTick;
        
        // Betrayal tracking
        public bool HasBetrayed;             // Did this guild betray us?
        public uint BetrayalTick;
    }
    
    /// <summary>
    /// Guild progression and specialized knowledge.
    /// </summary>
    public struct GuildKnowledge : IComponentData
    {
        // Threat-specific bonuses
        public byte DemonSlayingBonus;      // 0-100%
        public byte UndeadSlayingBonus;
        public byte BossHuntingBonus;
        public byte CelestialCombatBonus;
        
        // Tactical knowledge
        public byte EspionageEffectiveness;
        public byte CoordinationBonus;
        public byte SurvivalBonus;
        
        // Total kills (for learning)
        public ushort DemonsKilled;
        public ushort UndeadKilled;
        public ushort BossesKilled;
        public ushort CelestialsKilled;
    }
    
    /// <summary>
    /// Guild resources (loot, treasury).
    /// </summary>
    public struct GuildTreasury : IComponentData
    {
        public float GoldReserves;
        public float LootValue;              // Total value of stored equipment
        public ushort LegendaryItemCount;
    }
    
    /// <summary>
    /// Active guild mission.
    /// </summary>
    public struct GuildMission : IComponentData
    {
        public enum MissionType : byte
        {
            None,
            HuntWorldBoss,
            DefendVillage,
            SealHellGate,
            AssassinateTarget,
            EscortCaravan,
            Research
        }
        
        public MissionType Type;
        public Entity TargetEntity;          // Boss, village, etc.
        public float3 TargetPosition;
        public uint MissionStartTick;
        public ushort ParticipantCount;      // How many members on mission
        
        // Rewards
        public float ExpectedExperience;
        public float ExpectedLoot;
    }
    
    /// <summary>
    /// Guild warfare state.
    /// </summary>
    public struct GuildWarState : IComponentData
    {
        public bool AtWar;
        public Entity EnemyGuildEntity;
        public uint WarDeclaredTick;
        
        // War tactics (based on alignment)
        public bool TargetCivilians;         // Chaotic evil = true
        public bool UseEspionage;            // Lawful/subtle = true
        public bool AcceptSurrender;         // Lawful good = true
        
        // War progress
        public ushort EnemyMembersKilled;
        public ushort OwnMembersKilled;
        public ushort EmbassiesDestroyed;
    }
}

