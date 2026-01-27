#if UNITY_EDITOR
using PureDOTS.Runtime.Combat;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatLoadoutAuthoring : MonoBehaviour
    {
        public float patrolRadius = 25f;
        public float engagementRange = 5f;
        public float weaponCooldownSeconds = 3f;
        public float retreatThreshold = 0.2f;
        [Header("Pilot & Vessel")]
        public float pilotExperience = 0f;
        public float strafeThreshold = 10f;
        public float kiteThreshold = 25f;
        public float jTurnThreshold = 40f;
        [Header("Pilot Attributes")]
        public float intelligence = 10f;
        public float finesse = 10f;
        public float perception = 10f;
        [Header("Instrumentation")]
        public float instrumentTechLevel = 1f;
    }

    public sealed class CombatLoadoutBaker : Baker<CombatLoadoutAuthoring>
    {
        public override void Bake(CombatLoadoutAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(entity, new CombatLoopConfig
            {
                PatrolRadius = authoring.patrolRadius,
                EngagementRange = authoring.engagementRange,
                WeaponCooldownSeconds = authoring.weaponCooldownSeconds,
                RetreatThreshold = authoring.retreatThreshold
            });

            AddComponent(entity, new CombatLoopState
            {
                Phase = CombatLoopPhase.Idle,
                PhaseTimer = 0f,
                WeaponCooldown = 0f,
                Target = Entity.Null,
                LastKnownTargetPosition = float3.zero
            });

            AddComponent(entity, new PilotExperience
            {
                Experience = authoring.pilotExperience
            });

            AddComponent(entity, new VesselManeuverProfile
            {
                StrafeThreshold = authoring.strafeThreshold,
                KiteThreshold = authoring.kiteThreshold,
                JTurnThreshold = authoring.jTurnThreshold
            });

            AddComponent(entity, new PilotAttributes
            {
                Intelligence = authoring.intelligence,
                Finesse = authoring.finesse,
                Perception = authoring.perception
            });

            AddComponent(entity, new InstrumentTechLevel
            {
                TechLevel = authoring.instrumentTechLevel
            });
        }
    }
}
#endif
