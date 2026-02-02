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

        [Header("Combat - Gunnery Weights")]
        [SerializeField, Range(0f, 1f)] private float gunneryTacticsWeight = 0.45f;
        [SerializeField, Range(0f, 1f)] private float gunneryFinesseWeight = 0.35f;
        [SerializeField, Range(0f, 1f)] private float gunneryCommandWeight = 0.2f;

        [Header("Combat - Tracking Penalty")]
        [SerializeField, Min(0f)] private float trackingPenaltyMinScale = 0.6f;
        [SerializeField, Min(0f)] private float trackingPenaltyMaxScale = 1.4f;

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

            gunneryTacticsWeight = math.clamp(gunneryTacticsWeight, 0f, 1f);
            gunneryFinesseWeight = math.clamp(gunneryFinesseWeight, 0f, 1f);
            gunneryCommandWeight = math.clamp(gunneryCommandWeight, 0f, 1f);

            trackingPenaltyMinScale = math.max(0f, trackingPenaltyMinScale);
            trackingPenaltyMaxScale = math.max(0f, trackingPenaltyMaxScale);
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

                var combat = Space4XCombatTuningConfig.Default;
                combat.GunneryTacticsWeight = math.clamp(authoring.gunneryTacticsWeight, 0f, 1f);
                combat.GunneryFinesseWeight = math.clamp(authoring.gunneryFinesseWeight, 0f, 1f);
                combat.GunneryCommandWeight = math.clamp(authoring.gunneryCommandWeight, 0f, 1f);
                combat.TrackingPenaltyMinScale = math.max(0f, authoring.trackingPenaltyMinScale);
                combat.TrackingPenaltyMaxScale = math.max(0f, authoring.trackingPenaltyMaxScale);
                combat.AimLatencyMinSeconds = math.max(0f, authoring.aimLatencyMinSeconds);
                combat.AimLatencyMaxSeconds = math.max(combat.AimLatencyMinSeconds, authoring.aimLatencyMaxSeconds);

                AddComponent(entity, movement);
                AddComponent(entity, combat);
            }
        }
    }
}
