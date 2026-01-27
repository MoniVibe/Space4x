using Unity.Entities;

namespace PureDOTS.Runtime.Aggregate
{
    /// <summary>
    /// Adapter component linking existing Village entity to generic aggregate system.
    /// Added by adapter system, not by authoring.
    /// </summary>
    public struct VillageAggregateAdapter : IComponentData
    {
        /// <summary>Reference to the generic AggregateIdentity entity.</summary>
        public Entity AggregateEntity;
    }
    
    /// <summary>
    /// Adapter component linking existing Band entity to generic aggregate system.
    /// </summary>
    public struct BandAggregateAdapter : IComponentData
    {
        /// <summary>Reference to the generic AggregateIdentity entity.</summary>
        public Entity AggregateEntity;
    }
    
    /// <summary>
    /// Adapter component linking existing Guild entity to generic aggregate system.
    /// </summary>
    public struct GuildAggregateAdapter : IComponentData
    {
        /// <summary>Reference to the generic AggregateIdentity entity.</summary>
        public Entity AggregateEntity;
    }
}

