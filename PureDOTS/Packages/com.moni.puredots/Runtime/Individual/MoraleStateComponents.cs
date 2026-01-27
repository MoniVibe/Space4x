using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Individual
{
    /// <summary>
    /// Unified morale state for SimIndividuals.
    /// Uses normalized [-1..+1] range instead of 0-1000 for consistency.
    /// </summary>
    public struct MoraleState : IComponentData
    {
        /// <summary>
        /// Baseline morale [-1..+1]. Long-term average, changes slowly.
        /// </summary>
        public float Baseline;

        /// <summary>
        /// Current morale [-1..+1]. Short-term value, affected by events and needs.
        /// </summary>
        public float Current;

        /// <summary>
        /// Stress level [0..1]. Accumulates from negative events, reduces over time.
        /// </summary>
        public float Stress;

        /// <summary>
        /// Panic level [0..1]. Extreme stress state, triggers breakdowns.
        /// </summary>
        public float Panic;

        /// <summary>
        /// Impact of last morale-affecting event [-1..+1].
        /// Used for decay calculations.
        /// </summary>
        public float LastEventImpact;

        /// <summary>
        /// Tick when morale was last updated (for decay calculations).
        /// </summary>
        public uint LastUpdateTick;

        /// <summary>
        /// Create from baseline and current values with clamping.
        /// </summary>
        public static MoraleState FromValues(float baseline, float current)
        {
            return new MoraleState
            {
                Baseline = math.clamp(baseline, -1f, 1f),
                Current = math.clamp(current, -1f, 1f),
                Stress = 0f,
                Panic = 0f,
                LastEventImpact = 0f
            };
        }

        /// <summary>
        /// Apply a morale modifier and update LastEventImpact.
        /// </summary>
        public void ApplyModifier(float modifier)
        {
            var oldCurrent = Current;
            Current = math.clamp(Current + modifier, -1f, 1f);
            LastEventImpact = Current - oldCurrent;
        }
    }
}

