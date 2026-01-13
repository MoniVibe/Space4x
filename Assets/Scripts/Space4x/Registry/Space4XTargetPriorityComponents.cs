using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Target selection strategy based on alignment and outlook.
    /// </summary>
    public enum TargetStrategy : byte
    {
        /// <summary>
        /// No specific strategy - system default.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Prioritize protecting friendly assets.
        /// </summary>
        DefendAllies = 1,

        /// <summary>
        /// Prioritize highest-threat targets.
        /// </summary>
        NeutralizeThreats = 2,

        /// <summary>
        /// Prioritize nearest targets.
        /// </summary>
        OpportunisticNearest = 3,

        /// <summary>
        /// Prioritize weakest targets for quick kills.
        /// </summary>
        OpportunisticWeakest = 4,

        /// <summary>
        /// Prioritize high-value targets (salvage, strategic).
        /// </summary>
        HighValueTargets = 5,

        /// <summary>
        /// Prioritize specific species/faction.
        /// </summary>
        FocusedHostility = 6,

        /// <summary>
        /// Balance between threat and opportunity.
        /// </summary>
        Balanced = 7
    }

    /// <summary>
    /// Factors that influence target scoring.
    /// </summary>
    [System.Flags]
    public enum TargetFactors : ushort
    {
        None = 0,
        Distance = 1 << 0,
        ThreatLevel = 1 << 1,
        HullState = 1 << 2,
        SalvageValue = 1 << 3,
        StrategicValue = 1 << 4,
        AllyDefense = 1 << 5,
        SpeciesHostility = 1 << 6,
        FactionRelation = 1 << 7,
        CurrentlyTargeting = 1 << 8,
        PreviousDamage = 1 << 9,

        All = Distance | ThreatLevel | HullState | SalvageValue | StrategicValue |
              AllyDefense | SpeciesHostility | FactionRelation | CurrentlyTargeting | PreviousDamage
    }

    /// <summary>
    /// Profile that defines how targets are prioritized for this entity.
    /// </summary>
    public struct TargetSelectionProfile : IComponentData
    {
        /// <summary>
        /// Primary targeting strategy.
        /// </summary>
        public TargetStrategy Strategy;

        /// <summary>
        /// Which factors to consider in scoring.
        /// </summary>
        public TargetFactors EnabledFactors;

        /// <summary>
        /// Weight for distance factor (higher = prefer closer).
        /// </summary>
        public half DistanceWeight;

        /// <summary>
        /// Weight for threat factor (higher = prefer threatening).
        /// </summary>
        public half ThreatWeight;

        /// <summary>
        /// Weight for weakness factor (higher = prefer damaged targets).
        /// </summary>
        public half WeaknessWeight;

        /// <summary>
        /// Weight for value factor (higher = prefer high-value).
        /// </summary>
        public half ValueWeight;

        /// <summary>
        /// Weight for ally defense factor (higher = prioritize threats to allies).
        /// </summary>
        public half AllyDefenseWeight;

        /// <summary>
        /// Maximum range to consider targets (0 = unlimited).
        /// </summary>
        public float MaxEngagementRange;

        /// <summary>
        /// Minimum threat level to engage (ignore weaker targets).
        /// </summary>
        public half MinThreatThreshold;

        public static TargetSelectionProfile DefendAllies => new TargetSelectionProfile
        {
            Strategy = TargetStrategy.DefendAllies,
            EnabledFactors = TargetFactors.Distance | TargetFactors.ThreatLevel | TargetFactors.AllyDefense,
            DistanceWeight = (half)0.3f,
            ThreatWeight = (half)0.4f,
            WeaknessWeight = (half)0.1f,
            ValueWeight = (half)0.0f,
            AllyDefenseWeight = (half)0.8f,
            MaxEngagementRange = 0f,
            MinThreatThreshold = (half)0f
        };

        public static TargetSelectionProfile NeutralizeThreats => new TargetSelectionProfile
        {
            Strategy = TargetStrategy.NeutralizeThreats,
            EnabledFactors = TargetFactors.ThreatLevel | TargetFactors.Distance | TargetFactors.StrategicValue,
            DistanceWeight = (half)0.2f,
            ThreatWeight = (half)0.7f,
            WeaknessWeight = (half)0.1f,
            ValueWeight = (half)0.3f,
            AllyDefenseWeight = (half)0.2f,
            MaxEngagementRange = 0f,
            MinThreatThreshold = (half)0.2f
        };

        public static TargetSelectionProfile Opportunistic => new TargetSelectionProfile
        {
            Strategy = TargetStrategy.OpportunisticNearest,
            EnabledFactors = TargetFactors.Distance | TargetFactors.HullState,
            DistanceWeight = (half)0.8f,
            ThreatWeight = (half)0.1f,
            WeaknessWeight = (half)0.5f,
            ValueWeight = (half)0.2f,
            AllyDefenseWeight = (half)0f,
            MaxEngagementRange = 0f,
            MinThreatThreshold = (half)0f
        };

        public static TargetSelectionProfile HighValue => new TargetSelectionProfile
        {
            Strategy = TargetStrategy.HighValueTargets,
            EnabledFactors = TargetFactors.SalvageValue | TargetFactors.StrategicValue | TargetFactors.Distance,
            DistanceWeight = (half)0.2f,
            ThreatWeight = (half)0.1f,
            WeaknessWeight = (half)0.2f,
            ValueWeight = (half)0.9f,
            AllyDefenseWeight = (half)0f,
            MaxEngagementRange = 0f,
            MinThreatThreshold = (half)0f
        };

        public static TargetSelectionProfile Balanced => new TargetSelectionProfile
        {
            Strategy = TargetStrategy.Balanced,
            EnabledFactors = TargetFactors.All,
            DistanceWeight = (half)0.4f,
            ThreatWeight = (half)0.4f,
            WeaknessWeight = (half)0.3f,
            ValueWeight = (half)0.3f,
            AllyDefenseWeight = (half)0.3f,
            MaxEngagementRange = 0f,
            MinThreatThreshold = (half)0f
        };
    }

    /// <summary>
    /// Priority score for a specific target.
    /// </summary>
    public struct TargetPriority : IComponentData
    {
        /// <summary>
        /// Current primary target entity.
        /// </summary>
        public Entity CurrentTarget;

        /// <summary>
        /// Score of current target.
        /// </summary>
        public float CurrentScore;

        /// <summary>
        /// Tick when target was last evaluated.
        /// </summary>
        public uint LastEvaluationTick;

        /// <summary>
        /// Time this target has been engaged.
        /// </summary>
        public float EngagementDuration;

        /// <summary>
        /// Whether to force re-evaluation next tick.
        /// </summary>
        public byte ForceReevaluate;
    }

    /// <summary>
    /// Information about a potential target for scoring.
    /// </summary>
    public struct TargetCandidate : IBufferElementData
    {
        /// <summary>
        /// Target entity.
        /// </summary>
        public Entity Entity;

        /// <summary>
        /// Calculated priority score.
        /// </summary>
        public float Score;

        /// <summary>
        /// Distance to target.
        /// </summary>
        public float Distance;

        /// <summary>
        /// Threat level of target [0, 1].
        /// </summary>
        public half ThreatLevel;

        /// <summary>
        /// Hull percentage remaining [0, 1].
        /// </summary>
        public half HullRatio;

        /// <summary>
        /// Strategic/salvage value.
        /// </summary>
        public half Value;

        /// <summary>
        /// Whether target is threatening an ally.
        /// </summary>
        public byte IsThreateningAlly;
    }

    /// <summary>
    /// Tuning for target priority queries (cadence + candidate caps).
    /// </summary>
    public struct TargetPriorityQueryConfig : IComponentData
    {
        /// <summary>
        /// Minimum ticks between evaluations per entity.
        /// </summary>
        public uint EvaluationCadenceTicks;

        /// <summary>
        /// Maximum number of candidates scored per evaluation.
        /// </summary>
        public int MaxCandidates;

        /// <summary>
        /// Hard cap on evaluations per tick.
        /// </summary>
        public int MaxEvaluationsPerTick;

        /// <summary>
        /// Whether to stagger evaluations across ticks.
        /// </summary>
        public byte StaggerEvaluation;

        /// <summary>
        /// Use spatial grid when available.
        /// </summary>
        public byte UseSpatialGrid;

        /// <summary>
        /// Default max range when profile range is unset.
        /// </summary>
        public float DefaultSearchRadius;

        public static TargetPriorityQueryConfig Default => new TargetPriorityQueryConfig
        {
            EvaluationCadenceTicks = 10,
            MaxCandidates = 32,
            MaxEvaluationsPerTick = 4096,
            StaggerEvaluation = 1,
            UseSpatialGrid = 1,
            DefaultSearchRadius = 10000f
        };
    }

    /// <summary>
    /// Tracks which entities this entity has damaged.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct DamageHistory : IBufferElementData
    {
        /// <summary>
        /// Entity that was damaged.
        /// </summary>
        public Entity Target;

        /// <summary>
        /// Total damage dealt.
        /// </summary>
        public float DamageDealt;

        /// <summary>
        /// Last tick damage was dealt.
        /// </summary>
        public uint LastDamageTick;
    }

    /// <summary>
    /// Hostile species list for xenophobic targeting.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct HostileSpecies : IBufferElementData
    {
        /// <summary>
        /// Species ID to prioritize.
        /// </summary>
        public ushort SpeciesId;

        /// <summary>
        /// Additional priority boost for this species.
        /// </summary>
        public half PriorityBoost;
    }

    /// <summary>
    /// Utility functions for target priority calculations.
    /// </summary>
    public static class TargetPriorityUtility
    {
        /// <summary>
        /// Creates a target selection profile based on alignment.
        /// </summary>
        public static TargetSelectionProfile ProfileFromAlignment(in AlignmentTriplet alignment)
        {
            float law = (float)alignment.Law;
            float good = (float)alignment.Good;

            // Lawful Good = Defend allies
            if (law > 0.3f && good > 0.3f)
            {
                return TargetSelectionProfile.DefendAllies;
            }

            // Lawful Neutral/Evil = Neutralize threats
            if (law > 0.3f)
            {
                return TargetSelectionProfile.NeutralizeThreats;
            }

            // Chaotic = Opportunistic
            if (law < -0.3f)
            {
                var profile = TargetSelectionProfile.Opportunistic;

                // Chaotic Evil = focus on value
                if (good < -0.3f)
                {
                    profile.ValueWeight = (half)0.6f;
                    profile.Strategy = TargetStrategy.HighValueTargets;
                }

                return profile;
            }

            // Neutral = Balanced
            return TargetSelectionProfile.Balanced;
        }

        /// <summary>
        /// Calculates target score based on profile and candidate data.
        /// </summary>
        public static float CalculateScore(
            in TargetSelectionProfile profile,
            in TargetCandidate candidate,
            float maxRange)
        {
            float score = 0f;

            // Distance factor (inverse - closer is better)
            if ((profile.EnabledFactors & TargetFactors.Distance) != 0)
            {
                float normalizedDistance = maxRange > 0 ? candidate.Distance / maxRange : 1f;
                score += (1f - math.saturate(normalizedDistance)) * (float)profile.DistanceWeight;
            }

            // Threat factor
            if ((profile.EnabledFactors & TargetFactors.ThreatLevel) != 0)
            {
                score += (float)candidate.ThreatLevel * (float)profile.ThreatWeight;
            }

            // Weakness factor (inverse hull - lower hull = higher priority)
            if ((profile.EnabledFactors & TargetFactors.HullState) != 0)
            {
                score += (1f - (float)candidate.HullRatio) * (float)profile.WeaknessWeight;
            }

            // Value factor
            if ((profile.EnabledFactors & TargetFactors.SalvageValue) != 0 ||
                (profile.EnabledFactors & TargetFactors.StrategicValue) != 0)
            {
                score += (float)candidate.Value * (float)profile.ValueWeight;
            }

            // Ally defense factor
            if ((profile.EnabledFactors & TargetFactors.AllyDefense) != 0 && candidate.IsThreateningAlly == 1)
            {
                score += (float)profile.AllyDefenseWeight;
            }

            return score;
        }

        /// <summary>
        /// Applies bonus for previous engagement (focus fire).
        /// </summary>
        public static float ApplyEngagementBonus(float baseScore, float engagementDuration, bool isPreviousTarget)
        {
            if (!isPreviousTarget)
            {
                return baseScore;
            }

            // 20% bonus for targets we're already engaged with, decaying over time
            float engagementBonus = 0.2f * math.exp(-engagementDuration * 0.01f);
            return baseScore + engagementBonus;
        }
    }
}
