using System;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Aggregates
{
    /// <summary>
    /// Defines what a guild teaches based on thematic filters.
    /// Guilds teach knowledge based on conceptual alignment rather than strict school/category boundaries.
    /// Example: "Guild of Wrath" teaches all offensive abilities regardless of school or damage type.
    /// </summary>
    public struct GuildCurriculum : IComponentData
    {
        /// <summary>
        /// Primary thematic focus of the guild.
        /// </summary>
        public GuildTheme PrimaryTheme;

        /// <summary>
        /// Secondary thematic focus (optional, use None if not applicable).
        /// </summary>
        public GuildTheme SecondaryTheme;

        /// <summary>
        /// Teaching efficiency modifier (1.0 = normal, higher = faster learning).
        /// Guilds specialized in education have higher values.
        /// </summary>
        public float TeachingEfficiency;

        /// <summary>
        /// Minimum rank required to access advanced curriculum.
        /// 0=Member, 1=Officer, 2=Master
        /// </summary>
        public byte AdvancedRankRequirement;

        /// <summary>
        /// Whether guild accepts non-members for paid lessons.
        /// </summary>
        public bool AcceptsPublicStudents;

        /// <summary>
        /// Gold cost per lesson session for non-members.
        /// </summary>
        public float PublicLessonCost;

        /// <summary>
        /// Whether guild has exclusive/signature techniques.
        /// </summary>
        public bool HasSignatureTechniques;

        /// <summary>
        /// Minimum enlightenment level required to join.
        /// </summary>
        public byte RequiredEnlightenment;
    }

    /// <summary>
    /// Guild theme flags - multiple can be combined using bitwise OR.
    /// Used to filter lessons/spells that match guild philosophy.
    /// </summary>
    [Flags]
    public enum GuildTheme : uint
    {
        None = 0,

        // Combat themes
        Offensive = 1 << 0,        // All damage-dealing abilities
        Defensive = 1 << 1,        // All defensive/protection abilities
        Stealth = 1 << 2,          // Stealth and assassination
        MonsterHunting = 1 << 3,   // Boss/monster specialized
        Tactical = 1 << 4,         // Battlefield tactics and coordination

        // Magic themes
        Elemental = 1 << 5,        // Fire/ice/lightning/earth
        Necromancy = 1 << 6,       // Death and undead
        Divine = 1 << 7,           // Holy/light magic
        Arcane = 1 << 8,           // Pure magical research
        Temporal = 1 << 9,         // Time manipulation
        Illusion = 1 << 10,        // Deception and illusions
        Transmutation = 1 << 11,   // Transformation magic

        // Craft themes
        Smithing = 1 << 12,        // All metalworking
        Alchemy = 1 << 13,         // Potions and chemicals
        Enchanting = 1 << 14,      // Item enhancement
        Construction = 1 << 15,    // Building and engineering
        Cooking = 1 << 16,         // Food preparation
        Tailoring = 1 << 17,       // Clothing and armor crafting

        // Knowledge themes
        Trade = 1 << 18,           // Commerce and markets
        Healing = 1 << 19,         // All healing arts
        Survival = 1 << 20,        // Nature and wilderness
        Exploration = 1 << 21,     // Discovery and mapping
        ForbiddenKnowledge = 1 << 22, // Dark/dangerous lore
        History = 1 << 23,         // Lore and legends

        // Social themes
        Diplomacy = 1 << 24,       // Negotiation and influence
        Espionage = 1 << 25,       // Spying and intelligence
        Performance = 1 << 26,     // Arts and entertainment
        Leadership = 1 << 27,      // Command and coordination

        // Damage type themes (for specialized guilds)
        FireFocus = 1 << 28,       // Fire damage specialists
        IceFocus = 1 << 29,        // Ice damage specialists
        LightningFocus = 1 << 30,  // Lightning specialists
        PoisonFocus = 1u << 31,    // Poison specialists
    }

    /// <summary>
    /// Additional damage-focused themes (extended flags).
    /// </summary>
    [Flags]
    public enum GuildThemeExtended : uint
    {
        None = 0,
        HolyFocus = 1 << 0,        // Holy damage specialists
        DarkFocus = 1 << 1,        // Dark/shadow damage
        NatureFocus = 1 << 2,      // Nature/plant damage
        PsychicFocus = 1 << 3,     // Mind damage
        ForceFocus = 1 << 4,       // Pure force/kinetic
    }

    /// <summary>
    /// Lessons/spells that match guild curriculum filters.
    /// Generated at runtime by analyzing lesson/spell tags and themes.
    /// </summary>
    public struct GuildCurriculumEntry : IBufferElementData
    {
        public enum EntryType : byte
        {
            Lesson,
            Spell,
            Recipe,
            Technique,      // Combat maneuvers
            Ritual          // Special ceremonial knowledge
        }

        public EntryType Type;

        /// <summary>
        /// ID of the lesson/spell/recipe.
        /// </summary>
        public FixedString64Bytes EntryId;

        /// <summary>
        /// Minimum guild rank required to learn (0=Member, 1=Officer, 2=Master).
        /// </summary>
        public byte MinimumRank;

        /// <summary>
        /// How well this entry matches guild theme (0-100).
        /// Higher = more central to guild identity.
        /// </summary>
        public byte ThemeRelevance;

        /// <summary>
        /// Whether this is a signature technique (exclusive to this guild).
        /// </summary>
        public bool IsSignature;

        /// <summary>
        /// Whether this requires a teacher (can't be self-taught in guild).
        /// </summary>
        public bool RequiresTeacher;

        /// <summary>
        /// Base tuition cost for learning this (for non-members).
        /// </summary>
        public float TuitionCost;
    }

    /// <summary>
    /// Member's progress through guild curriculum.
    /// Tracks which lessons have been learned from this guild.
    /// </summary>
    public struct GuildStudentProgress : IBufferElementData
    {
        /// <summary>
        /// ID of the lesson/spell being learned.
        /// </summary>
        public FixedString64Bytes EntryId;

        /// <summary>
        /// Current mastery tier.
        /// </summary>
        public Knowledge.MasteryTier CurrentTier;

        /// <summary>
        /// Progress toward next tier (0-1).
        /// </summary>
        public float Progress;

        /// <summary>
        /// When started learning this entry.
        /// </summary>
        public uint StartedLearningTick;

        /// <summary>
        /// Last time practiced/studied.
        /// </summary>
        public uint LastPracticeTick;

        /// <summary>
        /// Which guild member taught this (for tracking lineages).
        /// </summary>
        public Entity TeacherEntity;

        /// <summary>
        /// Total time spent learning (in ticks).
        /// </summary>
        public uint TotalStudyTime;

        /// <summary>
        /// Whether this was learned as a paid lesson.
        /// </summary>
        public bool WasPaidLesson;
    }

    /// <summary>
    /// Active teaching session between guild member (teacher) and student.
    /// Created when formal instruction begins.
    /// </summary>
    public struct GuildTeachingSession : IComponentData
    {
        public Entity TeacherEntity;
        public Entity StudentEntity;
        public Entity GuildEntity;

        /// <summary>
        /// What is being taught.
        /// </summary>
        public FixedString64Bytes EntryId;

        /// <summary>
        /// When session started.
        /// </summary>
        public uint SessionStartTick;

        /// <summary>
        /// Planned session duration (in ticks).
        /// </summary>
        public uint SessionDuration;

        /// <summary>
        /// Teaching quality multiplier based on teacher mastery.
        /// Calculated from teacher's mastery tier + teaching bonuses.
        /// </summary>
        public float TeachingQuality;

        /// <summary>
        /// Whether student pays tuition.
        /// </summary>
        public bool IsPaidLesson;

        /// <summary>
        /// Amount paid for this session.
        /// </summary>
        public float TuitionPaid;

        /// <summary>
        /// Expected progress gain from this session.
        /// </summary>
        public float ExpectedProgressGain;
    }

    /// <summary>
    /// Guild specialization bonuses applied to members based on rank and curriculum focus.
    /// These stack with individual mastery bonuses.
    /// </summary>
    public struct GuildSpecializationBonus : IComponentData
    {
        /// <summary>
        /// Theme being specialized in.
        /// </summary>
        public GuildTheme SpecializedTheme;

        /// <summary>
        /// Cast speed / action speed bonus (1.0 = normal, 1.2 = 20% faster).
        /// </summary>
        public float SpeedBonus;

        /// <summary>
        /// Effectiveness bonus for themed abilities (1.0 = normal, 1.3 = 30% stronger).
        /// </summary>
        public float EffectivenessBonus;

        /// <summary>
        /// Resource cost reduction (0.1 = 10% cheaper mana/stamina).
        /// </summary>
        public float CostReduction;

        /// <summary>
        /// Critical chance bonus for themed abilities (0.05 = +5%).
        /// </summary>
        public float CriticalBonus;

        /// <summary>
        /// Learning speed bonus for curriculum subjects (1.2 = 20% faster).
        /// </summary>
        public float LearningSpeedBonus;

        /// <summary>
        /// Rank at which these bonuses were granted (scales with rank).
        /// </summary>
        public byte GrantedAtRank;
    }

    /// <summary>
    /// Theme tags for a lesson/spell/recipe.
    /// Used by guilds to determine curriculum membership.
    /// Added to lesson/spell definitions for filtering.
    /// </summary>
    public struct KnowledgeThemeTags : IComponentData
    {
        /// <summary>
        /// Primary theme flags.
        /// </summary>
        public GuildTheme Themes;

        /// <summary>
        /// Extended theme flags (if needed).
        /// </summary>
        public GuildThemeExtended ExtendedThemes;

        /// <summary>
        /// Damage type if applicable (for combat abilities).
        /// </summary>
        public Combat.DamageType DamageType;

        /// <summary>
        /// Purpose tags (what does this do?).
        /// </summary>
        public AbilityPurpose Purpose;

        /// <summary>
        /// Schools involved (for magic).
        /// </summary>
        public Spells.SpellSchool PrimarySchool;
        public Spells.SpellSchool SecondarySchool;

        /// <summary>
        /// Whether this knowledge is considered forbidden/dangerous.
        /// </summary>
        public bool IsForbidden;

        /// <summary>
        /// Whether this is a utility ability (non-combat, non-craft).
        /// </summary>
        public bool IsUtility;
    }

    /// <summary>
    /// Purpose classification for abilities/knowledge.
    /// Multiple purposes can be combined.
    /// </summary>
    [Flags]
    public enum AbilityPurpose : uint
    {
        None = 0,
        DealDamage = 1 << 0,      // Offensive combat
        Heal = 1 << 1,            // Restore health/mana
        Buff = 1 << 2,            // Enhance allies
        Debuff = 1 << 3,          // Weaken enemies
        Summon = 1 << 4,          // Call creatures/objects
        Craft = 1 << 5,           // Create items
        Harvest = 1 << 6,         // Gather resources
        Transport = 1 << 7,       // Move entities/items
        Utility = 1 << 8,         // General utility
        Social = 1 << 9,          // Interaction with NPCs
        Stealth = 1 << 10,        // Hide/sneak
        Detection = 1 << 11,      // Reveal hidden things
        Control = 1 << 12,        // Crowd control / disable
        Protection = 1 << 13,     // Shields / damage reduction
        Mobility = 1 << 14,       // Movement enhancement
        ResourceManagement = 1 << 15, // Mana/stamina efficiency
        Transformation = 1 << 16, // Shape change / transmutation
        Divination = 1 << 17,     // Information gathering / scrying
        Curse = 1 << 18,          // Long-term negative effects
        Blessing = 1 << 19,       // Long-term positive effects
        Resurrection = 1 << 20,   // Bring back dead
    }

    /// <summary>
    /// Guild teaching statistics (tracked per guild).
    /// Used for reputation and progression.
    /// </summary>
    public struct GuildTeachingStats : IComponentData
    {
        /// <summary>
        /// Total lessons taught by this guild.
        /// </summary>
        public uint TotalLessonsTaught;

        /// <summary>
        /// Total students trained.
        /// </summary>
        public ushort TotalStudentsTrained;

        /// <summary>
        /// Average teaching quality (0-100).
        /// </summary>
        public byte AverageTeachingQuality;

        /// <summary>
        /// Number of students who achieved mastery.
        /// </summary>
        public ushort MastersProduced;

        /// <summary>
        /// Total tuition income earned.
        /// </summary>
        public float TotalTuitionEarned;

        /// <summary>
        /// Number of signature techniques taught.
        /// </summary>
        public ushort SignatureTechniquesTaught;

        /// <summary>
        /// Tick of last teaching session.
        /// </summary>
        public uint LastTeachingTick;
    }

    /// <summary>
    /// Request to learn from guild curriculum.
    /// Created by villagers seeking instruction.
    /// </summary>
    public struct GuildLearningRequest : IBufferElementData
    {
        public Entity RequesterEntity;
        public FixedString64Bytes EntryId;
        public uint RequestTick;

        /// <summary>
        /// Whether requester is willing to pay tuition.
        /// </summary>
        public bool WillPayTuition;

        /// <summary>
        /// Maximum tuition willing to pay.
        /// </summary>
        public float MaxTuitionOffer;

        /// <summary>
        /// Preferred teacher (Entity.Null for any).
        /// </summary>
        public Entity PreferredTeacher;
    }

    /// <summary>
    /// Teacher availability and expertise.
    /// Tracked per guild member who can teach.
    /// </summary>
    public struct GuildTeacherProfile : IComponentData
    {
        /// <summary>
        /// Whether this member is currently available to teach.
        /// </summary>
        public bool IsAvailable;

        /// <summary>
        /// Whether accepts non-member students.
        /// </summary>
        public bool AcceptsPublicStudents;

        /// <summary>
        /// Teaching skill modifier (0.5-2.0).
        /// Some villagers are natural teachers.
        /// </summary>
        public float TeachingSkill;

        /// <summary>
        /// Current number of active students.
        /// </summary>
        public byte ActiveStudentCount;

        /// <summary>
        /// Maximum concurrent students.
        /// </summary>
        public byte MaxStudents;

        /// <summary>
        /// Total lessons taught (for reputation).
        /// </summary>
        public uint LessonsTaught;

        /// <summary>
        /// Custom tuition rate (overrides guild default if set).
        /// </summary>
        public float CustomTuitionRate;

        /// <summary>
        /// Whether charges custom rate.
        /// </summary>
        public bool UsesCustomRate;
    }
}
