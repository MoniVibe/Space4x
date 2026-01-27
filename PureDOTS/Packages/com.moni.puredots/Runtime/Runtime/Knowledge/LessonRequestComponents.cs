using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Knowledge
{
    /// <summary>
    /// Request to acquire a new lesson.
    /// Processed by LessonAcquisitionSystem.
    /// </summary>
    public struct LessonAcquisitionRequest : IBufferElementData
    {
        /// <summary>
        /// Lesson identifier to acquire.
        /// </summary>
        public FixedString64Bytes LessonId;

        /// <summary>
        /// Entity teaching/sharing this lesson (Entity.Null if discovered).
        /// </summary>
        public Entity TeacherEntity;

        /// <summary>
        /// Source of acquisition.
        /// </summary>
        public LessonAcquisitionSource Source;

        /// <summary>
        /// Tick when request was created.
        /// </summary>
        public uint RequestTick;
    }

    /// <summary>
    /// Source of lesson acquisition.
    /// </summary>
    public enum LessonAcquisitionSource : byte
    {
        Teaching = 0,      // Taught by another entity
        Discovery = 1,     // Discovered independently
        Combat = 2,        // Learned from battle experience
        Observation = 3,   // Learned by watching
        Ritual = 4,        // Learned through ritual/ceremony
        Quest = 5,         // Learned as quest reward
        Inheritance = 6,   // Inherited from parent/mentor
        Reading = 7,       // Learned from books/scrolls
        Experimentation = 8, // Learned by trying and iterating
        Failure = 9,       // Learned from mistakes
        Practice = 10      // Repetition and drills
    }

    /// <summary>
    /// XP gain for a lesson from performing related actions.
    /// Processed by LessonProgressionSystem.
    /// </summary>
    public struct LessonXpGain : IBufferElementData
    {
        /// <summary>
        /// Lesson identifier to gain XP for.
        /// </summary>
        public FixedString64Bytes LessonId;

        /// <summary>
        /// Amount of XP gained.
        /// </summary>
        public float XpAmount;

        /// <summary>
        /// Source of XP gain.
        /// </summary>
        public LessonXpSource Source;

        /// <summary>
        /// Tick when XP was gained.
        /// </summary>
        public uint XpTick;
    }

    /// <summary>
    /// Source of lesson XP.
    /// </summary>
    public enum LessonXpSource : byte
    {
        Practice = 0,          // Regular practice/use
        Success = 1,           // Successful action
        CriticalSuccess = 2,   // Exceptional success
        Teaching = 3,          // Teaching others
        Observation = 4,       // Observing others
        Experimentation = 5,   // Trying new approaches
        Failure = 6           // Learning from mistakes (small XP)
    }

    /// <summary>
    /// Aggregated effects from all mastered lessons.
    /// Updated by LessonEffectApplicationSystem.
    /// </summary>
    public struct LessonEffectCache : IComponentData
    {
        // Harvest bonuses
        public float HarvestYieldMultiplier;
        public float HarvestTimeMultiplier;
        public float HarvestQualityBonus;

        // Crafting bonuses
        public float CraftingQualityBonus;
        public float CraftingSpeedMultiplier;
        public float CraftingEfficiencyBonus; // Material cost reduction

        // Combat bonuses
        public float CombatDamageBonus;
        public float CombatAccuracyBonus;
        public float CombatDefenseBonus;

        // Skill bonuses (per skill, stored in separate buffer if needed)
        public float GeneralSkillBonus;

        // Spell unlock flags (stored as buffer if many)
        public uint UnlockedSpellFlags; // Bit flags for quick checks

        /// <summary>
        /// Tick when cache was last updated.
        /// </summary>
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Event raised when a lesson is acquired.
    /// </summary>
    public struct LessonAcquiredEvent : IBufferElementData
    {
        public FixedString64Bytes LessonId;
        public Entity Entity;
        public Entity TeacherEntity;
        public LessonAcquisitionSource Source;
        public uint AcquiredTick;
    }

    /// <summary>
    /// Event raised when lesson mastery tier increases.
    /// </summary>
    public struct LessonTierUpEvent : IBufferElementData
    {
        public FixedString64Bytes LessonId;
        public Entity Entity;
        public MasteryTier NewTier;
        public MasteryTier PreviousTier;
        public uint TierUpTick;
    }
}

