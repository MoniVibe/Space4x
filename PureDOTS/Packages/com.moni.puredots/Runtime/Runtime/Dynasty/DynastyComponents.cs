using Unity.Entities;
using Unity.Collections;

namespace PureDOTS.Runtime.Dynasty
{
    /// <summary>
    /// Dynasty identity - extended family lineage controlling aggregates.
    /// </summary>
    public struct DynastyIdentity : IComponentData
    {
        public FixedString64Bytes DynastyName;
        public Entity FounderEntity;
        public Entity ControlledAggregate;
        public uint FoundedTick;
    }

    /// <summary>
    /// Dynasty member - entity belonging to a dynasty.
    /// </summary>
    public struct DynastyMember : IComponentData
    {
        public Entity DynastyEntity;
        public DynastyRank Rank;
        public float LineageStrength;
    }

    /// <summary>
    /// Dynasty ranks.
    /// </summary>
    public enum DynastyRank : byte
    {
        Founder = 0,
        Heir = 1,
        Noble = 2,
        Member = 3
    }

    /// <summary>
    /// Dynasty lineage - bloodline tracking with generations.
    /// </summary>
    [InternalBufferCapacity(64)]
    public struct DynastyLineage : IBufferElementData
    {
        public Entity MemberEntity;
        public Entity ParentA;
        public Entity ParentB;
        public uint BirthTick;
        public byte Generation;
    }

    /// <summary>
    /// Dynasty prestige - reputation of the dynasty.
    /// </summary>
    public struct DynastyPrestige : IComponentData
    {
        public float PrestigeScore;
        public float DynastyReputation;
        public uint LastUpdatedTick;
    }

    /// <summary>
    /// Dynasty member list - buffer of all members in the dynasty.
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct DynastyMemberEntry : IBufferElementData
    {
        public Entity MemberEntity;
        public DynastyRank Rank;
        public float LineageStrength;
        public uint JoinedTick;
    }

    /// <summary>
    /// Dynasty succession rules - how leadership passes through the dynasty.
    /// </summary>
    public struct DynastySuccessionRules : IComponentData
    {
        public PureDOTS.Runtime.Succession.SuccessionType SuccessionType;
        public byte AllowFemaleHeirs;
        public byte RequiresBloodline;
        public float MinLineageStrength;
    }

    /// <summary>
    /// Dynasty wealth - aggregated wealth of the dynasty.
    /// </summary>
    public struct DynastyWealth : IComponentData
    {
        public float TotalWealth;        // Sum of individual member balances
        public float SharedWealth;       // Shared dynasty wallet balance
        public float AverageWealth;
        public uint LastUpdatedTick;
    }
}

