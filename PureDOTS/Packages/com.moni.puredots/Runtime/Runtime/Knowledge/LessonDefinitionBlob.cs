using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Knowledge
{
    /// <summary>
    /// Catalog of lesson definitions baked from authoring.
    /// Lessons unlock abilities, bonuses, and can be prerequisites for spells.
    /// </summary>
    public struct LessonDefinitionBlob
    {
        public BlobArray<LessonEntry> Lessons;
    }

    /// <summary>
    /// Individual lesson definition.
    /// </summary>
    public struct LessonEntry
    {
        /// <summary>
        /// Unique lesson identifier.
        /// </summary>
        public FixedString64Bytes LessonId;

        /// <summary>
        /// Display name for UI.
        /// </summary>
        public FixedString64Bytes DisplayName;

        /// <summary>
        /// Category of lesson (affects how it's learned and used).
        /// </summary>
        public LessonCategory Category;

        /// <summary>
        /// Description of what the lesson teaches.
        /// </summary>
        public FixedString128Bytes Description;

        /// <summary>
        /// Prerequisites for learning this lesson.
        /// </summary>
        public BlobArray<LessonPrerequisite> Prerequisites;

        /// <summary>
        /// Effects unlocked at each mastery tier.
        /// </summary>
        public BlobArray<LessonEffect> Effects;

        /// <summary>
        /// Base XP required per tier (Novice->Apprentice, Apprentice->Journeyman, etc.).
        /// </summary>
        public float XpPerTier;

        /// <summary>
        /// Teaching difficulty (0-1). Higher = harder to teach/share.
        /// </summary>
        public float TeachingDifficulty;

        /// <summary>
        /// Whether this lesson can be discovered independently (vs requiring a teacher).
        /// </summary>
        public bool CanBeDiscovered;

        /// <summary>
        /// Minimum enlightenment level required to learn.
        /// </summary>
        public byte RequiredEnlightenment;
    }

    /// <summary>
    /// Category of lesson.
    /// </summary>
    public enum LessonCategory : byte
    {
        Crafting = 0,      // Smithing, armorsmithing, advanced materials
        Harvest = 1,      // Mining, forestry, farming techniques
        Combat = 2,       // Tactical lessons, weapon mastery
        Magic = 3,        // Spell formulas, rituals
        Knowledge = 4,     // Starmap data, trade routes, cultural legends
        Survival = 5,     // Foraging, shelter building
        Social = 6,        // Diplomacy, leadership
        Other = 255
    }

    /// <summary>
    /// Prerequisite for learning a lesson.
    /// </summary>
    public struct LessonPrerequisite
    {
        /// <summary>
        /// Type of prerequisite.
        /// </summary>
        public LessonPrerequisiteType Type;

        /// <summary>
        /// Target identifier (lesson ID, spell ID, skill ID, etc.).
        /// </summary>
        public FixedString64Bytes TargetId;

        /// <summary>
        /// Required level/tier/progress (0 = just needs to exist).
        /// </summary>
        public byte RequiredLevel;

        /// <summary>
        /// For mastery prerequisites, the required tier.
        /// </summary>
        public MasteryTier RequiredTier;
    }

    /// <summary>
    /// Type of lesson prerequisite.
    /// </summary>
    public enum LessonPrerequisiteType : byte
    {
        Lesson = 0,        // Must know another lesson at tier
        Spell = 1,         // Must know a spell
        Skill = 2,         // Must have skill at level
        Attribute = 3,     // Must have attribute at level
        Enlightenment = 4, // Must have enlightenment level
        Culture = 5        // Must belong to specific culture
    }

    /// <summary>
    /// Effect unlocked by mastering a lesson to a certain tier.
    /// </summary>
    public struct LessonEffect
    {
        /// <summary>
        /// Mastery tier required to unlock this effect.
        /// </summary>
        public MasteryTier RequiredTier;

        /// <summary>
        /// Type of effect.
        /// </summary>
        public LessonEffectType Type;

        /// <summary>
        /// Effect value (multiplier, bonus, etc.).
        /// </summary>
        public float Value;

        /// <summary>
        /// Target identifier (spell ID, recipe ID, stat name, etc.).
        /// </summary>
        public FixedString64Bytes TargetId;

        /// <summary>
        /// Additional context for the effect.
        /// </summary>
        public FixedString64Bytes Context;
    }

    /// <summary>
    /// Type of lesson effect.
    /// </summary>
    public enum LessonEffectType : byte
    {
        YieldMultiplier = 0,      // Multiplies harvest/craft yield
        QualityBonus = 1,         // Adds to quality roll
        SpeedBonus = 2,           // Reduces time to complete action
        UnlockSpell = 3,          // Unlocks a spell for learning
        UnlockRecipe = 4,         // Unlocks a crafting recipe
        StatBonus = 5,            // Adds to base stat
        SkillBonus = 6,           // Adds to skill level
        ResistanceBonus = 7,      // Adds resistance to damage type
        HarvestTimeReduction = 8, // Reduces harvest time
        CraftingEfficiency = 9    // Reduces material cost
    }

    /// <summary>
    /// Singleton reference to lesson catalog blob.
    /// </summary>
    public struct LessonCatalogRef : IComponentData
    {
        public BlobAssetReference<LessonDefinitionBlob> Blob;
    }
}

