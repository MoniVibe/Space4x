using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Aggregates
{
    /// <summary>
    /// Tracks a villager's grievances and radicalization progress.
    /// When GrievanceLevel exceeds threshold, villager may radicalize and join a cell.
    /// </summary>
    public struct RadicalizationState : IComponentData
    {
        /// <summary>
        /// Accumulated grievance points (0-100).
        /// Higher = more likely to radicalize.
        /// </summary>
        public float GrievanceLevel;

        /// <summary>
        /// Radicalization threshold (personality-dependent, typically 40-80).
        /// Chaotic/rebellious villagers have lower thresholds.
        /// </summary>
        public float RadicalizationThreshold;

        /// <summary>
        /// Current radicalization stage.
        /// </summary>
        public RadicalizationStage Stage;

        /// <summary>
        /// Primary grievance driving radicalization.
        /// </summary>
        public GrievanceType PrimaryGrievance;

        /// <summary>
        /// Secondary grievance (if any).
        /// </summary>
        public GrievanceType SecondaryGrievance;

        /// <summary>
        /// Tick when radicalization began.
        /// </summary>
        public uint RadicalizedTick;

        /// <summary>
        /// Whether villager is currently part of a radical cell.
        /// </summary>
        public bool IsRadical;

        /// <summary>
        /// Whether villager is a cell leader.
        /// </summary>
        public bool IsCellLeader;

        /// <summary>
        /// Commitment to radical cause (0-100).
        /// Higher = harder to de-radicalize.
        /// </summary>
        public byte Commitment;

        /// <summary>
        /// Whether villager is willing to die for the cause.
        /// </summary>
        public bool WillingToDie;

        /// <summary>
        /// Whether villager is an infiltrator (double agent for authorities).
        /// </summary>
        public bool IsInfiltrator;
    }

    /// <summary>
    /// Radicalization progression stages.
    /// </summary>
    public enum RadicalizationStage : byte
    {
        Stable,         // No significant grievances (0-20)
        Discontented,   // Minor grievances, complains privately (21-40)
        Agitated,       // Significant grievances, openly critical (41-60)
        Radicalized,    // Joined a cell, willing to act against authority (61-80)
        Extremist       // Fully committed, willing to die for cause (81-100)
    }

    /// <summary>
    /// Types of grievances that drive radicalization.
    /// Multiple grievances can compound.
    /// </summary>
    [Flags]
    public enum GrievanceType : uint
    {
        None = 0,

        // Economic grievances
        Poverty = 1 << 0,              // Low wealth relative to village
        Unemployment = 1 << 1,         // No job or career
        Exploitation = 1 << 2,         // Unfair wages, overwork
        Taxation = 1 << 3,             // High tax burden
        Inequality = 1 << 4,           // Visible wealth gap

        // Social grievances
        Discrimination = 1 << 5,       // Unfair treatment based on identity
        Marginalization = 1 << 6,      // Excluded from social activities
        LackOfVoice = 1 << 7,          // No political representation
        CulturalSuppression = 1 << 8,  // Culture/language/customs banned

        // Authority grievances
        Authoritarianism = 1 << 9,     // Oppressive laws, no freedom
        Corruption = 1 << 10,          // Leaders enriching themselves
        Injustice = 1 << 11,           // Unfair punishments, no trial
        LackOfFreedom = 1 << 12,       // Restricted movement/speech

        // Material grievances
        Hunger = 1 << 13,              // Food scarcity, starvation
        Homelessness = 1 << 14,        // No shelter
        Illness = 1 << 15,             // No healthcare, disease
        Insecurity = 1 << 16,          // Constant danger, raids

        // Ideological grievances
        ReligiousPersecution = 1 << 17, // Faith suppressed or banned
        IdeologicalDifference = 1 << 18, // Disagrees with regime values
        Nationalism = 1 << 19,          // Opposes foreign rule
        ClassConflict = 1 << 20,        // Worker vs elite tension

        // Psychological grievances
        Humiliation = 1 << 21,         // Public shaming, honor loss
        Betrayal = 1 << 22,            // Trust violated by authority
        Revenge = 1 << 23,             // Seeks vengeance for wrong
        Despair = 1 << 24,             // Sees no future, hopelessness

        // Personal grievances
        FamilyHarm = 1 << 25,          // Family member killed/harmed
        PropertyLoss = 1 << 26,        // Property confiscated/destroyed
        FalseAccusation = 1 << 27,     // Wrongly accused of crime
    }

    /// <summary>
    /// Events that increase or decrease grievances.
    /// Tracked in buffer for historical analysis.
    /// </summary>
    public struct GrievanceEvent : IBufferElementData
    {
        public GrievanceType Type;
        public float Severity;           // 0-10 (how bad was it?)
        public uint OccurredTick;
        public Entity CausedBy;          // Leader, noble, system, etc.
        public FixedString64Bytes Description;

        /// <summary>
        /// Whether this event was public (witnessed by others).
        /// Public grievances can radicalize sympathizers.
        /// </summary>
        public bool IsPublic;

        /// <summary>
        /// Whether this event has been addressed/resolved.
        /// </summary>
        public bool Resolved;
    }

    /// <summary>
    /// A radical cell - small, semi-secretive group of radicalized individuals.
    /// Cells operate independently to undermine village authority.
    /// </summary>
    public struct RadicalCell : IComponentData
    {
        public enum CellType : byte
        {
            Agitators,      // Spread propaganda, recruit sympathizers
            Saboteurs,      // Destroy infrastructure (farms, markets, defenses)
            Rioters,        // Violence against authority, public disorder
            Infiltrators,   // Spy on leadership, gather intelligence
            Revolutionaries, // Seek to overthrow government
            Separatists,    // Want to leave village/form new one
            Cultists,       // Religious/ideological extremists
            Anarchists,     // Reject all authority and order
            Terrorists      // Maximum violence to create fear
        }

        public CellType Type;
        public FixedString64Bytes CellName; // "The Red Fist", "Children of Liberty"

        /// <summary>
        /// Cell ideology (what they believe in/oppose).
        /// </summary>
        public RadicalIdeology Ideology;

        /// <summary>
        /// Home village being undermined.
        /// </summary>
        public Entity TargetVillage;

        /// <summary>
        /// Cell secrecy level (0-100).
        /// Higher = harder for authorities to detect.
        /// Decreases with operations, increases with caution.
        /// </summary>
        public byte SecrecyLevel;

        /// <summary>
        /// Cell aggression level (0-100).
        /// Higher = more violent/destructive actions.
        /// </summary>
        public byte AggressionLevel;

        /// <summary>
        /// Number of members.
        /// </summary>
        public ushort MemberCount;

        /// <summary>
        /// When cell was formed.
        /// </summary>
        public uint FoundedTick;

        /// <summary>
        /// Whether cell has been discovered by authorities.
        /// </summary>
        public bool IsDiscovered;

        /// <summary>
        /// Whether authorities are actively suppressing this cell.
        /// </summary>
        public bool UnderSuppression;

        /// <summary>
        /// Cell resources (gold, weapons, supplies).
        /// </summary>
        public float Resources;

        /// <summary>
        /// Public support for cell (0-100).
        /// Sympathizers who don't actively join.
        /// </summary>
        public byte PublicSupport;

        /// <summary>
        /// Number of successful operations.
        /// </summary>
        public ushort SuccessfulOperations;

        /// <summary>
        /// Number of failed operations (caught/stopped).
        /// </summary>
        public ushort FailedOperations;
    }

    /// <summary>
    /// Radical ideologies - what cells believe in and fight for.
    /// Multiple ideologies can be combined.
    /// </summary>
    [Flags]
    public enum RadicalIdeology : uint
    {
        None = 0,

        // Economic ideologies
        AntiCapitalist = 1 << 0,       // Oppose merchant class, wealth accumulation
        AntiTax = 1 << 1,              // Oppose taxation
        Egalitarian = 1 << 2,          // Total economic equality
        WorkersRights = 1 << 3,        // Labor movement, unions
        Collectivist = 1 << 4,         // Common ownership

        // Political ideologies
        Anarchist = 1 << 5,            // No government at all
        Democratic = 1 << 6,           // Demand elections, representation
        Separatist = 1 << 7,           // Leave village/nation
        Revolutionary = 1 << 8,        // Overthrow current regime
        Monarchist = 1 << 9,           // Restore/install monarchy

        // Social ideologies
        Feminist = 1 << 10,            // Gender equality
        RacialJustice = 1 << 11,       // End discrimination
        Religious = 1 << 12,           // Faith-based movement
        Secular = 1 << 13,             // Oppose religious rule
        Traditionalist = 1 << 14,      // Return to old ways

        // Chaotic/destructive ideologies
        Nihilist = 1 << 15,            // Destroy everything, no goals
        Apocalyptic = 1 << 16,         // End times cult
        Reactionary = 1 << 17,         // Violently oppose change
        Supremacist = 1 << 18,         // Racial/cultural supremacy
        Misanthropic = 1 << 19,        // Hatred of humanity

        // Authority-focused
        AntiMonarchy = 1 << 20,        // Oppose kings/nobles
        AntiMilitary = 1 << 21,        // Oppose armed forces
        AntiMagic = 1 << 22,           // Fear/hate magic users
        AntiReligion = 1 << 23,        // Oppose organized faith
        AntiElite = 1 << 24,           // Oppose ruling class

        // Specific causes
        Environmental = 1 << 25,       // Protect nature from industry
        Pacifist = 1 << 26,            // Oppose all violence
        Militarist = 1 << 27,          // Glorify violence, war
        Nationalist = 1 << 28,         // Ethnic/national purity
    }

    /// <summary>
    /// Cell membership roster.
    /// </summary>
    public struct RadicalCellMember : IBufferElementData
    {
        public Entity VillagerEntity;
        public uint JoinedTick;
        public byte CommitmentLevel;    // 0-100
        public bool IsLeader;
        public bool IsInfiltrator;      // Double agent for authorities
        public bool WillingToDie;       // Accept suicide missions

        /// <summary>
        /// Role within cell.
        /// </summary>
        public CellRole Role;

        /// <summary>
        /// Operations participated in.
        /// </summary>
        public ushort OperationsCompleted;
    }

    /// <summary>
    /// Roles within a radical cell.
    /// </summary>
    public enum CellRole : byte
    {
        Leader,         // Organizes operations
        Recruiter,      // Finds new members
        Propagandist,   // Spreads ideology
        Combatant,      // Fights/riots
        Saboteur,       // Destroys infrastructure
        Spy,            // Gathers intelligence
        Financier,      // Raises funds
        Messenger       // Communication between cells
    }

    /// <summary>
    /// Active radical operation (riot, sabotage, assassination, etc.)
    /// Created when cell plans an action.
    /// </summary>
    public struct RadicalOperation : IComponentData
    {
        public enum OperationType : byte
        {
            Propaganda,         // Recruit, spread grievances, pamphlets
            Demonstration,      // Peaceful protest
            Riot,              // Violent uprising, street fighting
            Sabotage,          // Destroy infrastructure
            Assassination,     // Kill authority figures
            Theft,             // Steal resources/weapons
            Arson,             // Burn buildings
            Kidnapping,        // Take hostages
            Strike,            // Work stoppage, economic disruption
            Infiltration,      // Plant spies in government
            Bombing,           // Explosive attack
            Ambush             // Attack guards/soldiers
        }

        public OperationType Type;
        public Entity CellEntity;
        public Entity TargetEntity;      // Building, leader, resource, etc.
        public float3 TargetPosition;

        public uint OperationStartTick;
        public uint PlannedDuration;

        /// <summary>
        /// Number of cell members participating.
        /// </summary>
        public ushort ParticipantCount;

        /// <summary>
        /// Expected impact (0-100).
        /// How much damage/disruption.
        /// </summary>
        public byte ExpectedImpact;

        /// <summary>
        /// Chance of being caught (0-100).
        /// </summary>
        public byte DetectionRisk;

        /// <summary>
        /// Whether operation succeeded.
        /// </summary>
        public bool Succeeded;

        /// <summary>
        /// Whether authorities detected operation.
        /// </summary>
        public bool Detected;

        /// <summary>
        /// Whether operation was violent (casualties).
        /// </summary>
        public bool WasViolent;

        /// <summary>
        /// Casualties from operation.
        /// </summary>
        public ushort Casualties;
    }

    /// <summary>
    /// Cumulative impact of radical activities on village.
    /// Tracked per village.
    /// </summary>
    public struct RadicalImpact : IComponentData
    {
        /// <summary>
        /// Overall stability loss (0-100).
        /// High = village near collapse.
        /// </summary>
        public float StabilityLoss;

        /// <summary>
        /// Economic damage (gold value).
        /// </summary>
        public float EconomicDamage;

        /// <summary>
        /// Infrastructure damage (buildings destroyed/damaged).
        /// </summary>
        public ushort InfrastructureDamage;

        /// <summary>
        /// Authority figures killed.
        /// </summary>
        public ushort AuthorityDeaths;

        /// <summary>
        /// Civilian casualties from riots/fighting.
        /// </summary>
        public ushort CivilianCasualties;

        /// <summary>
        /// Villagers recruited to radical cells (cumulative).
        /// </summary>
        public ushort NewRadicals;

        /// <summary>
        /// Public support for radicals (0-100).
        /// Higher = more sympathizers, easier recruitment.
        /// </summary>
        public byte PublicSupport;

        /// <summary>
        /// Fear level in population (0-100).
        /// High fear can suppress or inflame radicalism.
        /// </summary>
        public byte PublicFear;

        /// <summary>
        /// Number of active cells.
        /// </summary>
        public ushort ActiveCells;

        /// <summary>
        /// Last operation tick.
        /// </summary>
        public uint LastOperationTick;
    }

    /// <summary>
    /// Village's policy toward radicals.
    /// Determines how authorities respond to radical activity.
    /// </summary>
    public struct RadicalResponsePolicy : IComponentData
    {
        public enum PolicyType : byte
        {
            Tolerance,      // Allow dissent, minimal intervention
            Surveillance,   // Monitor cells, gather intelligence
            Infiltration,   // Plant spies in cells
            Suppression,    // Arrest/exile/execute radicals
            Reform,         // Address grievances, reduce radicalization
            Negotiation,    // Talk to radical leaders, compromise
            Crackdown,      // Total martial law, extreme force
            Ignore          // Pretend problem doesn't exist
        }

        public PolicyType CurrentPolicy;

        /// <summary>
        /// Threshold of radical activity before policy escalates.
        /// (based on StabilityLoss)
        /// </summary>
        public byte EscalationThreshold;

        /// <summary>
        /// Whether village uses exile for radicals.
        /// </summary>
        public bool AllowsExile;

        /// <summary>
        /// Whether village uses execution for radicals.
        /// </summary>
        public bool AllowsExecution;

        /// <summary>
        /// Whether village uses torture for interrogation.
        /// </summary>
        public bool AllowsTorture;

        /// <summary>
        /// Whether village tolerates peaceful protest.
        /// </summary>
        public bool ToleratesProtest;

        /// <summary>
        /// Whether village attempts reform (address grievances).
        /// </summary>
        public bool AttemptsReform;

        /// <summary>
        /// Resources allocated to counter-radical operations (gold/day).
        /// </summary>
        public float CounterRadicalBudget;

        /// <summary>
        /// Effectiveness of counter-radical operations (0-100).
        /// </summary>
        public byte CounterRadicalEffectiveness;
    }

    /// <summary>
    /// Punishment for caught radicals.
    /// Created when radical is arrested/caught.
    /// </summary>
    public struct RadicalPunishment : IComponentData
    {
        public enum PunishmentType : byte
        {
            Warning,        // First offense, let go with warning
            Fine,           // Economic penalty
            Imprisonment,   // Jail time
            Exile,          // Banish from village permanently
            Execution,      // Death penalty
            Torture,        // Interrogation + pain
            PublicShaming,  // Humiliation (may increase grievances!)
            ForcedLabor,    // Work camp
            Rehabilitation  // Attempt to de-radicalize
        }

        public PunishmentType Type;
        public Entity TargetVillager;
        public uint PunishmentStartTick;
        public uint Duration;            // For imprisonment/labor (in ticks)

        /// <summary>
        /// Whether punishment is public (visible to others).
        /// Public punishment deters some, radicalizes others.
        /// </summary>
        public bool IsPublic;

        /// <summary>
        /// Deterrent effect on other potential radicals (0-100).
        /// </summary>
        public float DeterrentEffect;

        /// <summary>
        /// Martyrdom effect on sympathizers (0-100).
        /// Creates new radicals.
        /// </summary>
        public float MartyrdomEffect;

        /// <summary>
        /// Whether punishment created new grievances.
        /// </summary>
        public bool CreatedGrievances;

        /// <summary>
        /// Number of sympathizers who radicalized due to this punishment.
        /// </summary>
        public ushort NewRadicalsCreated;
    }

    /// <summary>
    /// Counter-radical operation by authorities.
    /// Villages can proactively fight cells.
    /// </summary>
    public struct CounterRadicalOperation : IComponentData
    {
        public enum OperationType : byte
        {
            Surveillance,   // Monitor cell activities
            Infiltration,   // Plant spy in cell
            Raid,          // Arrest cell members
            Propaganda,    // Counter radical messaging
            Reform,        // Address grievances
            Assassination  // Kill cell leaders (extreme)
        }

        public OperationType Type;
        public Entity TargetCell;
        public uint OperationStartTick;
        public uint Duration;

        /// <summary>
        /// Resources invested.
        /// </summary>
        public float Cost;

        /// <summary>
        /// Chance of success (0-100).
        /// </summary>
        public byte SuccessChance;

        /// <summary>
        /// Whether operation succeeded.
        /// </summary>
        public bool Succeeded;

        /// <summary>
        /// Cell members arrested/killed.
        /// </summary>
        public ushort MembersCaptured;

        /// <summary>
        /// Intelligence gathered.
        /// </summary>
        public byte IntelligenceGained;
    }

    /// <summary>
    /// De-radicalization progress for villagers undergoing rehabilitation.
    /// </summary>
    public struct DeRadicalizationProgress : IComponentData
    {
        /// <summary>
        /// Progress toward de-radicalization (0-100).
        /// </summary>
        public float Progress;

        /// <summary>
        /// Method being used.
        /// </summary>
        public DeRadicalizationMethod Method;

        /// <summary>
        /// Tick when started.
        /// </summary>
        public uint StartTick;

        /// <summary>
        /// Whether de-radicalization succeeded.
        /// </summary>
        public bool Succeeded;

        /// <summary>
        /// Whether villager relapsed (re-radicalized).
        /// </summary>
        public bool Relapsed;
    }

    /// <summary>
    /// De-radicalization methods.
    /// </summary>
    public enum DeRadicalizationMethod : byte
    {
        Education,      // Teach alternative ideology
        Therapy,        // Address psychological issues
        Employment,     // Provide economic opportunity
        Reconciliation, // Mediate with authorities
        Repression,     // Fear-based (torture)
        TimeAndForgiveness // Natural grievance decay
    }

    /// <summary>
    /// Tracks inter-cell relations (cells can cooperate or compete).
    /// </summary>
    public struct RadicalCellRelation : IBufferElementData
    {
        public Entity OtherCell;

        public enum RelationType : byte
        {
            Allied,         // Cooperate on operations
            Neutral,        // Ignore each other
            Competitive,    // Compete for recruits
            Hostile         // Actively undermine each other
        }

        public RelationType Relation;

        /// <summary>
        /// Joint operations conducted.
        /// </summary>
        public ushort JointOperations;
    }
}
