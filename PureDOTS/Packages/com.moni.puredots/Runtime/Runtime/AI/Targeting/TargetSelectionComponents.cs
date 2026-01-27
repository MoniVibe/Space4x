using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace PureDOTS.Runtime.AI.Targeting
{
    /// <summary>
    /// Priority score for a potential target.
    /// </summary>
    public struct TargetScore : IComponentData
    {
        public Entity TargetEntity;
        public float TotalScore;           // Combined weighted score
        public float ThreatScore;          // How dangerous is this target
        public float DistanceScore;        // Distance-based priority (closer = higher)
        public float HistoryScore;         // From damage memory / grudges
        public float ValueScore;           // Target value (high-value = priority)
        public uint EvaluatedTick;
    }

    /// <summary>
    /// Threat level assessment for an entity.
    /// </summary>
    public struct ThreatAssessment : IComponentData
    {
        public float BaseThreat;           // Innate danger (weapon damage, creature type)
        public float CurrentThreat;        // After modifiers (wounded = less threat)
        public float AggressionLevel;      // 0 = passive, 1 = attacking me
        public byte IsHostile;             // Currently hostile to evaluator
        public byte IsEngaged;             // Already in combat
        public uint LastAssessedTick;
    }

    /// <summary>
    /// Memory of damage received from entities.
    /// Used for revenge targeting and threat learning.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct DamageMemory : IBufferElementData
    {
        public Entity AttackerEntity;
        public float TotalDamageReceived;  // Cumulative damage from this attacker
        public float RecentDamage;         // Damage in last N ticks (decays)
        public ushort HitCount;            // Number of times hit by this entity
        public uint LastHitTick;
    }

    /// <summary>
    /// Candidate target for evaluation.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct TargetCandidate : IBufferElementData
    {
        public Entity CandidateEntity;
        public float Distance;
        public float Priority;             // Calculated priority score
        public byte IsValid;               // Passes basic filters
        public byte WasSelected;           // Was chosen as target
    }

    /// <summary>
    /// Configuration for target selection behavior.
    /// </summary>
    public struct TargetSelectionConfig : IComponentData
    {
        public float MaxRange;             // Maximum targeting range
        public float OptimalRange;         // Preferred engagement range
        public float ThreatWeight;         // Weight for threat score
        public float DistanceWeight;       // Weight for distance score
        public float HistoryWeight;        // Weight for damage memory
        public float ValueWeight;          // Weight for target value
        public byte PreferWounded;         // Prioritize damaged targets
        public byte PreferEngaged;         // Prioritize targets already fighting
    }

    /// <summary>
    /// Current selected target.
    /// </summary>
    public struct CurrentTarget : IComponentData
    {
        public Entity TargetEntity;
        public float TargetPriority;
        public float3 LastKnownPosition;
        public uint SelectedTick;
        public uint LastSeenTick;
        public byte IsLocked;              // Locked on, don't re-evaluate
    }

    /// <summary>
    /// Target acquisition state.
    /// </summary>
    public struct TargetAcquisitionState : IComponentData
    {
        public uint LastEvaluationTick;
        public uint EvaluationInterval;    // Ticks between re-evaluations
        public byte HasTarget;
        public byte NeedsReevaluation;
    }
}

