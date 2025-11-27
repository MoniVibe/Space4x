using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Category of dynamic event.
    /// </summary>
    public enum EventCategory : byte
    {
        None = 0,

        /// <summary>
        /// Artifact, derelict, anomaly discoveries.
        /// </summary>
        Discovery = 1,

        /// <summary>
        /// Schisms, coups, rebellions.
        /// </summary>
        Political = 2,

        /// <summary>
        /// Market crashes, booms, embargoes.
        /// </summary>
        Economic = 3,

        /// <summary>
        /// Invasions, plagues, disasters.
        /// </summary>
        Crisis = 4,

        /// <summary>
        /// First contact, migration, species events.
        /// </summary>
        Alien = 5,

        /// <summary>
        /// Tech breakthroughs, malfunctions.
        /// </summary>
        Technology = 6,

        /// <summary>
        /// Natural phenomena, stellar events.
        /// </summary>
        Celestial = 7,

        /// <summary>
        /// Faction-specific story events.
        /// </summary>
        Story = 8
    }

    /// <summary>
    /// Severity of event.
    /// </summary>
    public enum EventSeverity : byte
    {
        Minor = 0,
        Moderate = 1,
        Major = 2,
        Critical = 3,
        Catastrophic = 4
    }

    /// <summary>
    /// Event lifecycle phase.
    /// </summary>
    public enum EventPhase : byte
    {
        Triggered = 0,
        Announced = 1,
        Active = 2,
        AwaitingChoice = 3,
        Resolving = 4,
        Completed = 5,
        Failed = 6
    }

    /// <summary>
    /// Active event instance.
    /// </summary>
    public struct Space4XEvent : IComponentData
    {
        /// <summary>
        /// Event type identifier.
        /// </summary>
        public ushort EventTypeId;

        /// <summary>
        /// Event category.
        /// </summary>
        public EventCategory Category;

        /// <summary>
        /// Event severity.
        /// </summary>
        public EventSeverity Severity;

        /// <summary>
        /// Current phase.
        /// </summary>
        public EventPhase Phase;

        /// <summary>
        /// Affected faction ID.
        /// </summary>
        public ushort AffectedFactionId;

        /// <summary>
        /// Target entity (location, fleet, colony, etc.).
        /// </summary>
        public Entity TargetEntity;

        /// <summary>
        /// Secondary target entity.
        /// </summary>
        public Entity SecondaryTarget;

        /// <summary>
        /// Target location.
        /// </summary>
        public float3 TargetLocation;

        /// <summary>
        /// Tick when event triggered.
        /// </summary>
        public uint TriggeredTick;

        /// <summary>
        /// Tick when event expires/auto-resolves.
        /// </summary>
        public uint ExpirationTick;

        /// <summary>
        /// Remaining time for player decision.
        /// </summary>
        public uint DecisionTimer;

        /// <summary>
        /// Selected choice index (-1 = none).
        /// </summary>
        public sbyte SelectedChoice;

        /// <summary>
        /// Whether event has been acknowledged by player.
        /// </summary>
        public byte IsAcknowledged;

        /// <summary>
        /// Random seed for event outcomes.
        /// </summary>
        public uint RandomSeed;
    }

    /// <summary>
    /// Event choice option.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct EventChoice : IBufferElementData
    {
        /// <summary>
        /// Choice index.
        /// </summary>
        public byte ChoiceIndex;

        /// <summary>
        /// Required resource type (if any).
        /// </summary>
        public MarketResourceType RequiredResourceType;

        /// <summary>
        /// Required resource amount.
        /// </summary>
        public float RequiredResourceAmount;

        /// <summary>
        /// Required credits.
        /// </summary>
        public float RequiredCredits;

        /// <summary>
        /// Success chance [0, 1].
        /// </summary>
        public half SuccessChance;

        /// <summary>
        /// Whether choice is available.
        /// </summary>
        public byte IsAvailable;

        /// <summary>
        /// Skill type that affects success (0 = none).
        /// </summary>
        public byte RelevantSkillType;
    }

    /// <summary>
    /// Event outcome effect.
    /// </summary>
    [InternalBufferCapacity(6)]
    public struct EventOutcome : IBufferElementData
    {
        /// <summary>
        /// Outcome type.
        /// </summary>
        public EventOutcomeType Type;

        /// <summary>
        /// Target of the outcome.
        /// </summary>
        public EventOutcomeTarget Target;

        /// <summary>
        /// Numeric value/modifier.
        /// </summary>
        public float Value;

        /// <summary>
        /// Secondary value.
        /// </summary>
        public float SecondaryValue;

        /// <summary>
        /// Duration in ticks (0 = permanent).
        /// </summary>
        public uint Duration;

        /// <summary>
        /// Probability this outcome occurs.
        /// </summary>
        public half Probability;

        /// <summary>
        /// Whether outcome is for success (1) or failure (0).
        /// </summary>
        public byte IsSuccessOutcome;
    }

    /// <summary>
    /// Type of event outcome.
    /// </summary>
    public enum EventOutcomeType : byte
    {
        None = 0,

        // Resources
        GainCredits = 1,
        LoseCredits = 2,
        GainResource = 3,
        LoseResource = 4,

        // Military
        GainFleet = 10,
        LoseFleet = 11,
        DamageFleet = 12,
        RepairFleet = 13,
        SpawnHostiles = 14,

        // Relations
        GainRelation = 20,
        LoseRelation = 21,
        DeclareWar = 22,
        GainTrust = 23,
        LoseTrust = 24,

        // Territory
        GainColony = 30,
        LoseColony = 31,
        DamageColony = 32,
        SpawnAnomaly = 33,

        // Population
        GainPopulation = 40,
        LosePopulation = 41,
        GainMorale = 42,
        LoseMorale = 43,

        // Technology
        UnlockTech = 50,
        GainResearchPoints = 51,
        LoseResearchProgress = 52,

        // Status effects
        ApplyBuff = 60,
        ApplyDebuff = 61,
        TriggerSituation = 62,
        SpawnEvent = 63
    }

    /// <summary>
    /// Target of event outcome.
    /// </summary>
    public enum EventOutcomeTarget : byte
    {
        Faction = 0,
        TargetEntity = 1,
        SecondaryEntity = 2,
        AllFleets = 3,
        AllColonies = 4,
        RandomFleet = 5,
        RandomColony = 6,
        Location = 7
    }

    /// <summary>
    /// Trigger condition for events.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct EventTriggerCondition : IBufferElementData
    {
        /// <summary>
        /// Condition type.
        /// </summary>
        public EventConditionType Type;

        /// <summary>
        /// Comparison operator.
        /// </summary>
        public ComparisonOperator Operator;

        /// <summary>
        /// Threshold value.
        /// </summary>
        public float ThresholdValue;

        /// <summary>
        /// Optional target type.
        /// </summary>
        public byte TargetType;
    }

    /// <summary>
    /// Type of event trigger condition.
    /// </summary>
    public enum EventConditionType : byte
    {
        None = 0,

        // Time-based
        TickCount = 1,
        RandomChance = 2,
        Cooldown = 3,

        // Resource-based
        Credits = 10,
        ResourceLevel = 11,
        IncomeRate = 12,

        // Military
        FleetCount = 20,
        FleetStrength = 21,
        InCombat = 22,

        // Territory
        ColonyCount = 30,
        PopulationTotal = 31,
        TerritorySize = 32,

        // Relations
        RelationScore = 40,
        AtWar = 41,
        HasTreaty = 42,

        // State
        MoraleLevel = 50,
        SituationActive = 51,
        TechLevel = 52
    }

    /// <summary>
    /// Comparison operator for conditions.
    /// </summary>
    public enum ComparisonOperator : byte
    {
        Equal = 0,
        NotEqual = 1,
        GreaterThan = 2,
        LessThan = 3,
        GreaterOrEqual = 4,
        LessOrEqual = 5
    }

    /// <summary>
    /// Event definition template (for spawning events).
    /// </summary>
    public struct EventDefinition : IComponentData
    {
        /// <summary>
        /// Unique event type identifier.
        /// </summary>
        public ushort EventTypeId;

        /// <summary>
        /// Event category.
        /// </summary>
        public EventCategory Category;

        /// <summary>
        /// Base severity.
        /// </summary>
        public EventSeverity BaseSeverity;

        /// <summary>
        /// Base trigger probability per check.
        /// </summary>
        public half BaseProbability;

        /// <summary>
        /// Minimum ticks between triggers.
        /// </summary>
        public uint CooldownTicks;

        /// <summary>
        /// Last trigger tick.
        /// </summary>
        public uint LastTriggeredTick;

        /// <summary>
        /// Duration of active event.
        /// </summary>
        public uint Duration;

        /// <summary>
        /// Time given for player decision.
        /// </summary>
        public uint DecisionTime;

        /// <summary>
        /// Whether event can repeat.
        /// </summary>
        public byte CanRepeat;

        /// <summary>
        /// Whether event auto-resolves if no choice made.
        /// </summary>
        public byte AutoResolves;

        /// <summary>
        /// Default choice if auto-resolved.
        /// </summary>
        public byte DefaultChoice;

        /// <summary>
        /// Total times this event has triggered.
        /// </summary>
        public ushort TriggerCount;
    }

    /// <summary>
    /// History entry for completed events.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct EventHistoryEntry : IBufferElementData
    {
        /// <summary>
        /// Event type that occurred.
        /// </summary>
        public ushort EventTypeId;

        /// <summary>
        /// Category.
        /// </summary>
        public EventCategory Category;

        /// <summary>
        /// Severity when it occurred.
        /// </summary>
        public EventSeverity Severity;

        /// <summary>
        /// Choice made (if any).
        /// </summary>
        public sbyte ChoiceMade;

        /// <summary>
        /// Whether outcome was successful.
        /// </summary>
        public byte WasSuccessful;

        /// <summary>
        /// Tick when event completed.
        /// </summary>
        public uint CompletedTick;

        /// <summary>
        /// Primary numeric result.
        /// </summary>
        public float ResultValue;
    }

    /// <summary>
    /// Tag for entities that can trigger events.
    /// </summary>
    public struct EventTriggerCapable : IComponentData
    {
        /// <summary>
        /// Event categories this entity can trigger.
        /// </summary>
        public byte EnabledCategories;

        /// <summary>
        /// Multiplier for trigger probability.
        /// </summary>
        public half ProbabilityMultiplier;
    }

    /// <summary>
    /// Event math utilities (candidates for PureDOTS extraction).
    /// </summary>
    public static class EventMath
    {
        /// <summary>
        /// Evaluates a condition against a value.
        /// </summary>
        public static bool EvaluateCondition(float value, ComparisonOperator op, float threshold)
        {
            return op switch
            {
                ComparisonOperator.Equal => math.abs(value - threshold) < 0.001f,
                ComparisonOperator.NotEqual => math.abs(value - threshold) >= 0.001f,
                ComparisonOperator.GreaterThan => value > threshold,
                ComparisonOperator.LessThan => value < threshold,
                ComparisonOperator.GreaterOrEqual => value >= threshold,
                ComparisonOperator.LessOrEqual => value <= threshold,
                _ => false
            };
        }

        /// <summary>
        /// Calculates modified trigger probability.
        /// </summary>
        public static float CalculateTriggerProbability(float baseProbability, float multiplier, uint ticksSinceLast, uint cooldown)
        {
            if (ticksSinceLast < cooldown)
            {
                return 0f;
            }

            float timeFactor = math.min(2f, 1f + (ticksSinceLast - cooldown) / 10000f);
            return math.saturate(baseProbability * multiplier * timeFactor);
        }

        /// <summary>
        /// Rolls for success based on probability.
        /// </summary>
        public static bool RollSuccess(float probability, uint seed)
        {
            var random = new Unity.Mathematics.Random(seed);
            return random.NextFloat() < probability;
        }

        /// <summary>
        /// Calculates choice success chance with skill modifier.
        /// </summary>
        public static float CalculateChoiceSuccess(float baseChance, float skillLevel, EventSeverity severity)
        {
            float severityPenalty = severity switch
            {
                EventSeverity.Minor => 0f,
                EventSeverity.Moderate => 0.05f,
                EventSeverity.Major => 0.15f,
                EventSeverity.Critical => 0.25f,
                EventSeverity.Catastrophic => 0.4f,
                _ => 0f
            };

            float skillBonus = skillLevel * 0.2f;

            return math.saturate(baseChance + skillBonus - severityPenalty);
        }

        /// <summary>
        /// Determines outcome value variance.
        /// </summary>
        public static float CalculateOutcomeVariance(float baseValue, uint seed, EventSeverity severity)
        {
            var random = new Unity.Mathematics.Random(seed);
            float variance = random.NextFloat(-0.2f, 0.2f);

            // Higher severity = more variance
            float severityMod = 1f + (int)severity * 0.1f;

            return baseValue * (1f + variance * severityMod);
        }
    }
}

