using Unity.Entities;
using Unity.Collections;

namespace PureDOTS.Runtime.Family
{
    /// <summary>
    /// Family identity - represents a family unit.
    /// </summary>
    public struct FamilyIdentity : IComponentData
    {
        public FixedString64Bytes FamilyName;
        public Entity FounderEntity;
        public uint FoundedTick;
    }

    /// <summary>
    /// Family member - entity belonging to a family.
    /// </summary>
    public struct FamilyMember : IComponentData
    {
        public Entity FamilyEntity;
        public FamilyRole Role;
    }

    /// <summary>
    /// Family roles.
    /// </summary>
    public enum FamilyRole : byte
    {
        Founder = 0,
        Parent = 1,
        Child = 2,
        Sibling = 3,
        Spouse = 4,
        Extended = 5
    }

    /// <summary>
    /// Family relation - relationship between two family members.
    /// </summary>
    public struct FamilyRelation : IComponentData
    {
        public Entity RelatedEntity;
        public FamilyRelationType Type;
        public float RelationshipStrength;
    }

    /// <summary>
    /// Family relation types.
    /// </summary>
    public enum FamilyRelationType : byte
    {
        None = 0,
        Parent = 1,
        Child = 2,
        Sibling = 3,
        Spouse = 4,
        Grandparent = 5,
        Grandchild = 6,
        Uncle = 7,
        Aunt = 8,
        Cousin = 9
    }

    /// <summary>
    /// Family tree - buffer tracking parent-child relationships.
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct FamilyTree : IBufferElementData
    {
        public Entity MemberEntity;
        public Entity ParentA;
        public Entity ParentB;
        public uint BirthTick;
    }

    /// <summary>
    /// Family member list - buffer of all members in the family.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct FamilyMemberEntry : IBufferElementData
    {
        public Entity MemberEntity;
        public FamilyRole Role;
        public uint JoinedTick;
    }

    /// <summary>
    /// Family wealth aggregation - tracks family's shared wealth.
    /// </summary>
    public struct FamilyWealth : IComponentData
    {
        public float TotalWealth;        // Sum of individual member balances
        public float SharedWealth;       // Shared family wallet balance
        public float AverageWealth;
        public uint LastUpdatedTick;
    }

    /// <summary>
    /// Family reputation - shared reputation among family members.
    /// </summary>
    public struct FamilyReputation : IComponentData
    {
        public float ReputationScore;
        public float AverageReputation;
        public uint LastUpdatedTick;
    }

    /// <summary>
    /// Marks families that have already been promoted to a dynasty to avoid repeated promotions.
    /// </summary>
    public struct DynastyPromotionComplete : IComponentData
    {
        public Entity DynastyEntity;
        public uint ProcessedTick;
    }
}

