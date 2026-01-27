using Unity.Entities;

namespace PureDOTS.Runtime.Aggregate
{
    /// <summary>
    /// Links an individual entity to a group (village, guild, fleet, etc.).
    /// </summary>
    public struct GroupMembership : IComponentData
    {
        /// <summary>Primary group entity (village, guild, fleet, etc.).</summary>
        public Entity Group;
        
        /// <summary>Data-driven role index (0 = member, 1 = leader, etc.).</summary>
        public byte Role;
    }
    
    /// <summary>
    /// Tag component marking a group entity as needing stats recalculation.
    /// </summary>
    public struct AggregateStatsDirtyTag : IComponentData { }
}
























