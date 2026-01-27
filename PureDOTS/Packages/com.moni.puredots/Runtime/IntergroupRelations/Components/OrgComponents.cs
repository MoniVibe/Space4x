using Unity.Entities;
using Unity.Collections;

namespace PureDOTS.Runtime.IntergroupRelations
{
    /// <summary>
    /// Type of organization (guild, company, family, faction, empire, etc.).
    /// </summary>
    public enum OrgKind : byte
    {
        Family = 0,
        Household = 1,
        Guild = 2,
        Company = 3,
        Clan = 4,
        Church = 5,
        Faction = 6,
        Empire = 7,
        Culture = 8,
        Other = 9
    }

    /// <summary>
    /// Tag component marking an entity as an organization.
    /// </summary>
    public struct OrgTag : IComponentData { }

    /// <summary>
    /// Organization identity and metadata.
    /// </summary>
    public struct OrgId : IComponentData
    {
        public int Value;
        public OrgKind Kind;
        public int ParentOrgId;  // For nested orgs (faction in empire)
    }

    /// <summary>
    /// Aggregate personality traits computed from member behaviors.
    /// </summary>
    public struct OrgPersona : IComponentData
    {
        /// <summary>Average vengeful/forgiving (0-1, where 0=forgiving, 1=vengeful).</summary>
        public float VengefulForgiving;
        
        /// <summary>Average bold/craven (0-1, where 0=craven, 1=bold).</summary>
        public float CravenBold;
        
        /// <summary>Internal unity/cohesion (0-1, where 1=fully unified).</summary>
        public float Cohesion;
        
        /// <summary>Last tick when persona was recalculated.</summary>
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Type of relationship between organizations.
    /// </summary>
    public enum OrgRelationKind : byte
    {
        Neutral = 0,
        Friendly = 1,
        Allied = 2,
        Vassal = 3,
        Overlord = 4,
        Rival = 5,
        Hostile = 6,
        Shunned = 7,
        Sanctioned = 8,
        Integrated = 9,
        InternalFaction = 10
    }

    /// <summary>
    /// Treaty flags between organizations.
    /// </summary>
    [System.Flags]
    public enum OrgTreatyFlags : ushort
    {
        None = 0,
        NonAggression = 1 << 0,
        DefensivePact = 1 << 1,
        FullAlliance = 1 << 2,
        TradeAgreement = 1 << 3,
        ResearchPact = 1 << 4,
        OpenBorders = 1 << 5,
        Sanctions = 1 << 6,
        Embargo = 1 << 7,
        ShunSocial = 1 << 8,
        ShunReligious = 1 << 9,
        IntegrationProcess = 1 << 10
    }

    /// <summary>
    /// Relation edge between two organizations.
    /// Stored on a relation entity, not on the orgs themselves.
    /// </summary>
    public struct OrgRelation : IComponentData
    {
        public Entity OrgA;
        public Entity OrgB;
        
        public OrgRelationKind Kind;
        public OrgTreatyFlags Treaties;
        
        /// <summary>Attitude toward each other (-100 to +100, hate to love).</summary>
        public float Attitude;
        
        /// <summary>Trust level (0-1).</summary>
        public float Trust;
        
        /// <summary>Fear level (0-1).</summary>
        public float Fear;
        
        /// <summary>Respect level (0-1).</summary>
        public float Respect;
        
        /// <summary>Economic/military dependence (0-1).</summary>
        public float Dependence;
        
        /// <summary>When this relation was first established.</summary>
        public uint EstablishedTick;
        
        /// <summary>Last tick when relation was updated.</summary>
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Tag component marking an entity as an organization relation edge.
    /// </summary>
    public struct OrgRelationTag : IComponentData { }

    /// <summary>
    /// Ownership stake of one organization in another.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct OrgOwnership : IBufferElementData
    {
        public Entity OwnerOrg;
        public float Share;  // 0..1 ownership stake
    }

    /// <summary>
    /// Interaction policy flags determining what interactions are allowed.
    /// </summary>
    [System.Flags]
    public enum OrgInteractionMask : ushort
    {
        AllowTrade = 1 << 0,
        AllowMigration = 1 << 1,
        AllowMarriage = 1 << 2,
        AllowEmbassy = 1 << 3,
        AllowMilitary = 1 << 4,
        AllowEspionage = 1 << 5,
        AllowAid = 1 << 6
    }

    /// <summary>
    /// Policy state for interactions between two organizations.
    /// Computed from OrgRelationKind and OrgTreatyFlags.
    /// </summary>
    public struct OrgPolicyState : IComponentData
    {
        public OrgInteractionMask AToBMask;
        public OrgInteractionMask BToAMask;
        
        /// <summary>Last tick when policy was recalculated.</summary>
        public uint LastUpdateTick;
    }
}

