using Unity.Entities;
using Unity.Collections;

namespace PureDOTS.Runtime.Expertise
{
    /// <summary>
    /// Category of expertise.
    /// </summary>
    public enum ExpertiseCategory : byte
    {
        // Combat
        Combat = 0,
        Tactics = 1,
        Weapons = 2,
        Defense = 3,
        
        // Support
        Logistics = 4,
        Engineering = 5,
        Medical = 6,
        Science = 7,
        
        // Social
        Command = 8,
        Diplomacy = 9,
        Espionage = 10,
        Trading = 11,
        
        // Craft
        Crafting = 12,
        Farming = 13,
        Mining = 14,
        Construction = 15,
        
        // Special
        Psionic = 16,
        Beastmastery = 17,
        Navigation = 18,
        Research = 19
    }

    /// <summary>
    /// Mastery tier for expertise.
    /// </summary>
    public enum MasteryTier : byte
    {
        Novice = 0,         // 0-99 XP
        Apprentice = 1,     // 100-499 XP
        Journeyman = 2,     // 500-1999 XP
        Expert = 3,         // 2000-7999 XP
        Master = 4,         // 8000-24999 XP
        Grandmaster = 5,    // 25000+ XP
        Legend = 6          // 100000+ XP (rare)
    }

    /// <summary>
    /// Single expertise entry.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ExpertiseEntry : IBufferElementData
    {
        public ExpertiseCategory Category;
        public float CurrentXP;
        public float TotalXP;              // Lifetime accumulated
        public MasteryTier Tier;
        public byte Inclination;           // 1-10, natural aptitude
        public float RecentGain;           // XP gained this session
        public uint LastActivityTick;
    }

    /// <summary>
    /// Pending XP to be distributed.
    /// </summary>
    public struct XPPool : IComponentData
    {
        public float UnallocatedXP;        // XP waiting for distribution
        public float CombatXP;             // XP from combat activities
        public float CraftXP;              // XP from crafting activities
        public float SocialXP;             // XP from social activities
        public float SpecialXP;            // XP from special activities
    }

    /// <summary>
    /// Preferences for XP allocation.
    /// </summary>
    public struct XPAllocationPrefs : IComponentData
    {
        public ExpertiseCategory PrimaryFocus;
        public ExpertiseCategory SecondaryFocus;
        public float FocusWeight;          // 0-1, how much to favor focus
        public byte AutoAllocate;          // System allocates automatically
        public byte FollowAptitude;        // Favor high-inclination skills
    }

    /// <summary>
    /// Teaching/mentoring capability.
    /// </summary>
    public struct MentoringCapability : IComponentData
    {
        public ExpertiseCategory Specialty;
        public MasteryTier MinTierToTeach; // Minimum tier to be a mentor
        public float TeachingQuality;      // 0-1, how good at teaching
        public float MaxStudentsPerTick;   // Teaching capacity
        public byte IsAvailable;           // Currently mentoring
    }

    /// <summary>
    /// Learning from a mentor.
    /// </summary>
    public struct MentorshipState : IComponentData
    {
        public Entity MentorEntity;
        public ExpertiseCategory LearningCategory;
        public float LearningProgress;     // 0-1 current lesson
        public float XPMultiplier;         // Bonus from mentor quality
        public uint StartedTick;
        public byte IsActive;
    }

    /// <summary>
    /// Activity that grants XP.
    /// </summary>
    public struct XPActivity
    {
        public ExpertiseCategory Category;
        public float BaseXP;
        public float DifficultyModifier;   // Harder = more XP
        public float SuccessModifier;      // Success quality affects XP
        public uint CompletedTick;
    }

    /// <summary>
    /// Expertise thresholds configuration.
    /// </summary>
    public struct ExpertiseConfig : IComponentData
    {
        public float NoviceThreshold;      // 0
        public float ApprenticeThreshold;  // 100
        public float JourneymanThreshold;  // 500
        public float ExpertThreshold;      // 2000
        public float MasterThreshold;      // 8000
        public float GrandmasterThreshold; // 25000
        public float LegendThreshold;      // 100000
    }
}

