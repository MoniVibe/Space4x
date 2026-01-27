using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Individual
{
    /// <summary>
    /// Marker component indicating this entity is a SimIndividual (unified villager/crew/officer/band member/pilot).
    /// </summary>
    public struct SimIndividualTag : IComponentData
    {
    }

    /// <summary>
    /// Unique per-save ID for an individual. Used for persistence and cross-reference.
    /// </summary>
    public struct IndividualId : IComponentData
    {
        public int Value;
    }

    /// <summary>
    /// Optional name for heroes/officers. Only attached to important SimIndividuals, not mass population.
    /// </summary>
    public struct IndividualName : IComponentData
    {
        public FixedString64Bytes Name;
    }

    /// <summary>
    /// Types of organizations an individual can have allegiances to.
    /// Matches EntityHierarchy ownership layers.
    /// </summary>
    public enum AllegianceKind : byte
    {
        Individual = 0,      // Personal ownership
        Household = 1,       // Family/household
        Crew = 2,            // Ship crew, band
        Fleet = 3,            // Fleet, caravan
        Colony = 4,          // Village, settlement, colony
        Faction = 5,         // Guild, corporation, militia
        Empire = 6           // Sovereign political entity
    }

    /// <summary>
    /// Role within the organization (affects influence weight and command authority).
    /// </summary>
    public enum AllegianceRole : byte
    {
        Member = 0,          // Regular member
        Leader = 1,          // Organization leader
        Officer = 2,         // Officer/commander
        Elite = 3,           // Elite member (veteran, master craftsman)
        Founder = 4          // Founder of organization
    }

    /// <summary>
    /// Allegiance entry linking an individual to an organization.
    /// Stores role, loyalty strength, and ownership share for asset inheritance.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct AllegianceEntry : IBufferElementData
    {
        /// <summary>
        /// Type of organization (household, ship, colony, faction, empire).
        /// </summary>
        public AllegianceKind Kind;

        /// <summary>
        /// Entity reference to the organization.
        /// </summary>
        public Entity Organization;

        /// <summary>
        /// Role within the organization (affects influence weight).
        /// </summary>
        public AllegianceRole Role;

        /// <summary>
        /// Loyalty strength [0..1]. Higher loyalty reduces mutiny/desertion risk.
        /// </summary>
        public float Loyalty;

        /// <summary>
        /// Ownership share [0..1]. Fraction of organization assets owned by this individual.
        /// Used for inheritance and asset distribution.
        /// </summary>
        public float OwnershipShare;

        /// <summary>
        /// Tick when allegiance was formed (for decay calculations).
        /// </summary>
        public uint FormedTick;
    }
}

