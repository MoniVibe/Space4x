using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Economy.Production
{
    /// <summary>
    /// Recipe input specification.
    /// </summary>
    public struct RecipeInputBlob
    {
        public FixedString64Bytes ItemId;
        public float Quantity;
        public float MinPurity; // For extracted resources
        public float MinQuality; // For produced materials
    }

    /// <summary>
    /// Recipe output specification.
    /// </summary>
    public struct RecipeOutputBlob
    {
        public FixedString64Bytes ItemId;
        public float Quantity; // Can be formula-based (e.g., "InputPurity%")
    }

    /// <summary>
    /// Production recipe blob asset.
    /// Defines inputs, outputs, business type, tech tier, artisan requirements, time cost.
    /// </summary>
    public struct ProductionRecipeBlob
    {
        public FixedString64Bytes RecipeId;
        public ProductionStage Stage;
        public BlobArray<RecipeInputBlob> Inputs;
        public BlobArray<RecipeOutputBlob> Outputs;
        public BusinessType RequiredBusinessType;
        public int MinTechTier;
        public int MinArtisanExpertise;
        public float BaseTimeCost; // Worker-hours
        public float LaborCost; // Number of workers required
    }

    /// <summary>
    /// Catalog blob containing all production recipes.
    /// </summary>
    public struct ProductionRecipeCatalogBlob
    {
        public BlobArray<ProductionRecipeBlob> Recipes;
    }

    /// <summary>
    /// Singleton component holding the production recipe catalog reference.
    /// </summary>
    public struct ProductionRecipeCatalog : IComponentData
    {
        public BlobAssetReference<ProductionRecipeCatalogBlob> Catalog;
    }
}

