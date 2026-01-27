// [TRI-STUB] Stub components for mutual care system
using Unity.Entities;
using Unity.Collections;

namespace PureDOTS.Runtime.Cooperation
{
    /// <summary>
    /// Care relationship - entity caring for another.
    /// </summary>
    public struct CareRelationship : IComponentData
    {
        public Entity CareGiver;
        public Entity CareReceiver;
        public float CareLevel;
        public CareRelationshipType Type;
        public CareSupportProvided SupportProvided;
    }

    /// <summary>
    /// Care relationship types.
    /// </summary>
    public enum CareRelationshipType : byte
    {
        Mutual = 0,
        Protective = 1,
        Mentorship = 2,
        Professional = 3,
        Familial = 4,
        Companionship = 5
    }

    /// <summary>
    /// Care support provided flags.
    /// </summary>
    [System.Flags]
    public enum CareSupportProvided : byte
    {
        None = 0,
        ProvideFood = 1 << 0,
        Healing = 1 << 1,
        Comfort = 1 << 2,
        BoostMorale = 1 << 3,
        Protection = 1 << 4,
        Rescue = 1 << 5
    }

    /// <summary>
    /// Care priority - priority of care actions.
    /// </summary>
    public struct CarePriority : IComponentData
    {
        public Entity PriorityTarget;
        public float PriorityScore;
    }

    /// <summary>
    /// Care action buffer.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct CareAction : IBufferElementData
    {
        public CareActionType Action;
        public Entity Target;
        public float Magnitude;
        public uint PerformedTick;
    }

    /// <summary>
    /// Care action types.
    /// </summary>
    public enum CareActionType : byte
    {
        ProvideFood = 0,
        Healing = 1,
        Comfort = 2,
        BoostMorale = 3,
        Protection = 4,
        Rescue = 5
    }

    /// <summary>
    /// Mutual care bond - bidirectional care relationship.
    /// </summary>
    public struct MutualCareBond : IComponentData
    {
        public Entity EntityA;
        public Entity EntityB;
        public float BondStrength;
        public float MutualityScore;
        public float EntityACareForB;
        public float EntityBCareForA;
        public float MoraleBonus;
        public float StressReduction;
        public float PerformanceBonus;
    }
}

