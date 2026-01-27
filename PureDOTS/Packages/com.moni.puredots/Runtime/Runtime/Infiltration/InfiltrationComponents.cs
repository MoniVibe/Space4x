using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Infiltration
{
    /// <summary>
    /// Infiltration access tier - progression from contact to subversion.
    /// </summary>
    public enum InfiltrationLevel : byte
    {
        None = 0,           // No access
        Contact = 1,        // Initial contact, public data
        Embedded = 2,       // Inside organization, local intel
        Trusted = 3,        // Trusted member, intercept comms
        Influential = 4,    // Key position, steal secrets
        Subverted = 5       // Command access, issue false orders
    }

    /// <summary>
    /// Method used for infiltration.
    /// </summary>
    public enum InfiltrationMethod : byte
    {
        None = 0,
        Conscription = 1,   // Join military/workforce
        Celebrity = 2,      // Fame/popularity as cover
        Hacking = 3,        // Digital infiltration
        Blackmail = 4,      // Coerce cooperation
        Cultural = 5,       // Adopt rival customs
        Bribery = 6,        // Pay for access
        Seduction = 7,      // Romance-based access
        Forgery = 8         // Fake credentials
    }

    /// <summary>
    /// Current infiltration state for an agent.
    /// </summary>
    public struct InfiltrationState : IComponentData
    {
        public Entity TargetOrganization;  // Organization being infiltrated
        public InfiltrationLevel Level;
        public InfiltrationMethod Method;
        public float Progress;             // 0-1 progress to next level
        public float SuspicionLevel;       // 0-1 how suspicious target is
        public float CoverStrength;        // 0-1 quality of cover identity
        public uint InfiltrationStartTick;
        public uint LastActivityTick;
        public byte IsExposed;             // Cover blown
        public byte IsExtracting;          // Currently escaping
    }

    /// <summary>
    /// Counter-intelligence capabilities of an organization.
    /// </summary>
    public struct CounterIntelligence : IComponentData
    {
        public float DetectionRate;        // Base detection chance per tick
        public float SuspicionGrowth;      // How fast suspicion builds
        public float SuspicionDecayRate;   // Natural suspicion decay per tick
        public float InvestigationPower;   // Effectiveness of active hunts
        public byte SecurityLevel;         // 0-10 overall security tier
        public uint LastSweepTick;
        public byte ActiveMeasures;        // Active counter-intel operations
    }

    /// <summary>
    /// Cover identity for an infiltrating agent.
    /// </summary>
    public struct CoverIdentity : IComponentData
    {
        public FixedString64Bytes CoverName;
        public FixedString64Bytes CoverRole;
        public float Credibility;         // How believable (0-1), affects suspicion gain
        public float Authenticity;         // How authentic (0-1), affects detection
        public float Depth;                // How detailed the backstory (0-1)
        public uint CoverEstablishedTick;
        public uint CreatedTick;
        public uint LastVerifiedTick;
        public byte HasDocuments;          // Forged credentials
        public byte HasContacts;           // Supporting network
    }

    /// <summary>
    /// Extraction plan for when cover is blown.
    /// </summary>
    public struct ExtractionPlan : IComponentData
    {
        public Entity SafeHouseEntity;     // Where to flee
        public Entity ExfilContactEntity;  // Who helps extract
        public float3 ExtractionPoint;    // Backup position
        public float3 ExfilPosition;      // Primary extraction position
        public float SuccessChance;        // Calculated extraction odds
        public byte PlanQuality;           // 0-10 how well planned
        public uint PlannedExtractionTick;
        public ExtractionStatus Status;
        public byte IsActivated;          // Currently executing
    }

    /// <summary>
    /// Extraction status.
    /// </summary>
    public enum ExtractionStatus : byte
    {
        None = 0,
        Planned = 1,
        InProgress = 2,
        Completed = 3,
        Failed = 4
    }

    /// <summary>
    /// Intelligence gathered through infiltration.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct GatheredIntel : IBufferElementData
    {
        public FixedString64Bytes IntelId;
        public IntelType Type;
        public Entity SourceEntity;
        public InfiltrationLevel RequiredLevel;  // Minimum level needed to gather
        public float Value;                     // How valuable (affects rewards)
        public uint GatheredTick;
        public byte IsVerified;
        public byte IsStale;                     // Too old to be useful
    }

    /// <summary>
    /// Intelligence types.
    /// </summary>
    public enum IntelType : byte
    {
        Military = 0,
        Economic = 1,
        Political = 2,
        Technological = 3,
        Social = 4
    }

    /// <summary>
    /// Active counterintel investigation.
    /// </summary>
    public struct Investigation : IComponentData
    {
        public Entity SuspectEntity;       // Who is being investigated
        public float InvestigationProgress; // 0-1 investigation completion
        public float Evidence;             // 0-1 evidence gathered
        public uint InvestigationStartTick;
        public uint StartTick;
        public InvestigationStatus Status;
        public byte IsActive;
    }

    /// <summary>
    /// Investigation status.
    /// </summary>
    public enum InvestigationStatus : byte
    {
        None = 0,
        Suspicious = 1,
        UnderInvestigation = 2,
        Confirmed = 3,
        Cleared = 4
    }
}



