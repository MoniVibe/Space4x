using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Culture
{
    /// <summary>
    /// Catalog of culture stories and lore that can spread between entities.
    /// </summary>
    public struct CultureStoryCatalogBlob
    {
        public BlobArray<CultureStory> Stories;
    }

    /// <summary>
    /// Definition of a cultural story/legend/myth.
    /// </summary>
    public struct CultureStory
    {
        /// <summary>
        /// Unique story identifier.
        /// </summary>
        public FixedString64Bytes StoryId;

        /// <summary>
        /// Display name.
        /// </summary>
        public FixedString64Bytes DisplayName;

        /// <summary>
        /// Originating culture (can spread to others).
        /// </summary>
        public FixedString64Bytes OriginCultureId;

        /// <summary>
        /// Type of story.
        /// </summary>
        public StoryType Type;

        /// <summary>
        /// Cultural significance (0-255).
        /// Higher = more important, harder to forget.
        /// </summary>
        public byte ImportanceRank;

        /// <summary>
        /// How difficult to transmit (0-1).
        /// Complex stories are harder to pass on accurately.
        /// </summary>
        public float TransmissionDifficulty;

        /// <summary>
        /// Retention decay rate per day (0-1).
        /// </summary>
        public float DecayRate;

        /// <summary>
        /// Required stories that must be known first.
        /// </summary>
        public BlobArray<FixedString64Bytes> PrerequisiteStories;

        /// <summary>
        /// Effects granted when story is known.
        /// </summary>
        public BlobArray<StoryEffect> Effects;

        /// <summary>
        /// Tags for categorization and filtering.
        /// </summary>
        public BlobArray<FixedString32Bytes> Tags;
    }

    /// <summary>
    /// Type of cultural story.
    /// </summary>
    public enum StoryType : byte
    {
        /// <summary>
        /// Historical account of past events.
        /// </summary>
        History = 0,

        /// <summary>
        /// Heroic tale of legendary figures.
        /// </summary>
        Legend = 1,

        /// <summary>
        /// Creation/religious mythology.
        /// </summary>
        Myth = 2,

        /// <summary>
        /// Future prediction/vision.
        /// </summary>
        Prophecy = 3,

        /// <summary>
        /// Moral lesson/fable.
        /// </summary>
        Parable = 4,

        /// <summary>
        /// Technical/craft knowledge encoded as story.
        /// </summary>
        TechnicalLore = 5,

        /// <summary>
        /// Song/poem/artistic work.
        /// </summary>
        Art = 6,

        /// <summary>
        /// Secret knowledge (restricted transmission).
        /// </summary>
        Secret = 7
    }

    /// <summary>
    /// Effect granted by knowing a story.
    /// </summary>
    public struct StoryEffect
    {
        public StoryEffectType Type;
        public float Value;
        public FixedString64Bytes TargetId;  // Skill, stat, ability, etc.
    }

    /// <summary>
    /// Type of effect from knowing a story.
    /// </summary>
    public enum StoryEffectType : byte
    {
        /// <summary>
        /// Bonus to a skill.
        /// </summary>
        SkillBonus = 0,

        /// <summary>
        /// Bonus to morale.
        /// </summary>
        MoraleBonus = 1,

        /// <summary>
        /// Bonus to faith/belief.
        /// </summary>
        FaithBonus = 2,

        /// <summary>
        /// Unlocks an ability.
        /// </summary>
        UnlockAbility = 3,

        /// <summary>
        /// Unlocks a recipe.
        /// </summary>
        UnlockRecipe = 4,

        /// <summary>
        /// Modifies cultural identity.
        /// </summary>
        CulturalIdentity = 5,

        /// <summary>
        /// Provides resistance to something.
        /// </summary>
        Resistance = 6,

        /// <summary>
        /// Enables diplomatic options.
        /// </summary>
        DiplomaticOption = 7,

        /// <summary>
        /// Grants XP toward enlightenment.
        /// </summary>
        EnlightenmentXp = 8
    }

    /// <summary>
    /// Singleton reference to story catalog.
    /// </summary>
    public struct CultureStoryCatalogRef : IComponentData
    {
        public BlobAssetReference<CultureStoryCatalogBlob> Blob;
    }
}

