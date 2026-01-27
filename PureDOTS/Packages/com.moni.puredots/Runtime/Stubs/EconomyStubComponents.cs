// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.Economy
{
    public struct ResourceTypeId : IComponentData
    {
        public int Value;
    }

    public struct MaterialProperty : IComponentData
    {
        public float Density;
        public float Purity;
    }

    public struct ProductionRecipe : IComponentData
    {
        public int RecipeId;
        public byte Tier;
    }

    public struct ProductionQueueEntry : IBufferElementData
    {
        public int RecipeId;
        public byte BatchSize;
        public uint SubmitTick;
    }

    public struct FacilityState : IComponentData
    {
        public int FacilityId;
        public byte Status;
    }

    public struct FacilityInputElement : IBufferElementData
    {
        public int ResourceType;
        public float Amount;
    }

    public struct FacilityOutputElement : IBufferElementData
    {
        public int ResourceType;
        public float Amount;
    }

    public struct InventorySummary : IComponentData
    {
        public float TotalMass;
        public float TotalValue;
    }
}
