using Unity.Entities;

namespace PureDOTS.Runtime.Behavior
{
    /// <summary>
    /// Identifies which behavior profile (strategy, doctrine, or AI style) drives an entity.
    /// </summary>
    public struct BehaviorProfileId : IComponentData
    {
        public int Profile;
    }

    /// <summary>
    /// Simple modifier that biases decision cadence (positive accelerates, negative slows).
    /// </summary>
    public struct BehaviorModifier : IComponentData
    {
        public float Value;
    }

    /// <summary>
    /// Basic initiative stat: charge accumulates until it reaches Cooldown, then fires.
    /// </summary>
    public struct InitiativeStat : IComponentData
    {
        public float Charge;
        public float Cooldown;
    }

    /// <summary>
    /// Tag describing a primary need category for the entity.
    /// </summary>
    public struct NeedCategory : IComponentData
    {
        public byte Type;
    }

    /// <summary>
    /// Satisfaction value [0..1]. Dropping to zero emits a NeedRequestElement.
    /// </summary>
    public struct NeedSatisfaction : IComponentData
    {
        public float Value;
    }

    /// <summary>
    /// Outstanding needs for an entity, consumed by downstream planners.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct NeedRequestElement : IBufferElementData
    {
        public byte NeedType;
        public float Urgency;
    }
}
