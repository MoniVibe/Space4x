using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Per-entity morale state. Values range from -1 (mutinous) to +1 (inspired).
    /// Morale drifts toward baseline during calm periods and is modified by events, supply, and combat.
    /// </summary>
    public struct MoraleState : IComponentData
    {
        /// <summary>
        /// Current morale value [-1, +1].
        /// </summary>
        public half Current;

        /// <summary>
        /// Baseline morale derived from alignment and outlook. Morale drifts toward this value.
        /// </summary>
        public half Baseline;

        /// <summary>
        /// Rate at which morale drifts toward baseline per second (0 = no drift).
        /// </summary>
        public half DriftRate;

        /// <summary>
        /// Last tick when morale was updated.
        /// </summary>
        public uint LastUpdateTick;

        public static MoraleState Default => new MoraleState
        {
            Current = (half)0f,
            Baseline = (half)0f,
            DriftRate = (half)0.01f,
            LastUpdateTick = 0
        };

        public static MoraleState FromBaseline(float baseline, float driftRate = 0.01f)
        {
            return new MoraleState
            {
                Current = (half)math.clamp(baseline, -1f, 1f),
                Baseline = (half)math.clamp(baseline, -1f, 1f),
                DriftRate = (half)math.max(0f, driftRate),
                LastUpdateTick = 0
            };
        }
    }

    /// <summary>
    /// Source of a morale modifier.
    /// </summary>
    public enum MoraleModifierSource : byte
    {
        None = 0,
        Combat = 1,
        SupplyShortage = 2,
        SupplySurplus = 3,
        TaskAlignment = 4,
        TaskMisalignment = 5,
        Event = 6,
        Policy = 7,
        Environment = 8,
        Migration = 9,
        Victory = 10,
        Defeat = 11,
        Patriotism = 12,
        Grudge = 13,
        FamilyPresence = 14,
        SpeciesConflict = 15,
        Leisure = 16,
        Espionage = 17
    }

    /// <summary>
    /// Active morale modifier with duration and strength.
    /// Multiple modifiers stack additively before clamping.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct MoraleModifier : IBufferElementData
    {
        /// <summary>
        /// Source type for debugging and UI.
        /// </summary>
        public MoraleModifierSource Source;

        /// <summary>
        /// Modifier strength [-1, +1]. Positive = morale boost, negative = morale penalty.
        /// </summary>
        public half Strength;

        /// <summary>
        /// Remaining duration in ticks. 0 = permanent until removed.
        /// </summary>
        public uint RemainingTicks;

        /// <summary>
        /// Tick when this modifier was applied.
        /// </summary>
        public uint AppliedTick;

        public static MoraleModifier Create(MoraleModifierSource source, float strength, uint durationTicks, uint currentTick)
        {
            return new MoraleModifier
            {
                Source = source,
                Strength = (half)math.clamp(strength, -1f, 1f),
                RemainingTicks = durationTicks,
                AppliedTick = currentTick
            };
        }

        public static MoraleModifier Permanent(MoraleModifierSource source, float strength, uint currentTick)
        {
            return Create(source, strength, 0, currentTick);
        }
    }

    /// <summary>
    /// Aggregated morale state for crews, fleets, colonies, and factions.
    /// </summary>
    public struct AggregateMoraleState : IComponentData
    {
        /// <summary>
        /// Weighted mean morale of members.
        /// </summary>
        public half MeanMorale;

        /// <summary>
        /// Variance in morale. High variance indicates a polarized group (mutiny risk even with acceptable mean).
        /// </summary>
        public half Variance;

        /// <summary>
        /// Lowest morale among members.
        /// </summary>
        public half MinMorale;

        /// <summary>
        /// Highest morale among members.
        /// </summary>
        public half MaxMorale;

        /// <summary>
        /// Number of members in the aggregate.
        /// </summary>
        public int MemberCount;

        /// <summary>
        /// Last tick when aggregate was computed.
        /// </summary>
        public uint LastUpdateTick;

        public static AggregateMoraleState Default => new AggregateMoraleState
        {
            MeanMorale = (half)0f,
            Variance = (half)0f,
            MinMorale = (half)0f,
            MaxMorale = (half)0f,
            MemberCount = 0,
            LastUpdateTick = 0
        };
    }

    /// <summary>
    /// Morale thresholds for behavior triggers.
    /// </summary>
    public static class MoraleThresholds
    {
        /// <summary>
        /// Below this, entity is at risk of desertion/mutiny.
        /// </summary>
        public const float CriticalLow = -0.7f;

        /// <summary>
        /// Below this, entity receives performance penalties.
        /// </summary>
        public const float Low = -0.3f;

        /// <summary>
        /// Above this, entity receives performance bonuses.
        /// </summary>
        public const float High = 0.3f;

        /// <summary>
        /// Above this, entity is inspired (significant bonuses).
        /// </summary>
        public const float Inspired = 0.7f;

        /// <summary>
        /// Variance above this in aggregates indicates dangerous polarization.
        /// </summary>
        public const float DangerousVariance = 0.4f;
    }

    /// <summary>
    /// Helper utilities for morale calculations.
    /// </summary>
    public static class MoraleUtility
    {
        /// <summary>
        /// Computes baseline morale from alignment triplet.
        /// Integrity contributes positively, extreme law/chaos slightly negative.
        /// </summary>
        public static float ComputeBaseline(in AlignmentTriplet alignment)
        {
            float integrity = (float)alignment.Integrity;
            float law = (float)alignment.Law;

            // Integrity contributes positively (high integrity = stable baseline)
            float integrityContribution = integrity * 0.3f;

            // Extreme positions (very lawful or very chaotic) create slight tension
            float extremism = math.abs(law) * 0.1f;

            return math.clamp(integrityContribution - extremism, -0.5f, 0.5f);
        }

        /// <summary>
        /// Computes task alignment bonus/penalty based on alignment match.
        /// </summary>
        public static float ComputeTaskAlignmentModifier(in AlignmentTriplet entityAlignment, in AlignmentTriplet taskAlignment)
        {
            float3 entity = entityAlignment.AsFloat3();
            float3 task = taskAlignment.AsFloat3();

            // Dot product gives alignment similarity
            float similarity = math.dot(entity, task) / 3f; // Normalize to [-1, 1]

            // Scale to reasonable modifier range
            return similarity * 0.2f;
        }

        /// <summary>
        /// Returns performance multiplier based on current morale.
        /// </summary>
        public static float GetPerformanceMultiplier(float morale)
        {
            if (morale >= MoraleThresholds.Inspired)
            {
                return 1.15f; // 15% bonus
            }
            if (morale >= MoraleThresholds.High)
            {
                return 1.05f; // 5% bonus
            }
            if (morale <= MoraleThresholds.CriticalLow)
            {
                return 0.7f; // 30% penalty
            }
            if (morale <= MoraleThresholds.Low)
            {
                return 0.9f; // 10% penalty
            }
            return 1f; // Neutral
        }
    }
}
