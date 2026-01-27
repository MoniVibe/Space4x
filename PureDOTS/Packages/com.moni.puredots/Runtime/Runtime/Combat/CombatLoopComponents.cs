using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Combat
{
    public enum CombatLoopPhase : byte
    {
        Idle = 0,
        Patrol = 1,
        Intercept = 2,
        Attack = 3,
        Retreat = 4
    }

    public struct CombatLoopConfig : IComponentData
    {
        public float PatrolRadius;
        public float EngagementRange;
        public float WeaponCooldownSeconds;
        public float RetreatThreshold;
    }

    public struct CombatLoopState : IComponentData
    {
        public CombatLoopPhase Phase;
        public float PhaseTimer;
        public float WeaponCooldown;
        public Entity Target;
        public float3 LastKnownTargetPosition;

        /// <summary>
        /// Currently active combat maneuver (set by CombatManeuverSystem based on pilot XP).
        /// Games define XP thresholds via VesselManeuverProfile.
        /// </summary>
        public CombatManeuver ActiveManeuver;

        /// <summary>
        /// Time remaining in current maneuver (seconds).
        /// </summary>
        public float ManeuverTimer;

        /// <summary>
        /// Tick when current maneuver was started.
        /// </summary>
        public uint ManeuverStartTick;
    }

    /// <summary>
    /// Event emitted when a maneuver starts.
    /// Games can react to this for animations, sound, etc.
    /// </summary>
    public struct ManeuverStartEvent : IBufferElementData
    {
        public Entity VesselEntity;
        public CombatManeuver Maneuver;
        public uint StartTick;
    }
}
