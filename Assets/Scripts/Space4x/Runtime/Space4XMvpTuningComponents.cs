using Unity.Entities;

namespace Space4X.Runtime
{
    /// <summary>
    /// Baseline tuning defaults for movement behavior and contact gating.
    /// Intended to be overridden by scenarios or mod configs.
    /// </summary>
    public struct Space4XMovementTuningConfig : IComponentData
    {
        /// <summary>
        /// Contact range = max(ContactRangeMin, maxWeaponRange * ContactRangeScale).
        /// </summary>
        public float ContactRangeScale;
        public float ContactRangeMin;

        public float CruiseSpeedMultiplier;
        public float CruiseTurnMultiplier;
        public float CombatSpeedMultiplier;
        public float CombatAccelMultiplier;
        public float CombatTurnMultiplier;
        public float TransitionMinSeconds;
        public float TransitionMaxSeconds;

        public static Space4XMovementTuningConfig Default => new Space4XMovementTuningConfig
        {
            ContactRangeScale = 1.05f,
            ContactRangeMin = 0f,
            CruiseSpeedMultiplier = 1f,
            CruiseTurnMultiplier = 0.9f,
            CombatSpeedMultiplier = 0.9f,
            CombatAccelMultiplier = 1.1f,
            CombatTurnMultiplier = 1.1f,
            TransitionMinSeconds = 0.5f,
            TransitionMaxSeconds = 1.5f
        };
    }

    /// <summary>
    /// Baseline tuning for combat skill weighting and tracking penalties.
    /// </summary>
    public struct Space4XCombatTuningConfig : IComponentData
    {
        public float GunneryTacticsWeight;
        public float GunneryFinesseWeight;
        public float GunneryCommandWeight;

        public float TrackingPenaltyMinScale;
        public float TrackingPenaltyMaxScale;

        public float AimLatencyMinSeconds;
        public float AimLatencyMaxSeconds;

        public static Space4XCombatTuningConfig Default => new Space4XCombatTuningConfig
        {
            GunneryTacticsWeight = 0.45f,
            GunneryFinesseWeight = 0.35f,
            GunneryCommandWeight = 0.2f,
            TrackingPenaltyMinScale = 0.6f,
            TrackingPenaltyMaxScale = 1.4f,
            AimLatencyMinSeconds = 0.08f,
            AimLatencyMaxSeconds = 0.35f
        };
    }
}
