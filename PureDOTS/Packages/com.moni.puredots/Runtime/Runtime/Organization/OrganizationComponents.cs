using Unity.Entities;
using Unity.Collections;

namespace PureDOTS.Runtime.Organization
{
    /// <summary>
    /// Organization definition.
    /// </summary>
    public struct Organization : IComponentData
    {
        public FixedString64Bytes Name;
        public FixedString32Bytes OrgType;       // "Guild", "Order", "Federation"
        public Entity HeadquartersEntity;        // Base of operations
        public uint FoundedTick;
        public float Wealth;                      // Org treasury
        public float Influence;                   // Political power
        public byte AlignmentTendency;           // Avg member alignment
    }

    /// <summary>
    /// Organization member entry.
    /// </summary>
    [InternalBufferCapacity(32)]
    public struct OrganizationMember : IBufferElementData
    {
        public Entity MemberEntity;              // Villager, ship, etc.
        public FixedString32Bytes Rank;          // "Initiate", "Master", "Grandmaster"
        public byte RankLevel;                   // Numeric rank for sorting
        public float Standing;                    // Reputation within org
        public uint JoinedTick;
        public byte IsLeader;
    }

    /// <summary>
    /// Membership record on an entity.
    /// </summary>
    public struct MembershipRecord : IComponentData
    {
        public Entity OrganizationEntity;
        public FixedString32Bytes Rank;
        public byte RankLevel;
        public float Standing;
        public byte IsActive;
    }

    /// <summary>
    /// Organization presence at a location.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct OrganizationPresence : IBufferElementData
    {
        public Entity OrganizationEntity;
        public Entity LocationEntity;            // Village, station
        public FixedString32Bytes PresenceType;  // "Embassy", "Chapterhouse", "Office"
        public byte InfluenceLevel;              // 0-10
        public float LocalReputation;
    }

    /// <summary>
    /// Internal politics of an organization.
    /// </summary>
    public struct OrganizationPolitics : IComponentData
    {
        public Entity CurrentLeader;
        public FixedString32Bytes SuccessionType; // "Election", "Combat", "Seniority"
        public uint NextElectionTick;
        public float Stability;                   // 0-1, low = infighting
        public byte FactionCount;                 // Internal factions
    }

    /// <summary>
    /// Internal faction within organization.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct InternalFaction : IBufferElementData
    {
        public FixedString32Bytes FactionName;
        public Entity FactionLeader;
        public float Support;                     // % of members
        public FixedString64Bytes Agenda;         // What they want
        public byte IsRuling;
    }

    /// <summary>
    /// Relation between organizations.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct OrganizationRelation : IBufferElementData
    {
        public Entity OtherOrganization;
        public float RelationScore;               // -100 to +100
        public FixedString32Bytes RelationType;   // "Alliance", "Rivalry", "War"
        public uint RelationChangedTick;
    }

    /// <summary>
    /// Directive from organization to members.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct OrganizationDirective : IBufferElementData
    {
        public FixedString32Bytes DirectiveType;  // "Respond to threat", "Establish trade"
        public Entity TargetEntity;               // What to act on
        public float Priority;
        public uint IssuedTick;
        public uint ExpiryTick;
    }

    /// <summary>
    /// Organization configuration.
    /// </summary>
    public struct OrganizationConfig : IComponentData
    {
        public uint MinMembersToForm;
        public uint ElectionIntervalTicks;
        public float StabilityDecayRate;
        public float InfluenceGrowthRate;
        public byte AllowCrossTypeRelations;      // Guilds can ally with Orders
    }

    /// <summary>
    /// Request to join organization.
    /// </summary>
    public struct JoinRequest : IComponentData
    {
        public Entity ApplicantEntity;
        public Entity OrganizationEntity;
        public uint RequestTick;
        public byte IsApproved;
        public byte IsProcessed;
    }
}

