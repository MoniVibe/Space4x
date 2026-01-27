using Unity.Entities;
using Unity.Collections;

namespace PureDOTS.Runtime.Technology
{
    /// <summary>
    /// Current technology level of an entity.
    /// </summary>
    public struct TechLevel : IComponentData
    {
        public byte CurrentTier;            // 0-10 for Godgame, 0-N for Space4X
        public float ResearchProgress;      // Progress to next tier
        public uint TierUnlockedTick;       // When this tier was reached
    }

    /// <summary>
    /// Research project definition.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ResearchProject : IBufferElementData
    {
        public FixedString64Bytes ProjectId;
        public FixedString32Bytes Category;  // "metallurgy", "weapons", "magic"
        public byte RequiredTier;            // Min tech tier to start
        public float TotalResearchCost;      // Research points needed
        public float CurrentProgress;        // Points invested so far
        public byte IsCompleted;
        public byte IsActive;                // Currently being researched
    }

    /// <summary>
    /// Unlocked recipe from research.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct RecipeUnlock : IBufferElementData
    {
        public FixedString64Bytes RecipeId;  // What can now be crafted
        public FixedString64Bytes UnlockedBy; // Project or tier that unlocked it
        public uint UnlockedTick;
    }

    /// <summary>
    /// Entity contributing to research.
    /// </summary>
    public struct ResearchContributor : IComponentData
    {
        public Entity ContributingTo;        // Village/colony entity
        public float ResearchRate;           // Points per tick
        public float EfficiencyModifier;     // Education, facilities
        public FixedString32Bytes Specialty; // Bonus to certain categories
    }

    /// <summary>
    /// Knowledge pool for cultural advancement.
    /// </summary>
    public struct KnowledgePool : IComponentData
    {
        public float AccumulatedKnowledge;   // Cultural knowledge
        public float KnowledgeDecayRate;     // Lost if not maintained
        public byte MaxTierSupported;        // Can't exceed without scholars
    }

    /// <summary>
    /// Request to transfer technology.
    /// </summary>
    public struct TechTransferRequest : IComponentData
    {
        public Entity SourceEntity;          // Village with higher tech
        public Entity TargetEntity;          // Village receiving tech
        public byte TechTierToTransfer;
        public float TransferProgress;       // 0-1
        public FixedString32Bytes TransferMethod; // "trade", "espionage", "conquest"
    }

    /// <summary>
    /// Technology tree node.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct TechPrerequisite : IBufferElementData
    {
        public FixedString64Bytes RequiredProjectId;
        public byte IsMet;
    }

    /// <summary>
    /// Technology configuration.
    /// </summary>
    public struct TechConfig : IComponentData
    {
        public float BaseResearchCostPerTier;
        public float TierCostMultiplier;     // Each tier costs more
        public float KnowledgeToResearchRatio;
        public float TransferSpeedModifier;
        public byte AllowTechRegression;     // Can you lose tiers?
    }

    /// <summary>
    /// Research bonus from facilities.
    /// </summary>
    public struct ResearchFacilityBonus : IComponentData
    {
        public float BonusRate;              // Additional research per tick
        public float CategoryMultiplier;     // Multiplier for specific category
        public FixedString32Bytes BonusCategory;
    }
}

