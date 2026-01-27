// [TRI-STUB] Stub component for Wisdom stat extension
using Unity.Entities;

namespace PureDOTS.Runtime.Stats
{
    /// <summary>
    /// Wisdom stat extension - adds Wisdom to IndividualStats.
    /// Wisdom accumulates and generates general experience.
    /// Higher wisdom = faster overall progression.
    /// </summary>
    public struct WisdomStat : IComponentData
    {
        /// <summary>
        /// Wisdom value (0-100). General learning/cross-discipline attribute.
        /// </summary>
        public float Wisdom;

        /// <summary>
        /// Global XP gain modifier derived from Wisdom.
        /// Formula: 1.0 + (Wisdom / 100.0) * 0.5 (up to 1.5x gain at 100 Wisdom).
        /// </summary>
        public float GainModifier;

        /// <summary>
        /// Calculate gain modifier from Wisdom value.
        /// </summary>
        public static float CalculateGainModifier(float wisdom)
        {
            return 1.0f + (Unity.Mathematics.math.clamp(wisdom, 0f, 100f) / 100.0f) * 0.5f;
        }

        /// <summary>
        /// Create from value with clamping and auto-calculated gain modifier.
        /// </summary>
        public static WisdomStat FromValue(float wisdom)
        {
            var clamped = Unity.Mathematics.math.clamp(wisdom, 0f, 100f);
            return new WisdomStat
            {
                Wisdom = clamped,
                GainModifier = CalculateGainModifier(clamped)
            };
        }
    }
}

