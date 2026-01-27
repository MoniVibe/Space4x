using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Components
{
    public enum AggregateCategory : byte
    {
        None = 0,
        Crew = 1,
        Fleet = 2,
        Colony = 3,
        Village = 4,
        Guild = 5,
        Dynasty = 6,
        Elite = 7,
        Business = 8,
        Company = 9,
        Army = 10,
        Custom0 = 240
    }

    /// <summary>
    /// Authoritative metadata for aggregate entities (crews, guilds, fleets, businesses, etc.).
    /// </summary>
    public struct AggregateEntity : IComponentData
    {
        public AggregateCategory Category;
        public Entity Owner;
        public Entity Parent;
        public float Wealth;
        public float Reputation;
        public float Cohesion;
        public float Morale;
        public float Stress;
        public int MemberCount;
    }

    /// <summary>
    /// Buffer of entities participating in an aggregate along with optional weighting.
    /// </summary>
    public struct AggregateMember : IBufferElementData
    {
        public Entity Member;
        public float Weight;
    }

    /// <summary>
    /// Per-member stats projected into aggregates for averaging/aggregation.
    /// </summary>
    public struct AggregateMemberStats : IComponentData
    {
        public float Morale;
        public float Cohesion;
        public float WealthContribution;
        public float ReputationContribution;
        public float Stress;
        public float Energy;
        public float Discipline;
    }

    public struct AggregateWealthHistorySample : IBufferElementData
    {
        public float Wealth;
        public uint Tick;
    }

    public struct AggregateReputationHistorySample : IBufferElementData
    {
        public float Reputation;
        public uint Tick;
    }
}
