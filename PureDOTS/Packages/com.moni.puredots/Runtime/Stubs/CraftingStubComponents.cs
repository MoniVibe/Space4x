// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.Crafting
{
    public struct CraftingJobTicket : IComponentData
    {
        public int JobId;
        public int RecipeId;
    }

    public struct CraftingMaterialEntry : IBufferElementData
    {
        public int ResourceType;
        public float Amount;
    }

    public struct CraftingFormulaParams : IComponentData
    {
        public float SkillWeight;
        public float MaterialQualityWeight;
        public float FacilityBonus;
    }

    public struct CraftingQualityState : IComponentData
    {
        public float QualityScore;
        public byte Tier;
    }

    public struct CraftingResult : IComponentData
    {
        public int ProductId;
        public float QualityScore;
    }
}
