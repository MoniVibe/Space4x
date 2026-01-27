using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Spells
{
    /// <summary>
    /// Founded magic school created by an entity with 4+ hybrid spells.
    /// Founder gets bonuses, others can learn the school.
    /// </summary>
    public struct FoundedSchool : IBufferElementData
    {
        /// <summary>
        /// Unique school identifier (generated).
        /// </summary>
        public FixedString64Bytes SchoolId;

        /// <summary>
        /// Display name for the school.
        /// </summary>
        public FixedString64Bytes DisplayName;

        /// <summary>
        /// Entity that founded this school.
        /// </summary>
        public Entity FounderEntity;

        /// <summary>
        /// Tick when school was founded.
        /// </summary>
        public uint FoundedTick;

        /// <summary>
        /// Complexity level (1-10), affects learning difficulty.
        /// Higher = harder to learn/master.
        /// </summary>
        public byte Complexity;

        /// <summary>
        /// Required hybrid spells that form this school.
        /// </summary>
        public BlobArray<FixedString64Bytes> RequiredHybrids;

        /// <summary>
        /// Description of the school's theme/philosophy.
        /// </summary>
        public FixedString128Bytes Description;
    }

    /// <summary>
    /// Progress toward founding a new magic school.
    /// Tracks hybrid spells and determines when founding is possible.
    /// </summary>
    public struct SchoolFoundingProgress : IComponentData
    {
        /// <summary>
        /// Number of hybrid spells created (need 4+ for founding).
        /// </summary>
        public byte HybridSpellCount;

        /// <summary>
        /// Primary school from hybrid combinations.
        /// </summary>
        public SpellSchool PrimarySchool;

        /// <summary>
        /// Secondary school from hybrid combinations.
        /// </summary>
        public SpellSchool SecondarySchool;

        /// <summary>
        /// Whether entity can found a school (4+ hybrids).
        /// </summary>
        public bool CanFoundSchool;

        /// <summary>
        /// Tick when progress was last updated.
        /// </summary>
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Bonuses granted to the founder of a magic school.
    /// </summary>
    public struct SchoolFounderBonus : IComponentData
    {
        /// <summary>
        /// School identifier this bonus applies to.
        /// </summary>
        public FixedString64Bytes SchoolId;

        /// <summary>
        /// Cast speed bonus multiplier (1.0 = normal, higher = faster).
        /// </summary>
        public float CastSpeedBonus;

        /// <summary>
        /// Effect strength bonus multiplier (1.0 = normal, higher = stronger).
        /// </summary>
        public float EffectBonus;

        /// <summary>
        /// Teaching bonus multiplier (1.0 = normal, higher = teaches better).
        /// </summary>
        public float TeachingBonus;

        /// <summary>
        /// Mana cost reduction (0-1, 0.1 = 10% reduction).
        /// </summary>
        public float ManaCostReduction;
    }

    /// <summary>
    /// Request to found a new magic school.
    /// Requires 4+ hybrid spells and valid school combination.
    /// </summary>
    public struct SchoolFoundingRequest : IBufferElementData
    {
        /// <summary>
        /// Proposed school name.
        /// </summary>
        public FixedString64Bytes ProposedName;

        /// <summary>
        /// Proposed school description.
        /// </summary>
        public FixedString128Bytes ProposedDescription;

        /// <summary>
        /// Tick when request was created.
        /// </summary>
        public uint RequestTick;
    }

    /// <summary>
    /// Event raised when a magic school is founded.
    /// </summary>
    public struct SchoolFoundedEvent : IBufferElementData
    {
        public FixedString64Bytes SchoolId;
        public FixedString64Bytes DisplayName;
        public Entity FounderEntity;
        public byte Complexity;
        public uint FoundedTick;
    }
}

