using Unity.Entities;
using Unity.Collections;

namespace PureDOTS.Runtime.Operations
{
    /// <summary>
    /// Type of operation (blockade, siege, occupation, etc.).
    /// </summary>
    public enum OperationKind : byte
    {
        Blockade = 0,
        Siege = 1,
        Occupation = 2,
        Riot = 3,
        Protest = 4,
        CultRitual = 5,
        Funeral = 6,
        Festival = 7,
        Circus = 8,
        DeserterSettlement = 9
    }

    /// <summary>
    /// State of an operation lifecycle.
    /// </summary>
    public enum OperationState : byte
    {
        Planning = 0,
        Active = 1,
        Resolving = 2,
        Ended = 3
    }

    /// <summary>
    /// Tag component marking an entity as an operation.
    /// </summary>
    public struct OperationTag : IComponentData { }

    /// <summary>
    /// Core operation data.
    /// </summary>
    public struct Operation : IComponentData
    {
        public OperationKind Kind;
        public Entity InitiatorOrg;      // Organization starting the operation
        public Entity TargetOrg;          // Target organization (town, building owner, colony)
        public Entity TargetLocation;     // Specific village/colony/building/region entity
        public OperationState State;
        public uint StartedTick;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Rules of engagement and operation-specific parameters.
    /// </summary>
    public struct OperationRules : IComponentData
    {
        /// <summary>Operation severity (0-1, where 0=minimal, 1=maximum).</summary>
        public float Severity;
        
        /// <summary>Operation stance (0-1, where 0=light, 1=heavy).</summary>
        public float Stance;
        
        /// <summary>Allow humanitarian corridors (1=yes, 0=no).</summary>
        public byte AllowHumanitarianCorridors;
        
        /// <summary>Allow bombardment (1=yes, 0=no).</summary>
        public byte AllowBombardment;
        
        /// <summary>Time limit in ticks (0=no limit).</summary>
        public uint TimeLimitTicks;
        
        /// <summary>Success threshold (0-1).</summary>
        public float SuccessThreshold;
        
        /// <summary>Failure threshold (0-1).</summary>
        public float FailureThreshold;
    }

    /// <summary>
    /// Operation execution progress and metrics.
    /// </summary>
    public struct OperationProgress : IComponentData
    {
        /// <summary>Elapsed time in ticks.</summary>
        public uint ElapsedTicks;
        
        /// <summary>Success metric (0-1).</summary>
        public float SuccessMetric;
        
        /// <summary>Total casualties.</summary>
        public int Casualties;
        
        /// <summary>Unrest level (0-1).</summary>
        public float Unrest;
        
        /// <summary>Supply level for siege side (0-1).</summary>
        public float SiegeSupplyLevel;
        
        /// <summary>Supply level for inside target (0-1).</summary>
        public float TargetSupplyLevel;
        
        /// <summary>Morale of target population (0-1).</summary>
        public float TargetMorale;
    }

    /// <summary>
    /// Participant in an operation (band, army, civic group, etc.).
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct OperationParticipant : IBufferElementData
    {
        public Entity ParticipantEntity;  // Band, army, or group entity
        public FixedString32Bytes Role;  // "SiegeRing", "BlockadePatrol", "ProtestCrowd", etc.
        public float Contribution;       // Contribution weight (0-1)
    }

    /// <summary>
    /// Blockade-specific parameters.
    /// </summary>
    public struct BlockadeParams : IComponentData
    {
        /// <summary>Blockade scope (0=port, 1=city, 2=system, 3=region).</summary>
        public byte Scope;
        
        /// <summary>Who is blocked (flags: everyone, specific orgs, specific goods).</summary>
        public ushort BlockedTargets;
        
        /// <summary>Risk multiplier for routes (1.0 = no change, 2.0 = double risk).</summary>
        public float RiskMultiplier;
        
        /// <summary>Delay multiplier for routes (1.0 = no change, 2.0 = double delay).</summary>
        public float DelayMultiplier;
        
        /// <summary>Hard deny threshold (routes with risk > this are denied).</summary>
        public float HardDenyThreshold;
    }

    /// <summary>
    /// Siege-specific parameters.
    /// </summary>
    public struct SiegeParams : IComponentData
    {
        /// <summary>Encirclement completeness (0-1).</summary>
        public float EncirclementLevel;
        
        /// <summary>Minimum encirclement required (0-1).</summary>
        public float MinEncirclementRequired;
        
        /// <summary>Attrition rate per tick (0-1).</summary>
        public float AttritionRate;
        
        /// <summary>Famine threshold (supply level below this triggers famine).</summary>
        public float FamineThreshold;
        
        /// <summary>Disease risk multiplier.</summary>
        public float DiseaseRiskMultiplier;
    }

    /// <summary>
    /// Occupation-specific parameters.
    /// </summary>
    public struct OccupationParams : IComponentData
    {
        /// <summary>Occupation stance (0=light, 1=heavy).</summary>
        public float Stance;
        
        /// <summary>Law/order modifier (-1 to +1).</summary>
        public float LawOrderModifier;
        
        /// <summary>Crime modifier (-1 to +1).</summary>
        public float CrimeModifier;
        
        /// <summary>Unrest modifier (-1 to +1).</summary>
        public float UnrestModifier;
        
        /// <summary>Resistance cell spawn probability per tick.</summary>
        public float ResistanceSpawnProbability;
    }

    /// <summary>
    /// Protest/Riot-specific parameters.
    /// </summary>
    public struct ProtestRiotParams : IComponentData
    {
        /// <summary>Grievance level (0-1).</summary>
        public float GrievanceLevel;
        
        /// <summary>Crowd size (0-1).</summary>
        public float CrowdSize;
        
        /// <summary>Organization level (0=spontaneous, 1=highly organized).</summary>
        public float OrganizationLevel;
        
        /// <summary>Escalation threshold (grievance > this triggers riot).</summary>
        public float EscalationThreshold;
        
        /// <summary>Is currently a riot (1) or protest (0).</summary>
        public byte IsRiot;
    }

    /// <summary>
    /// Cult ritual-specific parameters.
    /// </summary>
    public struct CultRitualParams : IComponentData
    {
        /// <summary>Number of sacrifices.</summary>
        public int SacrificeCount;
        
        /// <summary>Ritual completion progress (0-1).</summary>
        public float CompletionProgress;
        
        /// <summary>Mana/favor gained.</summary>
        public float ManaGained;
        
        /// <summary>Area taint level (0-1).</summary>
        public float AreaTaint;
        
        /// <summary>Discovered by outsiders (1=yes, 0=no).</summary>
        public byte IsDiscovered;
    }

    /// <summary>
    /// Funeral-specific parameters.
    /// </summary>
    public struct FuneralParams : IComponentData
    {
        /// <summary>Deceased entity.</summary>
        public Entity DeceasedEntity;
        
        /// <summary>Renown level (0-1).</summary>
        public float RenownLevel;
        
        /// <summary>Procession progress (0-1).</summary>
        public float ProcessionProgress;
        
        /// <summary>Festival after funeral (1=yes, 0=no).</summary>
        public byte HasFestival;
        
        /// <summary>Legacy created (1=yes, 0=no).</summary>
        public byte LegacyCreated;
    }

    /// <summary>
    /// Festival/Circus-specific parameters.
    /// </summary>
    public struct FestivalParams : IComponentData
    {
        /// <summary>Festival type (0=circus, 1=festival, 2=pilgrimage market).</summary>
        public byte FestivalType;
        
        /// <summary>Duration in ticks.</summary>
        public uint DurationTicks;
        
        /// <summary>Trade multiplier (1.0 = no change, 2.0 = double trade).</summary>
        public float TradeMultiplier;
        
        /// <summary>Joy/happiness modifier (0-1).</summary>
        public float JoyModifier;
        
        /// <summary>Crime probability multiplier.</summary>
        public float CrimeProbabilityMultiplier;
        
        /// <summary>Recruitment probability per tick.</summary>
        public float RecruitmentProbability;
    }

    /// <summary>
    /// Deserter settlement-specific parameters.
    /// </summary>
    public struct DeserterSettlementParams : IComponentData
    {
        /// <summary>Original organization entity.</summary>
        public Entity OriginalOrg;
        
        /// <summary>Deserter count.</summary>
        public int DeserterCount;
        
        /// <summary>Settlement founded (1=yes, 0=no).</summary>
        public byte SettlementFounded;
        
        /// <summary>New organization entity (if founded).</summary>
        public Entity NewOrgEntity;
    }

    /// <summary>
    /// Operation request (temporary component processed by OperationInitSystem).
    /// </summary>
    public struct OperationRequest : IComponentData
    {
        public Entity InitiatorOrg;
        public Entity TargetOrg;
        public Entity TargetLocation;
        public OperationKind Kind;
        public uint RequestTick;
    }
}

