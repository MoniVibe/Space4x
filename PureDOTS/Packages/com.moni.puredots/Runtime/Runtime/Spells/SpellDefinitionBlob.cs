using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Spells
{
    /// <summary>
    /// Catalog of spell definitions baked from authoring.
    /// Shared between Godgame and Space4X with game-specific extensions.
    /// </summary>
    public struct SpellDefinitionBlob
    {
        public BlobArray<SpellEntry> Spells;
    }

    /// <summary>
    /// Individual spell definition.
    /// </summary>
    public struct SpellEntry
    {
        /// <summary>
        /// Unique spell identifier.
        /// </summary>
        public FixedString64Bytes SpellId;

        /// <summary>
        /// Display name for UI.
        /// </summary>
        public FixedString64Bytes DisplayName;

        /// <summary>
        /// School of magic (Fire, Ice, Divine, etc.).
        /// Game-specific interpretation.
        /// </summary>
        public SpellSchool School;

        /// <summary>
        /// How the spell is cast.
        /// </summary>
        public SpellCastType CastType;

        /// <summary>
        /// Targeting mode for the spell.
        /// </summary>
        public SpellTargetType TargetType;

        /// <summary>
        /// Base mana/energy cost to cast.
        /// </summary>
        public float ManaCost;

        /// <summary>
        /// Cooldown in seconds after cast.
        /// </summary>
        public float Cooldown;

        /// <summary>
        /// Cast time in seconds (0 = instant).
        /// </summary>
        public float CastTime;

        /// <summary>
        /// Maximum range (0 = self only).
        /// </summary>
        public float Range;

        /// <summary>
        /// Area of effect radius (0 = single target).
        /// </summary>
        public float AreaRadius;

        /// <summary>
        /// Minimum enlightenment level required.
        /// </summary>
        public byte RequiredEnlightenment;

        /// <summary>
        /// Minimum skill level required.
        /// </summary>
        public byte RequiredSkillLevel;

        /// <summary>
        /// Prerequisites (lessons, other spells, etc.).
        /// </summary>
        public BlobArray<SpellPrerequisite> Prerequisites;

        /// <summary>
        /// Effects applied when spell activates.
        /// </summary>
        public BlobArray<SpellEffect> Effects;
    }

    /// <summary>
    /// School of magic - abstract base, games extend with specifics.
    /// </summary>
    public enum SpellSchool : byte
    {
        None = 0,

        // Universal schools
        Arcane = 1,
        Divine = 2,
        Nature = 3,
        Elemental = 4,

        // Godgame-specific (10-49)
        Ancestral = 10,
        Shadow = 11,
        Light = 12,

        // Space4X-specific (50-99)
        Psionic = 50,
        Technological = 51,
        Tactical = 52
    }

    /// <summary>
    /// How the spell is activated.
    /// </summary>
    public enum SpellCastType : byte
    {
        Instant = 0,      // Immediate effect
        Channeled = 1,    // Must maintain cast, interruptible
        Toggled = 2,      // On/off sustained effect
        Charged = 3,      // Hold to power up, release to fire
        Ritual = 4        // Long cast, requires multiple participants
    }

    /// <summary>
    /// What the spell can target.
    /// </summary>
    public enum SpellTargetType : byte
    {
        Self = 0,
        SingleAlly = 1,
        SingleEnemy = 2,
        SingleAny = 3,
        AreaAlly = 4,
        AreaEnemy = 5,
        AreaAll = 6,
        Ground = 7,
        Direction = 8
    }

    /// <summary>
    /// Prerequisite for learning/casting a spell.
    /// </summary>
    public struct SpellPrerequisite
    {
        public PrerequisiteType Type;
        public FixedString64Bytes TargetId;  // Lesson ID, Spell ID, Skill ID
        public byte RequiredLevel;           // Minimum level/progress
    }

    /// <summary>
    /// Type of prerequisite.
    /// </summary>
    public enum PrerequisiteType : byte
    {
        Lesson = 0,       // Must know a specific lesson
        Spell = 1,        // Must know another spell
        Skill = 2,        // Must have skill at level
        Attribute = 3,    // Must have attribute at level
        Enlightenment = 4 // Must have enlightenment level
    }

    /// <summary>
    /// Effect applied by a spell.
    /// </summary>
    public struct SpellEffect
    {
        public SpellEffectType Type;
        public float BaseValue;
        public float ScalingFactor;   // Scales with caster stats
        public float Duration;        // 0 = instant
        public FixedString64Bytes BuffId; // For buff/debuff types
    }

    /// <summary>
    /// Type of spell effect.
    /// </summary>
    public enum SpellEffectType : byte
    {
        Damage = 0,
        Heal = 1,
        ApplyBuff = 2,
        ApplyDebuff = 3,
        Summon = 4,
        Teleport = 5,
        Shield = 6,
        Dispel = 7,
        ResourceGrant = 8,
        StatModify = 9
    }

    /// <summary>
    /// Singleton reference to spell catalog blob.
    /// </summary>
    public struct SpellCatalogRef : IComponentData
    {
        public BlobAssetReference<SpellDefinitionBlob> Blob;
    }
}

