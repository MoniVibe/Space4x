using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Economy.Production
{
    /// <summary>
    /// Refining formula configuration blob.
    /// Defines coefficients for quality calculation from purity + skill.
    /// </summary>
    public struct RefiningFormulaConfigBlob
    {
        public float PurityCoefficient;
        public float ExpertiseCoefficient;
        public float BusinessQualityCoefficient;
        public float TechTierCoefficient;
    }

    /// <summary>
    /// Crafting formula configuration blob.
    /// Defines coefficients for quality propagation from component qualities.
    /// </summary>
    public struct CraftingFormulaConfigBlob
    {
        public float AverageComponentCoefficient;
        public float ArtisanBonusCoefficient;
        public float BusinessBonusCoefficient;
        public float TechTierBonusCoefficient;
    }

    /// <summary>
    /// Durability configuration blob.
    /// Defines rules for durability inheritance from components.
    /// </summary>
    public struct DurabilityConfigBlob
    {
        public float WeakestLinkMultiplier;
        public float AverageComponentMultiplier;
        public float ArtisanBonusMultiplier;
    }

    /// <summary>
    /// Rarity configuration blob.
    /// Defines rules for rarity calculation.
    /// </summary>
    public struct RarityConfigBlob
    {
        public float BaseRarityFromComponents;
        public float CriticalSuccessChanceBase;
        public float ExpertiseCriticalBonus;
    }

    /// <summary>
    /// Singleton components holding formula config references.
    /// </summary>
    public struct RefiningFormulaConfig : IComponentData
    {
        public BlobAssetReference<RefiningFormulaConfigBlob> Config;
    }

    public struct CraftingFormulaConfig : IComponentData
    {
        public BlobAssetReference<CraftingFormulaConfigBlob> Config;
    }

    public struct DurabilityConfig : IComponentData
    {
        public BlobAssetReference<DurabilityConfigBlob> Config;
    }

    public struct RarityConfig : IComponentData
    {
        public BlobAssetReference<RarityConfigBlob> Config;
    }
}

