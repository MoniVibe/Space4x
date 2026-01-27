using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Knowledge
{
    /// <summary>
    /// Enlightenment level representing spiritual/intellectual growth.
    /// Higher enlightenment unlocks advanced spells, abilities, and knowledge.
    /// </summary>
    public struct Enlightenment : IComponentData
    {
        /// <summary>
        /// Current enlightenment level (0-10).
        /// 0 = Unawakened, 10 = Transcendent
        /// </summary>
        public byte Level;

        /// <summary>
        /// Progress toward next level (0-1).
        /// </summary>
        public float Progress;

        /// <summary>
        /// Primary path of enlightenment.
        /// </summary>
        public EnlightenmentPath PrimaryPath;

        /// <summary>
        /// Secondary path (if multiclassing).
        /// </summary>
        public EnlightenmentPath SecondaryPath;

        /// <summary>
        /// Total lessons learned (contributes to progress).
        /// </summary>
        public uint TotalLessonsLearned;

        /// <summary>
        /// Number of masteries achieved.
        /// </summary>
        public uint MasteriesAchieved;

        /// <summary>
        /// Tick when current level was reached.
        /// </summary>
        public uint LevelReachedTick;
    }

    /// <summary>
    /// Path of enlightenment determining unlocks and bonuses.
    /// </summary>
    public enum EnlightenmentPath : byte
    {
        None = 0,

        // Universal paths
        Arcane = 1,       // Mage/wizard - intellect-based magic
        Divine = 2,       // Priest/cleric - faith-based powers
        Martial = 3,      // Warrior/monk - combat techniques
        Natural = 4,      // Druid/ranger - nature magic

        // Godgame-specific (10-29)
        Ancestral = 10,   // Spirit communion, heritage magic
        Shadow = 11,      // Dark arts, forbidden knowledge
        Light = 12,       // Holy, purification

        // Space4X-specific (30-49)
        Technological = 30, // Tech mastery, engineering
        Psionic = 31,       // Mental powers
        Command = 32,       // Leadership, tactics
        Scientific = 33     // Research, analysis
    }

    /// <summary>
    /// Level thresholds and names for enlightenment.
    /// </summary>
    public static class EnlightenmentLevels
    {
        public const byte Unawakened = 0;
        public const byte Aware = 1;
        public const byte Initiated = 2;
        public const byte Apprentice = 3;
        public const byte Adept = 4;
        public const byte Proficient = 5;
        public const byte Expert = 6;
        public const byte Master = 7;
        public const byte Grandmaster = 8;
        public const byte Sage = 9;
        public const byte Transcendent = 10;

        public static FixedString32Bytes GetLevelName(byte level)
        {
            return level switch
            {
                0 => "Unawakened",
                1 => "Aware",
                2 => "Initiated",
                3 => "Apprentice",
                4 => "Adept",
                5 => "Proficient",
                6 => "Expert",
                7 => "Master",
                8 => "Grandmaster",
                9 => "Sage",
                10 => "Transcendent",
                _ => "Unknown"
            };
        }
    }

    /// <summary>
    /// Blob defining enlightenment progression rules.
    /// </summary>
    public struct EnlightenmentProfileBlob
    {
        /// <summary>
        /// XP/progress required for each level (index = target level).
        /// </summary>
        public BlobArray<float> LevelThresholds;

        /// <summary>
        /// Path-specific bonuses per level.
        /// </summary>
        public BlobArray<EnlightenmentPathBonus> PathBonuses;
    }

    /// <summary>
    /// Bonus granted by a specific path at a level.
    /// </summary>
    public struct EnlightenmentPathBonus
    {
        public EnlightenmentPath Path;
        public byte Level;
        public EnlightenmentBonusType BonusType;
        public float BonusValue;
        public FixedString64Bytes UnlockId;  // Spell, ability, or recipe unlocked
    }

    /// <summary>
    /// Type of bonus from enlightenment.
    /// </summary>
    public enum EnlightenmentBonusType : byte
    {
        ManaCostReduction = 0,
        CastSpeedBonus = 1,
        SpellPowerBonus = 2,
        CooldownReduction = 3,
        UnlockSpell = 4,
        UnlockAbility = 5,
        UnlockRecipe = 6,
        StatBonus = 7
    }

    /// <summary>
    /// Singleton reference to enlightenment profile blob.
    /// </summary>
    public struct EnlightenmentProfileRef : IComponentData
    {
        public BlobAssetReference<EnlightenmentProfileBlob> Blob;
    }
}

