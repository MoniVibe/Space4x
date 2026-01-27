using Unity.Entities;

namespace PureDOTS.Runtime.Identity
{
    /// <summary>
    /// Moral/ideological alignment values for an entity.
    /// Uses 3-axis system: Moral (Good ↔ Evil), Order (Lawful ↔ Chaotic), Purity (Pure ↔ Corrupt).
    /// </summary>
    public struct EntityAlignment : IComponentData
    {
        /// <summary>Good (+100) ↔ Evil (-100)</summary>
        public float Moral;
        
        /// <summary>Lawful (+100) ↔ Chaotic (-100)</summary>
        public float Order;
        
        /// <summary>Pure/Altruistic (+100) ↔ Corrupt/Selfish (-100)</summary>
        public float Purity;
        
        /// <summary>How strongly held (0..1). Higher = more resistant to change.</summary>
        public float Strength;
    }

    /// <summary>
    /// Cultural/lifestyle outlook tags that express alignment in concrete ways.
    /// Each entity has up to 3 outlook tags (Primary, Secondary, Tertiary).
    /// </summary>
    public enum OutlookType : byte
    {
        None = 0,
        Warlike = 1,
        Peaceful = 2,
        Spiritual = 3,
        Materialistic = 4,
        Scholarly = 5,
        Pragmatic = 6,
        Xenophobic = 7,
        Egalitarian = 8,
        Authoritarian = 9
    }

    /// <summary>
    /// Outlook tags for an entity. Primary is strongest cultural lens, Secondary and Tertiary are weaker.
    /// </summary>
    public struct EntityOutlook : IComponentData
    {
        public OutlookType Primary;
        public OutlookType Secondary;
        public OutlookType Tertiary;
    }

    /// <summary>
    /// Behavioral personality axes that determine HOW an entity reacts, not WHAT they value.
    /// These affect action selection, risk tolerance, and response to harm.
    /// </summary>
    public struct PersonalityAxes : IComponentData
    {
        /// <summary>Vengeful (-100) ↔ Forgiving (+100). Affects grudge-holding and revenge behavior.</summary>
        public float VengefulForgiving;
        
        /// <summary>Craven (-100) ↔ Bold (+100). Affects risk-taking and combat stance.</summary>
        public float CravenBold;
        
        // Future axes:
        // public float TrustingParanoid;
        // public float SelfishAltruistic;
    }

    /// <summary>
    /// Power preference axis: preference for physical/tech power vs mystical/psionic/divine power.
    /// </summary>
    public struct MightMagicAffinity : IComponentData
    {
        /// <summary>Pure Might (-100) ↔ Pure Magic (+100). Middle range (-30..+30) is hybrid.</summary>
        public float Axis;
        
        /// <summary>Commitment level (0..1). Strength=1 means strong preference, Strength=0 means just flavor.</summary>
        public float Strength;
    }

    // ========== Aggregate Components (Groups) ==========

    /// <summary>
    /// Aggregate alignment for groups (villages, fleets, empires).
    /// Computed from member alignments with cohesion and drift tracking.
    /// </summary>
    public struct AggregateAlignment : IComponentData
    {
        public float Moral;
        public float Order;
        public float Purity;
        
        /// <summary>Cultural cohesion (0..1). Higher = more unified, less diverse.</summary>
        public float Cohesion;
        
        /// <summary>How fast culture changes toward external influences (0..1).</summary>
        public float DriftRate;
    }

    /// <summary>
    /// Aggregate outlook for groups. Dominant cultural outlooks from member composition.
    /// </summary>
    public struct AggregateOutlook : IComponentData
    {
        public OutlookType DominantPrimary;
        public OutlookType DominantSecondary;
        public OutlookType DominantTertiary;
        
        /// <summary>How consistent members are (0..1). Higher = uniform culture.</summary>
        public float CulturalUniformity;
    }

    /// <summary>
    /// Group persona: aggregate personality traits from members.
    /// Affects group-level behavior (stance choices, risk appetite, retreat decisions).
    /// </summary>
    public struct GroupPersona : IComponentData
    {
        public float AvgVengefulForgiving;
        public float AvgCravenBold;
        
        /// <summary>Personality uniformity (0..1). Higher = members behave similarly.</summary>
        public float Cohesion;
    }

    /// <summary>
    /// Aggregate power profile for groups. Affects doctrine, tactics, and tech/spell preferences.
    /// </summary>
    public struct AggregatePowerProfile : IComponentData
    {
        /// <summary>Average might/magic axis from members.</summary>
        public float AvgMightMagicAxis;
        
        /// <summary>How weighted toward combat assets (0..1).</summary>
        public float MilitaryWeight;
        
        /// <summary>How interwoven tech and mystic systems are (0..1).</summary>
        public float MagitechBlend;
    }

    /// <summary>
    /// Membership entry for aggregate entities. Tracks which entities belong to a group.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct AggregateMember : IBufferElementData
    {
        public Entity MemberEntity;
        
        /// <summary>Influence weight for aggregate calculations (0..1). Leaders/elders have higher weight.</summary>
        public float InfluenceWeight;
    }
}

