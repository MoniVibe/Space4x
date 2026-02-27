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
        public float CombatOrbitDeadbandScale;
        public float AttackRunStartRangeScale;
        public float AttackRunMinBias;
        public float AttackRunCommitSeconds;
        public float AttackRunCooldownSeconds;
        public float AttackRunSpeedMinScale;
        public float AttackRunSpeedMaxScale;

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
            TransitionMaxSeconds = 1.5f,
            CombatOrbitDeadbandScale = 0.12f,
            AttackRunStartRangeScale = 1.1f,
            AttackRunMinBias = 0.25f,
            AttackRunCommitSeconds = 1.6f,
            AttackRunCooldownSeconds = 2.2f,
            AttackRunSpeedMinScale = 0.85f,
            AttackRunSpeedMaxScale = 1.15f
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

        public float GunneryAccuracyMinMultiplier;
        public float GunneryAccuracyMaxMultiplier;
        public float HitChanceSkillCeilingMin;
        public float HitChanceSkillCeilingMax;
        public float HitChanceSkillCeilingExponent;

        public float TrackingPenaltyMinScale;
        public float TrackingPenaltyMaxScale;

        public float RecoilPenaltyRookieScale;
        public float RecoilPenaltyEliteScale;
        public float RecoilPenaltyHeatScale;
        public float InertiaSkewPenaltyScale;
        public float InertiaCompensationMin;
        public float InertiaCompensationMax;
        public float RecoilImpulseVelocityScale;
        public float RecoilImpulseHeatScale;

        public float AimLatencyMinSeconds;
        public float AimLatencyMaxSeconds;

        public static Space4XCombatTuningConfig Default => new Space4XCombatTuningConfig
        {
            GunneryTacticsWeight = 0.45f,
            GunneryFinesseWeight = 0.35f,
            GunneryCommandWeight = 0.2f,
            GunneryAccuracyMinMultiplier = 0.62f,
            GunneryAccuracyMaxMultiplier = 1.08f,
            HitChanceSkillCeilingMin = 0.8f,
            HitChanceSkillCeilingMax = 0.99f,
            HitChanceSkillCeilingExponent = 2.2f,
            TrackingPenaltyMinScale = 0.6f,
            TrackingPenaltyMaxScale = 1.4f,
            RecoilPenaltyRookieScale = 1.25f,
            RecoilPenaltyEliteScale = 0.55f,
            RecoilPenaltyHeatScale = 0.85f,
            InertiaSkewPenaltyScale = 3f,
            InertiaCompensationMin = 0.15f,
            InertiaCompensationMax = 0.75f,
            RecoilImpulseVelocityScale = 1f,
            RecoilImpulseHeatScale = 0.5f,
            AimLatencyMinSeconds = 0.08f,
            AimLatencyMaxSeconds = 0.35f
        };
    }
}
