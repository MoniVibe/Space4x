using Unity.Entities;

namespace PureDOTS.Runtime.Social
{
    /// <summary>
    /// Social candidate category for indexing.
    /// </summary>
    public enum SocialCandidateCategory : byte
    {
        Spouse = 0,
        Apprentice = 1,
        BusinessPartner = 2,
        Friend = 3,
        Companion = 4
    }

    /// <summary>
    /// Social candidate entry in a candidate list.
    /// </summary>
    [InternalBufferCapacity(100)]
    public struct SocialCandidate : IBufferElementData
    {
        /// <summary>
        /// Candidate entity.
        /// </summary>
        public Entity CandidateEntity;

        /// <summary>
        /// Category this candidate belongs to.
        /// </summary>
        public SocialCandidateCategory Category;

        /// <summary>
        /// Compatibility score (0..1, higher = more compatible).
        /// </summary>
        public float CompatibilityScore;

        /// <summary>
        /// Location ID (village/colony ID) for spatial indexing.
        /// </summary>
        public int LocationId;

        /// <summary>
        /// Culture/faction ID for filtering.
        /// </summary>
        public int CultureId;

        /// <summary>
        /// Status/role flags (noble, soldier, mage, etc.).
        /// </summary>
        public byte StatusFlags;

        /// <summary>
        /// Age (for eligibility checks).
        /// </summary>
        public byte Age;
    }

    /// <summary>
    /// Social candidate list - maintains indexed candidates for social interactions.
    /// Indexed by location, culture/faction, status, eligible age/role.
    /// Small size (20-100 candidates max per category).
    /// </summary>
    public struct SocialCandidateList : IComponentData
    {
        /// <summary>
        /// Location ID this list is for (village/colony).
        /// </summary>
        public int LocationId;

        /// <summary>
        /// Last tick when list was updated.
        /// </summary>
        public uint LastUpdateTick;

        /// <summary>
        /// Whether list needs rebuilding (population changed significantly).
        /// </summary>
        public byte NeedsRebuild;
    }
}

