// [TRI-STUB] Stub components for extended personality axes
using Unity.Entities;

namespace PureDOTS.Runtime.Stats
{
    /// <summary>
    /// Extended personality axes - additional axes beyond core VengefulForgiving and BoldCraven.
    /// These extend the base PersonalityAxes component.
    /// </summary>
    public struct ExtendedPersonalityAxes : IComponentData
    {
        /// <summary>
        /// Cooperative (+100) ↔ Competitive (-100).
        /// Affects group formation, resource sharing, order obedience.
        /// </summary>
        public float CooperativeCompetitive;

        /// <summary>
        /// Warlike (+100) ↔ Peaceful (-100).
        /// Affects combat stance, conflict seeking, diplomatic approaches.
        /// </summary>
        public float WarlikePeaceful;

        /// <summary>
        /// Create from values with clamping.
        /// </summary>
        public static ExtendedPersonalityAxes FromValues(float cooperativeCompetitive, float warlikePeaceful)
        {
            return new ExtendedPersonalityAxes
            {
                CooperativeCompetitive = Unity.Mathematics.math.clamp(cooperativeCompetitive, -100f, 100f),
                WarlikePeaceful = Unity.Mathematics.math.clamp(warlikePeaceful, -100f, 100f)
            };
        }
    }
}

