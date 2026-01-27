using Unity.Entities;
using Unity.Collections;

namespace PureDOTS.Runtime.Crisis
{
    /// <summary>
    /// Phase of crisis lifecycle.
    /// </summary>
    public enum CrisisPhase : byte
    {
        Dormant = 0,        // Not active, trackers accumulating
        Seeding = 1,        // Hidden triggers building up
        Foreshadowing = 2,  // Visible warnings
        Emergence = 3,      // Crisis begins
        Escalation = 4,     // Getting worse
        Climax = 5,         // Peak intensity
        Resolution = 6,     // Being resolved
        Aftermath = 7       // Post-crisis effects
    }

    /// <summary>
    /// Type of crisis.
    /// </summary>
    public enum CrisisType : byte
    {
        // External
        Invasion = 0,
        NaturalDisaster = 1,
        Pandemic = 2,
        
        // Internal
        EconomicCollapse = 10,
        Famine = 11,
        CivilWar = 12,
        Rebellion = 13,
        
        // Environmental
        ResourceDepletion = 20,
        ClimateShift = 21,
        Contamination = 22,
        
        // Supernatural/Tech
        AnomalyBreach = 30,
        AIUprising = 31,
        MagicSurge = 32
    }

    /// <summary>
    /// Main crisis state component.
    /// </summary>
    public struct CrisisState : IComponentData
    {
        public CrisisType Type;
        public CrisisPhase Phase;
        public float Intensity;            // 0-1 current severity
        public float MaxIntensity;         // Highest reached
        public float TrackerValue;         // 0-1 progress to next phase
        public Entity OriginEntity;        // Where crisis started
        public Entity ScopeEntity;         // What area affected
        public uint StartTick;
        public uint PhaseStartTick;
        public uint EstimatedEndTick;
        public byte PlayerResponded;
    }

    /// <summary>
    /// Crisis trigger conditions.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct CrisisTrigger : IBufferElementData
    {
        public FixedString32Bytes ConditionType;
        public float CurrentValue;
        public float ThresholdValue;
        public float ContributionWeight;   // How much this contributes
        public byte IsMet;
    }

    /// <summary>
    /// Crisis tracker that accumulates toward triggering.
    /// </summary>
    public struct CrisisTracker : IComponentData
    {
        public CrisisType TrackedType;
        public float AccumulatedValue;     // 0-1
        public float TriggerThreshold;     // When crisis triggers
        public float GrowthRate;           // Base accumulation rate
        public float DecayRate;            // Natural decay
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Crisis escalation milestone.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct EscalationMilestone : IBufferElementData
    {
        public float IntensityThreshold;
        public FixedString32Bytes EventType;
        public float ImpactMultiplier;
        public byte WasReached;
        public uint ReachedTick;
    }

    /// <summary>
    /// Resolution path for crisis.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct ResolutionPath : IBufferElementData
    {
        public FixedString32Bytes PathId;
        public float SuccessChance;
        public float ResourceCost;
        public float TimeRequired;
        public float IntensityReduction;
        public byte RequiresAction;        // Needs player/AI input
    }

    /// <summary>
    /// Aftermath effect from resolved crisis.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct AftermathEffect : IBufferElementData
    {
        public FixedString32Bytes EffectType;
        public float Magnitude;
        public uint DurationTicks;
        public uint AppliedTick;
        public byte IsPositive;            // Some crises have silver linings
    }
}

