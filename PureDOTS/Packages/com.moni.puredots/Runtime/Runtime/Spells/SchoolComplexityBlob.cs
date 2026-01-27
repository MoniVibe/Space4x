using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Spells
{
    /// <summary>
    /// Catalog of school complexity and rarity data.
    /// Defines learning difficulty and rarity for each school.
    /// </summary>
    public struct SchoolComplexityBlob
    {
        public BlobArray<SchoolComplexityEntry> Entries;
    }

    /// <summary>
    /// Complexity and rarity data for a magic school.
    /// </summary>
    public struct SchoolComplexityEntry
    {
        /// <summary>
        /// School identifier.
        /// </summary>
        public SpellSchool School;

        /// <summary>
        /// Base complexity (1-10).
        /// Higher = harder to learn/master.
        /// </summary>
        public byte BaseComplexity;

        /// <summary>
        /// Learning time multiplier (1.0 = normal, higher = slower).
        /// </summary>
        public float LearningTimeMultiplier;

        /// <summary>
        /// Mastery XP multiplier (1.0 = normal, higher = slower mastery gain).
        /// </summary>
        public float MasteryXpMultiplier;

        /// <summary>
        /// Rarity level (1=common, 10=legendary).
        /// Affects how often this school appears/discovered.
        /// </summary>
        public byte Rarity;

        /// <summary>
        /// Display name for the school.
        /// </summary>
        public FixedString64Bytes DisplayName;
    }

    /// <summary>
    /// Singleton reference to school complexity catalog blob.
    /// </summary>
    public struct SchoolComplexityCatalogRef : IComponentData
    {
        public BlobAssetReference<SchoolComplexityBlob> Blob;
    }
}

