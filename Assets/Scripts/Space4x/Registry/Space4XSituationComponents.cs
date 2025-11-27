using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Type of crisis or opportunity situation.
    /// </summary>
    public enum SituationType : byte
    {
        None = 0,

        // Resource crises
        EnergyCrisis = 1,
        SupplyShortage = 2,
        FuelDepletion = 3,

        // Social crises
        MoraleCollapse = 10,
        MutinyRisk = 11,
        Rebellion = 12,

        // Economic crises
        EconomicRecession = 20,
        TradeDisruption = 21,
        Inflation = 22,

        // Military crises
        PirateIncursion = 30,
        EnemyAttack = 31,
        Blockade = 32,

        // Environmental crises
        SpaceStorm = 40,
        Anomaly = 41,
        Contamination = 42,

        // Opportunities
        ResourceBonanza = 100,
        TechBreakthrough = 101,
        DiplomaticOpening = 102,
        TradeOpportunity = 103
    }

    /// <summary>
    /// Current phase of a situation.
    /// </summary>
    public enum SituationPhase : byte
    {
        /// <summary>
        /// Situation detected but not yet escalated. Minimal impact.
        /// </summary>
        Detection = 0,

        /// <summary>
        /// Situation escalating. Penalties/effects beginning.
        /// </summary>
        Escalation = 1,

        /// <summary>
        /// Crisis peak. Must resolve or face consequences.
        /// </summary>
        Climax = 2,

        /// <summary>
        /// Resolution in progress. Outcomes being applied.
        /// </summary>
        Aftermath = 3,

        /// <summary>
        /// Situation fully resolved.
        /// </summary>
        Resolved = 4
    }

    /// <summary>
    /// Outcome of a situation resolution.
    /// </summary>
    public enum SituationOutcome : byte
    {
        None = 0,
        Success = 1,         // Fully resolved, positive outcome
        PartialSuccess = 2,  // Resolved with compromises
        Failure = 3,         // Failed to resolve, negative outcome
        Catastrophe = 4,     // Worst case scenario
        Transformed = 5      // Situation evolved into something else
    }

    /// <summary>
    /// Current state of a situation entity.
    /// </summary>
    public struct SituationState : IComponentData
    {
        /// <summary>
        /// Type of this situation.
        /// </summary>
        public SituationType Type;

        /// <summary>
        /// Current phase.
        /// </summary>
        public SituationPhase Phase;

        /// <summary>
        /// Severity level [0, 1]. Higher = more urgent.
        /// </summary>
        public half Severity;

        /// <summary>
        /// Progress through current phase [0, 1].
        /// </summary>
        public half PhaseProgress;

        /// <summary>
        /// Final outcome (only valid when Phase == Resolved).
        /// </summary>
        public SituationOutcome Outcome;

        /// <summary>
        /// Entity affected by this situation (colony, fleet, carrier).
        /// </summary>
        public Entity AffectedEntity;

        /// <summary>
        /// Tick when situation was created.
        /// </summary>
        public uint CreatedTick;

        /// <summary>
        /// Tick when current phase started.
        /// </summary>
        public uint PhaseStartTick;

        public static SituationState Create(SituationType type, Entity affected, float severity, uint tick)
        {
            return new SituationState
            {
                Type = type,
                Phase = SituationPhase.Detection,
                Severity = (half)math.clamp(severity, 0f, 1f),
                PhaseProgress = (half)0f,
                Outcome = SituationOutcome.None,
                AffectedEntity = affected,
                CreatedTick = tick,
                PhaseStartTick = tick
            };
        }
    }

    /// <summary>
    /// Timer configuration for situation phase progression.
    /// </summary>
    public struct SituationTimer : IComponentData
    {
        /// <summary>
        /// Duration of Detection phase in seconds.
        /// </summary>
        public float DetectionDuration;

        /// <summary>
        /// Duration of Escalation phase in seconds.
        /// </summary>
        public float EscalationDuration;

        /// <summary>
        /// Duration of Climax phase in seconds.
        /// </summary>
        public float ClimaxDuration;

        /// <summary>
        /// Duration of Aftermath phase in seconds.
        /// </summary>
        public float AftermathDuration;

        /// <summary>
        /// Time elapsed in current phase.
        /// </summary>
        public float ElapsedTime;

        /// <summary>
        /// Whether auto-escalation is enabled.
        /// </summary>
        public byte AutoEscalate;

        public static SituationTimer Default => new SituationTimer
        {
            DetectionDuration = 30f,
            EscalationDuration = 60f,
            ClimaxDuration = 90f,
            AftermathDuration = 30f,
            ElapsedTime = 0f,
            AutoEscalate = 1
        };

        public static SituationTimer Fast => new SituationTimer
        {
            DetectionDuration = 10f,
            EscalationDuration = 20f,
            ClimaxDuration = 30f,
            AftermathDuration = 10f,
            ElapsedTime = 0f,
            AutoEscalate = 1
        };

        public static SituationTimer Slow => new SituationTimer
        {
            DetectionDuration = 60f,
            EscalationDuration = 120f,
            ClimaxDuration = 180f,
            AftermathDuration = 60f,
            ElapsedTime = 0f,
            AutoEscalate = 1
        };

        public float GetPhaseDuration(SituationPhase phase)
        {
            return phase switch
            {
                SituationPhase.Detection => DetectionDuration,
                SituationPhase.Escalation => EscalationDuration,
                SituationPhase.Climax => ClimaxDuration,
                SituationPhase.Aftermath => AftermathDuration,
                _ => 0f
            };
        }
    }

    /// <summary>
    /// Effects applied by a situation during each phase.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct SituationEffect : IBufferElementData
    {
        /// <summary>
        /// Phase during which this effect is active.
        /// </summary>
        public SituationPhase Phase;

        /// <summary>
        /// Type of effect.
        /// </summary>
        public SituationEffectType EffectType;

        /// <summary>
        /// Magnitude of the effect.
        /// </summary>
        public half Magnitude;

        /// <summary>
        /// Target component/system affected.
        /// </summary>
        public byte TargetIndex;
    }

    /// <summary>
    /// Types of effects situations can apply.
    /// </summary>
    public enum SituationEffectType : byte
    {
        None = 0,

        // Resource effects
        ResourceDrain = 1,
        ResourceBonus = 2,
        ProductionPenalty = 3,
        ProductionBonus = 4,

        // Social effects
        MoralePenalty = 10,
        MoraleBonus = 11,
        LoyaltyPenalty = 12,
        LoyaltyBonus = 13,

        // Combat effects
        CombatPenalty = 20,
        CombatBonus = 21,
        DefensePenalty = 22,
        DefenseBonus = 23,

        // Movement effects
        SpeedPenalty = 30,
        SpeedBonus = 31,
        RangePenalty = 32,
        RangeBonus = 33
    }

    /// <summary>
    /// Resolution option for a situation.
    /// </summary>
    [InternalBufferCapacity(3)]
    public struct SituationResolutionOption : IBufferElementData
    {
        /// <summary>
        /// Unique ID for this option.
        /// </summary>
        public byte OptionId;

        /// <summary>
        /// Resource cost to attempt this resolution.
        /// </summary>
        public float ResourceCost;

        /// <summary>
        /// Base success chance [0, 1].
        /// </summary>
        public half SuccessChance;

        /// <summary>
        /// Likely outcome on success.
        /// </summary>
        public SituationOutcome SuccessOutcome;

        /// <summary>
        /// Likely outcome on failure.
        /// </summary>
        public SituationOutcome FailureOutcome;
    }

    /// <summary>
    /// Telemetry snapshot for active situations.
    /// </summary>
    public struct SituationTelemetry : IComponentData
    {
        public int ActiveSituationCount;
        public int CrisisCount;
        public int OpportunityCount;
        public int ResolvedThisTick;
        public int EscalatedThisTick;
        public uint LastUpdateTick;

        public static SituationTelemetry Default => new SituationTelemetry
        {
            ActiveSituationCount = 0,
            CrisisCount = 0,
            OpportunityCount = 0,
            ResolvedThisTick = 0,
            EscalatedThisTick = 0,
            LastUpdateTick = 0
        };
    }

    /// <summary>
    /// Utility helpers for situation logic.
    /// </summary>
    public static class SituationUtility
    {
        /// <summary>
        /// Checks if a situation type is a crisis (negative).
        /// </summary>
        public static bool IsCrisis(SituationType type)
        {
            return (byte)type < 100;
        }

        /// <summary>
        /// Checks if a situation type is an opportunity (positive).
        /// </summary>
        public static bool IsOpportunity(SituationType type)
        {
            return (byte)type >= 100;
        }

        /// <summary>
        /// Gets the default severity for a situation type.
        /// </summary>
        public static float GetDefaultSeverity(SituationType type)
        {
            return type switch
            {
                SituationType.EnergyCrisis => 0.7f,
                SituationType.SupplyShortage => 0.6f,
                SituationType.FuelDepletion => 0.8f,
                SituationType.MoraleCollapse => 0.9f,
                SituationType.MutinyRisk => 0.8f,
                SituationType.Rebellion => 1.0f,
                SituationType.PirateIncursion => 0.5f,
                SituationType.EnemyAttack => 0.9f,
                SituationType.ResourceBonanza => 0.3f,
                SituationType.TechBreakthrough => 0.4f,
                _ => 0.5f
            };
        }
    }
}

