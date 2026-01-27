using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Aggregate
{
    /// <summary>
    /// Type of aggregate entity.
    /// </summary>
    public enum AggregateType : byte
    {
        Family = 0,
        Dynasty = 1,
        Guild = 2,
        Corporation = 3,
        Band = 4,
        Army = 5,
        Fleet = 6,
        WorkCrew = 7,
        Expedition = 8,
        Cult = 9
    }

    /// <summary>
    /// An aggregate entity that contains members.
    /// </summary>
    public struct AggregateEntity : IComponentData
    {
        public AggregateType Type;
        public FixedString64Bytes Name;
        public Entity LeaderEntity;        // Who leads this aggregate
        public ushort MemberCount;
        public ushort MaxMembers;
        public float AverageSpeed;         // Aggregate movement speed
        public float TotalUpkeep;          // Combined resource cost
        public uint FormedTick;
        public byte IsActive;
    }

    /// <summary>
    /// Membership in an aggregate entity.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct AggregateMembership : IBufferElementData
    {
        public Entity AggregateEntity;
        public AggregateType Type;
        public float ContributionWeight;   // How much this member contributes
        public float LoyaltyToAggregate;   // 0-1 loyalty to the group
        public byte Rank;                  // Position in hierarchy
        public byte IsFounder;             // Original member
        public uint JoinedTick;
    }

    /// <summary>
    /// Members list for an aggregate.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct AggregateMember : IBufferElementData
    {
        public Entity MemberEntity;
        public float ContributionWeight;
        public byte Rank;
        public byte IsActive;
        public uint JoinedTick;
    }

    /// <summary>
    /// Aggregate stats calculated from members.
    /// </summary>
    public struct AggregateMemberStats : IComponentData
    {
        public float AverageHealth;
        public float AverageMorale;
        public float AverageSkill;
        public float TotalStrength;        // Combat power
        public float TotalWealth;          // Economic power
        public float Cohesion;             // How unified the group is
        public uint LastCalculatedTick;
    }

    /// <summary>
    /// Resources owned by aggregate.
    /// </summary>
    public struct AggregateResources : IComponentData
    {
        public float Treasury;             // Money/credits
        public float Supplies;             // Consumables
        public float Influence;            // Political capital
        public float Prestige;             // Reputation value
    }

    /// <summary>
    /// Order issued to aggregate.
    /// </summary>
    public struct AggregateOrder : IComponentData
    {
        public FixedString32Bytes OrderType;
        public Entity TargetEntity;
        public float3 TargetPosition;
        public float Priority;
        public uint IssuedTick;
        public uint ExpiresTime;
        public byte PropagateToMembers;    // Should members receive this?
        public byte RequiresConsensus;     // Need member agreement?
    }

    /// <summary>
    /// Split request for aggregate.
    /// </summary>
    public struct AggregateSplitRequest : IComponentData
    {
        public Entity SourceAggregate;
        public FixedString64Bytes NewName;
        public Entity NewLeader;
        public float SplitRatio;           // What fraction goes to new group
        public uint RequestTick;
        public byte IsApproved;
    }

    /// <summary>
    /// Merge request for aggregates.
    /// </summary>
    public struct AggregateMergeRequest : IComponentData
    {
        public Entity SourceAggregate;
        public Entity TargetAggregate;
        public Entity NewLeader;
        public uint RequestTick;
        public byte IsApproved;
    }
}

