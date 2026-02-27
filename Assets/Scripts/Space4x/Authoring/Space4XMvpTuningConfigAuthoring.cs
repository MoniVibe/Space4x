using Space4X.Runtime;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/MVP Tuning Config")]
    public sealed class Space4XMvpTuningConfigAuthoring : MonoBehaviour
    {
        [Header("Movement - Contact Range")]
        [SerializeField, Min(0f)] private float contactRangeScale = 1.05f;
        [SerializeField, Min(0f)] private float contactRangeMin = 0f;

        [Header("Movement - Mode Multipliers")]
        [SerializeField, Min(0f)] private float cruiseSpeedMultiplier = 1f;
        [SerializeField, Min(0f)] private float cruiseTurnMultiplier = 0.9f;
        [SerializeField, Min(0f)] private float combatSpeedMultiplier = 0.9f;
        [SerializeField, Min(0f)] private float combatAccelMultiplier = 1.1f;
        [SerializeField, Min(0f)] private float combatTurnMultiplier = 1.1f;

        [Header("Movement - Transition Timing (Seconds)")]
        [SerializeField, Min(0f)] private float transitionMinSeconds = 0.5f;
        [SerializeField, Min(0f)] private float transitionMaxSeconds = 1.5f;

        [Header("Movement - Orbit Smoothing")]
        [SerializeField, Range(0f, 0.5f)] private float combatOrbitDeadbandScale = 0.12f;

        [Header("Movement - Attack Runs")]
        [SerializeField, Min(0f)] private float attackRunStartRangeScale = 1.1f;
        [SerializeField, Range(0f, 1f)] private float attackRunMinBias = 0.25f;
        [SerializeField, Min(0f)] private float attackRunCommitSeconds = 1.6f;
        [SerializeField, Min(0f)] private float attackRunCooldownSeconds = 2.2f;
        [SerializeField, Min(0f)] private float attackRunSpeedMinScale = 0.85f;
        [SerializeField, Min(0f)] private float attackRunSpeedMaxScale = 1.15f;

        [Header("Combat - Gunnery Weights")]
        [SerializeField, Range(0f, 1f)] private float gunneryTacticsWeight = 0.45f;
        [SerializeField, Range(0f, 1f)] private float gunneryFinesseWeight = 0.35f;
        [SerializeField, Range(0f, 1f)] private float gunneryCommandWeight = 0.2f;
        [SerializeField, Min(0f)] private float gunneryAccuracyMinMultiplier = 0.62f;
        [SerializeField, Min(0f)] private float gunneryAccuracyMaxMultiplier = 1.08f;
        [SerializeField, Range(0f, 1f)] private float hitChanceSkillCeilingMin = 0.8f;
        [SerializeField, Range(0f, 1f)] private float hitChanceSkillCeilingMax = 0.99f;
        [SerializeField, Min(0.1f)] private float hitChanceSkillCeilingExponent = 2.2f;

        [Header("Combat - Tracking Penalty")]
        [SerializeField, Min(0f)] private float trackingPenaltyMinScale = 0.6f;
        [SerializeField, Min(0f)] private float trackingPenaltyMaxScale = 1.4f;

        [Header("Combat - Recoil and Inertia")]
        [SerializeField, Min(0f)] private float recoilPenaltyRookieScale = 1.25f;
        [SerializeField, Min(0f)] private float recoilPenaltyEliteScale = 0.55f;
        [SerializeField, Min(0f)] private float recoilPenaltyHeatScale = 0.85f;
        [SerializeField, Min(0f)] private float inertiaSkewPenaltyScale = 3f;
        [SerializeField, Range(0f, 1f)] private float inertiaCompensationMin = 0.15f;
        [SerializeField, Range(0f, 1f)] private float inertiaCompensationMax = 0.75f;
        [SerializeField, Min(0f)] private float recoilImpulseVelocityScale = 1f;
        [SerializeField, Min(0f)] private float recoilImpulseHeatScale = 0.5f;

        [Header("Combat - Aim Latency (Seconds)")]
        [SerializeField, Min(0f)] private float aimLatencyMinSeconds = 0.08f;
        [SerializeField, Min(0f)] private float aimLatencyMaxSeconds = 0.35f;

        private void OnValidate()
        {
            contactRangeScale = math.max(0f, contactRangeScale);
            contactRangeMin = math.max(0f, contactRangeMin);
            cruiseSpeedMultiplier = math.max(0f, cruiseSpeedMultiplier);
            cruiseTurnMultiplier = math.max(0f, cruiseTurnMultiplier);
            combatSpeedMultiplier = math.max(0f, combatSpeedMultiplier);
            combatAccelMultiplier = math.max(0f, combatAccelMultiplier);
            combatTurnMultiplier = math.max(0f, combatTurnMultiplier);
            transitionMinSeconds = math.max(0f, transitionMinSeconds);
            transitionMaxSeconds = math.max(transitionMinSeconds, transitionMaxSeconds);
            combatOrbitDeadbandScale = math.clamp(combatOrbitDeadbandScale, 0f, 0.5f);

            attackRunStartRangeScale = math.max(0f, attackRunStartRangeScale);
            attackRunMinBias = math.clamp(attackRunMinBias, 0f, 1f);
            attackRunCommitSeconds = math.max(0f, attackRunCommitSeconds);
            attackRunCooldownSeconds = math.max(attackRunCommitSeconds, attackRunCooldownSeconds);
            attackRunSpeedMinScale = math.max(0f, attackRunSpeedMinScale);
            attackRunSpeedMaxScale = math.max(attackRunSpeedMinScale, attackRunSpeedMaxScale);

            gunneryTacticsWeight = math.clamp(gunneryTacticsWeight, 0f, 1f);
            gunneryFinesseWeight = math.clamp(gunneryFinesseWeight, 0f, 1f);
            gunneryCommandWeight = math.clamp(gunneryCommandWeight, 0f, 1f);
            gunneryAccuracyMinMultiplier = math.max(0f, gunneryAccuracyMinMultiplier);
            gunneryAccuracyMaxMultiplier = math.max(gunneryAccuracyMinMultiplier, gunneryAccuracyMaxMultiplier);
            hitChanceSkillCeilingMin = math.clamp(hitChanceSkillCeilingMin, 0f, 1f);
            hitChanceSkillCeilingMax = math.max(hitChanceSkillCeilingMin, math.clamp(hitChanceSkillCeilingMax, 0f, 1f));
            hitChanceSkillCeilingExponent = math.max(0.1f, hitChanceSkillCeilingExponent);

            trackingPenaltyMinScale = math.max(0f, trackingPenaltyMinScale);
            trackingPenaltyMaxScale = math.max(0f, trackingPenaltyMaxScale);
            recoilPenaltyRookieScale = math.max(0f, recoilPenaltyRookieScale);
            recoilPenaltyEliteScale = math.max(0f, recoilPenaltyEliteScale);
            recoilPenaltyHeatScale = math.max(0f, recoilPenaltyHeatScale);
            inertiaSkewPenaltyScale = math.max(0f, inertiaSkewPenaltyScale);
            inertiaCompensationMin = math.clamp(inertiaCompensationMin, 0f, 1f);
            inertiaCompensationMax = math.max(inertiaCompensationMin, math.clamp(inertiaCompensationMax, 0f, 1f));
            recoilImpulseVelocityScale = math.max(0f, recoilImpulseVelocityScale);
            recoilImpulseHeatScale = math.max(0f, recoilImpulseHeatScale);
            aimLatencyMinSeconds = math.max(0f, aimLatencyMinSeconds);
            aimLatencyMaxSeconds = math.max(aimLatencyMinSeconds, aimLatencyMaxSeconds);
        }

        private sealed class Baker : Baker<Space4XMvpTuningConfigAuthoring>
        {
            public override void Bake(Space4XMvpTuningConfigAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);

                var movement = Space4XMovementTuningConfig.Default;
                movement.ContactRangeScale = math.max(0f, authoring.contactRangeScale);
                movement.ContactRangeMin = math.max(0f, authoring.contactRangeMin);
                movement.CruiseSpeedMultiplier = math.max(0f, authoring.cruiseSpeedMultiplier);
                movement.CruiseTurnMultiplier = math.max(0f, authoring.cruiseTurnMultiplier);
                movement.CombatSpeedMultiplier = math.max(0f, authoring.combatSpeedMultiplier);
                movement.CombatAccelMultiplier = math.max(0f, authoring.combatAccelMultiplier);
                movement.CombatTurnMultiplier = math.max(0f, authoring.combatTurnMultiplier);
                movement.TransitionMinSeconds = math.max(0f, authoring.transitionMinSeconds);
                movement.TransitionMaxSeconds = math.max(movement.TransitionMinSeconds, authoring.transitionMaxSeconds);
                movement.CombatOrbitDeadbandScale = math.clamp(authoring.combatOrbitDeadbandScale, 0f, 0.5f);
                movement.AttackRunStartRangeScale = math.max(0f, authoring.attackRunStartRangeScale);
                movement.AttackRunMinBias = math.clamp(authoring.attackRunMinBias, 0f, 1f);
                movement.AttackRunCommitSeconds = math.max(0f, authoring.attackRunCommitSeconds);
                movement.AttackRunCooldownSeconds = math.max(movement.AttackRunCommitSeconds, authoring.attackRunCooldownSeconds);
                movement.AttackRunSpeedMinScale = math.max(0f, authoring.attackRunSpeedMinScale);
                movement.AttackRunSpeedMaxScale = math.max(movement.AttackRunSpeedMinScale, authoring.attackRunSpeedMaxScale);

                var combat = Space4XCombatTuningConfig.Default;
                combat.GunneryTacticsWeight = math.clamp(authoring.gunneryTacticsWeight, 0f, 1f);
                combat.GunneryFinesseWeight = math.clamp(authoring.gunneryFinesseWeight, 0f, 1f);
                combat.GunneryCommandWeight = math.clamp(authoring.gunneryCommandWeight, 0f, 1f);
                combat.GunneryAccuracyMinMultiplier = math.max(0f, authoring.gunneryAccuracyMinMultiplier);
                combat.GunneryAccuracyMaxMultiplier = math.max(combat.GunneryAccuracyMinMultiplier, authoring.gunneryAccuracyMaxMultiplier);
                combat.HitChanceSkillCeilingMin = math.clamp(authoring.hitChanceSkillCeilingMin, 0f, 1f);
                combat.HitChanceSkillCeilingMax = math.max(combat.HitChanceSkillCeilingMin, math.clamp(authoring.hitChanceSkillCeilingMax, 0f, 1f));
                combat.HitChanceSkillCeilingExponent = math.max(0.1f, authoring.hitChanceSkillCeilingExponent);
                combat.TrackingPenaltyMinScale = math.max(0f, authoring.trackingPenaltyMinScale);
                combat.TrackingPenaltyMaxScale = math.max(0f, authoring.trackingPenaltyMaxScale);
                combat.RecoilPenaltyRookieScale = math.max(0f, authoring.recoilPenaltyRookieScale);
                combat.RecoilPenaltyEliteScale = math.max(0f, authoring.recoilPenaltyEliteScale);
                combat.RecoilPenaltyHeatScale = math.max(0f, authoring.recoilPenaltyHeatScale);
                combat.InertiaSkewPenaltyScale = math.max(0f, authoring.inertiaSkewPenaltyScale);
                combat.InertiaCompensationMin = math.clamp(authoring.inertiaCompensationMin, 0f, 1f);
                combat.InertiaCompensationMax = math.max(combat.InertiaCompensationMin, math.clamp(authoring.inertiaCompensationMax, 0f, 1f));
                combat.RecoilImpulseVelocityScale = math.max(0f, authoring.recoilImpulseVelocityScale);
                combat.RecoilImpulseHeatScale = math.max(0f, authoring.recoilImpulseHeatScale);
                combat.AimLatencyMinSeconds = math.max(0f, authoring.aimLatencyMinSeconds);
                combat.AimLatencyMaxSeconds = math.max(combat.AimLatencyMinSeconds, authoring.aimLatencyMaxSeconds);

                AddComponent(entity, movement);
                AddComponent(entity, combat);
            }
        }
    }
}
