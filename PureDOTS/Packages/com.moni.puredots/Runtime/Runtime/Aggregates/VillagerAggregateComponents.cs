using Unity.Entities;

namespace PureDOTS.Runtime.Components
{
    /// <summary>
    /// Links a villager to aggregate entities (family, guild, dynasty, village, company, etc.) with loyalty weights.
    /// </summary>
    public struct VillagerAggregateMembership : IBufferElementData
    {
        public Entity Aggregate;
        public AggregateCategory Category;
        public float Loyalty;   // 0-1
        public float Sympathy;  // -1 (resentment) to +1 (admiration)
    }

    /// <summary>
    /// Cached axis describing which aggregate currently holds the villager's strongest sense of belonging.
    /// </summary>
    public struct VillagerAggregateBelonging : IComponentData
    {
        public Entity PrimaryAggregate;
        public AggregateCategory Category;
        public float Loyalty;
        public float Sympathy;
    }

    /// <summary>
    /// Defines membership restrictions for an aggregate (e.g., disallow certain guild memberships).
    /// </summary>
    public struct AggregateMembershipRestriction : IBufferElementData
    {
        public AggregateCategory DisallowedCategory;
    }

    /// <summary>
    /// Represents an aggregate faction (sub-group) within a parent aggregate.
    /// </summary>
    public struct AggregateFaction : IComponentData
    {
        public Entity ParentAggregate;
        public int FactionId;
        public float Influence;
    }

    public struct AggregateFactionMember : IBufferElementData
    {
        public Entity Member;
        public float Loyalty;
    }

    public struct VillagerAggregateFaction : IComponentData
    {
        public Entity Faction;
        public float Loyalty;
    }
}
