using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Progression
{
    /// <summary>
    /// Skill domains representing broad categories of abilities.
    /// </summary>
    public enum SkillDomain : byte
    {
        None = 0,
        
        // Combat domains (1-9)
        Combat = 1,
        Finesse = 2,
        Arcane = 3,
        Divine = 4,
        Ranged = 5,
        Defense = 6,
        
        // Profession domains (10-19)
        Crafting = 10,
        Gathering = 11,
        Refining = 12,
        Cooking = 13,
        Alchemy = 14,
        Enchanting = 15,
        
        // Social domains (20-29)
        Social = 20,
        Leadership = 21,
        Teaching = 22,
        Trading = 23,
        Diplomacy = 24,
        
        // Utility domains (30-39)
        Survival = 30,
        Navigation = 31,
        Engineering = 32,
        Medicine = 33,
        Stealth = 34,
        Athletics = 35
    }

    /// <summary>
    /// Mastery tiers representing proficiency levels.
    /// </summary>
    public enum SkillMastery : byte
    {
        Untrained = 0,    // 0-19 XP
        Novice = 1,       // 20-49 XP
        Apprentice = 2,   // 50-99 XP
        Journeyman = 3,   // 100-199 XP
        Adept = 4,        // 200-499 XP
        Master = 5,       // 500-999 XP
        Grandmaster = 6   // 1000+ XP
    }

    /// <summary>
    /// Core progression data for an entity.
    /// </summary>
    public struct CharacterProgression : IComponentData
    {
        public uint TotalXPEarned;        // Lifetime XP earned
        public uint XPToNextLevel;        // XP required for next level
        public uint CurrentLevelXP;       // XP earned this level
        public byte Level;                // Current character level (1-100)
        public byte SkillPoints;          // Unspent skill points for unlocks
        public byte TalentPoints;         // Unspent talent points for passives
        public byte AttributePoints;      // Unspent attribute points
        
        // Extended fields for SimIndividual system
        public float Fame;                // Fame score (positive achievements, recognition)
        public float Renown;              // Renown score (reputation, influence)
        public bool IsLegendary;          // Flag indicating legendary status
        public FixedString64Bytes LegendaryTitle; // Title for legendary individuals (e.g., "The Unbreakable")
    }

    /// <summary>
    /// XP progress for a specific skill domain.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct SkillXP : IBufferElementData
    {
        public SkillDomain Domain;
        public uint CurrentXP;
        public SkillMastery Mastery;
        public uint LastGainTick;
    }

    /// <summary>
    /// An unlocked skill or ability.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct UnlockedSkill : IBufferElementData
    {
        public FixedString32Bytes SkillId;
        public SkillDomain Domain;
        public byte Tier;                 // Skill tier (1-5)
        public byte Rank;                 // Skill rank within tier (1-3)
        public uint UnlockedTick;
        public bool IsActive;             // Can be toggled on/off
    }

    /// <summary>
    /// A passive talent that provides bonuses.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct UnlockedTalent : IBufferElementData
    {
        public FixedString32Bytes TalentId;
        public SkillDomain Domain;
        public byte Tier;
        public float BonusValue;          // Bonus magnitude
        public uint UnlockedTick;
    }

    /// <summary>
    /// Defines the specialization path for autonomous progression.
    /// </summary>
    public struct PreordainedPath : IComponentData
    {
        public SkillDomain PrimaryDomain;
        public SkillDomain SecondaryDomain;
        public byte AutoSpecializeThreshold;  // Auto-pick skills below this tier
        public bool PlayerGuided;             // If true, player picks unlocks
        public float PrimaryAffinity;         // 0-1, preference for primary
    }

    /// <summary>
    /// Request to award XP to an entity.
    /// </summary>
    public struct XPAwardRequest : IComponentData
    {
        public Entity TargetEntity;
        public SkillDomain Domain;
        public uint Amount;
        public FixedString32Bytes Source;  // What granted the XP
    }

    /// <summary>
    /// Request to unlock a skill for an entity.
    /// </summary>
    public struct SkillUnlockRequest : IComponentData
    {
        public Entity TargetEntity;
        public FixedString32Bytes SkillId;
        public SkillDomain Domain;
        public byte Tier;
    }

    /// <summary>
    /// Event emitted when XP is gained.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct XPGainedEvent : IBufferElementData
    {
        public SkillDomain Domain;
        public uint Amount;
        public SkillMastery OldMastery;
        public SkillMastery NewMastery;
        public uint Tick;
    }

    /// <summary>
    /// Event emitted when a skill is unlocked.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct SkillUnlockedEvent : IBufferElementData
    {
        public FixedString32Bytes SkillId;
        public SkillDomain Domain;
        public byte Tier;
        public uint Tick;
    }

    /// <summary>
    /// Event emitted when character levels up.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct LevelUpEvent : IBufferElementData
    {
        public byte OldLevel;
        public byte NewLevel;
        public byte SkillPointsGained;
        public byte TalentPointsGained;
        public uint Tick;
    }

    /// <summary>
    /// Configuration for the progression system.
    /// </summary>
    public struct ProgressionConfig : IComponentData
    {
        public float XPMultiplier;           // Global XP multiplier
        public uint BaseXPPerLevel;          // Base XP required per level
        public float LevelXPScaling;         // XP scaling per level
        public byte MaxLevel;                // Maximum character level
        public byte SkillPointsPerLevel;     // Skill points gained per level
        public byte TalentPointsPerLevel;    // Talent points per X levels
        public byte TalentPointInterval;     // Levels between talent points
    }
}

