using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Marks a colony that has had its industry facilities bootstrapped.
    /// </summary>
    public struct ColonyIndustryBootstrapTag : IComponentData
    {
    }

    /// <summary>
    /// Accumulated resource budget for colony-driven industry feeds.
    /// </summary>
    public struct ColonyIndustryStock : IComponentData
    {
        public float OreReserve;
        public float SuppliesReserve;
        public float ResearchReserve;
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Links a facility entity back to its owning colony.
    /// </summary>
    public struct ColonyFacilityLink : IComponentData
    {
        public Entity Colony;
        public FacilityBusinessClass FacilityClass;
    }

    /// <summary>
    /// Links a ship entity to its origin colony for tech sync.
    /// </summary>
    public struct ColonyTechLink : IComponentData
    {
        public Entity Colony;
    }

    /// <summary>
    /// Shared industry inventory pool for a colony.
    /// </summary>
    public struct ColonyIndustryInventory : IComponentData
    {
        public Entity InventoryEntity;
    }

    /// <summary>
    /// Research pool used to drive tech diffusion.
    /// </summary>
    public struct TechResearchPool : IComponentData
    {
        public float Stored;
        public float Threshold;
        public byte MaxTier;
        public uint LastUpdateTick;
    }
}
