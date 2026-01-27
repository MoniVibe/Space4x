// [TRI-STUB] Stub components for relation decay system
using Unity.Entities;

namespace PureDOTS.Runtime.Relations
{
    /// <summary>
    /// Relation decay configuration.
    /// </summary>
    public struct RelationDecayConfig : IComponentData
    {
        public float DecayRatePerDay;
        public float DecayThreshold;
        public float MinimumRelationValue;
    }

    /// <summary>
    /// Last interaction timestamp - tracks when entities last interacted.
    /// </summary>
    public struct LastInteractionTimestamp : IComponentData
    {
        public Entity RelatedEntity;
        public uint LastInteractionTick;
    }
}

