using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Resource
{
    /// <summary>
    /// Singleton component that exposes the compiled recipe and family data to
    /// runtime systems.
    /// </summary>
    public struct ResourceRecipeSet : IComponentData
    {
        public BlobAssetReference<ResourceRecipeSetBlob> Value;
    }

    public struct ResourceRecipeSetBlob
    {
        public BlobArray<ResourceFamilyBlob> Families;
        public BlobArray<ResourceRecipeBlob> Recipes;
    }

    public struct ResourceFamilyBlob
    {
        public FixedString64Bytes Id;
        public FixedString64Bytes DisplayName;
        public FixedString64Bytes RawResourceId;
        public FixedString64Bytes RefinedResourceId;
        public FixedString64Bytes CompositeResourceId;
        public FixedString128Bytes Description;
    }

    public struct ResourceRecipeBlob
    {
        public FixedString64Bytes Id;
        public ResourceRecipeKind Kind;
        public FixedString32Bytes FacilityTag;
        public FixedString64Bytes OutputResourceId;
        public int OutputAmount;
        public float ProcessSeconds;
        public BlobArray<ResourceIngredientBlob> Ingredients;
        public FixedString128Bytes Notes;
    }

    public struct ResourceIngredientBlob
    {
        public FixedString64Bytes ResourceId;
        public int Amount;
    }
}

