using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Lifecycle
{
    /// <summary>
    /// Stage in an entity's lifecycle.
    /// </summary>
    public enum LifecycleStage : byte
    {
        Nascent = 0,        // Just created, initializing
        Seed = 1,           // Dormant, not yet active
        Juvenile = 2,       // Young, still growing
        Mature = 3,         // Fully developed
        Elder = 4,          // Aging, past prime
        Decaying = 5,       // Breaking down
        Dormant = 6,        // Hibernating/inactive
        Transformed = 7     // Has undergone metamorphosis
    }

    /// <summary>
    /// Type of lifecycle progression.
    /// </summary>
    public enum LifecycleType : byte
    {
        Linear = 0,         // Progresses through stages sequentially
        Cyclical = 1,       // Loops back (e.g., seasons, respawn)
        Branching = 2,      // Can evolve into different forms
        Terminal = 3        // Ends at final stage
    }

    /// <summary>
    /// Trigger for stage advancement.
    /// </summary>
    public enum StageTrigger : byte
    {
        Age = 0,            // Time-based
        Condition = 1,      // Environmental/health threshold
        Resource = 2,       // Resource accumulation
        Event = 3,          // External trigger
        Manual = 4          // Player/system initiated
    }

    /// <summary>
    /// Current lifecycle state of an entity.
    /// </summary>
    public struct LifecycleState : IComponentData
    {
        public LifecycleStage CurrentStage;
        public LifecycleType Type;
        public float StageProgress;             // 0-1 progress through current stage
        public float TotalAge;                  // Total ticks since birth
        public uint StageEnteredTick;           // When entered current stage
        public uint BirthTick;                  // When created
        public byte StageCount;                 // How many stages experienced
        public byte CanAdvance;                 // Eligible for next stage
        public byte IsFrozen;                   // Lifecycle paused
    }

    /// <summary>
    /// Configuration for lifecycle stages.
    /// </summary>
    public struct LifecycleConfig : IComponentData
    {
        public LifecycleType Type;
        public StageTrigger AdvanceTrigger;
        public float JuvenileDuration;          // Ticks in juvenile stage
        public float MatureDuration;            // Ticks in mature stage
        public float ElderDuration;             // Ticks in elder stage
        public float DecayDuration;             // Ticks before removal
        public float ProgressRate;              // Progress per tick
        public byte MaxStage;                   // Highest reachable stage
    }

    /// <summary>
    /// Stage-specific modifiers.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct LifecycleStageModifier : IBufferElementData
    {
        public LifecycleStage Stage;
        public float GrowthModifier;            // Affects growth rate
        public float ProductivityModifier;      // Affects output
        public float ConsumptionModifier;       // Affects resource needs
        public float DefenseModifier;           // Affects survivability
        public float ReproductionChance;        // Can spawn offspring
    }

    /// <summary>
    /// Metamorphosis capability and state.
    /// </summary>
    public struct Metamorphosis : IComponentData
    {
        public FixedString64Bytes TargetTypeId; // What to transform into
        public float TransformProgress;         // 0-1 transformation progress
        public float TransformDuration;         // Ticks to complete
        public uint TransformStartTick;
        public byte IsTransforming;
        public byte PreserveStats;              // Keep stats through transform
        public byte PreserveInventory;          // Keep items through transform
    }

    /// <summary>
    /// Evolution path options.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct EvolutionPath : IBufferElementData
    {
        public FixedString64Bytes EvolutionId;  // Unique evolution identifier
        public FixedString64Bytes TargetTypeId; // What this evolves into
        public FixedString64Bytes RequirementId;// Condition to unlock
        public float UnlockProgress;            // 0-1 progress to unlock
        public byte IsUnlocked;
        public byte IsSelected;                 // Player/AI has chosen this
    }

    /// <summary>
    /// Evolution requirements.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct EvolutionRequirement : IBufferElementData
    {
        public FixedString64Bytes RequirementId;
        public ushort ResourceTypeId;           // Required resource
        public float RequiredAmount;            // Amount needed
        public float CurrentAmount;             // Amount accumulated
        public byte IsMet;
    }

    /// <summary>
    /// Ascension/promotion state for entity transcendence.
    /// </summary>
    public struct AscensionState : IComponentData
    {
        public byte AscensionLevel;             // Current ascension tier
        public byte MaxAscensionLevel;          // Highest reachable
        public float AscensionProgress;         // Progress to next level
        public float AscensionThreshold;        // Required progress
        public uint LastAscensionTick;
        public FixedString32Bytes AscensionTitle; // Rank/title from ascension
        public byte IsAscending;                // Currently in ascension process
    }

    /// <summary>
    /// Bonuses granted by ascension levels.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct AscensionBonus : IBufferElementData
    {
        public byte Level;                      // Required ascension level
        public FixedString32Bytes BonusId;      // Bonus identifier
        public float Magnitude;                 // Bonus strength
        public byte IsActive;
    }

    /// <summary>
    /// Aging effects on an entity.
    /// </summary>
    public struct AgingEffects : IComponentData
    {
        public float VitalityModifier;          // Health/stamina modifier
        public float WisdomModifier;            // Experience bonus
        public float StrengthModifier;          // Physical capability
        public float FertilityModifier;         // Reproduction chance
        public float DecayRate;                 // How fast deteriorating
    }

    /// <summary>
    /// Death/removal configuration.
    /// </summary>
    public struct MortalityConfig : IComponentData
    {
        public float NaturalLifespan;           // Expected lifespan in ticks
        public float LifespanVariance;          // Random variance
        public float DeathChancePerTick;        // Chance of natural death
        public float MinimumAge;                // Cannot die before this
        public byte CanDieOfAge;
        public byte CanResurrect;
        public byte LeavesCorpse;
    }

    /// <summary>
    /// Reproduction state.
    /// </summary>
    public struct ReproductionState : IComponentData
    {
        public float MaturityAge;               // Age when can reproduce
        public float ReproductionCooldown;      // Ticks between offspring
        public float LastReproductionTick;
        public byte OffspringCount;             // Total offspring produced
        public byte MaxOffspring;               // Lifetime limit
        public byte CanReproduce;               // Currently able
        public byte IsPregnant;                 // Carrying offspring
    }

    /// <summary>
    /// Offspring configuration.
    /// </summary>
    public struct OffspringConfig : IComponentData
    {
        public FixedString64Bytes OffspringTypeId;  // What type of entity to spawn
        public byte MinOffspring;               // Minimum per reproduction
        public byte MaxOffspring;               // Maximum per reproduction
        public float GestationTicks;            // Time to produce
        public float InheritanceStrength;       // How much stats inherited
        public float MutationChance;            // Chance of variation
    }

    /// <summary>
    /// Registry for lifecycle entities.
    /// </summary>
    public struct LifecycleRegistry : IComponentData
    {
        public int TotalEntities;
        public int NascentCount;
        public int JuvenileCount;
        public int MatureCount;
        public int ElderCount;
        public int DecayingCount;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Entry in lifecycle registry.
    /// </summary>
    [InternalBufferCapacity(64)]
    public struct LifecycleEntry : IBufferElementData
    {
        public Entity Entity;
        public LifecycleStage Stage;
        public float Age;
        public float StageProgress;
        public byte HasEvolutionPath;
    }
}

