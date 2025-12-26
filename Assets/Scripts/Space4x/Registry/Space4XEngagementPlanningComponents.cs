using Unity.Entities;

namespace Space4X.Registry
{
    public enum EngagementIntentKind : byte
    {
        None = 0,
        Hold = 1,
        Fight = 2,
        Harass = 3,
        BreakThrough = 4,
        Retreat = 5,
        Pursue = 6,
        Screen = 7,
        Rescue = 8
    }

    /// <summary>
    /// High-level engagement intent for a group (slow cadence).
    /// </summary>
    public struct EngagementIntent : IComponentData
    {
        public EngagementIntentKind Kind;
        public Entity PrimaryTarget;
        public float AdvantageRatio;
        public float ThreatPressure;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Aggregated threat snapshot for a group (slow cadence).
    /// </summary>
    public struct EngagementThreatSummary : IComponentData
    {
        public Entity PrimaryThreat;
        public float PrimaryThreatDistance;
        public float PrimaryThreatHullRatio;
        public float FriendlyAverageHull;
        public float ThreatAverageHull;
        public float FriendlyStrength;
        public float ThreatStrength;
        public float ThreatPressure;
        public float AdvantageRatio;
        public int FriendlyCount;
        public int ThreatCount;
        public float EscapeProbability;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Shared planning cadence state for engagement/targeting systems.
    /// </summary>
    public struct EngagementPlannerState : IComponentData
    {
        public uint LastIntentTick;
        public uint LastTacticTick;
        public uint LastTargetingTick;
    }

    public enum ModuleTargetPolicyKind : byte
    {
        Default = 0,
        DisableMobility = 1,
        DisableFighting = 2,
        DisableSensors = 3,
        DisableLogistics = 4
    }

    /// <summary>
    /// Per-attacker module targeting policy (interpreted in combat initiation).
    /// </summary>
    public struct ModuleTargetPolicy : IComponentData
    {
        public ModuleTargetPolicyKind Kind;
    }

    /// <summary>
    /// Tuning knobs for engagement intent/tactical planning.
    /// </summary>
    public struct EngagementDoctrineConfig : IComponentData
    {
        public uint ThreatUpdateIntervalTicks;
        public uint IntentUpdateIntervalTicks;
        public uint TacticUpdateIntervalTicks;
        public byte ThreatSampleLimit;
        public float EscapeDistance;
        public float MinEscapeProbability;
        public float FightAdvantageThreshold;
        public float HarassAdvantageThreshold;
        public float RetreatAdvantageThreshold;
        public float BreakthroughAdvantageThreshold;
        public float AggressionFightBias;
        public float CautionFightBias;
        public float AggressionRetreatBias;
        public float CautionRetreatBias;
        public float RiskBreakthroughBias;
        public float PatienceHarassBias;
        public float DisciplineTightenThreshold;
        public float AggressionFlankThreshold;
        public float CohesionFlankThreshold;
        public float RetreatRangeScale;
        public byte AllowBreakthrough;

        public static EngagementDoctrineConfig Default => new EngagementDoctrineConfig
        {
            ThreatUpdateIntervalTicks = 10,
            IntentUpdateIntervalTicks = 30,
            TacticUpdateIntervalTicks = 15,
            ThreatSampleLimit = 8,
            EscapeDistance = 150f,
            MinEscapeProbability = 0.35f,
            FightAdvantageThreshold = 1.1f,
            HarassAdvantageThreshold = 0.9f,
            RetreatAdvantageThreshold = 0.7f,
            BreakthroughAdvantageThreshold = 0.6f,
            AggressionFightBias = 0.25f,
            CautionFightBias = 0.2f,
            AggressionRetreatBias = 0.2f,
            CautionRetreatBias = 0.3f,
            RiskBreakthroughBias = 0.25f,
            PatienceHarassBias = 0.2f,
            DisciplineTightenThreshold = 0.55f,
            AggressionFlankThreshold = 0.6f,
            CohesionFlankThreshold = 0.55f,
            RetreatRangeScale = 0.6f,
            AllowBreakthrough = 0
        };
    }
}
