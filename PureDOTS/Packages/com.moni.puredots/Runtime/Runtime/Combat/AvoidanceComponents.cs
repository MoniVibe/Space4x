using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace PureDOTS.Runtime.Combat
{
    /// <summary>
    /// Avoidance profile - defines how an entity perceives and responds to hazards.
    /// Experience/command policy stored as pure data.
    /// </summary>
    public struct AvoidanceProfile : IComponentData
    {
        public float LookaheadSec; // How far ahead to predict hazards (veterans see farther)
        public float ReactionSec; // Delay before acting on detected hazards

        // Risk weights per hazard kind
        public float RiskWeightAoE;
        public float RiskWeightChain;
        public float RiskWeightPlague;
        public float RiskWeightHoming;
        public float RiskWeightSpray;

        public float BreakFormationThresh; // Risk threshold to justify breaking formation
        public float LooseSpacingMin; // Minimum spacing in loose formation (meters)
        public float LooseSpacingMax; // Maximum spacing in loose formation (meters)
    }

    /// <summary>
    /// Per-entity configuration for ray / pseudo-sphere hazard probing.
    /// </summary>
    public struct HazardRaycastProbe : IComponentData
    {
        public float RayLength;
        public float SphereRadius;
        public byte SampleCount;
        public float SpreadAngleDeg;
        public float UrgencyFalloff; // seconds before urgency decays back to zero
        public CollisionFilter CollisionFilter;
        public float CooldownSeconds;
        public uint LastSampleTick;
    }

    /// <summary>
    /// Diagnostic state written by the raycast avoidance system.
    /// </summary>
    public struct HazardRaycastState : IComponentData
    {
        public float3 LastAvoidanceDirection;
        public float LastHitDistance;
        public byte HitCount;
        public uint LastHitTick;
    }

    /// <summary>
    /// Command policy - defines formation and evasion behavior.
    /// </summary>
    public struct CommandPolicy : IComponentData
    {
        public float FormationStiffness; // 0 = fluid, 1 = rigid
        public float MaxEvasionAccel; // Maximum evasion acceleration budget (m/s^2)
        public float SpacingElasticity; // How fast formation widens (0-1 per second)
        public float GroupBreakCooldown; // Seconds before allowing formation break again
    }

    /// <summary>
    /// Current hazard avoidance steering state for an entity.
    /// Populated by hazard sensing/avoidance systems and consumed by movement composition.
    /// </summary>
    public struct HazardAvoidanceState : IComponentData
    {
        public float3 CurrentAdjustment; // Directional steering adjustment away from hazards
        public Entity AvoidingEntity; // Entity currently being avoided (optional)
        public float AvoidanceUrgency; // 0-1 urgency scalar used by downstream systems
    }

    /// <summary>
    /// Formation anchor - defines relative position in a formation.
    /// </summary>
    public struct FormationAnchor : IComponentData
    {
        public Entity Leader; // Formation leader entity (Entity.Null when broken)
        public float3 LocalOffset; // Local offset from leader position
    }

    /// <summary>
    /// Squadron tag - identifies squadron membership.
    /// </summary>
    public struct SquadronTag : IComponentData
    {
        public int Id; // Squadron identifier
    }

    /// <summary>
    /// Last avoidance decision - tracks formation state with hysteresis.
    /// </summary>
    public struct LastAvoidanceDecision : IComponentData
    {
        public uint Tick; // Tick when decision was made
        public byte Mode; // AvoidanceMode enum (Hold, Loose, Break)
    }

    /// <summary>
    /// Avoidance mode enumeration.
    /// </summary>
    public enum AvoidanceMode : byte
    {
        Hold = 0, // Maintain current formation
        Loose = 1, // Widen spacing
        Break = 2 // Break formation, solo avoidance
    }

    /// <summary>
    /// Ring buffer entry for delayed avoidance reaction.
    /// Stores sampled avoidance data for applying ReactionSec delay.
    /// Games configure ReactionSec via AvoidanceProfile.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct AvoidanceReactionSample : IBufferElementData
    {
        /// <summary>
        /// Computed avoidance adjustment at sample time.
        /// </summary>
        public float3 Adjustment;

        /// <summary>
        /// Urgency level at sample time.
        /// </summary>
        public float Urgency;

        /// <summary>
        /// Tick when this sample was taken.
        /// </summary>
        public uint SampleTick;
    }

    /// <summary>
    /// State tracking for avoidance reaction delay ring buffer.
    /// </summary>
    public struct AvoidanceReactionState : IComponentData
    {
        /// <summary>
        /// Current write index in the ring buffer.
        /// </summary>
        public byte WriteIndex;

        /// <summary>
        /// Number of valid samples in buffer.
        /// </summary>
        public byte SampleCount;
    }
}

